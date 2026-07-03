namespace ISO11820.Models;

/// <summary>
/// 校准记录
/// </summary>
public class CalibrationRecord
{
    public string Id { get; set; } = "";               // GUID
    public string CalibrationDate { get; set; } = "";   // ISO 8601
    public string CalibrationType { get; set; } = "";   // Surface / Center
    public int ApparatusId { get; set; }
    public string Operator { get; set; } = "";
    public string TemperatureData { get; set; } = "";   // JSON
    public double? UniformityResult { get; set; }
    public double? MaxDeviation { get; set; }
    public double? AverageTemperature { get; set; }
    public int PassedCriteria { get; set; }             // 0=未通过, 1=通过
    public string Remarks { get; set; } = "";
    public string CreatedAt { get; set; } = "";

    // 炉壁9测温点
    public double? TempA1 { get; set; }
    public double? TempA2 { get; set; }
    public double? TempA3 { get; set; }
    public double? TempB1 { get; set; }
    public double? TempB2 { get; set; }
    public double? TempB3 { get; set; }
    public double? TempC1 { get; set; }
    public double? TempC2 { get; set; }
    public double? TempC3 { get; set; }

    // 计算结果
    public double? TAvg { get; set; }
    public double? TAvgAxis1 { get; set; }
    public double? TAvgAxis2 { get; set; }
    public double? TAvgAxis3 { get; set; }
    public double? TAvgLevela { get; set; }
    public double? TAvgLevelb { get; set; }
    public double? TAvgLevelc { get; set; }
    public double? TDevAxis1 { get; set; }
    public double? TDevAxis2 { get; set; }
    public double? TDevAxis3 { get; set; }
    public double? TDevLevela { get; set; }
    public double? TDevLevelb { get; set; }
    public double? TDevLevelc { get; set; }
    public double? TAvgDevAxis { get; set; }
    public double? TAvgDevLevel { get; set; }

    public string? CenterTempData { get; set; }
    public string? Memo { get; set; }
}
