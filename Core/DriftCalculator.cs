using MathNet.Numerics;

namespace ISO11820.Core;

/// <summary>
/// 温漂计算器 — 使用线性回归计算温度变化趋势
/// </summary>
public class DriftCalculator
{
    /// <summary>
    /// 计算温漂（°C/10min）
    /// </summary>
    /// <param name="temperatures">温度序列（按时间顺序）</param>
    /// <param name="intervalSeconds">采样间隔（秒）</param>
    /// <returns>温漂值（°C/10min）</returns>
    public static double CalculateDrift(double[] temperatures, double intervalSeconds = 0.8)
    {
        if (temperatures.Length < 10) return 0;

        // 构建 (时间秒, 温度) 数据对
        var x = new double[temperatures.Length];
        var y = temperatures;

        for (int i = 0; i < temperatures.Length; i++)
            x[i] = i * intervalSeconds;

        // 使用最小二乘法线性回归
        var (intercept, slope) = Fit.Line(x, y);

        // 转换为 °C/10min
        return slope * 600;
    }

    /// <summary>
    /// 判断温度是否稳定
    /// </summary>
    /// <param name="temperatures">最近600个温度点</param>
    /// <param name="maxDriftPer10Min">最大允许温漂（°C/10min），默认2.0</param>
    /// <returns>是否稳定</returns>
    public static bool IsStable(double[] temperatures, double maxDriftPer10Min = 2.0)
    {
        if (temperatures.Length < 30) return false;
        var drift = CalculateDrift(temperatures);
        return Math.Abs(drift) <= maxDriftPer10Min;
    }
}
