namespace ISO11820.Models;

/// <summary>
/// 传感器温度数据快照
/// </summary>
public class SensorData
{
    public int SensorId { get; set; }
    public string SensorName { get; set; } = "";
    public string DispName { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "℃";
}
