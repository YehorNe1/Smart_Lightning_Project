import { useState, useEffect, useCallback } from 'react';

export default function useSensorData(timeWindow) {
  const [lightData, setLightData] = useState([]);
  const [soundData, setSoundData] = useState([]);
  const [motionData, setMotionData] = useState([]);
  const [annotations, setAnnotations] = useState({});
  const [currentTime, setCurrentTime] = useState(Date.now());

  /* таймер сдвига оси X */
  useEffect(() => {
    const t = setInterval(() => setCurrentTime(Date.now()), 1000);
    return () => clearInterval(t);
  }, []);

  /* стартовые точки графиков */
  useEffect(() => {
    const left = new Date(Date.now() - timeWindow);
    const now = new Date();
    setLightData([{ x: left, y: 0 }, { x: now, y: 0 }]);
    setSoundData([{ x: left, y: 0 }, { x: now, y: 0 }]);
    setMotionData([{ x: left, y: 0 }, { x: now, y: 0 }]);
  }, [timeWindow]);

  /* парсеры категорий */
  const parseLight = (s) =>
    s === 'Very dark' ? 10 : s === 'Dim' ? 30 : s === 'Bright' ? 60 : s === 'Very bright' ? 90 : 0;

  const parseSound = (s) =>
    s === 'Silent'
      ? 10
      : s === 'Small noise'
        ? 30
        : s === 'Quite loud'
          ? 60
          : s === 'Very loud!'
            ? 90
            : 0;

  /* входящее сообщение датчиков */
  const handleSensorMessage = useCallback(
    (msg) => {
      const nowTime = Date.now();

      setLightData((p) => {
        const f = p.filter((pt) => pt.x.getTime() >= nowTime - timeWindow);
        return [...f, { x: new Date(), y: parseLight(msg.light) }];
      });

      setSoundData((p) => {
        const f = p.filter((pt) => pt.x.getTime() >= nowTime - timeWindow);
        return [...f, { x: new Date(), y: parseSound(msg.sound) }];
      });

      setMotionData((p) => {
        const f = p.filter((pt) => pt.x.getTime() >= nowTime - timeWindow);
        const v = msg.motion === 'Motion detected!' ? 1 : 0;
        return [...f, { x: new Date(), y: v }];
      });
    },
    [timeWindow]
  );

  /* >>> СТАБИЛЬНАЯ <<< функция для добавления аннотаций */
  const addAnnotation = useCallback((anno) => {
    setAnnotations((p) => ({ ...p, [anno.id]: anno.cfg }));
  }, []);

  /* общие опции графиков */
  const sharedChartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    scales: {
      x: {
        type: 'time',
        min: new Date(currentTime - timeWindow),
        max: new Date(currentTime),
        title: { display: true, text: 'Time' }
      },
      y: { min: 0, max: 100 }
    },
    plugins: { annotation: { annotations: Object.values(annotations) } }
  };

  const motionChartOptions = {
    ...sharedChartOptions,
    scales: {
      ...sharedChartOptions.scales,
      y: { min: 0, max: 1.5, ticks: { stepSize: 0.5 } }
    }
  };

  /* datasets */
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
    sharedChartOptions,
    motionChartOptions,
    handleSensorMessage,
    addAnnotation
  };
}
