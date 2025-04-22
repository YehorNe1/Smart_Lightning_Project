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

            // ——— Конфиг: appsettings.json + appsettings.Development.json + Env
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json",             optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true,  reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // ——— Читаем строку подключения к Mongo
            string envConn  = Environment.GetEnvironmentVariable("MONGO_CONN");
            string fileConn = config["MongoSettings:ConnectionString"];
            Console.WriteLine($"DEBUG: MONGO_CONN env var       = '{envConn}'");
            Console.WriteLine($"DEBUG: appsettings ConnectionString = '{fileConn}'");

            string mongoConnectionString = envConn ?? fileConn;
            Console.WriteLine($"DEBUG: Using mongoConnectionString  = '{mongoConnectionString}'");

            // если здесь пусто — файл не прочитался и переменная окружения не задана

            string databaseName   = config["MongoSettings:DatabaseName"];
            string collectionName = config["MongoSettings:CollectionName"];

            var sensorReadingRepository =
                new SensorReadingRepository(mongoConnectionString, databaseName, collectionName);
            var sensorReadingService = new SensorReadingService(sensorReadingRepository);

            // ——— MQTT
            string mqttBroker = config["MqttSettings:Broker"];
            int    mqttPort   = int.Parse(config["MqttSettings:Port"] ?? "1883");

            string mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                              ?? config["MqttSettings:User"];
            string mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                              ?? config["MqttSettings:Pass"];

            var mqttClientService = new MqttClientService(
                mqttBroker, mqttPort, mqttUser, mqttPass, sensorReadingService);

            // ——— Пересылаем MQTT → WebSocket
            mqttClientService.ReadingReceived += (_, ev) => Broadcast(
                $"{{ \"light\":\"{ev.Light}\", \"sound\":\"{ev.Sound}\", \"motion\":\"{ev.Motion}\" }}");
            mqttClientService.CommandAckReceived += (_, ev) => Broadcast(ev.AckJson);
            mqttClientService.ConfigReceived     += (_, ev) => Broadcast(ev.ConfigJson);

            // ——— Запуск WebSocket‑сервера
            string wsHost = config["WebSocketSettings:Host"] ?? "0.0.0.0";
            int    wsPort = int.Parse(config["WebSocketSettings:Port"] ?? "8181");

            var server = new WebSocketServer($"ws://{wsHost}:{wsPort}");
            server.Start(socket =>
            {
                socket.OnOpen    = () => { lock (_allSockets) _allSockets.Add(socket); };
                socket.OnClose   = () => { lock (_allSockets) _allSockets.Remove(socket); };
                socket.OnMessage = msg => mqttClientService.PublishCommand(msg);
            });

            Console.WriteLine($"WebSocket server started on ws://{wsHost}:{wsPort}");
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
        public int    Value   { get; set; }
    }
}