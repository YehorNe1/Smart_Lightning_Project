using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyIoTProject.Application.Services;
using MyIoTProject.Core.Interfaces;
using MyIoTProject.Infrastructure.Repositories;
using MyIoTProject.Infrastructure.Mqtt;
using MyIoTProject.Presentation.Services;

namespace MyIoTProject.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Build the host with default settings
            var host = Host.CreateDefaultBuilder(args)
                
                // Load settings from JSON files and environment variables
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    cfg.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
                    cfg.AddEnvironmentVariables();
                })
                
                // Register services for dependency injection
                .ConfigureServices((ctx, services) =>
                {
                    var config = ctx.Configuration;

                    // MongoDB repository: one instance per scope
                    services.AddScoped<ISensorReadingRepository>(sp =>
                    {
                        var uri   = Environment.GetEnvironmentVariable("MONGO_CONN")
                                     ?? config["MongoSettings:ConnectionString"]!;
                        var db    = config["MongoSettings:DatabaseName"]!;
                        var col   = config["MongoSettings:CollectionName"]!;
                        return new SensorReadingRepository(uri, db, col);
                    });

                    // Service to handle sensor readings: one instance per scope
                    services.AddScoped<SensorReadingService>();

                    // Register MQTT client as a singleton
                    services.AddSingleton<MqttClientService>();

                    // Use the same MQTT client instance as a hosted background service
                    services.AddSingleton<IHostedService>(sp =>
                        sp.GetRequiredService<MqttClientService>());

                    // Register the Fleck WebSocket server as a hosted service
                    services.AddHostedService<WebSocketServerService>();
                })
                
                .Build();

            // Run the host, starting all hosted services
            host.Run();
        }
    }
}