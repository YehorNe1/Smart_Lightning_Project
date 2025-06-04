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
            // Build a web app (includes Kestrel HTTP server)
            var builder = WebApplication.CreateBuilder(args);

            // Load settings from JSON files and environment variables
            builder.Configuration
                   .AddJsonFile("appsettings.json",             optional: false, reloadOnChange: true)
                   .AddJsonFile("appsettings.Development.json", optional: true,  reloadOnChange: true)
                   .AddEnvironmentVariables();

            // ------------------------
            // 1) Register repository (Onion architecture)
            builder.Services.AddScoped<ISensorReadingRepository>(sp =>
            {
                var cfg      = sp.GetRequiredService<IConfiguration>();
                var mongoUri = Environment.GetEnvironmentVariable("MONGO_CONN")
                               ?? cfg["MongoSettings:ConnectionString"]!;
                var dbName   = cfg["MongoSettings:DatabaseName"]!;
                var colName  = cfg["MongoSettings:CollectionName"]!;
                return new SensorReadingRepository(mongoUri, dbName, colName);
            });

            // 2) Register service that uses the repository
            builder.Services.AddScoped<SensorReadingService>();

            // 3) Register MQTT client service
            builder.Services.AddSingleton<MqttClientService>(sp =>
            {
                var cfg      = sp.GetRequiredService<IConfiguration>();
                var mqttHost = cfg["MqttSettings:Broker"]!;
                var mqttPort = int.Parse(cfg["MqttSettings:Port"]!);
                var mqttUser = Environment.GetEnvironmentVariable("MQTT_USER")
                               ?? cfg["MqttSettings:User"]!;
                var mqttPass = Environment.GetEnvironmentVariable("MQTT_PASS")
                               ?? cfg["MqttSettings:Pass"]!;

                var sensorSvc = sp.GetRequiredService<SensorReadingService>();
                return new MqttClientService(mqttHost, mqttPort, mqttUser, mqttPass, sensorSvc);
            });

            // 4) Fleck WebSocket server will read its own internal port/host from IConfiguration
            builder.Services.AddHostedService<WebSocketServerService>();

            var app = builder.Build();

            // ------------------------
            // 5) Allow WebSocket upgrades so Kestrel can forward /ws requests to Fleck
            app.UseWebSockets();

            app.Map("/ws", httpContext =>
            {
                // Accept the WebSocket upgrade here; Fleck is listening on internal port (from appsettings.json)
                return httpContext.WebSockets.AcceptWebSocketAsync()
                    .ContinueWith(_ => Task.CompletedTask);
            });

            // 6) Health endpoint, if needed
            app.MapGet("/health", () => Results.Ok("OK"));

            // 7) Listen on the port that Render provides (or default 5000 locally)
            var httpPort = Environment.GetEnvironmentVariable("PORT")
                           ?? builder.Configuration["HttpSettings:Port"]
                           ?? "5000";
            app.Urls.Add($"http://*:{httpPort}");

            app.Run();
        }
    }
}