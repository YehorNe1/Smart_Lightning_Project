using System;
using System.Collections.Generic;
using System.IO;
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

            // Read the port from the environment, fallback to 8181
            string portEnv = Environment.GetEnvironmentVariable("PORT") ?? "8181";
            if (!int.TryParse(portEnv, out int port))
                port = 8181;

            // Configuration: appsettings.json + appsettings.Development.json + Environment variables
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Read MongoDB connection string
            string envConn = Environment.GetEnvironmentVariable("MONGO_CONN");
            string fileConn = config["MongoSettings:ConnectionString"];
            Console.WriteLine($"DEBUG: MONGO_CONN env var       = '{envConn}'");
            Console.WriteLine($"DEBUG: appsettings ConnectionString = '{fileConn}'");

            string mongoConnectionString = envConn ?? fileConn;
            Console.WriteLine($"DEBUG: Using mongoConnectionString  = '{mongoConnectionString}'");

            string databaseName = config["MongoSettings:DatabaseName"];
            string collectionName = config["MongoSettings:CollectionName"];

            var sensorReadingRepository = new SensorReadingRepository(
                mongoConnectionString, databaseName, collectionName);
            var sensorReadingService = new SensorReadingService(sensorReadingRepository);

            // MQTT
            string mqttBroker = config["MqttSettings:Broker"];
            int mqttPort = int.Parse(config["MqttSettings:Port"] ?? "1883");

            string mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                              ?? config["MqttSettings:User"];
            string mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                              ?? config["MqttSettings:Pass"];

            var mqttClientService = new MqttClientService(
                mqttBroker, mqttPort, mqttUser, mqttPass, sensorReadingService);

            // Forward MQTT → WebSocket
            mqttClientService.ReadingReceived += (_, ev) => Broadcast(
                $"{{ \"light\":\"{ev.Light}\", \"sound\":\"{ev.Sound}\", \"motion\":\"{ev.Motion}\" }}");
            mqttClientService.CommandAckReceived += (_, ev) => Broadcast(ev.AckJson);
            mqttClientService.ConfigReceived += (_, ev) => Broadcast(ev.ConfigJson);

            // Start WebSocket server on dynamic port
            FleckLog.Level = LogLevel.Warn;
            var server = new WebSocketServer($"ws://0.0.0.0:{port}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    lock (_allSockets) _allSockets.Add(socket);
                    var info = socket.ConnectionInfo;
                    Console.WriteLine(
                        $"[WebSocket] Connected: {info.ClientIpAddress}:{info.ClientPort} (Total: {_allSockets.Count})");
                };

                socket.OnClose = () =>
                {
                    lock (_allSockets) _allSockets.Remove(socket);
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
            Console.ReadLine();
        }

        private static void Broadcast(string msg)
        {
            lock (_allSockets)
            {
                foreach (var s in _allSockets) s.Send(msg);
            }
        }
    }

    public class CommandMessage
    {
        public string Command { get; set; } = "";
        public int Value { get; set; }
    }
}
