using System;
using System.Collections.Immutable;
using TavernOracle.Data.State;

namespace TavernOracle.Decision.Positioning;

/// <summary>
/// 随从站位排列启发式排序器接口。
/// 根据场上随从的嘲讽、风怒、圣盾、破盾怪、亡语机制及防狂战斧规则，
/// 严格在 &lt;= 0.5ms 内输出最优站位坐标序列及解释。
/// 对标天梯万分级选手站位法则与暴雪“纸笔准则”。
/// </summary>
public interface IMinionPositionOptimizer
{
    /// <summary>
    /// 对战场随从集合进行最优站位重排与槽位推导。
    /// </summary>
    /// <param name="minions">在场随从不可变数组 (最大 7 个)</param>
    /// <returns>站位结果结构体 (包含重排随从、最优槽位映射与决策理由)</returns>
    PositioningResult OptimizePosition(ImmutableArray<MinionEntity> minions);

    /// <summary>
    /// 基于盘面全局快照执行最优站位推导。
    /// </summary>
    /// <param name="snapshot">盘面全局快照</param>
    /// <returns>站位结果结构体</returns>
    PositioningResult OptimizePosition(in BoardSnapshot snapshot);

    /// <summary>
    /// 基于 Span 的零 GC 极速站位重排方法，适用于热点高频推导循环。
    /// </summary>
    /// <param name="inputMinions">输入的随从只读切片 (最大 7 个)</param>
    /// <param name="outputOrderedMinions">输出重排后的随从切片缓冲区</param>
    /// <param name="outputSlotIndices">输出各随从对应最优槽位索引缓冲区 (OptimalSlotIndices[originalIndex] = newSlotIndex)</param>
    /// <returns>重排随从总数</returns>
    int OptimizePositionSpan(
        ReadOnlySpan<MinionEntity> inputMinions,
        Span<MinionEntity> outputOrderedMinions,
        Span<int> outputSlotIndices);
}
