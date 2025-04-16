using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MyIoTProject.Application.Services;

namespace MyIoTProject.Infrastructure.Mqtt
{
    public class MqttClientService
    {
        private readonly IMqttClient _mqttClient;
        private readonly SensorReadingService _sensorReadingService;
        private readonly string _brokerHost;
        private readonly int _brokerPort;
        private readonly string _user;
        private readonly string _pass;

        // Events for new data, ACK and configuration
        public event EventHandler<ReadingReceivedEventArgs> ReadingReceived;
        public event EventHandler<CommandAckReceivedEventArgs> CommandAckReceived;
        public event EventHandler<ConfigReceivedEventArgs> ConfigReceived;

        public MqttClientService(string brokerHost, int brokerPort, string user, string pass, SensorReadingService sensorReadingService)
        {
            _brokerHost = brokerHost;
            _brokerPort = brokerPort;
            _user = user;
            _pass = pass;
            _sensorReadingService = sensorReadingService;
            
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_brokerHost, _brokerPort)
                .WithCredentials(_user, _pass)
                .WithCleanSession()
                .Build();

            _mqttClient.ConnectedHandler = new MqttClientConnectedHandlerDelegate(OnConnected);
            _mqttClient.DisconnectedHandler = new MqttClientDisconnectedHandlerDelegate(OnDisconnected);
            _mqttClient.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnMessageReceived);

            Task.Run(async () =>
            {
                try
                {
                    await _mqttClient.ConnectAsync(options, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Initial MQTT connection failed: " + ex.Message);
                }
            });
        }

        private async Task OnConnected(MqttClientConnectedEventArgs e)
        {
            Console.WriteLine("MQTT connected!");
            await _mqttClient.SubscribeAsync("house/test_room/light");
            await _mqttClient.SubscribeAsync("house/test_room/sound");
            await _mqttClient.SubscribeAsync("house/test_room/motion");
            await _mqttClient.SubscribeAsync("house/test_room/ack");
            await _mqttClient.SubscribeAsync("house/test_room/config");
            Console.WriteLine("Subscribed to: light, sound, motion, ack, config.");
        }

        private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine("MQTT disconnected! Trying to reconnect...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                var options = new MqttClientOptionsBuilder()
                    .WithTcpServer(_brokerHost, _brokerPort)
                    .WithCredentials(_user, _pass)
                    .WithCleanSession()
                    .Build();
                await _mqttClient.ConnectAsync(options, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Reconnect failed: " + ex.Message);
            }
        }

        private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                string topic = e.ApplicationMessage.Topic;
                string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload ?? Array.Empty<byte>());
                Console.WriteLine($"[MQTT] Received on '{topic}': {payload}");

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
                    string lightValue = null;
                    string soundValue = null;
                    string motionValue = null;
                    
                    if (topic.EndsWith("light"))
                        lightValue = payload;
                    else if (topic.EndsWith("sound"))
                        soundValue = payload;
                    else if (topic.EndsWith("motion"))
                        motionValue = payload;

                    await _sensorReadingService.AddReadingAsync(
                        lightValue ?? "N/A",
                        soundValue ?? "N/A",
                        motionValue ?? "N/A"
                    );

                    ReadingReceived?.Invoke(this, new ReadingReceivedEventArgs
                    {
                        Light = lightValue ?? "",
                        Sound = soundValue ?? "",
                        Motion = motionValue ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in OnMessageReceived: " + ex.Message);
            }
        }

        public void PublishCommand(string payload)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic("house/test_room/cmd")
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

    public class CommandAckReceivedEventArgs : EventArgs
    {
        public string AckJson { get; set; } = "";
    }

    public class ConfigReceivedEventArgs : EventArgs
    {
        public string ConfigJson { get; set; } = "";
    }
}