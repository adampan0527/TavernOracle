using System;

namespace TavernOracle.Data;

/// <summary>
/// 酒馆战棋卡牌核心机制与关键词标签位掩码 (Bitmask)。
/// 采用位运算支持零 GC 分配的高速机制匹配与多标签组合判定。
/// </summary>
[Flags]
public enum CardMechanicsMask : uint
{
    /// <summary>
    /// 无特殊机制
    /// </summary>
    None = 0,

    /// <summary>
    /// 嘲讽 (Taunt): 敌方随从必须优先攻击具有嘲讽的目标
    /// </summary>
    Taunt = 1 << 0,

    /// <summary>
    /// 圣盾 (Divine Shield): 免疫下一次受到的伤害
    /// </summary>
    DivineShield = 1 << 1,

    /// <summary>
    /// 剧毒 (Poisonous): 造成伤害即可消灭敌方随从 (永久效果)
    /// </summary>
    Poisonous = 1 << 2,

    /// <summary>
    /// 烈毒 (Venomous): 造成一次伤害即可消灭敌方随从 (触发一次后失效)
    /// </summary>
    Venomous = 1 << 3,

    /// <summary>
    /// 复生 (Reborn): 随从死亡后以 1 点生命值复活
    /// </summary>
    Reborn = 1 << 4,

    /// <summary>
    /// 磁力 / 模块化 (Magnetic / Modular): 可吸附于机械随从上赋予身材与异能
    /// </summary>
    Magnetic = 1 << 5,

    /// <summary>
    /// 风怒 (Windfury): 每回合可进行两次攻击
    /// </summary>
    Windfury = 1 << 6,

    /// <summary>
    /// 超级风怒 (Mega-Windfury): 每回合可进行四次攻击
    /// </summary>
    MegaWindfury = 1 << 7,

    /// <summary>
    /// 亡语 (Deathrattle): 随从阵亡时触发的效果
    /// </summary>
    Deathrattle = 1 << 8,

    /// <summary>
    /// 潜行 (Stealth): 无法被指定或主动攻击直到发起攻击
    /// </summary>
    Stealth = 1 << 9,

    /// <summary>
    /// 塑造法术 (Spellcraft): 每个招募回合开始时获取一张临时法术
    /// </summary>
    Spellcraft = 1 << 10,

    /// <summary>
    /// 顺劈 / 狂战斧 (Cleave): 同时对攻击目标相邻随从造成伤害
    /// </summary>
    Cleave = 1 << 11
}
