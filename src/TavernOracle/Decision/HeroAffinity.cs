namespace TavernOracle.Decision;

using TavernOracle.Priors;

/// <summary>
/// 英雄流派/种族亲和先验配置。
/// 定义特定英雄在对局初始阶段对特定种族或流派的先验偏置权重。
/// </summary>
public readonly record struct HeroAffinity
{
    /// <summary>
    /// 英雄唯一标识符（CardID、名称或标准别名，例如 "TB_BaconShop_HERO_78" 或 "Chenvaala"）。
    /// </summary>
    public string HeroIdentifier { get; init; }

    /// <summary>
    /// 亲和种族掩码（例如 MinionRaceMask.Elemental）。
    /// </summary>
    public MinionRaceMask RaceAffinity { get; init; }

    /// <summary>
    /// 亲和流派唯一标识符（可选，精确绑定指定流派，如 "Elementals_Nomi"）。
    /// </summary>
    public string? ArchetypeIdAffinity { get; init; }

    /// <summary>
    /// 先验权重增益乘数（基准平权为 1.0，亲和英雄通常为 3.0 ~ 4.0）。
    /// </summary>
    public double WeightMultiplier { get; init; }

    public HeroAffinity(
        string heroIdentifier,
        MinionRaceMask raceAffinity,
        string? archetypeIdAffinity = null,
        double weightMultiplier = 3.0)
    {
        HeroIdentifier = heroIdentifier;
        RaceAffinity = raceAffinity;
        ArchetypeIdAffinity = archetypeIdAffinity;
        WeightMultiplier = weightMultiplier > 0.0 ? weightMultiplier : 1.0;
    }
}
