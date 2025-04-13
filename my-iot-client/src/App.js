import React, { useEffect, useState } from 'react';
import {
  AppBar,
  Toolbar,
  Typography,
  Container,
  Paper,
  Button,
  TextField,
  Grid,
  Box
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
import 'chartjs-adapter-date-fns';
import { Line } from 'react-chartjs-2';

// Register Chart.js components
ChartJS.register(
  TimeScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend
);

function App() {
  // Address of the Fleck WebSocket server
  const WS_SERVER_URL = "ws://localhost:8181";

  const [socket, setSocket] = useState(null);

  // Chart data states
  const [lightData, setLightData] = useState([]);
  const [soundData, setSoundData] = useState([]);
  const [motionEvents, setMotionEvents] = useState([]);

  // Command parameters
  const [intervalValue, setIntervalValue] = useState('2000');
  const [lightThresholdValue, setLightThresholdValue] = useState('100');
  const [soundThresholdValue, setSoundThresholdValue] = useState('600');

  useEffect(() => {
    const ws = new WebSocket(WS_SERVER_URL);
    setSocket(ws);

    ws.onopen = () => {
      console.log("WebSocket connected");
    };

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data);
        // Create a Date object for the current time
        const currentTime = new Date();

        const numericLight = parseLightCategory(msg.light);
        const numericSound = parseSoundCategory(msg.sound);

        if (msg.light) {
          setLightData(prev => [...prev, { x: currentTime, y: numericLight }]);
        }
        if (msg.sound) {
          setSoundData(prev => [...prev, { x: currentTime, y: numericSound }]);
        }
        if (msg.motion === "Motion detected!") {
          setMotionEvents(prev => [
            ...prev,
            `Motion at ${currentTime.toLocaleTimeString()}`
          ]);
        }
      } catch (err) {
        console.error("Failed to parse WS message:", err);
      }
    };

    ws.onclose = () => {
      console.log("WebSocket disconnected");
    };

    return () => {
      ws.close();
    };
  }, []);

  // Converts light category string to a numeric value
  function parseLightCategory(lightStr) {
    switch (lightStr) {
      case "Very dark":   return 10;
      case "Dim":         return 30;
      case "Bright":      return 60;
      case "Very bright": return 90;
      default:            return 0;
    }
  }

  // Converts sound category string to a numeric value
  function parseSoundCategory(soundStr) {
    switch (soundStr) {
      case "Silent":      return 10;
      case "Small noise": return 30;
      case "Quite loud":  return 60;
      case "Very loud!":  return 90;
      default:            return 0;
    }
  }

  // Sends a command to the WebSocket server
  function sendCommand(command, value) {
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      console.warn("WebSocket is not connected");
      return;
    }
    const obj = { command, value: parseInt(value, 10) };

    // Log the exact JSON string we are sending:
    console.log("[React] Will send to WS:", JSON.stringify(obj));

    socket.send(JSON.stringify(obj));
    // Optional: also log the plain JS object for clarity
    console.log("[React] raw object sent:", obj);
  }

  // Handlers for each command
  const handleSetInterval = () => {
    sendCommand("setInterval", intervalValue);
  };
  const handleSetLightThreshold = () => {
    sendCommand("setLightThreshold", lightThresholdValue);
  };
  const handleSetSoundThreshold = () => {
    sendCommand("setSoundThreshold", soundThresholdValue);
  };

  // Chart.js data
  const chartData = {
    datasets: [
      {
        label: 'Light',
        data: lightData,
        borderColor: 'blue',
        backgroundColor: 'rgba(0,0,255,0.2)',
        pointRadius: 3,
        tension: 0.2,
        yAxisID: 'yLeft'
      },
      {
        label: 'Sound',
        data: soundData,
        borderColor: 'red',
        backgroundColor: 'rgba(255,0,0,0.2)',
        pointRadius: 3,
        tension: 0.2,
        yAxisID: 'yRight'
      }
    ]
  };

  // Chart.js options (time-based X axis)
  const chartOptions = {
    responsive: true,
    scales: {
      x: {
        type: 'time',
        time: {
          // unit: 'second' // or 'minute', 'hour', etc.
        },
        title: {
          display: true,
          text: 'Time'
        }
      },
      yLeft: {
        type: 'linear',
        position: 'left',
        min: 0,
        max: 100
      },
      yRight: {
        type: 'linear',
        position: 'right',
        min: 0,
        max: 100
      }
    }
  };

  return (
    <>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6">My Dashboard</Typography>
        </Toolbar>
      </AppBar>

      <Container maxWidth="md" style={{ marginTop: 20 }}>
        <Paper style={{ padding: 20, marginBottom: 20 }}>
          <Typography variant="h5" gutterBottom>
            Live Sensor Charts
          </Typography>
          <Box style={{ height: 400 }}>
            <Line data={chartData} options={chartOptions} />
          </Box>
        </Paper>

        <Paper style={{ padding: 20, marginBottom: 20 }}>
          <Typography variant="h5" gutterBottom>
            Send Commands
          </Typography>
          <Box mt={2}>
            <Grid container spacing={2}>
              {/* Interval */}
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
                  style={{ height: '56px' }}
                >
                  Set Interval
                </Button>
              </Grid>

              {/* Light threshold */}
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
                  color="secondary"
                  onClick={handleSetLightThreshold}
                  fullWidth
                  style={{ height: '56px' }}
                >
                  Set Light Threshold
                </Button>
              </Grid>

              {/* Sound threshold */}
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
                  color="primary"
                  onClick={handleSetSoundThreshold}
                  fullWidth
                  style={{ height: '56px' }}
                >
                  Set Sound Threshold
                </Button>
              </Grid>
            </Grid>
          </Box>
        </Paper>

        <Paper style={{ padding: 20 }}>
          <Typography variant="h5" gutterBottom>
            Motion Events
          </Typography>
          {motionEvents.length === 0 ? (
            <Typography>No motion yet</Typography>
          ) : (
            motionEvents.map((ev, idx) => (
              <Typography key={idx}>&bull; {ev}</Typography>
            ))
          )}
        </Paper>
      </Container>
    </>
  );
}

export default App;
