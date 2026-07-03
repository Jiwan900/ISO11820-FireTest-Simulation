namespace ISO11820.Models;

/// <summary>
/// 数据广播事件参数 — 从后台线程传到 UI 线程
/// </summary>
public class DataBroadcastEventArgs : EventArgs
{
    public Dictionary<int, SensorData> SensorValues { get; set; } = new();
    public List<MasterMessage> Messages { get; set; } = new();
    public TestStatus CurrentStatus { get; set; }
    public int ElapsedSeconds { get; set; }
    public double TemperatureDrift { get; set; }       // 温漂 °C/10min
}
