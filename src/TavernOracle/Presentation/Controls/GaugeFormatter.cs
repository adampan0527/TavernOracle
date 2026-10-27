namespace TavernOracle.Presentation.Controls;

/// <summary>
/// 仪表盘超高性能零 GC 文本格式化工具。
/// 预计算所有可能的百分比（0.0% ~ 100.0%）与高频伤害数值（-50.0 ~ +50.0），
/// 在战力模拟密集刷新链路中完全消除字符串堆内存分配（Zero GC Allocation）。
/// </summary>
public static class GaugeFormatter
{
    private static readonly string[] PercentCache = new string[1001];
    private static readonly string[] DamageCache = new string[1001];

    static GaugeFormatter()
    {
        for (int i = 0; i <= 1000; i++)
        {
            PercentCache[i] = $"{i / 10.0:F1}%";
            double dmg = (i - 500) / 10.0;
            DamageCache[i] = dmg switch
            {
                > 0 => $"+{dmg:F1}",
                < 0 => $"{dmg:F1}",
                _ => "0.0"
            };
        }
    }

    /// <summary>
    /// 将 0.0 ~ 1.0 的浮点概率极速格式化为 "XX.X%" 格式，返回静态复用字符串引用（零 GC）
    /// </summary>
    public static string FormatPercentage(double rate)
    {
        if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0.0)
            return PercentCache[0];
        if (rate >= 1.0)
            return PercentCache[1000];

        int index = (int)Math.Round(rate * 1000.0);
        if ((uint)index > 1000)
            index = MathEx.Clamp(index, 0, 1000);

        return PercentCache[index];
    }

    /// <summary>
    /// 将平均伤害数值格式化为 "+X.X" / "-X.X" / "0.0"，高频区间走缓存（零 GC）
    /// </summary>
    public static string FormatDamage(double damage)
    {
        if (double.IsNaN(damage) || double.IsInfinity(damage))
            return "0.0";

        if (damage >= -50.0 && damage <= 50.0)
        {
            int index = (int)Math.Round((damage + 50.0) * 10.0);
            if ((uint)index <= 1000)
            {
                return DamageCache[index];
            }
        }

        return damage switch
        {
            > 0 => $"+{damage:F1}",
            < 0 => $"{damage:F1}",
            _ => "0.0"
        };
    }
}
