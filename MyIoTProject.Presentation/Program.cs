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
using MyIoTProject.Infrastructure.Mqtt;
using MyIoTProject.Infrastructure.Repositories;

namespace MyIoTProject.Presentation
{
    public class Program
    {
        /// <summary>All active WebSocket connections.</summary>
        private static readonly ConcurrentBag<WebSocket> _sockets = new();

        public static async Task Main(string[] args)
        {
            // --- 1) Port -------------------------------------------------------------------------
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8181";

            // --- 2) WebApplication ---------------------------------------------------------------
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration
                   .AddJsonFile("appsettings.json",             optional: false, reloadOnChange: true)
                   .AddJsonFile("appsettings.Development.json", optional: true,  reloadOnChange: true)
                   .AddEnvironmentVariables();

            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

            var app = builder.Build();

            // --- 3) Health & root ---------------------------------------------------------------
            app.MapGet("/",      () => Results.Text("MyIoTProject is running"));
            app.MapGet("/health", () => Results.Ok("OK"));

            // --- 4) Settings ---------------------------------------------------------------------
            var cfg      = app.Configuration;
            var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN") 
                           ?? cfg["MongoSettings:ConnectionString"]!;
            var dbName   = cfg["MongoSettings:DatabaseName"]!;
            var colName  = cfg["MongoSettings:CollectionName"]!;

            var mqttHost = cfg["MqttSettings:Broker"]!;
            var mqttPort = int.Parse(cfg["MqttSettings:Port"] ?? "1883");
            var mqttUser = Environment.GetEnvironmentVariable("MQTT_USER") ?? cfg["MqttSettings:User"]!;
            var mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS") ?? cfg["MqttSettings:Pass"]!;

            // --- 5) Dependency graph -------------------------------------------------------------
            var repo    = new SensorReadingRepository(mongoUri, dbName, colName);
            var service = new SensorReadingService(repo);
            var mqtt    = new MqttClientService(mqttHost, mqttPort, mqttUser, mqttPass, service);

            // MQTT → all WebSockets
            mqtt.ReadingReceived    += (_, ev) => _ = BroadcastAsync(
                $"{{\"light\":\"{ev.Light}\",\"sound\":\"{ev.Sound}\",\"motion\":\"{ev.Motion}\"}}");
            mqtt.CommandAckReceived += (_, ev) => _ = BroadcastAsync(ev.AckJson);
            mqtt.ConfigReceived     += (_, ev) => _ = BroadcastAsync(ev.ConfigJson);

            // --- 6) WebSockets -------------------------------------------------------------------
            app.UseWebSockets();

            app.Map("/ws", async context =>
            {
                if (!context.WebSockets.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    return;
                }

                WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
                _sockets.Add(socket);

                var buffer = new byte[4096];
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                    // Forward TEXT frames to MQTT as commands
                    if (result.MessageType == WebSocketMessageType.Text && result.Count > 0)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        mqtt.PublishCommand(json);
                    }

                    if (result.CloseStatus.HasValue)
                        break;
                }

                // Clean-up
                _sockets.TryTake(out _);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                        "Closed by server", CancellationToken.None);
            });

            // --- 7) Listen -----------------------------------------------------------------------
            app.Urls.Add($"http://*:{port}");
            await app.RunAsync();
        }

        /// <summary>Broadcasts a text message to every open WebSocket.</summary>
        private static async Task BroadcastAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);

            foreach (var socket in _sockets)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true,
                                            cancellationToken: CancellationToken.None);
                }
            }
        }
    }

    // Legacy DTO — kept for compatibility with existing Arduino code
    public class CommandMessage
    {
        public string Command { get; set; } = string.Empty;
        public int    Value   { get; set; }
    }
}