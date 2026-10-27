using System.Text.Json.Serialization;

namespace TavernOracle.Data.State;

/// <summary>
/// 手牌实体轻量 DTO (Zero GC 结构体)。
/// 封装玩家手牌中的随从或法术卡牌，支持最大 10 张手牌槽位管理。
/// 具备不可变语义与深度隔离特性。
/// </summary>
public readonly record struct HandEntity
{
    public bool HasLiveStats { get; init; }
    public int Attack { get; init; }
    public int Health { get; init; }
    public TavernOracle.Priors.MinionRaceMask Race { get; init; }
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
    /// 打出消耗铸币 (通常随从为 0 打出，法术为 1~3 铸币)
    /// </summary>
    public int Cost { get; init; }

    /// <summary>
    /// 是否为随从牌
    /// </summary>
    public bool IsMinion { get; init; }

    /// <summary>
    /// 是否为酒馆法术 / 塑造法术
    /// </summary>
    public bool IsSpell { get; init; }

    /// <summary>
    /// 手牌排布槽位 (0 ~ 9，最大 10 张)
    /// </summary>
    public int ZonePosition { get; init; }

    /// <summary>
    /// 构造完整手牌实体
    /// </summary>
    [JsonConstructor]
    public HandEntity(
        int entityId,
        string? cardId,
        int cost,
        bool isMinion,
        bool isSpell,
        int zonePosition)
    {
        EntityId = entityId;
        _cardId = cardId ?? string.Empty;
        Cost = cost;
        IsMinion = isMinion;
        IsSpell = isSpell;
        ZonePosition = zonePosition;
    }

    public override string ToString()
    {
        string kind = IsMinion ? "Minion" : (IsSpell ? "Spell" : "Card");
        return $"[Hand #{EntityId} Pos:{ZonePosition}] {CardId} ({kind}, Cost:{Cost})";
    }
}
