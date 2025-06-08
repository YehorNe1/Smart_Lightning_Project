using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyIoTProject.Application.Services;
using MyIoTProject.Core.Interfaces;
using MyIoTProject.Infrastructure.Mqtt;
using MyIoTProject.Infrastructure.Repositories;
using MyIoTProject.Presentation.Services;

namespace MyIoTProject.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Fleck WebSocket server
            var builder = Host.CreateDefaultBuilder(args);

            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Load appsettings.json and environment variables
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                      .AddEnvironmentVariables();
            });

            builder.ConfigureServices((context, services) =>
            {
                var cfg = context.Configuration;

                // Register MongoDB repository
                services.AddScoped<ISensorReadingRepository>(sp =>
                {
                    // Get connection string from ENV or settings
                    var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN")
                                   ?? cfg["MongoSettings:ConnectionString"]!;
                    var dbName = cfg["MongoSettings:DatabaseName"]!;
                    var colName = cfg["MongoSettings:CollectionName"]!;
                    return new SensorReadingRepository(mongoUri, dbName, colName);
                });

                // Register sensor service
                services.AddScoped<SensorReadingService>();

                // Register MQTT client
                services.AddSingleton<MqttClientService>(sp =>
                {
                    var mqttHost = cfg["MqttSettings:Broker"]!;
                    var mqttPort = int.Parse(cfg["MqttSettings:Port"]!);
                    var mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                                   ?? cfg["MqttSettings:User"]!;
                    var mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                                   ?? cfg["MqttSettings:Pass"]!;
                    var sensorSvc = sp.GetRequiredService<SensorReadingService>();
                    return new MqttClientService(mqttHost, mqttPort, mqttUser, mqttPass, sensorSvc);
                });

                // Register Fleck WebSocket server as a hosted service
                services.AddHostedService<WebSocketServerService>();
            });

            // Run the host Fleck
            builder.Build().Run();
        }
    }
}