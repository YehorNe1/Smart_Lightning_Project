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
            // Read internal port (for Fleck) from configuration (always 8181)
            var internalPort = _configuration["WebSocketSettings:Port"] ?? "8181";
            var internalHost = _configuration["WebSocketSettings:Host"] ?? "0.0.0.0";

            // Build Fleck connection string like "ws://0.0.0.0:8181/ws"
            var connectionString = $"ws://{internalHost}:{internalPort}/ws";

            // Create and start Fleck server that listens on internal port 8181
            _server = new WebSocketServer(connectionString)
            {
                RestartAfterListenError = true
            };

            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine($"[Fleck] Client connected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}");
                    _clients.Add(socket);
                };

                socket.OnClose = () =>
                {
                    Console.WriteLine($"[Fleck] Client disconnected: {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}");
                    _clients.TryTake(out _);
                };

                socket.OnMessage = message =>
                {
                    Console.WriteLine($"[Fleck] Received from WebSocket client: {message}");
                    try
                    {
                        // Whenever Fleck gets a message, forward to MQTT
                        _mqttService.PublishCommand(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Fleck] Failed to publish MQTT command: {ex.Message}");
                    }
                };
            });

            // Subscribe to MQTT events so we can broadcast to all connected WebSocket clients
            _mqttService.ReadingReceived    += OnMqttReading;
            _mqttService.CommandAckReceived += OnMqttAck;
            _mqttService.ConfigReceived     += OnMqttConfig;

            Console.WriteLine($"[WebSocketServerService] Fleck started at {connectionString}");
            return Task.CompletedTask;
        }

        private void OnMqttReading(object? sender, ReadingReceivedEventArgs e)
        {
            // Build a JSON string with sensor data
            var json = $"{{\"light\":\"{e.Light}\",\"sound\":\"{e.Sound}\",\"motion\":\"{e.Motion}\"}}";
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

        private void Broadcast(string message)
        {
            // Send message to all WebSocket clients if available
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
            // Unsubscribe from MQTT events on stop
            _mqttService.ReadingReceived    -= OnMqttReading;
            _mqttService.CommandAckReceived -= OnMqttAck;
            _mqttService.ConfigReceived     -= OnMqttConfig;

            // Dispose the Fleck server
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