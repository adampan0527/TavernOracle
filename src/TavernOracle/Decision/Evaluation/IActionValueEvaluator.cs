namespace TavernOracle.Decision.Evaluation;

using System;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Decision.ActionGeneration;

/// <summary>
/// 动作边际收益价值评估器契约接口。
/// 依据战力生死线风控权重 (w_tempo, w_scale)、贝叶斯流派后验概率分布、手牌与战场已有随从，
/// 对招募阶段单步原子候选动作 (Buy, Sell, Play, UpgradeTier, Reroll, Freeze) 计算边际收益打分：
/// ΔV(a) = w_tempo * Δ战力 + w_scale * Δ协同 + 对子加成
/// 严格遵守低时延 (&lt;= 1ms，目标 &lt; 0.02ms) 与零堆内存分配 (Zero GC) 规范。
/// </summary>
public interface IActionValueEvaluator
{
    /// <summary>
    /// 对原子候选动作执行高精度边际收益打分。
    /// </summary>
    /// <param name="snapshot">当前盘面不可变快照</param>
    /// <param name="action">待评估的原子候选动作</param>
    /// <param name="posteriors">贝叶斯流派后验概率分布快照</param>
    /// <param name="riskProfile">战力生死线风控权重快照</param>
    /// <returns>动作边际收益评分明细 (只读结构体，零堆分配)</returns>
    ActionEvaluationScore Evaluate(
        in BoardSnapshot snapshot,
        in CandidateAction action,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile);

    /// <summary>
    /// 便捷重载：根据盘面对战模拟数据与已有卡牌就地推导风控与流派分布，完成动作打分。
    /// </summary>
    /// <param name="snapshot">当前盘面不可变快照</param>
    /// <param name="action">待评估的原子候选动作</param>
    /// <returns>动作边际收益评分明细</returns>
    ActionEvaluationScore Evaluate(
        in BoardSnapshot snapshot,
        in CandidateAction action);

    /// <summary>
    /// 批量评估候选动作缓冲区中的所有动作，将评分就地写入目标切片中。
    /// 纯栈操作，零 GC 堆垃圾分配。
    /// </summary>
    /// <param name="snapshot">当前盘面不可变快照</param>
    /// <param name="actions">候选动作缓冲区</param>
    /// <param name="posteriors">贝叶斯流派后验概率分布快照</param>
    /// <param name="riskProfile">战力生死线风控权重快照</param>
    /// <param name="destination">接收评分的目标 Span 切片</param>
    void EvaluateBatch(
        in BoardSnapshot snapshot,
        in CandidateActionBuffer actions,
        in ArchetypePosteriorDistribution posteriors,
        in RiskWeightsProfile riskProfile,
        Span<ActionEvaluationScore> destination);
}
