import { useState, useEffect, useCallback } from 'react';

/* ------------------------------------------------------------------
 * Helpers for converting categorical strings to numeric points that
 * we plot and for displaying the inverse mapping on the Y‑axis.
 * ------------------------------------------------------------------*/
const CATEGORY_TO_POINT_LIGHT = {
  'Very dark': 10,
  Dim: 30,
  Bright: 60,
  'Very bright': 90
};
const CATEGORY_TO_POINT_SOUND = {
  Silent: 10,
  'Small noise': 30,
  'Quite loud': 60,
  'Very loud!': 90
};
const POINT_TO_LABEL_LIGHT = Object.fromEntries(
  Object.entries(CATEGORY_TO_POINT_LIGHT).map(([k, v]) => [v, k])
);
const POINT_TO_LABEL_SOUND = Object.fromEntries(
  Object.entries(CATEGORY_TO_POINT_SOUND).map(([k, v]) => [v, k])
);
const POINT_TO_LABEL_MOTION = { 0: 'No motion', 1: 'Motion' };

/* Factory that creates a common options object and lets you pass a
 * y‑axis tick callback that turns the numeric tick into a label. */
function makeOptions(yTickCb, yMax, additionalY = {}) {
  return (currentTime, timeWindow, annotations) => ({
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        type: 'time',
        min: new Date(currentTime - timeWindow),
        max: new Date(currentTime),
        title: { display: true, text: 'Time' }
      },
      y: {
        min: 0,
        max: yMax,
        ...additionalY,
        ticks: {
          stepSize: additionalY.stepSize ?? 10,
          callback: yTickCb
        }
      }
    },
    plugins: { annotation: { annotations: Object.values(annotations) } }
  });
}

export default function useSensorData(timeWindow) {
  /* ---------------------- raw series state ---------------------- */
  const [lightData, setLightData] = useState([]);
  const [soundData, setSoundData] = useState([]);
  const [motionData, setMotionData] = useState([]);
  const [annotations, setAnnotations] = useState({});
  const [currentTime, setCurrentTime] = useState(Date.now());

  /* shift X‑axis window every second so the chart scrolls */
  useEffect(() => {
    const t = setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  /* give every chart two initial points so it renders immediately */
  useEffect(() => {
    const left = new Date(Date.now() - timeWindow);
    const now = new Date();
    setLightData([
      { x: left, y: 0 },
      { x: now, y: 0 }
    ]);
    setSoundData([
      { x: left, y: 0 },
      { x: now, y: 0 }
    ]);
    setMotionData([
      { x: left, y: 0 },
      { x: now, y: 0 }
    ]);
  }, [timeWindow]);

  /* ------------------- helpers to parse messages ---------------- */
  const toPointLight = (s) => CATEGORY_TO_POINT_LIGHT[s] ?? 0;
  const toPointSound = (s) => CATEGORY_TO_POINT_SOUND[s] ?? 0;

  /* -------------------------------------------------------------- */
  const handleSensorMessage = useCallback(
    (msg) => {
      const nowTime = Date.now();

      setLightData((prev) => {
        const filtered = prev.filter((pt) => pt.x.getTime() >= nowTime - timeWindow);
        return [...filtered, { x: new Date(), y: toPointLight(msg.light) }];
      });

      setSoundData((prev) => {
        const filtered = prev.filter((pt) => pt.x.getTime() >= nowTime - timeWindow);
        return [...filtered, { x: new Date(), y: toPointSound(msg.sound) }];
      });

      setMotionData((prev) => {
        const filtered = prev.filter((pt) => pt.x.getTime() >= nowTime - timeWindow);
        const v = msg.motion === 'Motion detected!' ? 1 : 0;
        return [...filtered, { x: new Date(), y: v }];
      });
    },
    [timeWindow]
  );

  /* Stable helper for external additions of annotations */
  const addAnnotation = useCallback((anno) => {
    setAnnotations((prev) => ({ ...prev, [anno.id]: anno.cfg }));
  }, []);

  /* ------------------------ chart options ----------------------- */
  const lightChartOptions = makeOptions(
    (v) => POINT_TO_LABEL_LIGHT[v] ?? '',
    100
  )(currentTime, timeWindow, annotations);

  const soundChartOptions = makeOptions(
    (v) => POINT_TO_LABEL_SOUND[v] ?? '',
    100
  )(currentTime, timeWindow, annotations);

  const motionChartOptions = makeOptions(
    (v) => POINT_TO_LABEL_MOTION[v] ?? '',
    1.5,
    { stepSize: 0.5 }
  )(currentTime, timeWindow, annotations);

  /* ------------------------- datasets --------------------------- */
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

  return {
    chartDataLight,
    chartDataSound,
    chartDataMotion,
    lightChartOptions,
    soundChartOptions,
    motionChartOptions,
    handleSensorMessage,
    addAnnotation
  };
}
