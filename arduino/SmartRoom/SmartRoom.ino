#include <WiFi.h>
#include <PubSubClient.h>
#include <math.h>
#include "wifi_credentials.h"   // SSID/PASSWORD and MQTT_USER/PASS

// MQTT broker
const char* mqtt_server = "mqtt.flespi.io";
const int   mqtt_port   = 1883;

// MQTT topics
const char* topicLight  = "house/test_room/light";
const char* topicSound  = "house/test_room/sound";
const char* topicMotion = "house/test_room/motion";
const char* topicCmd    = "house/test_room/cmd";
const char* topicAck    = "house/test_room/ack";
const char* topicConfig = "house/test_room/config";

// Sensor pins
const int LDR_PIN          = 39;
const int SOUND_SENSOR_PIN = 36;
const int PIR_SENSOR_PIN   = 17;

// Parameters
const int DC_OFFSET            = 1800;
const unsigned long SOUND_DURATION = 200;
unsigned long UPDATE_INTERVAL  = 2000;  // milliseconds
const int NUM_READS_LDR        = 10;

// Thresholds
unsigned int LIGHT_THRESHOLD = 100;
unsigned int SOUND_THRESHOLD = 600;

WiFiClient espClient;
PubSubClient client(espClient);

bool lastMotionState = LOW;
unsigned long lastUpdateTime = 0;

// Connect to Wi‑Fi
void setup_wifi() {
  delay(10);
  Serial.print("Connecting to WiFi: ");
  Serial.println(WIFI_SSID);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWiFi connected! IP:");
  Serial.println(WiFi.localIP());
}

// Reconnect to MQTT broker
void reconnectMQTT() {
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    if (client.connect("ESP32Client", MQTT_USER, MQTT_PASS)) {
      Serial.println("connected!");
      client.subscribe(topicCmd);
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println("; retrying in 5s");
      delay(5000);
    }
  }
}

// Classify light level
const char* classifyLight(int v) {
  if (v < 20)                    return "Very dark";
  else if (v < 50)               return "Dim";
  else if (v < LIGHT_THRESHOLD)  return "Bright";
  else                           return "Very bright";
}

// Classify sound level
const char* classifySound(double rms) {
  if (rms < 100)                    return "Silent";
  else if (rms < 300)               return "Small noise";
  else if (rms < SOUND_THRESHOLD)   return "Quite loud";
  else                              return "Very loud!";
}

// Classify motion state
const char* classifyMotion(bool m) {
  return m ? "Motion detected!" : "No motion...";
}

// Handle incoming MQTT messages
void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String msg;
  for (unsigned int i = 0; i < length; i++) msg += (char)payload[i];
  Serial.print("Received on ");
  Serial.print(topic);
  Serial.print(": ");
  Serial.println(msg);

  // Respond to getConfig
  if (msg.indexOf("getConfig") >= 0) {
    String cfg = String("{\"interval\":") + UPDATE_INTERVAL +
                 ",\"lightThreshold\":" + LIGHT_THRESHOLD +
                 ",\"soundThreshold\":" + SOUND_THRESHOLD + "}";
    client.publish(topicConfig, cfg.c_str());
    return;
  }

  // Handle setInterval, setLightThreshold, setSoundThreshold
  if (strcmp(topic, topicCmd) == 0) {
    if (msg.indexOf("setInterval") >= 0) {
      int idx = msg.indexOf("\"value\":");
      String val = msg.substring(idx + 8);
      val.trim(); val.replace("}", ""); val.replace("\"", "");
      unsigned long ni = val.toInt();
      if (ni > 0) {
        UPDATE_INTERVAL = ni;
        Serial.print("UPDATE_INTERVAL changed to ");
        Serial.println(UPDATE_INTERVAL);
        String ack = String("{\"ackCommand\":\"setInterval\",\"value\":") + ni + "}";
        client.publish(topicAck, ack.c_str());
      }
    }
    else if (msg.indexOf("setLightThreshold") >= 0) {
      int idx = msg.indexOf("\"value\":");
      String val = msg.substring(idx + 8);
      val.trim(); val.replace("}", ""); val.replace("\"", "");
      unsigned int nt = val.toInt();
      if (nt >= 10) {
        LIGHT_THRESHOLD = nt;
        Serial.print("LIGHT_THRESHOLD changed to ");
        Serial.println(LIGHT_THRESHOLD);
        String ack = String("{\"ackCommand\":\"setLightThreshold\",\"value\":") + nt + "}";
        client.publish(topicAck, ack.c_str());
      }
    }
    else if (msg.indexOf("setSoundThreshold") >= 0) {
      int idx = msg.indexOf("\"value\":");
      String val = msg.substring(idx + 8);
      val.trim(); val.replace("}", ""); val.replace("\"", "");
      unsigned int nt = val.toInt();
      if (nt >= 100) {
        SOUND_THRESHOLD = nt;
        Serial.print("SOUND_THRESHOLD changed to ");
        Serial.println(SOUND_THRESHOLD);
        String ack = String("{\"ackCommand\":\"setSoundThreshold\",\"value\":") + nt + "}";
        client.publish(topicAck, ack.c_str());
      }
    }
    else {
      Serial.println("Unknown command");
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
}

void loop() {
  if (!client.connected()) reconnectMQTT();
  client.loop();

  // Motion: on state change, publish and log
  bool motion = digitalRead(PIR_SENSOR_PIN);
  if (motion != lastMotionState) {
    lastMotionState = motion;
    const char* mStr = classifyMotion(motion);
    Serial.print("Motion changed: ");
    Serial.println(mStr);
    client.publish(topicMotion, mStr);
  }

  // Periodic LDR and sound readings + single-line Serial output
  unsigned long now = millis();
  if (now - lastUpdateTime >= UPDATE_INTERVAL) {
    lastUpdateTime = now;

    // LDR
    long sum = 0;
    for (int i = 0; i < NUM_READS_LDR; i++) { sum += analogRead(LDR_PIN); delay(5); }
    int lv = sum / NUM_READS_LDR;
    const char* lStr = classifyLight(lv);

    // Sound RMS
    unsigned long start = millis();
    double sq = 0; int cnt = 0;
    while (millis() - start < SOUND_DURATION) {
      int v = analogRead(SOUND_SENSOR_PIN) - DC_OFFSET;
      sq += (double)v * v; cnt++;
    }
    double rms = sqrt(sq / cnt);
    const char* sStr = classifySound(rms);

    // Use current motion state for logging
    const char* mStr = classifyMotion(lastMotionState);

    // Single-line status in Serial Monitor
    Serial.print("Light=");
    Serial.print(lStr);
    Serial.print(", Sound=");
    Serial.print(sStr);
    Serial.print(", Motion=");
    Serial.println(mStr);

    // Publish light and sound via MQTT
    client.publish(topicLight, lStr);
    client.publish(topicSound, sStr);
    // Motion is published only on change above
  }
}