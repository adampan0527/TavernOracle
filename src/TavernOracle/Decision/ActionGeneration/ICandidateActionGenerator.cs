namespace TavernOracle.Decision.ActionGeneration;

using System;
using TavernOracle.Data.State;

/// <summary>
/// 原子操作候选动作集生成器契约接口。
/// 根据盘面快照生成全部合法的候选原子动作，严格剪枝非法行为，全链路 0 GC。
/// </summary>
public interface ICandidateActionGenerator
{
    /// <summary>
    /// 为指定盘面快照生成全部合法的候选原子操作集合，直接填充写入指定缓冲区 (推荐的高性能零 GC 入口)。
    /// </summary>
    /// <param name="snapshot">当前只读盘面物理快照</param>
    /// <param name="buffer">承载候选动作集的只读值类型缓冲区 (自动 Clear 并写入)</param>
    void Generate(in BoardSnapshot snapshot, ref CandidateActionBuffer buffer);

    /// <summary>
    /// 为指定盘面快照生成全部合法的候选原子操作集合，并返回装载动作的缓冲结构体。
    /// </summary>
    /// <param name="snapshot">当前只读盘面物理快照</param>
    /// <returns>填充了合法动作的 CandidateActionBuffer 结构体</returns>
    CandidateActionBuffer Generate(in BoardSnapshot snapshot);

    /// <summary>
    /// 为指定盘面快照生成全部合法的候选原子操作集合，并写入指定的 Span 内存切片中。
    /// </summary>
    /// <param name="snapshot">当前只读盘面物理快照</param>
    /// <param name="destination">目标内存切片</param>
    /// <returns>实际写入的动作总数</returns>
    int Generate(in BoardSnapshot snapshot, Span<CandidateAction> destination);
}
