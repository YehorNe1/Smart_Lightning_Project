# My IoT Project

Simple smart-home demo that streams **light, sound and motion** data from an ESP32 to the cloud and shows it live in a React dashboard.

---

## What it does

1. **ESP32 firmware**  
   * Reads LDR, microphone and PIR sensors.  
   * Publishes categories (“Very dark”, “Very loud!” …) to MQTT topics.  
   * Listens for commands → changes its sample interval and thresholds.

2. **Back-end (C# / .NET 8)**  
   * Uses **MQTTnet** to receive sensor messages and store filtered readings in MongoDB.  
   * Pushes all incoming MQTT traffic to any connected WebSocket clients.  
   * Built with **Onion Architecture**; WebSocket server built on Fleck-compatible ASP.NET core.

3. **Front-end (React 19 + MUI)**  
   * Opens a WebSocket to the back-end.  
   * Live charts (Chart.js) for the three sensor streams.  
   * Command panel to change interval / thresholds; shows ACK + adds chart annotations.

---

## Folder layout

```
MyIoTProject.Core/          Domain entities & interfaces
MyIoTProject.Application/   Business logic (services)
MyIoTProject.Infrastructure/Repositories, MQTT client
MyIoTProject.Presentation/  ASP.NET WebSocket/API host
my-iot-client/              React dashboard
firmware/                   ESP32 Arduino sketch (.ino + headers)
```

## Quick start (local)

### 1. Back-end

```bash
# Prerequisites: .NET 8 SDK, Docker (for MongoDB)
docker run -d --name mongo -p 27017:27017 mongo:latest

cd MyIoTProject.Presentation
dotnet run
```



### 2. Front-end

```bash
cd my-iot-client
npm install        # or pnpm / yarn
npm start
```

### 3. Firmware

* Open `firmware/esp32.ino` in the Arduino IDE.
* Fill in your Wi-Fi and MQTT credentials in `wifi_credentials.h`.
* Flash to an ESP32 board.

## Cloud deployment

Frontend: **https://smart-lightning-project.vercel.app**  
Backend: **https://smart-lightning-project.onrender.com**  

---

## Build scripts

```bash
# Build release container image
docker build -t my-iot-api .

# React production build
cd my-iot-client
npm run build
```