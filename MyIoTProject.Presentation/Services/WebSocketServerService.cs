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

        // Track clients with their unique IDs
        private readonly ConcurrentDictionary<string, IWebSocketConnection> _clients = new();

        public WebSocketServerService(
            IConfiguration configuration,
            MqttClientService mqttService)
        {
            _configuration = configuration;
            _mqttService = mqttService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var envPort = Environment.GetEnvironmentVariable("PORT");
            if (!int.TryParse(envPort, out var port))
            {
                var portString = _configuration["WebSocketSettings:Port"];
                if (!int.TryParse(portString, out port))
                {
                    port = 8181;
                }
            }

            var host = _configuration["WebSocketSettings:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "0.0.0.0";
            }

            var connectionString = $"ws://{host}:{port}/ws";

            _server = new WebSocketServer(connectionString)
            {
                RestartAfterListenError = true
            };

            _server.Start(socket =>
            {
                // Generate unique ID for the client
                var clientId = Guid.NewGuid().ToString();

                socket.OnOpen = () =>
                {
                    _clients[clientId] = socket;
                    Console.WriteLine($"Client connected: {clientId}");
                };

                socket.OnClose = () =>
                {
                    _clients.TryRemove(clientId, out _);
                    Console.WriteLine($"Client disconnected: {clientId}");
                };

                socket.OnMessage = message =>
                {
                    Console.WriteLine($"Received from {clientId}: {message}");
                    try
                    {
                        _mqttService.PublishCommand(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending to MQTT from {clientId}: {ex.Message}");
                    }
                };
            });

            // Subscribe to MQTT messages
            _mqttService.ReadingReceived    += OnMqttReading;
            _mqttService.CommandAckReceived += OnMqttAck;
            _mqttService.ConfigReceived     += OnMqttConfig;

            Console.WriteLine("Fleck listening at " + connectionString);
            return Task.CompletedTask;
        }

        private void OnMqttReading(object? sender, ReadingReceivedEventArgs e)
        {
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
            foreach (var kvp in _clients)
            {
                var clientId = kvp.Key;
                var client = kvp.Value;

                if (client.IsAvailable)
                {
                    Console.WriteLine($"Sending to {clientId}: {msg}");
                    client.Send(msg);
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
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