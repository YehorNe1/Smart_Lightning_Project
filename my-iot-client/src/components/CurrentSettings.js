import React from 'react';
import { Paper, Typography } from '@mui/material';

function CurrentSettings({ currentInterval, currentLightTh, currentSoundTh }) {
  return (
    <Paper style={{ padding: 20, marginBottom: 20 }}>
      <Typography variant="h5" gutterBottom>Current Arduino Settings</Typography>
      <Typography>Interval: {currentInterval}</Typography>
      <Typography>Light Threshold: {currentLightTh}</Typography>
      <Typography>Sound Threshold: {currentSoundTh}</Typography>
      <Typography variant="body2" color="textSecondary">
        (Updates occur upon ACK/config response)
      </Typography>
    </Paper>
  );
}

export default CurrentSettings;
