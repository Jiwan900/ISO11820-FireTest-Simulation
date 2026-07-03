using ISO11820.Core;
using ISO11820.Global;
using ISO11820.Models;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ISO11820.Services;

/// <summary>
/// 数据采集工作线程 — 每800ms一个循环
/// </summary>
public class DaqWorker
{
    private System.Threading.Timer? _timer;
    private bool _isRunning;
    private readonly object _lock = new();

    // 温度数据缓冲（每秒记录一行到CSV）
    private readonly List<string> _csvLines = new();
    private int _csvWriteCounter;

    /// <summary>
    /// 数据广播事件（在后台线程触发）
    /// </summary>
    public event EventHandler<DataBroadcastEventArgs>? DataBroadcast;

    /// <summary>
    /// 温度历史队列（用于温漂计算）
    /// </summary>
    private readonly Queue<double> _tf1History = new(750);

    /// <summary>
    /// 启动数据采集
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _csvLines.Clear();
            _csvWriteCounter = 0;

            var config = AppRuntime.Instance.Configuration;
            double initialTemp = config.GetValue<double>("Simulation:InitialFurnaceTemp", 720.0);
            AppRuntime.Instance.Simulator.Initialize(initialTemp);

            _timer = new System.Threading.Timer(OnTick, null, 0, 800);
            Log.Information("数据采集已启动（800ms周期）");
        }
    }

    /// <summary>
    /// 停止数据采集
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
            Log.Information("数据采集已停止");
        }
    }

    /// <summary>
    /// 获取已缓存的CSV行
    /// </summary>
    public List<string> GetCsvLines()
    {
        lock (_lock) return new List<string>(_csvLines);
    }

    /// <summary>
    /// 清空CSV缓存
    /// </summary>
    public void ClearCsvBuffer()
    {
        lock (_lock) _csvLines.Clear();
    }

    /// <summary>
    /// 每800ms的定时回调
    /// </summary>
    private void OnTick(object? state)
    {
        try
        {
            var ctx = AppRuntime.Instance;
            var controller = ctx.Controller;
            var simulator = ctx.Simulator;

            // 1. 仿真更新
            simulator.Update(controller.Status);

            // 2. 控制器tick
            controller.Tick(simulator.TF1, simulator.TF2);

            // 3. 维护温度历史
            _tf1History.Enqueue(simulator.TF1);
            while (_tf1History.Count > 750) _tf1History.Dequeue();

            // 4. 计算温漂
            double drift = DriftCalculator.CalculateDrift(_tf1History.ToArray(), 0.8);

            // 5. 记录中：每秒写一行CSV数据
            if (controller.Status == TestStatus.Recording)
            {
                _csvWriteCounter++;
                if (_csvWriteCounter >= 1) // 每800ms ≈ 1秒近似
                {
                    _csvLines.Add(simulator.GetCsvLine(controller.ElapsedSeconds));
                    _csvWriteCounter = 0;
                }
            }

            // 6. 构建消息列表
            var messages = new List<MasterMessage>();
            // 消息由外部状态变更事件产生，此处仅传递空列表或增量消息

            // 7. 广播数据
            var args = new DataBroadcastEventArgs
            {
                SensorValues = simulator.GetSensorData(),
                Messages = messages,
                CurrentStatus = controller.Status,
                ElapsedSeconds = controller.Status == TestStatus.Recording ? controller.ElapsedSeconds : 0,
                TemperatureDrift = drift
            };
            DataBroadcast?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据采集循环异常");
        }
    }
}
