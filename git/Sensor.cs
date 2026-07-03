namespace ISO11820.Models;

/// <summary>
/// 传感器通道配置
/// </summary>
public class Sensor
{
    public int SensorId { get; set; }
    public string SensorName { get; set; } = "";
    public string DispName { get; set; } = "";        // 显示名
    public string SensorGroup { get; set; } = "";
    public string Unit { get; set; } = "℃";
    public string Discription { get; set; } = "";
    public string Flag { get; set; } = "启用";
    public double SignalZero { get; set; }
    public double SignalSpan { get; set; }
    public double OutputZero { get; set; }
    public double OutputSpan { get; set; }
    public double OutputValue { get; set; }            // 当前温度值
    public double InputValue { get; set; }             // 当前输入值
    public int SignalType { get; set; }                // 4=数字量
}
