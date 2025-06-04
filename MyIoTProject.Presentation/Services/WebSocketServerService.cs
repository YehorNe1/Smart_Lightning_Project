using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MyIoTProject.Infrastructure.Mqtt;

namespace MyIoTProject.Presentation.Services
{
    public class WebSocketServerService : IHostedService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly MqttClientService _mqttService;
        private WebSocketServer? _server;
        private readonly ConcurrentBag<IWebSocketConnection> _clients = new();

        public WebSocketServerService(
            IConfiguration configuration,
            MqttClientService mqttService)
        {
            _configuration = configuration;
            _mqttService = mqttService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // 1. Read container PORT or use 8181 if missing
            var envPort = Environment.GetEnvironmentVariable("PORT");
            if (!int.TryParse(envPort, out var port))
            {
                // if no PORT env, fallback to appsettings or 8181
                var portString = _configuration["WebSocketSettings:Port"];
                if (!int.TryParse(portString, out port))
                {
                    port = 8181;
                }
            }

            // 2. Build Fleck URL: e.g. "ws://0.0.0.0:10000/ws"
            var host = _configuration["WebSocketSettings:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "0.0.0.0";
            }
            var connectionString = $"ws://{host}:{port}/ws";

            // 3. Start Fleck on that port
            _server = new WebSocketServer(connectionString)
            {
                RestartAfterListenError = true
            };

            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    // client just connected
                    Console.WriteLine("new client connected");
                    _clients.Add(socket);
                };

                socket.OnClose = () =>
                {
                    // client just disconnected
                    Console.WriteLine("client disconnected");
                    _clients.TryTake(out _);
                };

                socket.OnMessage = message =>
                {
                    // send message from WebSocket client to MQTT
                    Console.WriteLine("got from WS: " + message);
                    try
                    {
                        _mqttService.PublishCommand(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("error sending to MQTT: " + ex.Message);
                    }
                };
            });

            // 4. Subscribe to MQTT events and broadcast
            _mqttService.ReadingReceived    += OnMqttReading;
            _mqttService.CommandAckReceived += OnMqttAck;
            _mqttService.ConfigReceived     += OnMqttConfig;

            Console.WriteLine("Fleck listening at " + connectionString);
            return Task.CompletedTask;
        }

        private void OnMqttReading(object? sender, ReadingReceivedEventArgs e)
        {
            // build JSON and send to all clients
            var json = "{\"light\":\"" + e.Light + "\",\"sound\":\"" + e.Sound + "\",\"motion\":\"" + e.Motion + "\"}";
            Broadcast(json);
        }

        private void OnMqttAck(object? sender, CommandAckReceivedEventArgs e)
        {
            Broadcast(e.AckJson);
        }

        private void OnMqttConfig(object? sender, ConfigReceivedEventArgs e)
        {
            Broadcast(e.ConfigJson);
        }

        private void Broadcast(string msg)
        {
            // send msg to every client still open
            foreach (var client in _clients)
            {
                if (client.IsAvailable)
                {
                    client.Send(msg);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // unsubscribe from MQTT and stop Fleck
            _mqttService.ReadingReceived    -= OnMqttReading;
            _mqttService.CommandAckReceived -= OnMqttAck;
            _mqttService.ConfigReceived     -= OnMqttConfig;

            _server?.Dispose();
            Console.WriteLine("Fleck stopped");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }
}