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
            // Get the host from settings or use "0.0.0.0" if not set.
            var host = _configuration["WebSocketSettings:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "0.0.0.0";
            }

            // Get the port from settings or use 8181 if not a number.
            var portString = _configuration["WebSocketSettings:Port"];
            if (!int.TryParse(portString, out var port))
            {
                port = 8181;
            }

            // Build the Fleck URL, for example "ws://0.0.0.0:8181/ws".
            var connectionString = $"ws://{host}:{port}/ws";

            // Start the Fleck server on that internal port.
            _server = new WebSocketServer(connectionString)
            {
                RestartAfterListenError = true
            };

            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    // When a client connects, we add it to our list.
                    Console.WriteLine($"[Fleck] Client connected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}");
                    _clients.Add(socket);
                };

                socket.OnClose = () =>
                {
                    // When a client disconnects, we remove it.
                    Console.WriteLine($"[Fleck] Client disconnected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}");
                    _clients.TryTake(out _);
                };

                socket.OnMessage = message =>
                {
                    // When a message comes from a WebSocket client, send it to MQTT.
                    Console.WriteLine($"[Fleck] Received from WebSocket client: {message}");
                    try
                    {
                        _mqttService.PublishCommand(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Fleck] Failed to publish MQTT command: {ex.Message}");
                    }
                };
            });

            // When MQTT sends new data, we will broadcast to WebSocket clients.
            _mqttService.ReadingReceived    += OnMqttReading;
            _mqttService.CommandAckReceived += OnMqttAck;
            _mqttService.ConfigReceived     += OnMqttConfig;

            Console.WriteLine($"[WebSocketServerService] Fleck started at {connectionString}");
            return Task.CompletedTask;
        }

        private void OnMqttReading(object? sender, ReadingReceivedEventArgs e)
        {
            // Build a simple JSON with light, sound, motion values.
            var json = $"{{\"light\":\"{e.Light}\",\"sound\":\"{e.Sound}\",\"motion\":\"{e.Motion}\"}}";
            Broadcast(json);
        }

        private void OnMqttAck(object? sender, CommandAckReceivedEventArgs e)
        {
            // Broadcast the acknowledgement JSON to clients.
            Broadcast(e.AckJson);
        }

        private void OnMqttConfig(object? sender, ConfigReceivedEventArgs e)
        {
            // Broadcast the config JSON to clients.
            Broadcast(e.ConfigJson);
        }

        private void Broadcast(string message)
        {
            // Send the message to every client that is connected.
            foreach (var client in _clients)
            {
                if (client.IsAvailable)
                {
                    client.Send(message);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // Unsubscribe from MQTT events when stopping.
            _mqttService.ReadingReceived    -= OnMqttReading;
            _mqttService.CommandAckReceived -= OnMqttAck;
            _mqttService.ConfigReceived     -= OnMqttConfig;

            // Dispose Fleck server.
            _server?.Dispose();
            Console.WriteLine("[WebSocketServerService] Fleck stopped.");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }
}