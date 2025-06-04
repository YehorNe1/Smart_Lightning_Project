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
            // Create a Host that does not start the HTTP server
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config
                        .AddJsonFile("appsettings.json",             optional: false, reloadOnChange: true)
                        .AddJsonFile("appsettings.Development.json", optional: true,  reloadOnChange: true)
                        .AddEnvironmentVariables();
                })
                .ConfigureServices((hostingContext, services) =>
                {
                    var cfg = hostingContext.Configuration;

                    // 1) Register the repository
                    services.AddScoped<ISensorReadingRepository>(sp =>
                    {
                        // Get Mongo settings from environment or file
                        var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN")
                                       ?? cfg["MongoSettings:ConnectionString"]!;
                        var dbName   = cfg["MongoSettings:DatabaseName"]!;
                        var colName  = cfg["MongoSettings:CollectionName"]!;

                        // Make a new object that can save readings to Mongo
                        return new SensorReadingRepository(mongoUri, dbName, colName);
                    });

                    // 2) Register the service that uses the repository
                    services.AddScoped<SensorReadingService>();

                    // 3) Register the MQTT client
                    services.AddSingleton<MqttClientService>(sp =>
                    {
                        // Get MQTT settings from environment or file
                        var mqttHost = cfg["MqttSettings:Broker"]!;
                        var mqttPort = int.Parse(cfg["MqttSettings:Port"]!);
                        var mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                                       ?? cfg["MqttSettings:User"]!;
                        var mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                                       ?? cfg["MqttSettings:Pass"]!;

                        // Get the service to pass into the MQTT client
                        var sensorSvc = sp.GetRequiredService<SensorReadingService>();

                        // Make a new MQTT client that can publish and receive
                        return new MqttClientService(mqttHost, mqttPort, mqttUser, mqttPass, sensorSvc);
                    });

                    // 4) Run the Fleck WebSocket server as a background job
                    services.AddHostedService<WebSocketServerService>();
                })
                .Build()
                .Run();
        }
    }
}