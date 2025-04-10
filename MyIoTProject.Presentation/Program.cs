using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly List<IWebSocketConnection> _allSockets = new List<IWebSocketConnection>();

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting MyIoTProject...");

            // 1) Load configuration from appsettings.json
            var config = BuildConfiguration();

            // MongoDB settings
            var mongoConnectionString = config["MongoSettings:ConnectionString"];
            var databaseName = config["MongoSettings:DatabaseName"];
            var collectionName = config["MongoSettings:CollectionName"];

            // Create repository
            ISensorReadingRepository sensorReadingRepository =
                new SensorReadingRepository(mongoConnectionString, databaseName, collectionName);

            // Create service with business logic
            var sensorReadingService = new SensorReadingService(sensorReadingRepository);

            // MQTT settings
            string mqttBroker = config["MqttSettings:Broker"];
            int mqttPort = int.Parse(config["MqttSettings:Port"]);
            string mqttUser = config["MqttSettings:User"];
            string mqttPass = config["MqttSettings:Pass"];

            var mqttClientService = new MqttClientService(
                mqttBroker,
                mqttPort,
                mqttUser,
                mqttPass,
                sensorReadingService
            );

            // Subscribe to the ReadingReceived event to broadcast new data via WebSocket
            mqttClientService.ReadingReceived += (sender, eventArgs) =>
            {
                string message = $"{{ \"light\": \"{eventArgs.Light}\", \"sound\": \"{eventArgs.Sound}\", \"motion\": \"{eventArgs.Motion}\" }}";
                Console.WriteLine("Broadcasting new reading to WebSocket clients: " + message);

                lock (_allSockets)
                {
                    foreach (var socket in _allSockets)
                    {
                        socket.Send(message);
                    }
                }
            };

            // WebSocket settings
            var wsHost = config["WebSocketSettings:Host"];
            var wsPort = int.Parse(config["WebSocketSettings:Port"] ?? "8181");

            // Start Fleck WebSocket server
            var server = new WebSocketServer($"ws://{wsHost}:{wsPort}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("New WebSocket connection!");
                    lock (_allSockets)
                    {
                        _allSockets.Add(socket);
                    }
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("WebSocket disconnected!");
                    lock (_allSockets)
                    {
                        _allSockets.Remove(socket);
                    }
                };
                socket.OnMessage = message =>
                {
                    Console.WriteLine("Received from WS client: " + message);
                };
            });

            Console.WriteLine($"WebSocket server started on ws://{wsHost}:{wsPort}");
            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();
        }

        private static IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            return builder.Build();
        }
    }
}