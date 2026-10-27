using System.Text.Json.Serialization;
using TavernOracle.Priors;

namespace TavernOracle.Data.State;

/// <summary>
/// 酒馆在售随从轻量 DTO (Zero GC 结构体)。
/// 封装当前酒馆商店正在售卖的随从，包含购买费用、星级、种族与冻结状态（最大 7 席位）。
/// 具备不可变语义与深度隔离特性。
/// </summary>
public readonly record struct TavernMinion
{
    public bool HasLiveStats { get; init; }
    public int Attack { get; init; }
    public int Health { get; init; }
    public TavernOracle.Data.CardMechanicsMask Mechanics { get; init; }
    public string? DisplayName { get; init; }
    private readonly string? _cardId;

    /// <summary>
    /// 游戏内唯一实体 ID (EntityId)
    /// </summary>
    public int EntityId { get; init; }

    /// <summary>
    /// 卡牌官方文本字符串 ID (如 "BG21_000")
    /// </summary>
    public string CardId
    {
        get => _cardId ?? string.Empty;
        init => _cardId = value ?? string.Empty;
    }

    /// <summary>
    /// 购买该随从所需的铸币花费 (通常为 3 铸币)
    /// </summary>
    public int Cost { get; init; }

    /// <summary>
    /// 酒馆星级 (1 ~ 7)
    /// </summary>
    public int Tier { get; init; }

    /// <summary>
    /// 随从种族位掩码
    /// </summary>
    public MinionRaceMask Race { get; init; }

    /// <summary>
    /// 该随从是否处于冻结状态
    /// </summary>
    public bool IsFrozen { get; init; }

    /// <summary>Preserves spell slots for HUD alignment; spell evaluation is not yet supported.</summary>
    public bool IsTavernSpell { get; init; }

    /// <summary>
    /// 构造完整酒馆在售随从实体
    /// </summary>
    [JsonConstructor]
    public TavernMinion(
        int entityId,
        string? cardId,
        int cost,
        int tier,
        MinionRaceMask race,
        bool isFrozen = false)
    {
        EntityId = entityId;
        _cardId = cardId ?? string.Empty;
        Cost = cost;
        Tier = tier;
        Race = race;
        IsFrozen = isFrozen;
    }

    public override string ToString()
    {
        string frozen = IsFrozen ? " [Frozen]" : string.Empty;
        return $"[Shop #{EntityId}] {CardId} (Tier {Tier}, Cost {Cost}, {Race}){frozen}";
    }
}
