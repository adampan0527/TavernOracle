using System;
using System.Collections.Generic;
using TavernOracle.Priors;
using TavernOracle.Simulation;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// HDT 实体逻辑区域枚举。
/// 映射炉石底层 Zone (PLAY, HAND, SETASIDE, GRAVEYARD, DECK, SECRET)。
/// </summary>
public enum HdtZone
{
    Unknown = 0,
    Play = 1,
    Hand = 2,
    SetAside = 3,
    Graveyard = 4,
    Deck = 5,
    Secret = 6
}

/// <summary>
/// HDT 原始实体轻量只读 DTO (Zero GC 结构体)。
/// 用于在事件监听层、状态机与转换器之间以极速值传递方式承载实体属性。
/// </summary>
public readonly record struct HdtEntityDto(
    int EntityId,
    string CardId,
    int Controller, // 1 = 我方玩家, 2 = 鲍勃酒馆/敌方
    HdtZone Zone,
    int ZonePosition,
    int Attack = 0,
    int Health = 0,
    int Cost = 0,
    int Tier = 0,
    MinionRaceMask Race = MinionRaceMask.None,
    CardMechanicsMask Mechanics = CardMechanicsMask.None,
    bool IsGolden = false,
    bool IsFrozen = false,
    bool IsTavernSpell = false,
    string? DisplayName = null,
    bool? IsMinionCard = null
);

/// <summary>
/// 区域流转变更事件数据 DTO (Zero GC 结构体)。
/// </summary>
public readonly record struct HdtZoneChangeDto(
    int EntityId,
    string? CardId,
    HdtZone FromZone,
    HdtZone ToZone,
    int ZonePosition,
    int Controller
);

/// <summary>
/// 游戏标签变更事件数据 DTO (Zero GC 结构体)。
/// 覆盖 RESOURCES, RESOURCES_USED, PLAYER_TECH_LEVEL, ARMOR, HEALTH, DAMAGE, ATK 等。
/// </summary>
public readonly record struct HdtTagChangeDto(
    int EntityId,
    string TagName,
    int Value,
    int PrevValue = 0
);

/// <summary>
/// HDT 盘面原始全量状态容器 (支持批量同步与离线脱机回放构建)。
/// </summary>
public sealed class HdtBoardRawState
{
    public int Turn { get; set; } = 1;
    public string HeroCardId { get; set; } = string.Empty;
    public int Health { get; set; } = 30;
    public int Armor { get; set; } = 0;
    public int TavernTier { get; set; } = 1;
    public int UpgradeCost { get; set; } = 5;
    public bool IsFrozen { get; set; } = false;
    public MinionRaceMask BannedRaces { get; set; } = MinionRaceMask.None;
    public int GoldAvailable { get; set; } = 3;
    public int GoldMax { get; set; } = 3;
    public int GoldLocked { get; set; } = 0;
    public CombatSimulationResult Simulation { get; set; } = CombatSimulationResult.CreateDefaultSafePrior();

    public List<HdtEntityDto> PlayerBoard { get; } = new(7);
    public List<HdtEntityDto> PlayerHand { get; } = new(10);
    public List<HdtEntityDto> TavernMinions { get; } = new(7);
}
