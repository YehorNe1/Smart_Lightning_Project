import React, { useState } from 'react';
import { Container, Snackbar, Alert } from '@mui/material';
import Navbar from './components/Navbar';
import CurrentSettings from './components/CurrentSettings';
import SensorChart from './components/SensorChart';
import useSensorData from './hooks/useSensorData';
import useWebSocket from './hooks/useWebSocket';
import CommandPanel from './components/CommandPanel';

export default function App() {
  const timeWindow = 30000;

  const {
    chartDataLight,
    chartDataSound,
    chartDataMotion,
    lightChartOptions,
    soundChartOptions,
    motionChartOptions,
    handleSensorMessage,
    addAnnotation
  } = useSensorData(timeWindow);

  const {
    currentInterval,
    currentLightTh,
    currentSoundTh,
    sendCommand,
    notificationOpen,
    notificationMessage,
    handleCloseNotification
  } = useWebSocket(handleSensorMessage, addAnnotation);   // <‑‑ URL берётся из хука

  const [intervalValue,       setIntervalValue]       = useState('2000');
  const [lightThresholdValue, setLightThresholdValue] = useState('100');
  const [soundThresholdValue, setSoundThresholdValue] = useState('600');

  return (
    <>
      <Navbar />

      <Container maxWidth="md" sx={{ mt: 2 }}>
        <CurrentSettings
          currentInterval={currentInterval}
          currentLightTh={currentLightTh}
          currentSoundTh={currentSoundTh}
        />

        <SensorChart title="Light"  chartData={chartDataLight}  chartOptions={lightChartOptions}  />
        <SensorChart title="Sound"  chartData={chartDataSound}  chartOptions={soundChartOptions}  />
        <SensorChart title="Motion" chartData={chartDataMotion} chartOptions={motionChartOptions} />

        <CommandPanel
          intervalValue={intervalValue}
          lightThresholdValue={lightThresholdValue}
          soundThresholdValue={soundThresholdValue}
          setIntervalValue={setIntervalValue}
          setLightThresholdValue={setLightThresholdValue}
          setSoundThresholdValue={setSoundThresholdValue}
          handleSetInterval       ={() => sendCommand('setInterval',       intervalValue)}
          handleSetLightThreshold ={() => sendCommand('setLightThreshold', lightThresholdValue)}
          handleSetSoundThreshold ={() => sendCommand('setSoundThreshold', soundThresholdValue)}
        />
      </Container>

      <Snackbar
        open={notificationOpen}
        autoHideDuration={3000}
        onClose={handleCloseNotification}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="success" onClose={handleCloseNotification} sx={{ width: '100%' }}>
          {notificationMessage}
        </Alert>
      </Snackbar>
    </>
  );
}
