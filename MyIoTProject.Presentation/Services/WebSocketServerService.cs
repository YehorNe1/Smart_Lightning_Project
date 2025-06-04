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
            var wsHost = _configuration["WebSocketSettings:Host"] ?? "0.0.0.0";
            var wsPort = _configuration["WebSocketSettings:Port"] ?? "8181";
            var connectionString = $"ws://{wsHost}:{wsPort}/ws";

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
                        _mqttService.PublishCommand(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Fleck] Failed to publish MQTT command: {ex.Message}");
                    }
                };
            });

            _mqttService.ReadingReceived    += OnMqttReading;
            _mqttService.CommandAckReceived += OnMqttAck;
            _mqttService.ConfigReceived     += OnMqttConfig;

            Console.WriteLine($"[WebSocketServerService] Fleck started at {connectionString}");
            return Task.CompletedTask;
        }

        private void OnMqttReading(object? sender, ReadingReceivedEventArgs e)
        {
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
            _mqttService.ReadingReceived    -= OnMqttReading;
            _mqttService.CommandAckReceived -= OnMqttAck;
            _mqttService.ConfigReceived     -= OnMqttConfig;

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