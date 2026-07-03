using ISO11820.Global;
using ISO11820.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ISO11820.Services;

/// <summary>
/// 温度仿真引擎 — 5通道温度仿真
/// </summary>
public class SensorSimulator
{
    private readonly Random _rng = new();
    private readonly object _lock = new();

    // 5通道温度值
    public double TF1 { get; private set; }  // 炉温1
    public double TF2 { get; private set; }  // 炉温2
    public double TS { get; private set; }   // 表面温
    public double TC { get; private set; }   // 中心温
    public double TCal { get; private set; } // 校准温

    // PID输出值（仿真计算用）
    public int PidOutput { get; private set; }

    // 仿真内部状态
    private bool _isRecording;
    private double _lastTF1, _lastTF2, _lastTS, _lastTC;

    /// <summary>
    /// 初始化仿真引擎
    /// </summary>
    public void Initialize(double initialTemp)
    {
        var config = AppRuntime.Instance.Configuration;
        double fluctuation = config.GetValue<double>("Simulation:TempFluctuation", 0.5);

        TF1 = initialTemp + Noise(fluctuation);
        TF2 = initialTemp + Noise(fluctuation);
        TS = initialTemp * 0.3 + Noise(fluctuation);
        TC = initialTemp * 0.25 + Noise(fluctuation);
        TCal = TF1 + Noise(fluctuation) * 2;
        PidOutput = config.GetValue<int>("Hardware:ConstPower", 2048);

        _lastTF1 = TF1; _lastTF2 = TF2; _lastTS = TS; _lastTC = TC;
        _isRecording = false;
    }

    /// <summary>
    /// 每800ms更新一次
    /// </summary>
    /// <param name="status">当前试验状态</param>
    public void Update(TestStatus status)
    {
        var config = AppRuntime.Instance.Configuration;
        double targetTemp = config.GetValue<double>("Simulation:TargetFurnaceTemp", 750.0);
        double heatingRate = config.GetValue<double>("Simulation:HeatingRatePerSecond", 40.0);
        double fluctuation = config.GetValue<double>("Simulation:TempFluctuation", 0.5);

        lock (_lock)
        {
            _isRecording = (status == TestStatus.Recording);

            switch (status)
            {
                case TestStatus.Idle:
                    // 空闲：缓慢冷却
                    TF1 = Math.Max(25, TF1 - 0.5 + Noise(fluctuation * 0.1));
                    TF2 = Math.Max(25, TF2 - 0.5 + Noise(fluctuation * 0.1));
                    TS = TF1 * 0.3 + Noise(fluctuation);
                    TC = TF1 * 0.25 + Noise(fluctuation);
                    TCal = TF1 + Noise(fluctuation) * 2;
                    PidOutput = 0;
                    break;

                case TestStatus.Preparing:
                case TestStatus.Ready:
                    UpdateHeating(targetTemp, heatingRate, fluctuation);
                    break;

                case TestStatus.Recording:
                    UpdateHeating(targetTemp, heatingRate, fluctuation);
                    UpdateSampleTemps(fluctuation);
                    break;

                case TestStatus.Complete:
                    UpdateHeating(targetTemp, heatingRate, fluctuation); // 保持温度
                    UpdateSampleTemps(fluctuation);
                    break;
            }

            _lastTF1 = TF1; _lastTF2 = TF2; _lastTS = TS; _lastTC = TC;
        }
    }

    /// <summary>
    /// 升温阶段 / 稳定阶段
    /// </summary>
    private void UpdateHeating(double targetTemp, double heatingRate, double fluctuation)
    {
        double step = heatingRate * 0.8; // 每800ms

        if (TF1 < targetTemp - 3.0) // StableThreshold = 3.0
        {
            // 升温阶段
            TF1 += step + Noise(fluctuation);
            TF2 += step + Noise(fluctuation);
        }
        else
        {
            // 稳定阶段：钳位到目标温度
            TF1 = targetTemp + Noise(fluctuation);
            TF2 = targetTemp + Noise(fluctuation);
        }

        // 非记录阶段：样品温为低值跟随
        if (!_isRecording)
        {
            TS = TF1 * 0.3 + Noise(fluctuation);
            TC = TF1 * 0.25 + Noise(fluctuation);
        }

        TCal = TF1 + Noise(fluctuation) * 2;

        // PID输出（仿真：温度越低输出越大）
        double pidPercent = Math.Max(0, Math.Min(1, (targetTemp - TF1) / targetTemp));
        PidOutput = (int)(pidPercent * 25600);
    }

    /// <summary>
    /// 记录阶段样品温度计算
    /// </summary>
    private void UpdateSampleTemps(double fluctuation)
    {
        // 表面温：向 TF1 × 0.95 指数接近
        double surfaceTarget = Math.Min(TF1 * 0.95, 800);
        TS += (surfaceTarget - TS) * 0.02 + Noise(fluctuation);

        // 中心温：向 TF1 × 0.85 指数接近（更慢）
        double centerTarget = Math.Min(TF1 * 0.85, 750);
        TC += (centerTarget - TC) * 0.01 + Noise(fluctuation);
    }

    /// <summary>
    /// 随机噪声
    /// </summary>
    private double Noise(double amplitude)
    {
        return (_rng.NextDouble() * 2 - 1) * amplitude;
    }

    /// <summary>
    /// 获取5通道传感器数据
    /// </summary>
    public Dictionary<int, SensorData> GetSensorData()
    {
        lock (_lock)
        {
            return new Dictionary<int, SensorData>
            {
                [0] = new SensorData { SensorId = 0, SensorName = "Sensor0", DispName = "炉温1", Value = Math.Round(TF1, 1), Unit = "℃" },
                [1] = new SensorData { SensorId = 1, SensorName = "Sensor1", DispName = "炉温2", Value = Math.Round(TF2, 1), Unit = "℃" },
                [2] = new SensorData { SensorId = 2, SensorName = "Sensor2", DispName = "表面温度", Value = Math.Round(TS, 1), Unit = "℃" },
                [3] = new SensorData { SensorId = 3, SensorName = "Sensor3", DispName = "中心温度", Value = Math.Round(TC, 1), Unit = "℃" },
                [16] = new SensorData { SensorId = 16, SensorName = "Sensor16", DispName = "校准温度", Value = Math.Round(TCal, 1), Unit = "℃" }
            };
        }
    }

    /// <summary>
    /// 获取CSV格式的温度行
    /// </summary>
    public string GetCsvLine(int timeSeconds)
    {
        lock (_lock)
        {
            return $"{timeSeconds},{TF1:F1},{TF2:F1},{TS:F1},{TC:F1},{TCal:F1}";
        }
    }
}
