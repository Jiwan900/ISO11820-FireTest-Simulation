using ISO11820.Models;
using ISO11820.Global;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ISO11820.Core;

/// <summary>
/// 试验控制器 — 管理5状态试验状态机
/// </summary>
public class TestController
{
    private TestStatus _status = TestStatus.Idle;
    private readonly object _lock = new();

    // 温度历史队列（用于稳定判定和温漂计算）
    private readonly Queue<double> _tf1History = new(750); // 10分钟 × 60秒/0.8s ≈ 750点

    /// <summary>
    /// 当前状态
    /// </summary>
    public TestStatus Status
    {
        get { lock (_lock) return _status; }
        private set
        {
            lock (_lock)
            {
                if (_status == value) return;
                var oldStatus = _status;
                _status = value;
                Log.Information("状态切换: {Old} → {New}", oldStatus, value);
                OnStatusChanged(oldStatus, value);
            }
        }
    }

    /// <summary>
    /// 记录中的秒数
    /// </summary>
    public int ElapsedSeconds { get; private set; }

    /// <summary>
    /// 稳定计数器（连续满足温度条件的tick数）
    /// </summary>
    public int StableCount { get; private set; }

    /// <summary>
    /// 目标试验时长（秒），0=标准60分钟
    /// </summary>
    public int TargetDurationSeconds { get; set; }

    /// <summary>
    /// 试验时长模式
    /// </summary>
    public TestDurationMode DurationMode { get; set; } = TestDurationMode.Standard60Min;

    /// <summary>
    /// PID输出值队列（用于恒功率计算）
    /// </summary>
    public Queue<int> PidQueue { get; } = new(600);

    /// <summary>
    /// 状态变更事件
    /// </summary>
    public event EventHandler<TestStatus>? StatusChanged;

    /// <summary>
    /// 尝试开始升温：Idle → Preparing
    /// </summary>
    public bool TryStartHeating()
    {
        if (Status != TestStatus.Idle) return false;
        Status = TestStatus.Preparing;
        StableCount = 0;
        ElapsedSeconds = 0;
        PidQueue.Clear();
        Log.Information("开始升温");
        return true;
    }

    /// <summary>
    /// 尝试停止升温：Preparing/Ready/Complete → Idle
    /// </summary>
    public bool TryStopHeating()
    {
        if (Status != TestStatus.Preparing && Status != TestStatus.Ready && Status != TestStatus.Complete) return false;
        Status = TestStatus.Idle;
        Log.Information("停止升温");
        return true;
    }

    /// <summary>
    /// 尝试开始记录：Ready → Recording
    /// </summary>
    public bool TryStartRecording()
    {
        if (Status != TestStatus.Ready) return false;
        Status = TestStatus.Recording;
        ElapsedSeconds = 0;
        Log.Information("开始记录");
        return true;
    }

    /// <summary>
    /// 尝试停止记录：Recording → Complete
    /// </summary>
    public bool TryStopRecording()
    {
        if (Status != TestStatus.Recording) return false;
        Status = TestStatus.Complete;
        Log.Information("停止记录，试验完成");
        return true;
    }

    /// <summary>
    /// 完成保存后回到 Preparing（炉温保持）
    /// </summary>
    public void AfterSaveReturnToPreparing()
    {
        if (Status == TestStatus.Complete)
        {
            Status = TestStatus.Preparing;
            ElapsedSeconds = 0;
            StableCount = 0;
        }
    }

    /// <summary>
    /// 强制重置到 Idle（新建试验时调用）
    /// </summary>
    public void ResetToIdle()
    {
        _status = TestStatus.Idle;
        ElapsedSeconds = 0;
        StableCount = 0;
        PidQueue.Clear();
    }

    /// <summary>
    /// 每800ms调用一次，检查温度并更新状态
    /// </summary>
    public void Tick(double tf1, double tf2)
    {
        var config = AppRuntime.Instance.Configuration;
        double targetTemp = config.GetValue<double>("Simulation:TargetFurnaceTemp", 750.0);
        double stableThreshold = config.GetValue<double>("Simulation:StableThreshold", 3.0);

        // 维护温度历史队列
        _tf1History.Enqueue(tf1);
        while (_tf1History.Count > 750) _tf1History.Dequeue();

        switch (Status)
        {
            case TestStatus.Preparing:
                // 检查是否达到稳定条件
                bool inRange = tf1 >= targetTemp - 5 && tf1 <= targetTemp + 5
                            && tf2 >= targetTemp - 5 && tf2 <= targetTemp + 5;

                if (inRange)
                    StableCount++;
                else
                    StableCount = 0;

                if (inRange && StableCount > 3) // >3个tick ≈ 3.2秒
                {
                    Status = TestStatus.Ready;
                    StableCount = 0;
                }
                break;

            case TestStatus.Ready:
                // 检查温度是否跌出稳定范围
                bool stillStable = tf1 >= targetTemp - 5 && tf1 <= targetTemp + 5;
                if (!stillStable)
                {
                    Status = TestStatus.Preparing;
                    StableCount = 0;
                }
                break;

            case TestStatus.Recording:
                ElapsedSeconds++;

                // 检查终止条件
                if (DurationMode == TestDurationMode.Custom && TargetDurationSeconds > 0)
                {
                    if (ElapsedSeconds >= TargetDurationSeconds)
                    {
                        Status = TestStatus.Complete;
                        Log.Information("固定时长到达，试验自动结束");
                    }
                }
                else // 标准60分钟
                {
                    if (ElapsedSeconds >= 3600)
                    {
                        Status = TestStatus.Complete;
                        Log.Information("记录时间到达3600秒，试验自动结束");
                    }
                }
                break;
        }
    }

    /// <summary>
    /// 获取最近600个温度数据点（用于温漂计算）
    /// </summary>
    public double[] GetRecentTemperatures()
    {
        lock (_lock)
        {
            return _tf1History.ToArray();
        }
    }

    private void OnStatusChanged(TestStatus oldStatus, TestStatus newStatus)
    {
        StatusChanged?.Invoke(this, newStatus);
    }
}
