import { useState, useEffect, useCallback, useRef } from 'react';

export default function useWebSocket(url, handleSensorMessage, addAnnotation) {
  const [socket, setSocket] = useState(null);
  const [currentInterval, setCurrentInterval] = useState('unknown');
  const [currentLightTh, setCurrentLightTh] = useState('unknown');
  const [currentSoundTh, setCurrentSoundTh] = useState('unknown');
  const [notificationOpen, setNotificationOpen] = useState(false);
  const [notificationMessage, setNotificationMessage] = useState('');

  const sensorCbRef = useRef(handleSensorMessage);
  const addAnnoRef = useRef(addAnnotation);

  useEffect(() => { sensorCbRef.current = handleSensorMessage; }, [handleSensorMessage]);
  useEffect(() => { addAnnoRef.current = addAnnotation; }, [addAnnotation]);

  const handleAckMessage = useCallback((msg) => {
    if (msg.ackCommand === 'setInterval') setCurrentInterval(msg.value);
    else if (msg.ackCommand === 'setLightThreshold') setCurrentLightTh(msg.value);
    else if (msg.ackCommand === 'setSoundThreshold') setCurrentSoundTh(msg.value);

    const color =
      msg.ackCommand === 'setInterval'
        ? 'purple'
        : msg.ackCommand === 'setLightThreshold'
          ? 'orange'
          : 'teal';

    addAnnoRef.current({
      id: `ack-${Date.now()}`,
      cfg: {
        type: 'line',
        xMin: Date.now(),
        xMax: Date.now(),
        borderColor: color,
        borderWidth: 2,
        label: { enabled: true, content: `${msg.ackCommand}: ${msg.value}`, position: 'start', xAdjust: -10 }
      }
    });

    setNotificationMessage(`Command ${msg.ackCommand} executed successfully`);
    setNotificationOpen(true);
  }, []);

  const handleConfigMessage = useCallback((cfg) => {
    setCurrentInterval(cfg.interval);
    setCurrentLightTh(cfg.lightThreshold);
    setCurrentSoundTh(cfg.soundThreshold);
  }, []);

  /* WS is created ONCE */
  useEffect(() => {
    const ws = new WebSocket(url);

    ws.onopen = () => {
      ws.send(JSON.stringify({ command: 'getConfig' }));
    };

    ws.onmessage = (e) => {
      try {
        const msg = JSON.parse(e.data);
        if (msg.light || msg.sound || msg.motion) sensorCbRef.current(msg);
        else if (msg.ackCommand) handleAckMessage(msg);
        else if (typeof msg.interval !== 'undefined') handleConfigMessage(msg);
      } catch {}
    };

    setSocket(ws);
    return () => ws.close();
  }, [url, handleAckMessage, handleConfigMessage]);

  const sendCommand = (command, value) => {
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(JSON.stringify({ command, value: parseInt(value, 10) }));
    }
  };

  const handleCloseNotification = (_, r) => {
    if (r !== 'clickaway') setNotificationOpen(false);
  };

  return {
    currentInterval,
    currentLightTh,
    currentSoundTh,
    sendCommand,
    notificationOpen,
    notificationMessage,
    handleCloseNotification
  };
}
