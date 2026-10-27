namespace TavernOracle.Decision.ActionGeneration;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// 原子操作候选动作 (Zero GC 只读记录结构体)。
/// 封装单步酒馆战棋动作的全部决策参数与经济变化：
/// - ActionType: 动作类型 (Buy, Sell, Reroll, Freeze, UpgradeTier, Play)
/// - EntityId: 目标卡牌实体 ID (若无对应实体则为 0)
/// - SlotIndex: 源实体所在的槽位下标 (在售酒馆索引 / 战场席位索引 / 手牌槽位索引，无则 -1)
/// - TargetPosition: 随从入场排布的目标站位槽位 (0 ~ BoardCount，非打出动作则为 -1)
/// - GoldCost: 该动作消耗的正向铸币 (例如升本费用、刷新 1 铸币、随从花费 3 铸币)
/// - GoldDelta: 该动作对可用铸币的净变化 (扣减为负，出售为 +1，冻结为 0)
/// - CardId: 目标卡牌的官方字符串标识 (如 "BG20_001")
/// </summary>
public readonly record struct CandidateAction
{
    public int TargetEntityId { get; init; }
    private readonly string? _cardId;

    /// <summary>
    /// 动作类型 (Buy, Sell, Reroll, Freeze, UpgradeTier, Play)
    /// </summary>
    public ActionType ActionType { get; init; }

    /// <summary>
    /// 便捷访问别名
    /// </summary>
    [JsonIgnore]
    public ActionType Type => ActionType;

    /// <summary>
    /// 目标实体 ID (若为全局操作如 Reroll/Freeze/UpgradeTier，可为 0)
    /// </summary>
    public int EntityId { get; init; }

    /// <summary>
    /// 源实体槽位索引 (酒馆在售索引 / 战场席位索引 / 手牌槽位索引，缺省为 -1)
    /// </summary>
    public int SlotIndex { get; init; }

    /// <summary>
    /// 打出目标排布槽位 (0 ~ BoardCount，非打出动作缺省为 -1)
    /// </summary>
    public int TargetPosition { get; init; }

    /// <summary>
    /// 消耗铸币绝对数值 (&gt;= 0)
    /// </summary>
    public int GoldCost { get; init; }

    /// <summary>
    /// 铸币净增量 (扣减为负数，卖出为 +1，冻结为 0)
    /// </summary>
    public int GoldDelta { get; init; }

    /// <summary>
    /// 目标卡牌 CardId
    /// </summary>
    public string CardId
    {
        get => _cardId ?? string.Empty;
        init => _cardId = value ?? string.Empty;
    }

    /// <summary>
    /// 构造原子操作候选动作
    /// </summary>
    [JsonConstructor]
    public CandidateAction(
        ActionType actionType,
        int entityId,
        int slotIndex,
        int targetPosition,
        int goldCost,
        int goldDelta,
        string? cardId = null)
    {
        ActionType = actionType;
        EntityId = entityId;
        SlotIndex = slotIndex;
        TargetPosition = targetPosition;
        GoldCost = Math.Max(0, goldCost);
        GoldDelta = goldDelta;
        _cardId = cardId ?? string.Empty;
    }

    #region 零 GC 快速静态工厂方法

    /// <summary>
    /// 创建升级酒馆动作 (UpgradeTier)
    /// </summary>
    public static CandidateAction CreateUpgradeTier(int upgradeCost) =>
        new(ActionType.UpgradeTier, entityId: 0, slotIndex: -1, targetPosition: -1, goldCost: upgradeCost, goldDelta: -upgradeCost, cardId: string.Empty);

    /// <summary>
    /// 创建刷新酒馆动作 (Reroll, 消耗 1 铸币)
    /// </summary>
    public static CandidateAction CreateReroll(int cost = 1) =>
        new(ActionType.Reroll, entityId: 0, slotIndex: -1, targetPosition: -1, goldCost: cost, goldDelta: -cost, cardId: string.Empty);

    /// <summary>
    /// 创建冻结酒馆动作 (Freeze, 0 铸币)
    /// </summary>
    public static CandidateAction CreateFreeze() =>
        new(ActionType.Freeze, entityId: 0, slotIndex: -1, targetPosition: -1, goldCost: 0, goldDelta: 0, cardId: string.Empty);

    /// <summary>
    /// 创建购买酒馆随从动作 (Buy)
    /// </summary>
    public static CandidateAction CreateBuy(int entityId, int slotIndex, int cost, string? cardId) =>
        new(ActionType.Buy, entityId, slotIndex, targetPosition: -1, goldCost: cost, goldDelta: -cost, cardId);

    /// <summary>
    /// 创建出售在场随从动作 (Sell, 获得 1 铸币)
    /// </summary>
    public static CandidateAction CreateSell(int entityId, int slotIndex, string? cardId) =>
        new(ActionType.Sell, entityId, slotIndex, targetPosition: -1, goldCost: 0, goldDelta: 1, cardId);

    /// <summary>
    /// 创建从手牌打出随从动作 (Play)
    /// </summary>
    public static CandidateAction CreatePlay(int entityId, int slotIndex, int targetPosition, int cost, string? cardId) =>
        new(ActionType.Play, entityId, slotIndex, targetPosition, goldCost: cost, goldDelta: -cost, cardId);

    #endregion

    public override string ToString() => ActionType switch
    {
        ActionType.UpgradeTier => $"[UpgradeTier Cost:{GoldCost}]",
        ActionType.Reroll => $"[Reroll Cost:{GoldCost}]",
        ActionType.Freeze => "[Freeze]",
        ActionType.Buy => $"[Buy Slot:{SlotIndex} #{EntityId} {CardId} Cost:{GoldCost}]",
        ActionType.Sell => $"[Sell Slot:{SlotIndex} #{EntityId} {CardId} +{GoldDelta}g]",
        ActionType.Play => $"[Play Slot:{SlotIndex} #{EntityId} {CardId} Pos:{TargetPosition} Cost:{GoldCost}]",
        _ => $"[{ActionType} #{EntityId}]"
    };
}
