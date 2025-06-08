using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MyIoTProject.Application.Services;

namespace MyIoTProject.Infrastructure.Mqtt
{
    // Runs MQTT in the background and handles messages
    public class MqttClientService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration      _config;
        private readonly IMqttClient         _client;
        private readonly IMqttClientOptions  _options;

        public MqttClientService(
            IConfiguration config,
            IServiceScopeFactory scopeFactory)
        {
            _config       = config;
            _scopeFactory = scopeFactory;

            // set up the raw MQTT client
            var factory = new MqttFactory();
            _client     = factory.CreateMqttClient();

            // read broker info (env vars override config)
            var host = _config["MqttSettings:Broker"]!;
            var port = int.Parse(_config["MqttSettings:Port"]!);
            var user = Environment.GetEnvironmentVariable("MQTT_USER")
                       ?? _config["MqttSettings:User"]!;
            var pass = Environment.GetEnvironmentVariable("MQTT_PASS")
                       ?? _config["MqttSettings:Pass"]!;

            // prepare connect options
            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(host, port)
                .WithCredentials(user, pass)
                .WithCleanSession()
                .Build();

            // hook up our handlers
            _client.ConnectedHandler               = new MqttClientConnectedHandlerDelegate(OnConnected);
            _client.DisconnectedHandler            = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _client.ApplicationMessageReceivedHandler
                                                   = new MqttApplicationMessageReceivedHandlerDelegate(OnMessageReceived);
        }

        // called by the host to start this background job
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // keep retrying until we connect or the host shuts down
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine("Connecting to MQTT broker...");
                    await _client.ConnectAsync(_options, stoppingToken);
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"MQTT connect failed: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        // after a successful connect
        private async Task OnConnected(MqttClientConnectedEventArgs args)
        {
            Console.WriteLine("MQTT connected!");
            await _client.SubscribeAsync("house/test_room/light");
            await _client.SubscribeAsync("house/test_room/sound");
            await _client.SubscribeAsync("house/test_room/motion");
            await _client.SubscribeAsync("house/test_room/ack");
            await _client.SubscribeAsync("house/test_room/config");
            Console.WriteLine("Subscribed to all topics");
        }

        // if we ever get disconnected
        private async Task OnDisconnected(MqttClientDisconnectedEventArgs args)
        {
            Console.WriteLine("MQTT disconnected, retrying in 5 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                await _client.ConnectAsync(_options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reconnect failed: {ex.Message}");
            }
        }

        // handle each incoming message
        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            var topic   = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(args.ApplicationMessage.Payload ?? Array.Empty<byte>());
            Console.WriteLine($"Message on '{topic}': {payload}");

            try
            {
                // create a fresh scope for each message
                using var scope    = _scopeFactory.CreateScope();
                var sensorService = scope.ServiceProvider.GetRequiredService<SensorReadingService>();

                if (topic.EndsWith("ack"))
                {
                    CommandAckReceived?.Invoke(this, new CommandAckReceivedEventArgs { AckJson = payload });
                }
                else if (topic.EndsWith("config"))
                {
                    ConfigReceived?.Invoke(this, new ConfigReceivedEventArgs { ConfigJson = payload });
                }
                else
                {
                    // parse and store sensor readings
                    string light  = null, sound = null, motion = null;
                    if (topic.EndsWith("light"))  light  = payload;
                    if (topic.EndsWith("sound"))  sound  = payload;
                    if (topic.EndsWith("motion")) motion = payload;

                    await sensorService.AddReadingAsync(
                        light  ?? "N/A",
                        sound  ?? "N/A",
                        motion ?? "N/A"
                    );

                    ReadingReceived?.Invoke(this, new ReadingReceivedEventArgs
                    {
                        Light  = light  ?? "",
                        Sound  = sound  ?? "",
                        Motion = motion ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        // called by WebSocketService to send commands back to devices
        public void PublishCommand(string payload)
        {
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("house/test_room/cmd")
                .WithPayload(payload)
                .Build();
            _client.PublishAsync(msg, CancellationToken.None);
        }

        // events so other parts (WebSocket) can subscribe
        public event EventHandler<ReadingReceivedEventArgs>    ReadingReceived;
        public event EventHandler<CommandAckReceivedEventArgs> CommandAckReceived;
        public event EventHandler<ConfigReceivedEventArgs>     ConfigReceived;
    }

    // simple data carriers for events
    public class ReadingReceivedEventArgs : EventArgs
    {
        public string Light  { get; set; } = "";
        public string Sound  { get; set; } = "";
        public string Motion { get; set; } = "";
    }

    public class CommandAckReceivedEventArgs : EventArgs
    {
        public string AckJson { get; set; } = "";
    }

    public class ConfigReceivedEventArgs : EventArgs
    {
        public string ConfigJson { get; set; } = "";
    }
}