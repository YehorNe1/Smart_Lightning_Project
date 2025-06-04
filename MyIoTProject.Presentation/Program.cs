using System;
using Microsoft.AspNetCore.Builder;
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
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration
                   .AddJsonFile("appsettings.json",             optional: false, reloadOnChange: true)
                   .AddJsonFile("appsettings.Development.json", optional: true,  reloadOnChange: true)
                   .AddEnvironmentVariables();

            // 1) Register repository with settings from config
            builder.Services.AddScoped<ISensorReadingRepository>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();

                // Get Mongo connection string and names from appsettings or env
                var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN")
                               ?? cfg["MongoSettings:ConnectionString"]!;
                var dbName   = cfg["MongoSettings:DatabaseName"]!;
                var colName  = cfg["MongoSettings:CollectionName"]!;

                // Create repository instance with those values
                return new SensorReadingRepository(mongoUri, dbName, colName);
            });

            // 2) Register service that uses the repository
            builder.Services.AddScoped<SensorReadingService>();

            // 3) Register MQTT client with settings from config
            builder.Services.AddSingleton<MqttClientService>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();

                // Get MQTT broker info from appsettings or env
                var mqttHost = cfg["MqttSettings:Broker"]!;
                var mqttPort = int.Parse(cfg["MqttSettings:Port"]!);
                var mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                               ?? cfg["MqttSettings:User"]!;
                var mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                               ?? cfg["MqttSettings:Pass"]!;

                // Get the service that saves readings
                var sensorSvc = sp.GetRequiredService<SensorReadingService>();

                // Create MQTT client service with these parameters
                return new MqttClientService(mqttHost, mqttPort, mqttUser, mqttPass, sensorSvc);
            });

            // 4) Register the Fleck WebSocket server as a background service
            builder.Services.AddHostedService<WebSocketServerService>();

            var app = builder.Build();

            // 5) Simple HTTP endpoints: root and health check
            app.MapGet("/",      () => Results.Text("MyIoTProject is running"));
            app.MapGet("/health", () => Results.Ok("OK"));

            // 6) Decide HTTP port from env or config, default = 5000
            var httpPort = Environment.GetEnvironmentVariable("PORT")
                           ?? builder.Configuration["HttpSettings:Port"]
                           ?? "5000";
            app.Urls.Add($"http://*:{httpPort}");

            app.Run();
        }
    }
}