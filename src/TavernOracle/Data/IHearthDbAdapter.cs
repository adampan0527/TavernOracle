using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TavernOracle.Data;

/// <summary>
/// HearthDb 元数据适配服务接口。
/// 提供针对酒馆战棋卡牌的高性能全内存 O(1) 检索、三连映射解析与按星级过滤能力。
/// 严格遵守时延预算 (<= 0.1ms) 与零 GC 分配规范。
/// </summary>
public interface IHearthDbAdapter
{
    /// <summary>
    /// 尝试通过官方 CardId (如 "BG21_000") 快速检索战棋卡牌元数据。
    /// 全内存 O(1) 检索，零堆内存分配。
    /// </summary>
    /// <param name="cardId">卡牌 ID</param>
    /// <param name="metadata">若检索命中则输出卡牌元数据；否则为 null</param>
    /// <returns>是否成功命中</returns>
    bool TryGetCard(string cardId, [NotNullWhen(true)] out BattlegroundsCardMetadata? metadata);

    /// <summary>
    /// 尝试通过数据库 DbfId 快速检索战棋卡牌元数据。
    /// </summary>
    /// <param name="dbfId">卡牌 DbfId</param>
    /// <param name="metadata">若检索命中则输出卡牌元数据；否则为 null</param>
    /// <returns>是否成功命中</returns>
    bool TryGetCardByDbfId(int dbfId, [NotNullWhen(true)] out BattlegroundsCardMetadata? metadata);

    /// <summary>
    /// 查询卡牌对应的三连 CardId：
    /// 输入普通卡 CardId 返回对应的金色卡 CardId；
    /// 输入金色卡 CardId 返回对应的普通卡 CardId；
    /// 若无对应三连关系或卡牌不存在则返回 null。
    /// </summary>
    /// <param name="cardId">目标卡牌 CardId</param>
    /// <returns>三连对应 CardId 或 null</returns>
    string? GetTripleCardId(string cardId);

    /// <summary>
    /// 获取战棋随从列表。可按酒馆星级 (1~7) 过滤；tier 为 null 时返回全量随从。
    /// </summary>
    /// <param name="tier">酒馆星级 (1~7)，传 null 时返回全量战棋随从</param>
    /// <param name="includeGolden">是否包含金色三连随从 (默认仅返回基础随从，便于酒馆随从池评估)</param>
    /// <returns>满足条件的不可变只读随从列表</returns>
    IReadOnlyList<BattlegroundsCardMetadata> GetAllBattlegroundsMinions(int? tier = null, bool includeGolden = false);

    /// <summary>
    /// 当前已缓存索引的战棋卡牌总数。
    /// </summary>
    int CachedCardCount { get; }

    /// <summary>
    /// 适配器是否已成功完成内存预热并就绪。
    /// </summary>
    bool IsInitialized { get; }
}
