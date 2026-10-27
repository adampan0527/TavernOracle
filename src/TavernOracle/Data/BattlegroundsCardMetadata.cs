using System;
using TavernOracle.Priors;

namespace TavernOracle.Data;

/// <summary>
/// 酒馆战棋卡牌核心元数据模型 (Battlegrounds Card Metadata)。
/// 封装 CardID、DbfID、酒馆星级、种族位掩码、身材数值、三连对应关系及核心机制关键词。
/// 具备不可变语义与零 GC 分配引用传递特性。
/// </summary>
public sealed record BattlegroundsCardMetadata
{
    /// <summary>
    /// 卡牌官方文本字符串 ID (例如 "BG21_000"、"BGS_071"、"BG21_000_G")
    /// </summary>
    public string CardId { get; init; } = string.Empty;

    /// <summary>
    /// 数据库数字唯一标识符 (DbfId)
    /// </summary>
    public int DbfId { get; init; }

    /// <summary>
    /// 卡牌官方名称 (英文或本地化名称，如 "Leapfrogger")
    /// </summary>
    public string Name { get; init; } = string.Empty;
    public string? LocalizedName { get; init; }

    /// <summary>
    /// 酒馆战棋星级等级 (1 ~ 7 星；法术或特殊卡牌可能为 0)
    /// </summary>
    public int Tier { get; init; }

    /// <summary>
    /// 随从种族位掩码 (支持双种族组合与全部族融合怪)
    /// </summary>
    public MinionRaceMask Race { get; init; } = MinionRaceMask.None;

    /// <summary>
    /// 基础初始攻击力
    /// </summary>
    public int BaseAttack { get; init; }

    /// <summary>
    /// 基础初始生命值
    /// </summary>
    public int BaseHealth { get; init; }

    /// <summary>
    /// 是否为金色 (三连升级) 卡牌
    /// </summary>
    public bool IsGolden { get; init; }

    /// <summary>
    /// 三连卡牌映射 CardId：
    /// 普通卡指向其对应的金色卡 ID；金色卡反向指向其原始普通卡 ID；若无对应三连则为 null。
    /// </summary>
    public string? TripleCardId { get; init; }

    /// <summary>
    /// 机制标签位掩码组合
    /// </summary>
    public CardMechanicsMask Mechanics { get; init; } = CardMechanicsMask.None;

    #region 机制便捷布尔属性 (O(1) 位运算解算)

    /// <summary>
    /// 是否具备嘲讽机制
    /// </summary>
    public bool Taunt => (Mechanics & CardMechanicsMask.Taunt) != 0;

    /// <summary>
    /// 是否具备圣盾机制
    /// </summary>
    public bool DivineShield => (Mechanics & CardMechanicsMask.DivineShield) != 0;

    /// <summary>
    /// 是否具备剧毒机制 (永久秒杀)
    /// </summary>
    public bool Poisonous => (Mechanics & CardMechanicsMask.Poisonous) != 0;

    /// <summary>
    /// 是否具备烈毒机制 (单次秒杀)
    /// </summary>
    public bool Venomous => (Mechanics & CardMechanicsMask.Venomous) != 0;

    /// <summary>
    /// 是否具备即死杀伤能力 (剧毒或烈毒)
    /// </summary>
    public bool HasPoisonOrVenom => (Mechanics & (CardMechanicsMask.Poisonous | CardMechanicsMask.Venomous)) != 0;

    /// <summary>
    /// 是否具备复生机制
    /// </summary>
    public bool Reborn => (Mechanics & CardMechanicsMask.Reborn) != 0;

    /// <summary>
    /// 是否具备磁力 / 模块化机制
    /// </summary>
    public bool Magnetic => (Mechanics & CardMechanicsMask.Magnetic) != 0;

    /// <summary>
    /// 是否具备风怒机制 (含普通风怒与超级风怒)
    /// </summary>
    public bool Windfury => (Mechanics & (CardMechanicsMask.Windfury | CardMechanicsMask.MegaWindfury)) != 0;

    /// <summary>
    /// 是否具备超级风怒机制
    /// </summary>
    public bool MegaWindfury => (Mechanics & CardMechanicsMask.MegaWindfury) != 0;

    /// <summary>
    /// 是否具备亡语机制
    /// </summary>
    public bool Deathrattle => (Mechanics & CardMechanicsMask.Deathrattle) != 0;

    /// <summary>
    /// 是否具备潜行机制
    /// </summary>
    public bool Stealth => (Mechanics & CardMechanicsMask.Stealth) != 0;

    /// <summary>
    /// 是否具备塑造法术机制
    /// </summary>
    public bool Spellcraft => (Mechanics & CardMechanicsMask.Spellcraft) != 0;

    /// <summary>
    /// 是否具备顺劈 / 狂战斧机制
    /// </summary>
    public bool Cleave => (Mechanics & CardMechanicsMask.Cleave) != 0;

    #endregion

    /// <summary>
    /// 诊断与日志可读性表达
    /// </summary>
    public override string ToString()
    {
        string goldenSuffix = IsGolden ? " (Golden)" : string.Empty;
        string tripleInfo = !string.IsNullOrEmpty(TripleCardId) ? $" [Triple:{TripleCardId}]" : string.Empty;
        return $"[{CardId}] {Name}{goldenSuffix} (Tier {Tier}, {BaseAttack}/{BaseHealth}, Race:{Race}){tripleInfo}";
    }
}
