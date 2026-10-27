namespace TavernOracle.Decision.Evaluation;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// 单个原子动作的边际收益评估结果快照 (Zero GC 只读值类型)。
/// 严格对齐决策大脑核心边际收益打分公式：
/// ΔV(a) = w_tempo * Δ战力 + w_scale * Δ协同 + 对子加成 (+ 三连质变)
/// 并提供金币消耗效益比 (GoldEfficiency) 与原始未加权战力/协同指标。
/// </summary>
public readonly record struct ActionEvaluationScore
{
    /// <summary>
    /// 静态零分默认值。
    /// </summary>
    public static readonly ActionEvaluationScore Zero = default;

    /// <summary>
    /// 动作总边际收益得分: ΔV(a) = TempoScore + SynergyScore + PairScore + TripleBonus
    /// </summary>
    public double TotalScore { get; init; }

    /// <summary>
    /// 即时战力加权得分: w_tempo * Δ战力 (保命即时身材与机制等效加成)
    /// </summary>
    public double TempoScore { get; init; }

    /// <summary>
    /// 远期成长/协同加权得分: w_scale * Δ协同 (当 P_lethal > 30% 熔断时强制为 0.0)
    /// </summary>
    public double SynergyScore { get; init; }

    /// <summary>
    /// 对子加成得分 (构成 2 张同名卡牌时的额外留对增益)
    /// </summary>
    public double PairScore { get; init; }

    /// <summary>
    /// 三连质变得分 (达成第 3 张同名卡牌金色三连质变及发现高本随从潜在收益)
    /// </summary>
    public double TripleBonus { get; init; }

    /// <summary>
    /// 金币消耗收益比: TotalScore / GoldCost
    /// </summary>
    public double GoldEfficiency { get; init; }

    /// <summary>
    /// 未加权原始即时战力增量 Δ战力
    /// </summary>
    public double RawTempoDelta { get; init; }

    /// <summary>
    /// 未加权原始流派协同增量 Δ协同
    /// </summary>
    public double RawSynergyDelta { get; init; }

    /// <summary>
    /// 战术意图与决策标签 (如 "三连质变", "流派核心", "对子成型", "保命身材")
    /// </summary>
    public string Tag { get; init; }

    /// <summary>
    /// 构造完整的动作边际收益评分快照。
    /// </summary>
    [JsonConstructor]
    public ActionEvaluationScore(
        double totalScore,
        double tempoScore,
        double synergyScore,
        double pairScore,
        double tripleBonus,
        double goldEfficiency,
        double rawTempoDelta = 0.0,
        double rawSynergyDelta = 0.0,
        string? tag = null)
    {
        TotalScore = totalScore;
        TempoScore = tempoScore;
        SynergyScore = synergyScore;
        PairScore = pairScore;
        TripleBonus = tripleBonus;
        GoldEfficiency = goldEfficiency;
        RawTempoDelta = rawTempoDelta;
        RawSynergyDelta = rawSynergyDelta;
        Tag = tag ?? string.Empty;
    }

    public override string ToString() =>
        $"[Score:{TotalScore:F1} | Tempo:{TempoScore:F1} | Syn:{SynergyScore:F1} | Pair:{PairScore:F1} | Triple:{TripleBonus:F1} | Eff:{GoldEfficiency:F2}] {(string.IsNullOrEmpty(Tag) ? string.Empty : $"({Tag})")}";
}
