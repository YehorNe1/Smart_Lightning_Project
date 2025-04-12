#include <WiFi.h>
#include <PubSubClient.h>
#include <math.h>

// WiFi credentials
const char* ssid = "iPhone";
const char* password = "HugoNe04";

// MQTT settings
const char* mqtt_server   = "mqtt.flespi.io";
const int   mqtt_port     = 1883;
const char* mqtt_user     = "lL1opHkfTmTLfIRmMNa54uPNVpZ99uOAUAw09aeKemsu4jfxapAB6Q1vnyArnWEd";
const char* mqtt_pass     = "lL1opHkfTmTLfIRmMNa54uPNVpZ99uOAUAw09aeKemsu4jfxapAB6Q1vnyArnWEd";

// MQTT topics
const char* topicLight   = "house/test_room/light";
const char* topicSound   = "house/test_room/sound";
const char* topicMotion  = "house/test_room/motion";
const char* topicCmd     = "house/test_room/cmd";   // for commands

// Sensor pins
const int LDR_PIN          = 39;
const int SOUND_SENSOR_PIN = 36;
const int PIR_SENSOR_PIN   = 17;

// Parameters
const int DC_OFFSET = 1800;
const unsigned long SOUND_DURATION = 200; 
unsigned long UPDATE_INTERVAL = 2000;  // default 2000ms, changed by setInterval
const int NUM_READS_LDR = 10;

// Thresholds for upper categories
unsigned int LIGHT_THRESHOLD = 100; // boundary between "Bright" and "Very bright"
unsigned int SOUND_THRESHOLD = 600; // boundary between "Quite loud" and "Very loud!"

// WiFi and MQTT clients
WiFiClient espClient;
PubSubClient client(espClient);

// PIR state tracking
bool lastMotionState = LOW;
unsigned long lastUpdateTime = 0;

// Function prototypes
void setup_wifi();
void reconnectMQTT();
void mqttCallback(char* topic, byte* payload, unsigned int length);

const char* classifyLight(int value);
const char* classifySound(double rms);
const char* classifyMotion(bool motionState);

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
  // Check MQTT connection
  if (!client.connected()) {
    reconnectMQTT();
  }
  client.loop();

  // Check PIR sensor
  bool currentMotionState = digitalRead(PIR_SENSOR_PIN);
  if (currentMotionState != lastMotionState) {
    lastMotionState = currentMotionState;
    const char* motionStr = classifyMotion(currentMotionState);
    Serial.print("Motion changed: ");
    Serial.println(motionStr);
    client.publish(topicMotion, motionStr);
  }

  // Read light/sound every UPDATE_INTERVAL ms
  unsigned long currentTime = millis();
  if (currentTime - lastUpdateTime >= UPDATE_INTERVAL) {
    lastUpdateTime = currentTime;

    // Read and average LDR
    long sumLDR = 0;
    for (int i = 0; i < NUM_READS_LDR; i++) {
      sumLDR += analogRead(LDR_PIN);
      delay(5);
    }
    int lightValue = sumLDR / NUM_READS_LDR;

    // Classify light
    const char* lightStr = classifyLight(lightValue);

    // Calculate RMS for sound
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

    // Classify sound
    const char* soundStr = classifySound(rms);

    // Debug print
    Serial.print("Light=");
    Serial.print(lightStr);
    Serial.print(", Sound=");
    Serial.print(soundStr);
    Serial.print(", Motion=");
    Serial.println(classifyMotion(lastMotionState));

    // Publish sensor data to MQTT
    client.publish(topicLight,  lightStr);
    client.publish(topicSound,  soundStr);
  }
}

//------------------------------------------
// MQTT callback to handle incoming commands
//------------------------------------------
void mqttCallback(char* topic, byte* payload, unsigned int length) {
  Serial.print("[Arduino] mqttCallback triggered! topic=");
  Serial.println(topic);

  // Convert payload to string
  String msg;
  for (unsigned int i = 0; i < length; i++) {
    msg += (char)payload[i];
  }
  Serial.print("[Arduino] Received raw payload: ");
  Serial.println(msg);

  // Check if this is the CMD topic
  if (strcmp(topic, topicCmd) == 0) {
    Serial.println("[Arduino] This is a CMD topic!");

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
        }
      }
    }
    else if (msg.indexOf("setLightThreshold") >= 0) {
      int idx = msg.indexOf("\"value\":");
      if (idx != -1) {
        String valStr = msg.substring(idx + 8);
        valStr.trim();
        valStr.replace("}", "");
        valStr.replace("\"", "");
        unsigned int newTh = valStr.toInt();
        if (newTh >= 50) {
          LIGHT_THRESHOLD = newTh;
          Serial.print("LIGHT_THRESHOLD changed to: ");
          Serial.println(LIGHT_THRESHOLD);
        }
      }
    }
    else if (msg.indexOf("setSoundThreshold") >= 0) {
      int idx = msg.indexOf("\"value\":");
      if (idx != -1) {
        String valStr = msg.substring(idx + 8);
        valStr.trim();
        valStr.replace("}", "");
        valStr.replace("\"", "");
        unsigned int newSoundTh = valStr.toInt();
        if (newSoundTh >= 300) {
          SOUND_THRESHOLD = newSoundTh;
          Serial.print("SOUND_THRESHOLD changed to: ");
          Serial.println(SOUND_THRESHOLD);
        }
      }
    }
    else {
      Serial.println("[Arduino] Unknown command in payload");
    }
  } else {
    Serial.println("[Arduino] Not a CMD topic; ignoring.");
  }
}

//-----------------------------------
// Classification logic
//-----------------------------------
const char* classifyLight(int value) {
  if (value < 20) {
    return "Very dark";
  } else if (value < 50) {
    return "Dim";
  } else if (value < LIGHT_THRESHOLD) {
    return "Bright";
  } else {
    return "Very bright";
  }
}

const char* classifySound(double rms) {
  if (rms < 100) {
    return "Silent";
  } else if (rms < 300) {
    return "Small noise";
  } else if (rms < SOUND_THRESHOLD) {
    return "Quite loud";
  } else {
    return "Very loud!";
  }
}

const char* classifyMotion(bool motionState) {
  return motionState ? "Motion detected!" : "No motion...";
}

//------------------------------------------
// WiFi + MQTT Reconnect
//------------------------------------------
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
    // "ESP32Client" is the client name; can be anything unique
    if (client.connect("ESP32Client", mqtt_user, mqtt_pass)) {
      Serial.println("connected!");
      // Re-subscribe every time we reconnect
      client.subscribe(topicCmd);
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println("; try again in 5 seconds");
      delay(5000);
    }
  }
}