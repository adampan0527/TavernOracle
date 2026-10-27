using System;
using System.Text.Json.Serialization;
using TavernOracle.Priors;

namespace TavernOracle.Data.State;

/// <summary>
/// 战场随从实体轻量 DTO (Zero GC 结构体)。
/// 封装战场随从的核心属性、星级、身材、种族掩码、关键词机制与站位槽位。
/// 具备不可变语义与深度隔离特性。
/// </summary>
public readonly record struct MinionEntity
{
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
    /// 当前实时攻击力
    /// </summary>
    public int Attack { get; init; }

    /// <summary>
    /// 当前实时生命值
    /// </summary>
    public int Health { get; init; }

    /// <summary>
    /// 酒馆星级 (1 ~ 7)
    /// </summary>
    public int Tier { get; init; }

    /// <summary>
    /// 随从种族位掩码 (支持双种族与全部族融合怪)
    /// </summary>
    public MinionRaceMask Race { get; init; }

    /// <summary>
    /// 关键词机制位掩码 (圣盾、剧毒、复生、嘲讽等)
    /// </summary>
    public CardMechanicsMask Mechanics { get; init; }

    /// <summary>
    /// 是否为金色 (三连升级) 随从
    /// </summary>
    public bool IsGolden { get; init; }

    /// <summary>
    /// 随从在战场上的排布槽位 (0 ~ 6，最大 7 席位)
    /// </summary>
    public int ZonePosition { get; init; }

    #region 机制便捷布尔属性 (O(1) 位运算解算，忽略 JSON 序列化)

    [JsonIgnore]
    public bool Taunt => (Mechanics & CardMechanicsMask.Taunt) != 0;

    [JsonIgnore]
    public bool DivineShield => (Mechanics & CardMechanicsMask.DivineShield) != 0;

    [JsonIgnore]
    public bool Poisonous => (Mechanics & CardMechanicsMask.Poisonous) != 0;

    [JsonIgnore]
    public bool Venomous => (Mechanics & CardMechanicsMask.Venomous) != 0;

    [JsonIgnore]
    public bool HasPoisonOrVenom => (Mechanics & (CardMechanicsMask.Poisonous | CardMechanicsMask.Venomous)) != 0;

    [JsonIgnore]
    public bool Reborn => (Mechanics & CardMechanicsMask.Reborn) != 0;

    [JsonIgnore]
    public bool Magnetic => (Mechanics & CardMechanicsMask.Magnetic) != 0;

    [JsonIgnore]
    public bool Windfury => (Mechanics & (CardMechanicsMask.Windfury | CardMechanicsMask.MegaWindfury)) != 0;

    [JsonIgnore]
    public bool MegaWindfury => (Mechanics & CardMechanicsMask.MegaWindfury) != 0;

    [JsonIgnore]
    public bool Deathrattle => (Mechanics & CardMechanicsMask.Deathrattle) != 0;

    [JsonIgnore]
    public bool Stealth => (Mechanics & CardMechanicsMask.Stealth) != 0;

    [JsonIgnore]
    public bool Spellcraft => (Mechanics & CardMechanicsMask.Spellcraft) != 0;

    [JsonIgnore]
    public bool Cleave => (Mechanics & CardMechanicsMask.Cleave) != 0;

    #endregion

    /// <summary>
    /// 构造完整的战场随从实体
    /// </summary>
    [JsonConstructor]
    public MinionEntity(
        int entityId,
        string? cardId,
        int attack,
        int health,
        int tier,
        MinionRaceMask race,
        CardMechanicsMask mechanics,
        bool isGolden,
        int zonePosition)
    {
        EntityId = entityId;
        _cardId = cardId ?? string.Empty;
        Attack = attack;
        Health = health;
        Tier = tier;
        Race = race;
        Mechanics = mechanics;
        IsGolden = isGolden;
        ZonePosition = zonePosition;
    }

    public override string ToString()
    {
        string golden = IsGolden ? " (Golden)" : string.Empty;
        return $"[Minion #{EntityId} Pos:{ZonePosition}] {CardId}{golden} ({Attack}/{Health}, Tier {Tier}, {Race})";
    }
}
