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

            // Initialize MongoDB repository and service
            string mongoConnectionString = config["MongoSettings:ConnectionString"];
            string databaseName = config["MongoSettings:DatabaseName"];
            string collectionName = config["MongoSettings:CollectionName"];
            ISensorReadingRepository sensorReadingRepository = 
                new SensorReadingRepository(mongoConnectionString, databaseName, collectionName);
            var sensorReadingService = new SensorReadingService(sensorReadingRepository);

            // MQTT settings
            string mqttBroker = config["MqttSettings:Broker"];
            int mqttPort = int.Parse(config["MqttSettings:Port"] ?? "1883");
            string mqttUser = config["MqttSettings:User"];
            string mqttPass = config["MqttSettings:Pass"];
            var mqttClientService = new MqttClientService(mqttBroker, mqttPort, mqttUser, mqttPass, sensorReadingService);

            // Subscribe to sensor data
            mqttClientService.ReadingReceived += (sender, ev) =>
            {
                string message = $"{{ \"light\":\"{ev.Light}\", \"sound\":\"{ev.Sound}\", \"motion\":\"{ev.Motion}\" }}";
                Console.WriteLine("Broadcasting sensor data to WS: " + message);
                lock (_allSockets)
                {
                    foreach (var socket in _allSockets)
                        socket.Send(message);
                }
            };

            // Subscribe to ACK
            mqttClientService.CommandAckReceived += (sender, ev) =>
            {
                Console.WriteLine("Broadcasting ACK to WS: " + ev.AckJson);
                lock (_allSockets)
                {
                    foreach (var socket in _allSockets)
                        socket.Send(ev.AckJson);
                }
            };

            // Subscribe to configuration
            mqttClientService.ConfigReceived += (sender, ev) =>
            {
                Console.WriteLine("Broadcasting CONFIG to WS: " + ev.ConfigJson);
                lock (_allSockets)
                {
                    foreach (var socket in _allSockets)
                        socket.Send(ev.ConfigJson);
                }
            };

            // Start WebSocket server
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
                    Console.WriteLine("WS client -> .NET: " + messageStr);
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var cmd = JsonSerializer.Deserialize<CommandMessage>(messageStr, options);
                        if (cmd != null)
                        {
                            if (cmd.Command == "getConfig")
                            {
                                string payload = "{\"command\":\"getConfig\"}";
                                mqttClientService.PublishCommand(payload);
                                Console.WriteLine("Published getConfig");
                            }
                            else if (cmd.Command == "setInterval")
                            {
                                string payload = $"{{ \"command\":\"setInterval\", \"value\":{cmd.Value} }}";
                                mqttClientService.PublishCommand(payload);
                                Console.WriteLine("Published setInterval=" + cmd.Value);
                            }
                            else if (cmd.Command == "setLightThreshold")
                            {
                                string payload = $"{{ \"command\":\"setLightThreshold\", \"value\":{cmd.Value} }}";
                                mqttClientService.PublishCommand(payload);
                                Console.WriteLine("Published setLightThreshold=" + cmd.Value);
                            }
                            else if (cmd.Command == "setSoundThreshold")
                            {
                                string payload = $"{{ \"command\":\"setSoundThreshold\", \"value\":{cmd.Value} }}";
                                mqttClientService.PublishCommand(payload);
                                Console.WriteLine("Published setSoundThreshold=" + cmd.Value);
                            }
                            else
                            {
                                Console.WriteLine("Unknown command: " + cmd.Command);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error parsing WS command: " + ex.Message);
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

    public class CommandMessage
    {
        public string Command { get; set; } = "";
        public int Value { get; set; }
    }
}