import React from 'react';
import { Paper, Typography, Box } from '@mui/material';

function CurrentSettings({ currentInterval, currentLightTh, currentSoundTh }) {
  /* Fallbacks so the ranges are still meaningful until the first config comes in */
  const lightTh = currentLightTh === 'unknown' ? 100 : Number(currentLightTh);
  const soundTh = currentSoundTh === 'unknown' ? 600 : Number(currentSoundTh);

  const lightLevels = [
    { label: 'Very dark', range: '< 20' },
    { label: 'Dim', range: '20 – 49' },
    { label: 'Bright', range: `50 – ${lightTh - 1}` },
    { label: 'Very bright', range: `≥ ${lightTh}` }
  ];

  const soundLevels = [
    { label: 'Silent', range: '< 100' },
    { label: 'Small noise', range: '100 – 299' },
    { label: 'Quite loud', range: `300 – ${soundTh - 1}` },
    { label: 'Very loud', range: `≥ ${soundTh}` }
  ];

  return (
    <Paper style={{ padding: 20, marginBottom: 20 }}>
      <Typography variant="h5" gutterBottom>
        Current Arduino Settings
      </Typography>

      <Typography>Interval: {currentInterval}</Typography>
      <Typography>Light Threshold: {currentLightTh}</Typography>
      <Typography>Sound Threshold: {currentSoundTh}</Typography>

      <Box mt={2}>
        <Typography variant="subtitle1">Light level mapping:</Typography>
        {lightLevels.map((l) => (
          <Typography key={l.label} variant="body2">
            {l.range} → {l.label}
          </Typography>
        ))}
      </Box>

      <Box mt={2}>
        <Typography variant="subtitle1">Sound level mapping:</Typography>
        {soundLevels.map((s) => (
          <Typography key={s.label} variant="body2">
            {s.range} → {s.label}
          </Typography>
        ))}
      </Box>

      <Typography variant="caption" color="textSecondary">
        (Updates occur upon ACK/config response)
      </Typography>
    </Paper>
  );
}

export default CurrentSettings;
