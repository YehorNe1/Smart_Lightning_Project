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

        // Событие: новые данные (light/sound/motion) пришли, отправим на WebSocket
        public event EventHandler<ReadingReceivedEventArgs>? ReadingReceived;

        public MqttClientService(string brokerHost, int brokerPort, string user, string pass,
                                 SensorReadingService sensorReadingService)
        {
            _sensorReadingService = sensorReadingService;

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            // Настройки MQTT
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .WithCredentials(user, pass)
                .WithCleanSession()
                .Build();

            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnMessageReceived);

            // Попытка соединения
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
            // Подпишемся на три топика
            await _mqttClient.SubscribeAsync("house/test_room/light");
            await _mqttClient.SubscribeAsync("house/test_room/sound");
            await _mqttClient.SubscribeAsync("house/test_room/motion");
            Console.WriteLine("Subscribed to: light, sound, motion topics.");
        }

        private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine("MQTT disconnected! Trying to reconnect...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer("mqtt.flespi.io", 1883)
                    .WithCredentials("user", "pass") // <-- Если нужно, замените
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
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? Array.Empty<byte>());

                Console.WriteLine($"[MQTT] Received on topic '{topic}': {payload}");

                // Узнаём, что это: light, sound или motion
                string? lightValue = null;
                string? soundValue = null;
                string? motionValue = null;

                if (topic.EndsWith("light"))
                    lightValue = payload;
                else if (topic.EndsWith("sound"))
                    soundValue = payload;
                else if (topic.EndsWith("motion"))
                    motionValue = payload;

                // Сохраняем в DB (с фильтром)
                await _sensorReadingService.AddReadingAsync(
                    lightValue ?? "N/A",
                    soundValue ?? "N/A",
                    motionValue ?? "N/A"
                );

                // Уведомим WebSocket (если есть)
                ReadingReceived?.Invoke(this, new ReadingReceivedEventArgs
                {
                    Light = lightValue ?? "",
                    Sound = soundValue ?? "",
                    Motion = motionValue ?? ""
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing MQTT message: {ex.Message}");
            }
        }

        // Публикация команды (Arduino прослушивает "house/test_room/cmd")
        public void PublishCommand(string payload)
        {
            // Топик команд:
            var topicCmd = "house/test_room/cmd"; 
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topicCmd)
                .WithPayload(payload)
                .Build();

            _mqttClient.PublishAsync(message, CancellationToken.None);
        }
    }

    public class ReadingReceivedEventArgs : EventArgs
    {
        public string Light { get; set; } = "";
        public string Sound { get; set; } = "";
        public string Motion { get; set; } = "";
    }
}