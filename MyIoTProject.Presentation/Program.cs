using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fleck;
using Microsoft.Extensions.Configuration;
using MyIoTProject.Application.Services;
using MyIoTProject.Core.Interfaces;
using MyIoTProject.Infrastructure.Mqtt;
using MyIoTProject.Infrastructure.Repositories;

namespace MyIoTProject.Presentation
{
    class Program
    {
        private static readonly List<IWebSocketConnection> _allSockets = new();

        static void Main(string[] args)
        {
            Console.WriteLine("Starting MyIoTProject...");

            // Read PORT environment variable, default to 8181
            string portEnv = Environment.GetEnvironmentVariable("PORT") ?? "8181";
            if (!int.TryParse(portEnv, out int port))
            {
                port = 8181;
            }

            //
            // --- Begin simple HTTP listener to answer health checks and port scan ---
            //
            var http = new HttpListener();
            // Listen on all paths under root: GET / and GET /health
            http.Prefixes.Add($"http://*:{port}/");
            http.Start();
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var ctx = await http.GetContextAsync();
                    var path = ctx.Request.Url.AbsolutePath;
                    if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
                    {
                        // Health check endpoint
                        ctx.Response.StatusCode = 200;
                        var data = Encoding.UTF8.GetBytes("OK");
                        ctx.Response.ContentLength64 = data.Length;
                        await ctx.Response.OutputStream.WriteAsync(data, 0, data.Length);
                    }
                    else
                    {
                        // Respond OK for any other path to satisfy port scan
                        ctx.Response.StatusCode = 200;
                    }
                    ctx.Response.OutputStream.Close();
                }
            });
            //
            // --- End HTTP listener ---
            //

            // Load configuration: appsettings.json + appsettings.Development.json + environment variables
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Read MongoDB connection string
            string envConn  = Environment.GetEnvironmentVariable("MONGO_CONN");
            string fileConn = config["MongoSettings:ConnectionString"];
            Console.WriteLine($"DEBUG: MONGO_CONN env var             = '{envConn}'");
            Console.WriteLine($"DEBUG: appsettings ConnectionString   = '{fileConn}'");
            string mongoConnectionString = envConn ?? fileConn;
            Console.WriteLine($"DEBUG: Using mongoConnectionString    = '{mongoConnectionString}'");

            string databaseName   = config["MongoSettings:DatabaseName"];
            string collectionName = config["MongoSettings:CollectionName"];

            var sensorReadingRepository = new SensorReadingRepository(
                mongoConnectionString, databaseName, collectionName);
            var sensorReadingService    = new SensorReadingService(sensorReadingRepository);

            // MQTT settings
            string mqttBroker = config["MqttSettings:Broker"];
            int    mqttPort   = int.Parse(config["MqttSettings:Port"] ?? "1883");
            string mqttUser   = Environment.GetEnvironmentVariable("MQTT_USER") ?? config["MqttSettings:User"];
            string mqttPass   = Environment.GetEnvironmentVariable("MQTT_PASS") ?? config["MqttSettings:Pass"];

            var mqttClientService = new MqttClientService(
                mqttBroker, mqttPort, mqttUser, mqttPass, sensorReadingService);

            // Forward MQTT events to WebSocket clients
            mqttClientService.ReadingReceived    += (_, ev) => Broadcast(
                $"{{ \"light\":\"{ev.Light}\", \"sound\":\"{ev.Sound}\", \"motion\":\"{ev.Motion}\" }}");
            mqttClientService.CommandAckReceived += (_, ev) => Broadcast(ev.AckJson);
            mqttClientService.ConfigReceived     += (_, ev) => Broadcast(ev.ConfigJson);

            // Start WebSocket server on the same port
            FleckLog.Level = Fleck.LogLevel.Warn;
            var server = new WebSocketServer($"ws://0.0.0.0:{port}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    lock (_allSockets)
                        _allSockets.Add(socket);
                    var info = socket.ConnectionInfo;
                    Console.WriteLine(
                        $"[WebSocket] Connected: {info.ClientIpAddress}:{info.ClientPort} (Total: {_allSockets.Count})");
                };

                socket.OnClose = () =>
                {
                    lock (_allSockets)
                        _allSockets.Remove(socket);
                    var info = socket.ConnectionInfo;
                    Console.WriteLine(
                        $"[WebSocket] Disconnected: {info.ClientIpAddress}:{info.ClientPort} (Total: {_allSockets.Count})");
                };

                socket.OnError = ex =>
                {
                    var info = socket.ConnectionInfo;
                    Console.WriteLine(
                        $"[WebSocket] Error from {info.ClientIpAddress}:{info.ClientPort}: {ex.Message}");
                };

                socket.OnMessage = msg =>
                {
                    Console.WriteLine($"[WebSocket] Received message: {msg}");
                    mqttClientService.PublishCommand(msg);
                };
            });

            Console.WriteLine($"WebSocket server started on ws://0.0.0.0:{port}");

            // Keep the app running indefinitely
            Thread.Sleep(Timeout.Infinite);
        }

        private static void Broadcast(string msg)
        {
            lock (_allSockets)
            {
                foreach (var socket in _allSockets)
                    socket.Send(msg);
            }
        }
    }

    public class CommandMessage
    {
        public string Command { get; set; } = string.Empty;
        public int    Value   { get; set; }
    }
}