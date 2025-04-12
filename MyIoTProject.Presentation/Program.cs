using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

        static void Main(string[] args)
        {
            Console.WriteLine("Starting MyIoTProject...");

            var config = BuildConfiguration();

            // === Mongo ===
            string mongoConnectionString = config["MongoSettings:ConnectionString"];
            string databaseName = config["MongoSettings:DatabaseName"];
            string collectionName = config["MongoSettings:CollectionName"];

            ISensorReadingRepository sensorReadingRepository =
                new SensorReadingRepository(mongoConnectionString, databaseName, collectionName);
            var sensorReadingService = new SensorReadingService(sensorReadingRepository);

            // === MQTT ===
            string mqttBroker = config["MqttSettings:Broker"];
            int mqttPort = int.Parse(config["MqttSettings:Port"] ?? "1883");
            string mqttUser = config["MqttSettings:User"];
            string mqttPass = config["MqttSettings:Pass"];

            var mqttClientService = new MqttClientService(
                mqttBroker, mqttPort, mqttUser, mqttPass, sensorReadingService
            );

            // When new sensor data arrives => broadcast to all WebSocket clients
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

            // === Fleck WebSocket server ===
            string wsHost = config["WebSocketSettings:Host"] ?? "0.0.0.0";
            int wsPort = int.Parse(config["WebSocketSettings:Port"] ?? "8181");
            var server = new WebSocketServer($"ws://{wsHost}:{wsPort}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("New WebSocket connection!");
                    lock (_allSockets) { _allSockets.Add(socket); }
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("WebSocket disconnected!");
                    lock (_allSockets) { _allSockets.Remove(socket); }
                };
                socket.OnMessage = (messageStr) =>
                {
                    // 1) Show raw message string
                    Console.WriteLine("[.NET] Raw messageStr: " + messageStr);

                    // 2) Show length
                    Console.WriteLine("[.NET] messageStr length: " + messageStr.Length);

                    // 3) Show hex dump of each character
                    Console.Write("[.NET] messageStr (hex): ");
                    foreach (char c in messageStr)
                    {
                        Console.Write(((int)c).ToString("X2") + " ");
                    }
                    Console.WriteLine();

                    try
                    {
                        // Explicitly set case-insensitive property matching
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        var cmd = JsonSerializer.Deserialize<CommandMessage>(messageStr, options);

                        if (cmd != null)
                        {
                            Console.WriteLine($"[.NET] After parse => cmd.Command='{cmd.Command}', cmd.Value={cmd.Value}");

                            if (cmd.Command == "setInterval")
                            {
                                string payload = $"{{ \"command\":\"setInterval\", \"value\":{cmd.Value} }}";
                                Console.WriteLine("[.NET] Publishing setInterval to MQTT: " + payload);
                                mqttClientService.PublishCommand(payload);
                            }
                            else if (cmd.Command == "setLightThreshold")
                            {
                                string payload = $"{{ \"command\":\"setLightThreshold\", \"value\":{cmd.Value} }}";
                                Console.WriteLine("[.NET] Publishing setLightThreshold to MQTT: " + payload);
                                mqttClientService.PublishCommand(payload);
                            }
                            else if (cmd.Command == "setSoundThreshold")
                            {
                                string payload = $"{{ \"command\":\"setSoundThreshold\", \"value\":{cmd.Value} }}";
                                Console.WriteLine("[.NET] Publishing setSoundThreshold to MQTT: " + payload);
                                mqttClientService.PublishCommand(payload);
                            }
                            else
                            {
                                Console.WriteLine("[.NET] Unknown command: " + cmd.Command);
                            }
                        }
                        else
                        {
                            Console.WriteLine("[.NET] cmd is null after parsing.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[.NET] Error parsing command: " + ex.Message);
                    }
                };
            });

            Console.WriteLine($"WebSocket server started on ws://{wsHost}:{wsPort}");
            Console.WriteLine("Press ENTER to stop...");
            Console.ReadLine();
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
        }
    }

    // JSON model for incoming commands
    public class CommandMessage
    {
        public string Command { get; set; } = "";
        public int Value { get; set; }
    }
}