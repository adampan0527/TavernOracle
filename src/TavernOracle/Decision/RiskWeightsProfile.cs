namespace TavernOracle.Decision;

/// <summary>
/// 战力生死线风控权重快照（只读值类型，确保零 GC 堆垃圾分配）。
/// 包含保命即时战力权重 w_tempo 与远期成长贪婪权重 w_scale 及策略突变标志。
/// </summary>
public readonly record struct RiskWeightsProfile
{
    /// <summary>
    /// 规整后的对战暴毙率 P_lethal [0.0, 1.0]
    /// </summary>
    public double PLethal { get; init; }

    /// <summary>
    /// 保命即时战力权重 w_tempo = 1.0 + 3.0 * (P_lethal)^2
    /// </summary>
    public double TempoWeight { get; init; }

    /// <summary>
    /// 远期成长/贪婪权重 w_scale:
    /// 常态 (P_lethal &lt;= 0.30) 为 max(0, 1.0 - 0.9 * P_lethal);
    /// 策略突变 (P_lethal &gt; 0.30) 强制归零 (0.0)。
    /// </summary>
    public double ScaleWeight { get; init; }

    /// <summary>
    /// 是否触发战力生死线危机突变 (P_lethal &gt; 0.30)
    /// </summary>
    public bool IsEmergencyMutated { get; init; }

    /// <summary>
    /// 风险等级分类 (Safe, Low, Warning, Critical)
    /// </summary>
    public RiskTier RiskTier { get; init; }

    /// <summary>
    /// 保命即时战力权重数学别名 w_tempo
    /// </summary>
    public double WTempo => TempoWeight;

    /// <summary>
    /// 远期成长贪婪权重数学别名 w_scale
    /// </summary>
    public double WScale => ScaleWeight;

    /// <summary>
    /// 构造完整的风控权重快照
    /// </summary>
    public RiskWeightsProfile(
        double pLethal,
        double tempoWeight,
        double scaleWeight,
        bool isEmergencyMutated,
        RiskTier riskTier)
    {
        PLethal = pLethal;
        TempoWeight = tempoWeight;
        ScaleWeight = scaleWeight;
        IsEmergencyMutated = isEmergencyMutated;
        RiskTier = riskTier;
    }

    public override string ToString()
    {
        return $"[RiskWeightsProfile: P_lethal={PLethal:P1}, w_tempo={TempoWeight:F3}, w_scale={ScaleWeight:F3}, Mutated={IsEmergencyMutated}, Tier={RiskTier}]";
    }
}
