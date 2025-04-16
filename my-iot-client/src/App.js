import React, { useEffect, useState, useCallback } from 'react';
import {
  AppBar,
  Toolbar,
  Typography,
  Container,
  Paper,
  Button,
  TextField,
  Grid,
  Box,
  Snackbar,
  Alert
} from '@mui/material';
import {
  Chart as ChartJS,
  TimeScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
} from 'chart.js';
import annotationPlugin from 'chartjs-plugin-annotation';
import 'chartjs-adapter-date-fns';
import { Line } from 'react-chartjs-2';

// Register Chart.js components and annotation plugin
ChartJS.register(
  TimeScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  annotationPlugin
);

function App() {
  const WS_SERVER_URL = "ws://localhost:8181";

  // State variables for data and WebSocket
  const [socket, setSocket] = useState(null);
  const [lightData, setLightData] = useState([]);
  const [soundData, setSoundData] = useState([]);
  const [motionData, setMotionData] = useState([]);

  // State variables for Arduino configuration settings
  const [currentInterval, setCurrentInterval] = useState("unknown");
  const [currentLightTh, setCurrentLightTh] = useState("unknown");
  const [currentSoundTh, setCurrentSoundTh] = useState("unknown");

  // Input fields for commands
  const [intervalValue, setIntervalValue] = useState('2000');
  const [lightThresholdValue, setLightThresholdValue] = useState('100');
  const [soundThresholdValue, setSoundThresholdValue] = useState('600');

  // State for annotations (vertical lines)
  const [annotations, setAnnotations] = useState({});

  // State for notification
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [notificationMessage, setNotificationMessage] = useState("");

  // Fixed time window for charts (e.g., 30 seconds)
  const timeWindow = 30000; // 30 seconds

  // Current time for dynamic X-axis shifting
  const [currentTime, setCurrentTime] = useState(Date.now());
  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);

  // Initial data setup for all charts (light, sound, motion)
  useEffect(() => {
    const leftTime = new Date(Date.now() - timeWindow);
    const now = new Date();
    setLightData([{ x: leftTime, y: 0 }, { x: now, y: 0 }]);
    setSoundData([{ x: leftTime, y: 0 }, { x: now, y: 0 }]);
    setMotionData([{ x: leftTime, y: 0 }, { x: now, y: 0 }]);
  }, [timeWindow]);

  // Handle incoming sensor data
  const handleSensorMessage = useCallback((msg) => {
    const nowTime = Date.now();
    // Update data for the light chart
    setLightData(prev => {
      const filtered = prev.filter(point => point.x.getTime() >= nowTime - timeWindow);
      return [...filtered, { x: new Date(), y: parseLightCategory(msg.light) }];
    });
    // Update data for the sound chart
    setSoundData(prev => {
      const filtered = prev.filter(point => point.x.getTime() >= nowTime - timeWindow);
      return [...filtered, { x: new Date(), y: parseSoundCategory(msg.sound) }];
    });
    // Update data for the motion chart:
    // If the message contains motion = "Motion detected!" set value to 1, otherwise 0.
    setMotionData(prev => {
      const filtered = prev.filter(point => point.x.getTime() >= nowTime - timeWindow);
      const motionVal = (msg.motion === "Motion detected!") ? 1 : 0;
      return [...filtered, { x: new Date(), y: motionVal }];
    });
  }, [timeWindow]);

  // Handle ACK message (annotations and notifications)
  const handleAckMessage = useCallback((msg) => {
    console.log("ACK from Arduino:", msg);
    if (msg.ackCommand === "setInterval") {
      setCurrentInterval(msg.value);
    } else if (msg.ackCommand === "setLightThreshold") {
      setCurrentLightTh(msg.value);
    } else if (msg.ackCommand === "setSoundThreshold") {
      setCurrentSoundTh(msg.value);
    }
    const colorMap = {
      setInterval: "purple",
      setLightThreshold: "orange",
      setSoundThreshold: "teal"
    };
    const borderColor = colorMap[msg.ackCommand] || "black";
    const annoId = `ack-${Date.now()}`;
    const now = new Date();
    setAnnotations(prev => ({
      ...prev,
      [annoId]: {
        type: 'line',
        xMin: now.valueOf(),
        xMax: now.valueOf(),
        borderColor: borderColor,
        borderWidth: 2,
        label: {
          enabled: true,
          content: `${msg.ackCommand}: ${msg.value}`,
          position: 'start',
          xAdjust: -10
        }
      }
    }));
    setNotificationMessage(`Command ${msg.ackCommand} executed successfully`);
    setNotificationOpen(true);
  }, []);

  const handleConfigMessage = useCallback((cfg) => {
    console.log("CONFIG from Arduino:", cfg);
    setCurrentInterval(cfg.interval);
    setCurrentLightTh(cfg.lightThreshold);
    setCurrentSoundTh(cfg.soundThreshold);
  }, []);

  // Create WebSocket and establish connection
  useEffect(() => {
    const ws = new WebSocket(WS_SERVER_URL);
    ws.onopen = () => {
      console.log("WebSocket connected");
      ws.send(JSON.stringify({ command: "getConfig" }));
    };
    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data);
        if (msg.light || msg.sound || msg.motion) {
          handleSensorMessage(msg);
        } else if (msg.ackCommand) {
          handleAckMessage(msg);
        } else if (typeof msg.interval !== "undefined") {
          handleConfigMessage(msg);
        } else {
          console.log("Unknown message from WS:", msg);
        }
      } catch (err) {
        console.error("Failed to parse WS message:", err);
      }
    };
    ws.onclose = () => console.log("WebSocket disconnected");
    setSocket(ws);
    return () => ws.close();
  }, [handleSensorMessage, handleAckMessage, handleConfigMessage]);

  const handleCloseNotification = (event, reason) => {
    if (reason === 'clickaway') return;
    setNotificationOpen(false);
  };

  // Function to parse string category values for light
  function parseLightCategory(lightStr) {
    switch (lightStr) {
      case "Very dark":   return 10;
      case "Dim":         return 30;
      case "Bright":      return 60;
      case "Very bright": return 90;
      default:            return 0;
    }
  }

  // Function to parse string category values for sound
  function parseSoundCategory(soundStr) {
    switch (soundStr) {
      case "Silent":      return 10;
      case "Small noise": return 30;
      case "Quite loud":  return 60;
      case "Very loud!":  return 90;
      default:            return 0;
    }
  }

  // Send command via WebSocket
  function sendCommand(command, value) {
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      console.warn("WebSocket not connected");
      return;
    }
    const obj = { command, value: parseInt(value, 10) };
    console.log("Sending command:", obj);
    socket.send(JSON.stringify(obj));
  }

  const handleSetInterval = () => { sendCommand("setInterval", intervalValue); };
  const handleSetLightThreshold = () => { sendCommand("setLightThreshold", lightThresholdValue); };
  const handleSetSoundThreshold = () => { sendCommand("setSoundThreshold", soundThresholdValue); };

  // Common options for light and sound charts (annotations added)
  const sharedChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        type: 'time',
        time: {},
        min: new Date(currentTime - timeWindow),
        max: new Date(currentTime),
        title: { display: true, text: 'Time' }
      },
      y: {
        type: 'linear',
        min: 0,
        max: 100
      }
    },
    plugins: {
      annotation: {
        annotations: Object.values(annotations)
      }
    }
  };

  // Options for the motion chart (Y-axis from 0 to 1.5, annotations enabled)
  const motionChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        type: 'time',
        time: {},
        min: new Date(currentTime - timeWindow),
        max: new Date(currentTime),
        title: { display: true, text: 'Time' }
      },
      y: {
        type: 'linear',
        min: 0,
        max: 1.5,
        ticks: {
          stepSize: 0.5
        }
      }
    },
    plugins: {
      annotation: {
        annotations: Object.values(annotations)
      }
    }
  };

  // Datasets for charts
  const chartDataLight = {
    datasets: [
      {
        label: 'Light',
        data: lightData,
        borderColor: 'blue',
        backgroundColor: 'rgba(0,0,255,0.2)',
        pointRadius: 3,
        tension: 0.4,
        cubicInterpolationMode: 'monotone'
      }
    ]
  };

  const chartDataSound = {
    datasets: [
      {
        label: 'Sound',
        data: soundData,
        borderColor: 'red',
        backgroundColor: 'rgba(255,0,0,0.2)',
        pointRadius: 3,
        tension: 0.4,
        cubicInterpolationMode: 'monotone'
      }
    ]
  };

  const chartDataMotion = {
    datasets: [
      {
        label: 'Motion',
        data: motionData,
        borderColor: 'black',
        backgroundColor: 'rgba(0,0,0,0.2)',
        pointRadius: 3,
        tension: 0.4,
        cubicInterpolationMode: 'monotone'
      }
    ]
  };

  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6">My IoT Dashboard</Typography>
        </Toolbar>
      </AppBar>

      <Container maxWidth="md" style={{ marginTop: 20 }}>
        {/* Current Arduino Settings */}
        <Paper style={{ padding: 20, marginBottom: 20 }}>
          <Typography variant="h5" gutterBottom>Current Arduino Settings</Typography>
          <Typography>Interval: {currentInterval}</Typography>
          <Typography>Light Threshold: {currentLightTh}</Typography>
          <Typography>Sound Threshold: {currentSoundTh}</Typography>
          <Typography variant="body2" color="textSecondary">
            (Updates occur upon ACK/config response)
          </Typography>
        </Paper>

        {/* Light Chart */}
        <Paper style={{ padding: 20, marginBottom: 20, height: 400 }}>
          <Typography variant="h5" gutterBottom>Light Sensor Chart</Typography>
          <Box style={{ height: "100%" }}>
            <Line data={chartDataLight} options={sharedChartOptions} />
          </Box>
        </Paper>

        {/* Sound Chart */}
        <Paper style={{ padding: 20, marginBottom: 20, height: 400 }}>
          <Typography variant="h5" gutterBottom>Sound Sensor Chart</Typography>
          <Box style={{ height: "100%" }}>
            <Line data={chartDataSound} options={sharedChartOptions} />
          </Box>
        </Paper>

        {/* Motion Chart */}
        <Paper style={{ padding: 20, marginBottom: 20, height: 400 }}>
          <Typography variant="h5" gutterBottom>Motion Sensor Chart</Typography>
          <Box style={{ height: "100%" }}>
            <Line data={chartDataMotion} options={motionChartOptions} />
          </Box>
        </Paper>

        {/* Command Panel */}
        <Paper style={{ padding: 20, marginBottom: 20 }}>
          <Typography variant="h5" gutterBottom>Send Commands</Typography>
          <Box mt={2}>
            <Grid container spacing={2}>
              <Grid item xs={12} md={6}>
                <TextField
                  label="Interval (ms)"
                  variant="outlined"
                  value={intervalValue}
                  onChange={(e) => setIntervalValue(e.target.value)}
                  fullWidth
                />
              </Grid>
              <Grid item xs={12} md={6}>
                <Button
                  variant="contained"
                  onClick={handleSetInterval}
                  fullWidth
                  style={{ height: '56px', backgroundColor: 'purple', color: '#fff' }}
                >
                  Set Interval
                </Button>
              </Grid>
              <Grid item xs={12} md={6}>
                <TextField
                  label="Light Threshold"
                  variant="outlined"
                  value={lightThresholdValue}
                  onChange={(e) => setLightThresholdValue(e.target.value)}
                  fullWidth
                />
              </Grid>
              <Grid item xs={12} md={6}>
                <Button
                  variant="contained"
                  onClick={handleSetLightThreshold}
                  fullWidth
                  style={{ height: '56px', backgroundColor: 'orange', color: '#fff' }}
                >
                  Set Light Threshold
                </Button>
              </Grid>
              <Grid item xs={12} md={6}>
                <TextField
                  label="Sound Threshold"
                  variant="outlined"
                  value={soundThresholdValue}
                  onChange={(e) => setSoundThresholdValue(e.target.value)}
                  fullWidth
                />
              </Grid>
              <Grid item xs={12} md={6}>
                <Button
                  variant="contained"
                  onClick={handleSetSoundThreshold}
                  fullWidth
                  style={{ height: '56px', backgroundColor: 'teal', color: '#fff' }}
                >
                  Set Sound Threshold
                </Button>
              </Grid>
            </Grid>
          </Box>
        </Paper>
      </Container>

      <Snackbar
        open={notificationOpen}
        autoHideDuration={3000}
        onClose={handleCloseNotification}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert onClose={handleCloseNotification} severity="success" sx={{ width: '100%' }}>
          {notificationMessage}
        </Alert>
      </Snackbar>
    </>
  );
}

export default App;
