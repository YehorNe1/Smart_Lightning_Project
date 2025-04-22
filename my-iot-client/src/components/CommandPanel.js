import React from 'react';
import { Paper, Typography, Box, Grid, TextField, Button } from '@mui/material';

function CommandPanel({
                        intervalValue,
                        lightThresholdValue,
                        soundThresholdValue,
                        setIntervalValue,
                        setLightThresholdValue,
                        setSoundThresholdValue,
                        handleSetInterval,
                        handleSetLightThreshold,
                        handleSetSoundThreshold
                      }) {
  return (
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
  );
}

export default CommandPanel;
