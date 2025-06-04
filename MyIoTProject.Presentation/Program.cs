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
            // We create the web application (this gives us Kestrel server).
            var builder = WebApplication.CreateBuilder(args);

            // We load settings from appsettings.json and environment variables.
            builder.Configuration
                   .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                   .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables();

            // We tell the app how to save sensor readings in MongoDB.
            builder.Services.AddScoped<ISensorReadingRepository>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN")
                               ?? cfg["MongoSettings:ConnectionString"]!;
                var dbName = cfg["MongoSettings:DatabaseName"]!;
                var colName = cfg["MongoSettings:CollectionName"]!;

                return new SensorReadingRepository(mongoUri, dbName, colName);
            });

            // We tell the app what to do with sensor readings (filtering).
            builder.Services.AddScoped<SensorReadingService>();

            // We set up MQTT client so we can send and receive messages.
            builder.Services.AddSingleton<MqttClientService>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var mqttHost = cfg["MqttSettings:Broker"]!;
                var mqttPort = int.Parse(cfg["MqttSettings:Port"]!);
                var mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                               ?? cfg["MqttSettings:User"]!;
                var mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                               ?? cfg["MqttSettings:Pass"]!;

                var sensorSvc = sp.GetRequiredService<SensorReadingService>();
                return new MqttClientService(mqttHost, mqttPort, mqttUser, mqttPass, sensorSvc);
            });

            // We start the Fleck WebSocket server in the background.
            builder.Services.AddHostedService<WebSocketServerService>();

            var app = builder.Build();

            // We allow WebSocket upgrades so that Kestrel can forward /ws to Fleck.
            app.UseWebSockets();

            // When someone connects to /ws, we accept WebSocket and do nothing else here.
            app.Map("/ws", httpContext =>
            {
                return httpContext.WebSockets.AcceptWebSocketAsync()
                    .ContinueWith(_ => Task.CompletedTask);
            });

            // A simple endpoint to check if the app is running.
            app.MapGet("/health", () => Results.Ok("OK"));

            // Kestrel listens on the port from environment or 5000 if none is set.
            var httpPort = Environment.GetEnvironmentVariable("PORT")
                           ?? builder.Configuration["HttpSettings:Port"]
                           ?? "5000";
            app.Urls.Add($"http://*:{httpPort}");

            // Run the application (starts both Kestrel and Fleck).
            app.Run();
        }
    }
}