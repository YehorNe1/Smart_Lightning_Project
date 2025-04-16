import React, { useState } from 'react';
import { Container, Snackbar, Alert } from '@mui/material';
import Navbar from './components/Navbar';
import CurrentSettings from './components/CurrentSettings';
import SensorChart from './components/SensorChart';
import CommandPanel from './components/CommandPanel';
import useSensorData from './hooks/useSensorData';
import useWebSocket from './hooks/useWebSocket';

function App() {
  const timeWindow = 30000;

  const {
    chartDataLight,
    chartDataSound,
    chartDataMotion,
    sharedChartOptions,
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
  } = useWebSocket('ws://localhost:8181', handleSensorMessage, addAnnotation);

  const [intervalValue, setIntervalValue] = useState('2000');
  const [lightThresholdValue, setLightThresholdValue] = useState('100');
  const [soundThresholdValue, setSoundThresholdValue] = useState('600');

  const handleSetInterval = () => sendCommand('setInterval', intervalValue);
  const handleSetLightThreshold = () => sendCommand('setLightThreshold', lightThresholdValue);
  const handleSetSoundThreshold = () => sendCommand('setSoundThreshold', soundThresholdValue);

  return (
    <>
      <Navbar />

      <Container maxWidth="md" style={{ marginTop: 20 }}>
        <CurrentSettings
          currentInterval={currentInterval}
          currentLightTh={currentLightTh}
          currentSoundTh={currentSoundTh}
        />

        <SensorChart
          title="Light Sensor Chart"
          chartData={chartDataLight}
          chartOptions={sharedChartOptions}
        />
        <SensorChart
          title="Sound Sensor Chart"
          chartData={chartDataSound}
          chartOptions={sharedChartOptions}
        />
        <SensorChart
          title="Motion Sensor Chart"
          chartData={chartDataMotion}
          chartOptions={motionChartOptions}
        />

        <CommandPanel
          intervalValue={intervalValue}
          lightThresholdValue={lightThresholdValue}
          soundThresholdValue={soundThresholdValue}
          setIntervalValue={setIntervalValue}
          setLightThresholdValue={setLightThresholdValue}
          setSoundThresholdValue={setSoundThresholdValue}
          handleSetInterval={handleSetInterval}
          handleSetLightThreshold={handleSetLightThreshold}
          handleSetSoundThreshold={handleSetSoundThreshold}
        />
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
