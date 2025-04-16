import React from 'react';
import { Paper, Typography, Box } from '@mui/material';
import { Line } from 'react-chartjs-2';

function SensorChart({ title, chartData, chartOptions }) {
  return (
    <Paper style={{ padding: 20, marginBottom: 20, height: 400 }}>
      <Typography variant="h5" gutterBottom>{title}</Typography>
      <Box style={{ height: '100%' }}>
        <Line data={chartData} options={chartOptions} />
      </Box>
    </Paper>
  );
}

export default SensorChart;
