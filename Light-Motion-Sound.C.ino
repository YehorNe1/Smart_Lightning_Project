#include <WiFi.h>
#include <PubSubClient.h>
#include <math.h>

// WiFi credentials
const char* ssid = "BE House";
const char* password = "Esbjerg2022";

// MQTT settings
const char* mqtt_server   = "mqtt.flespi.io";
const int   mqtt_port     = 1883;
const char* mqtt_user     = "lL1opHkfTmTLfIRmMNa54uPNVpZ99uOAUAw09aeKemsu4jfxapAB6Q1vnyArnWEd";
const char* mqtt_pass     = "lL1opHkfTmTLfIRmMNa54uPNVpZ99uOAUAw09aeKemsu4jfxapAB6Q1vnyArnWEd";

// MQTT topics
const char* topicLight  = "house/test_room/light";
const char* topicSound  = "house/test_room/sound";
const char* topicMotion = "house/test_room/motion";

// Sensor pins
const int LDR_PIN          = 39;
const int SOUND_SENSOR_PIN = 36;
const int PIR_SENSOR_PIN   = 17;

// Parameters
const int DC_OFFSET = 1800;
const unsigned long SOUND_DURATION = 200; 
const unsigned long UPDATE_INTERVAL = 2000;
const int NUM_READS_LDR = 10;

// WiFi and MQTT clients
WiFiClient espClient;
PubSubClient client(espClient);

// PIR state tracking
bool lastMotionState = LOW;
unsigned long lastUpdateTime = 0;

// Function prototypes
void setup_wifi();
void reconnectMQTT();
const char* classifyLight(int lightValue);
const char* classifySound(double rmsValue);
const char* classifyMotion(bool motionState);

void setup() {
  Serial.begin(115200);

  pinMode(LDR_PIN, INPUT);
  pinMode(SOUND_SENSOR_PIN, INPUT);
  pinMode(PIR_SENSOR_PIN, INPUT);

  setup_wifi();
  client.setServer(mqtt_server, mqtt_port);
  reconnectMQTT();

  Serial.println("Setup done. Starting main loop...");
}

void loop() {
  // Check MQTT connection
  if (!client.connected()) {
    reconnectMQTT();
  }
  client.loop();

  // Read PIR sensor continuously and detect changes
  bool currentMotionState = digitalRead(PIR_SENSOR_PIN);
  if (currentMotionState != lastMotionState) {
    lastMotionState = currentMotionState;
    const char* motionStr = classifyMotion(currentMotionState);
    Serial.print("Motion changed: ");
    Serial.println(motionStr);
    client.publish(topicMotion, motionStr);
  }

  // Read light and sound every 2 seconds
  unsigned long currentTime = millis();
  if (currentTime - lastUpdateTime >= UPDATE_INTERVAL) {
    lastUpdateTime = currentTime;

    // Read and average LDR values
    long sumLDR = 0;
    for (int i = 0; i < NUM_READS_LDR; i++) {
      sumLDR += analogRead(LDR_PIN);
      delay(5);
    }
    int lightValue = sumLDR / NUM_READS_LDR;
    const char* lightStr = classifyLight(lightValue);

    // Calculate RMS for sound sensor
    unsigned long soundStart = millis();
    double sumSq = 0.0;
    int count = 0;
    while (millis() - soundStart < SOUND_DURATION) {
      int rawVal = analogRead(SOUND_SENSOR_PIN);
      int adjusted = rawVal - DC_OFFSET;
      sumSq += (double)adjusted * adjusted;
      count++;
    }
    double rms = sqrt(sumSq / count);
    const char* soundStr = classifySound(rms);

    // Print data to Serial
    Serial.print("Light=");
    Serial.print(lightStr);
    Serial.print(", Sound=");
    Serial.print(soundStr);
    Serial.print(", Motion=");
    Serial.println(classifyMotion(lastMotionState));

    // Publish to MQTT
    client.publish(topicLight,  lightStr);
    client.publish(topicSound,  soundStr);
  }
}

const char* classifyLight(int lightValue) {
  // Example thresholds
  if (lightValue < 20) {
    return "Very dark";
  } else if (lightValue < 50) {
    return "Dim";
  } else if (lightValue < 100) {
    return "Bright";
  } else {
    return "Very bright";
  }
}

const char* classifySound(double rmsValue) {
  // Example thresholds
  if (rmsValue < 100) {
    return "Silent";
  } else if (rmsValue < 300) {
    return "Small noise";
  } else if (rmsValue < 600) {
    return "Quite loud";
  } else {
    return "Very loud!";
  }
}

const char* classifyMotion(bool motionState) {
  if (motionState) {
    return "Motion detected!";
  } else {
    return "No motion...";
  }
}

void setup_wifi() {
  delay(10);
  Serial.println();
  Serial.print("Connecting to WiFi: ");
  Serial.println(ssid);

  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println();
  Serial.println("WiFi connected!");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());
}

void reconnectMQTT() {
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    if (client.connect("ESP32Client", mqtt_user, mqtt_pass)) {
      Serial.println("connected!");
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println("; try again in 5 seconds");
      delay(5000);
    }
  }
}
