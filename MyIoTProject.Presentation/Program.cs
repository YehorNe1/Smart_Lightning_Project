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
            // We build a host without Kestrel, just to run our Fleck WebSocket server.
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

                // 1. Register MongoDB repository
                services.AddScoped<ISensorReadingRepository>(sp =>
                {
                    // Get connection string from ENV or settings
                    var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN")
                                   ?? cfg["MongoSettings:ConnectionString"]!;
                    var dbName = cfg["MongoSettings:DatabaseName"]!;
                    var colName = cfg["MongoSettings:CollectionName"]!;
                    return new SensorReadingRepository(mongoUri, dbName, colName);
                });

                // 2. Register sensor service
                services.AddScoped<SensorReadingService>();

                // 3. Register MQTT client
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

                // 4. Register Fleck WebSocket server as a hosted service
                services.AddHostedService<WebSocketServerService>();
            });

            // Run the host (starts Fleck)
            builder.Build().Run();
        }
    }
}