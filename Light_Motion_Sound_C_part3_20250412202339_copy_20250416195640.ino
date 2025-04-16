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
const char* topicLight    = "house/test_room/light";
const char* topicSound    = "house/test_room/sound";
const char* topicMotion   = "house/test_room/motion";
const char* topicCmd      = "house/test_room/cmd";      // incoming commands
const char* topicAck      = "house/test_room/ack";      // publishing ACK
const char* topicConfig   = "house/test_room/config";   // configuration request/response

// Sensor pins
const int LDR_PIN          = 39;
const int SOUND_SENSOR_PIN = 36;
const int PIR_SENSOR_PIN   = 17;

// Parameters
const int DC_OFFSET = 1800;
const unsigned long SOUND_DURATION = 200;
unsigned long UPDATE_INTERVAL = 2000;  // default update interval
const int NUM_READS_LDR = 10;

// Threshold values
unsigned int LIGHT_THRESHOLD = 100;
unsigned int SOUND_THRESHOLD = 600;

WiFiClient espClient;
PubSubClient client(espClient);

bool lastMotionState = LOW;
unsigned long lastUpdateTime = 0;

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
      client.subscribe(topicCmd);
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println("; try again in 5 seconds");
      delay(5000);
    }
  }
}

const char* classifyLight(int value) {
  if (value < 20) return "Very dark";
  else if (value < 50) return "Dim";
  else if (value < LIGHT_THRESHOLD) return "Bright";
  else return "Very bright";
}

const char* classifySound(double rms) {
  if (rms < 100) return "Silent";
  else if (rms < 300) return "Small noise";
  else if (rms < SOUND_THRESHOLD) return "Quite loud";
  else return "Very loud!";
}

const char* classifyMotion(bool motionState) {
  return motionState ? "Motion detected!" : "No motion...";
}

void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String msg = "";
  for (unsigned int i = 0; i < length; i++) {
    msg += (char)payload[i];
  }
  Serial.print("[Arduino] Received on topic ");
  Serial.print(topic);
  Serial.print(": ");
  Serial.println(msg);

  // If configuration request
  if (msg.indexOf("getConfig") >= 0) {
    String configJson = "{\"interval\":" + String(UPDATE_INTERVAL) +
                        ",\"lightThreshold\":" + String(LIGHT_THRESHOLD) +
                        ",\"soundThreshold\":" + String(SOUND_THRESHOLD) + "}";
    client.publish(topicConfig, configJson.c_str());
    return;
  }

  // Command processing
  if (strcmp(topic, topicCmd) == 0) {
    if (msg.indexOf("setInterval") >= 0) {
      int idx = msg.indexOf("\"value\":");
      if (idx != -1) {
        String valStr = msg.substring(idx + 8);
        valStr.trim();
        valStr.replace("}", "");
        valStr.replace("\"", "");
        unsigned long newInterval = valStr.toInt();
        if (newInterval > 0) {
          UPDATE_INTERVAL = newInterval;
          Serial.print("UPDATE_INTERVAL changed to: ");
          Serial.println(UPDATE_INTERVAL);
          String ack = "{\"ackCommand\":\"setInterval\",\"value\":" + String(newInterval) + "}";
          client.publish(topicAck, ack.c_str());
        }
      }
    } else if (msg.indexOf("setLightThreshold") >= 0) {
      int idx = msg.indexOf("\"value\":");
      if (idx != -1) {
        String valStr = msg.substring(idx + 8);
        valStr.trim();
        valStr.replace("}", "");
        valStr.replace("\"", "");
        unsigned int newTh = valStr.toInt();
        if (newTh >= 10) {  
          LIGHT_THRESHOLD = newTh;
          Serial.print("LIGHT_THRESHOLD changed to: ");
          Serial.println(LIGHT_THRESHOLD);
          String ack = "{\"ackCommand\":\"setLightThreshold\",\"value\":" + String(newTh) + "}";
          client.publish(topicAck, ack.c_str());
        }
      }
    } else if (msg.indexOf("setSoundThreshold") >= 0) {
      int idx = msg.indexOf("\"value\":");
      if (idx != -1) {
        String valStr = msg.substring(idx + 8);
        valStr.trim();
        valStr.replace("}", "");
        valStr.replace("\"", "");
        unsigned int newSoundTh = valStr.toInt();
        if (newSoundTh >= 100) {
          SOUND_THRESHOLD = newSoundTh;
          Serial.print("SOUND_THRESHOLD changed to: ");
          Serial.println(SOUND_THRESHOLD);
          String ack = "{\"ackCommand\":\"setSoundThreshold\",\"value\":" + String(newSoundTh) + "}";
          client.publish(topicAck, ack.c_str());
        }
      }
    } else {
      Serial.println("[Arduino] Unknown command received");
    }
  }
}

void setup() {
  Serial.begin(115200);
  pinMode(LDR_PIN, INPUT);
  pinMode(SOUND_SENSOR_PIN, INPUT);
  pinMode(PIR_SENSOR_PIN, INPUT);

  setup_wifi();
  client.setServer(mqtt_server, mqtt_port);
  client.setCallback(mqttCallback);
  reconnectMQTT();

  // Subscribe to command topic
  client.subscribe(topicCmd);

  Serial.println("Setup done. Starting main loop...");
}

void loop() {
  if (!client.connected()) {
    reconnectMQTT();
  }
  client.loop();

  // Check PIR sensor
  bool currentMotion = digitalRead(PIR_SENSOR_PIN);
  if (currentMotion != lastMotionState) {
    lastMotionState = currentMotion;
    const char* motionStr = classifyMotion(currentMotion);
    Serial.print("Motion changed: ");
    Serial.println(motionStr);
    client.publish(topicMotion, motionStr);
  }

  // Read sensors every UPDATE_INTERVAL ms
  unsigned long now = millis();
  if (now - lastUpdateTime >= UPDATE_INTERVAL) {
    lastUpdateTime = now;
    // Read LDR (average value)
    long sumLDR = 0;
    for (int i = 0; i < NUM_READS_LDR; i++) {
      sumLDR += analogRead(LDR_PIN);
      delay(5);
    }
    int lightValue = sumLDR / NUM_READS_LDR;
    const char* lightStr = classifyLight(lightValue);

    // Calculate RMS for sound
    unsigned long soundStart = millis();
    double sumSq = 0.0;
    int count = 0;
    while (millis() - soundStart < SOUND_DURATION) {
      int rawVal = analogRead(SOUND_SENSOR_PIN);
      int adjusted = rawVal - DC_OFFSET;
      sumSq += (double) adjusted * adjusted;
      count++;
    }
    double rms = sqrt(sumSq / count);
    const char* soundStr = classifySound(rms);

    Serial.print("Light=");
    Serial.print(lightStr);
    Serial.print(", Sound=");
    Serial.print(soundStr);
    Serial.print(", Motion=");
    Serial.println(classifyMotion(lastMotionState));

    client.publish(topicLight, lightStr);
    client.publish(topicSound, soundStr);
  }
}