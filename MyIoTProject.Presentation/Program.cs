using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MyIoTProject.Application.Services;
using MyIoTProject.Core.Interfaces;
using MyIoTProject.Infrastructure.Mqtt;
using MyIoTProject.Infrastructure.Repositories;

namespace MyIoTProject.Presentation
{
    public class Program
    {
        // Thread-safe collection of active WebSocket connections
        private static readonly ConcurrentBag<WebSocket> _sockets = new();

        public static async Task Main(string[] args)
        {
            // 1) Read PORT environment variable (default to 8181)
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8181";

            // 2) Build WebApplication
            var builder = WebApplication.CreateBuilder(args);

            // 3) Configure JSON + Development + Env-vars
            builder.Configuration
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables();

            // 3a) Configure logging to suppress INFO-level 404 logs
            builder.Logging
                   .AddConfiguration(builder.Configuration.GetSection("Logging"));

            var app = builder.Build();

            // 4) Root endpoint to avoid 404 on "/"
            app.MapGet("/", () => Results.Text("MyIoTProject is running"));

            // 5) Health check endpoint
            app.MapGet("/health", () => Results.Ok("OK"));

            // 6) Read Mongo & MQTT settings
            var config = app.Configuration;
            string envConn    = Environment.GetEnvironmentVariable("MONGO_CONN");
            string fileConn   = config["MongoSettings:ConnectionString"]!;
            string mongoConn  = envConn ?? fileConn;
            string dbName     = config["MongoSettings:DatabaseName"]!;
            string collName   = config["MongoSettings:CollectionName"]!;

            string mqttBroker = config["MqttSettings:Broker"]!;
            int    mqttPort   = int.Parse(config["MqttSettings:Port"] ?? "1883");
            string mqttUser   = Environment.GetEnvironmentVariable("MQTT_USER") ?? config["MqttSettings:User"]!;
            string mqttPass   = Environment.GetEnvironmentVariable("MQTT_PASS") ?? config["MqttSettings:Pass"]!;

            // 7) Initialize services
            var repo    = new SensorReadingRepository(mongoConn, dbName, collName);
            var service = new SensorReadingService(repo);
            var mqtt    = new MqttClientService(mqttBroker, mqttPort, mqttUser, mqttPass, service);

            // 8) Wire MQTT → WebSocket broadcast
            mqtt.ReadingReceived    += (_, ev) => _ = BroadcastAsync($"{{\"light\":\"{ev.Light}\",\"sound\":\"{ev.Sound}\",\"motion\":\"{ev.Motion}\"}}");
            mqtt.CommandAckReceived += (_, ev) => _ = BroadcastAsync(ev.AckJson);
            mqtt.ConfigReceived     += (_, ev) => _ = BroadcastAsync(ev.ConfigJson);

            // 9) Enable WebSockets
            app.UseWebSockets();

            // 10) Map WebSocket endpoint
            app.Map("/ws", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
                _sockets.Add(socket);

                var buffer = new byte[1024];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.CloseStatus.HasValue) break;
                }

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", CancellationToken.None);
            });

            // 11) Listen on the configured port
            app.Urls.Add($"http://*:{port}");
            await app.RunAsync();
        }

        // Broadcast text message to all open WebSockets
        private static async Task BroadcastAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            foreach (var socket in _sockets)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }

    // Legacy DTO — kept for compatibility
    public class CommandMessage
    {
        public string Command { get; set; } = string.Empty;
        public int    Value   { get; set; }
    }
}