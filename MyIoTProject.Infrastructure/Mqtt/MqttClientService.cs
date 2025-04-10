using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Receiving;
using MyIoTProject.Application.Services;

namespace MyIoTProject.Infrastructure.Mqtt
{
    public class MqttClientService
    {
        private readonly IMqttClient _mqttClient;
        private readonly SensorReadingService _sensorReadingService;

        // This event notifies the Presentation layer (WebSocket) about new readings
        public event EventHandler<ReadingReceivedEventArgs> ReadingReceived; 

        public MqttClientService(string brokerHost, int brokerPort, string user, string pass,
                                 SensorReadingService sensorReadingService)
        {
            _sensorReadingService = sensorReadingService;

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // Configure MQTT client options
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .WithCredentials(user, pass)
                .WithCleanSession()
                .Build();

            // Register handlers (newer style with .NET MQTTnet 3+)
            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnMessageReceived);

            // Attempt initial connection
            Task.Run(async () =>
            {
                try
                {
                    await _mqttClient.ConnectAsync(options, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Initial MQTT connection failed: {ex.Message}");
                }
            });
        }

        private async Task OnConnected(MqttClientConnectedEventArgs e)
        {
            Console.WriteLine("MQTT connected!");

            // Subscribe to three topics when connected
            await _mqttClient.SubscribeAsync("house/test_room/light");
            await _mqttClient.SubscribeAsync("house/test_room/sound");
            await _mqttClient.SubscribeAsync("house/test_room/motion");

            Console.WriteLine("Subscribed to: light, sound, motion topics.");
        }

        private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine("MQTT disconnected! Attempting to reconnect...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                // Example reconnection logic: create new options or reuse them
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("mqtt.flespi.io", 1883)
                    .WithCredentials("user", "pass")
                    .WithCleanSession()
                    .Build();

                await _mqttClient.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Reconnect failed: {ex.Message}");
            }
        }

        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                string topic = e.ApplicationMessage.Topic;
                byte[] payloadBytes = e.ApplicationMessage.Payload ?? new byte[0];
                string payload = Encoding.UTF8.GetString(payloadBytes);

                Console.WriteLine($"[MQTT] Received on topic '{topic}': {payload}");

                // Determine which sensor produced the data
                string lightValue = null;
                string soundValue = null;
                string motionValue = null;

                if (topic.EndsWith("light"))
                {
                    lightValue = payload;
                }
                else if (topic.EndsWith("sound"))
                {
                    soundValue = payload;
                }
                else if (topic.EndsWith("motion"))
                {
                    motionValue = payload;
                }

                // Persist to MongoDB using the service (with filtering logic)
                await _sensorReadingService.AddReadingAsync(
                    lightValue ?? "N/A",
                    soundValue ?? "N/A",
                    motionValue ?? "N/A"
                );

                // Notify any WebSocket subscribers
                ReadingReceived?.Invoke(this, new ReadingReceivedEventArgs
                {
                    Light = lightValue,
                    Sound = soundValue,
                    Motion = motionValue
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing MQTT message: {ex.Message}");
            }
        }
    }

    public class ReadingReceivedEventArgs : EventArgs
    {
        public string? Light { get; set; }
        public string? Sound { get; set; }
        public string? Motion { get; set; }
    }
}