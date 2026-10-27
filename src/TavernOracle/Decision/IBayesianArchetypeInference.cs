namespace TavernOracle.Decision;

using TavernOracle.Priors;

/// <summary>
/// 贝叶斯流派后验概率推导器接口。
/// 根据当前英雄特征 H、禁选种族池 B、以及场面/手牌已有组件证据 C，
/// 基于贝叶斯法则实时求解各终局流派的后验概率分布：
/// P(Archetype_k | H, C, B) = [P(C | Archetype_k) * P(Archetype_k | H, B)] / Sum_j [P(C | Archetype_j) * P(Archetype_j | H, B)]
/// </summary>
public interface IBayesianArchetypeInference
{
    /// <summary>
    /// 流派与种族协同先验知识图谱仓储。
    /// </summary>
    IArchetypeRepository Repository { get; }

    /// <summary>
    /// 英雄特征与亲和先验提供器。
    /// </summary>
    IHeroAffinityProvider HeroAffinityProvider { get; }

    /// <summary>
    /// 基于只读列表集合执行贝叶斯后验推导。
    /// </summary>
    /// <param name="heroCardIdOrName">英雄 CardID 或名称 (可选，如 "Chenvaala" 或 "TB_BaconShop_HERO_78")</param>
    /// <param name="bannedRaces">当前对局禁选种族位掩码 (如 MinionRaceMask.Mech)</param>
    /// <param name="boardMinions">场上已有随从 CardID 集合 (最大 7 个)</param>
    /// <param name="handMinions">手牌已有随从 CardID 集合 (最大 10 个)</param>
    /// <returns>栈上只读后验概率分布快照 (零 GC 堆分配)</returns>
    ArchetypePosteriorDistribution Infer(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        IReadOnlyList<string>? boardMinions,
        IReadOnlyList<string>? handMinions = null);

    /// <summary>
    /// 基于 ReadOnlySpan 输入执行极致性能零 GC 贝叶斯后验推导（适用于热点高频推导循环）。
    /// </summary>
    /// <param name="heroCardIdOrName">英雄 CardID 或名称 (可选)</param>
    /// <param name="bannedRaces">当前对局禁选种族位掩码</param>
    /// <param name="boardMinions">场上已有随从 CardID Span (最大 7 个)</param>
    /// <param name="handMinions">手牌已有随从 CardID Span (最大 10 个)</param>
    /// <returns>栈上只读后验概率分布快照 (零 GC 堆分配)</returns>
    ArchetypePosteriorDistribution InferSpan(
        string? heroCardIdOrName,
        MinionRaceMask bannedRaces,
        ReadOnlySpan<string> boardMinions,
        ReadOnlySpan<string> handMinions = default);
}
