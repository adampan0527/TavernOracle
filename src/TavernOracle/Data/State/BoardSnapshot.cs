using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using TavernOracle.Priors;
using TavernOracle.Simulation;

namespace TavernOracle.Data.State;

/// <summary>
/// 盘面全局不可变快照 (Board Snapshot)。
/// 封装单回合酒馆战棋招募阶段的完整物理世界状态：
/// - 基础局势：回合数、英雄 CardId、血量、护甲、酒馆星级、升本费用、冻结状态、禁选种族位掩码；
/// - 经济与战力：铸币预算 (GoldBudget)、对战仿真暴毙率结果 (CombatSimulationResult)；
/// - 实体槽位：战场随从 (最大 7 席位)、手牌 (最大 10 张)、酒馆在售 (最大 7 席位)。
/// 全链路采用 readonly record struct 与 ImmutableArray，实现 0 GC 堆内存分配与纯值语义深度隔离。
/// </summary>
public readonly record struct BoardSnapshot
{
    public const int MaxBoardMinions = 7;
    public const int MaxHandCards = 10;
    public const int MaxTavernMinions = 7;

    private readonly string? _heroCardId;
    private readonly ImmutableArray<MinionEntity> _boardMinions;
    private readonly ImmutableArray<HandEntity> _handCards;
    private readonly ImmutableArray<TavernMinion> _tavernMinions;

    #region 基础属性

    /// <summary>
    /// 当前游戏回合数 (从第 1 回合起算)
    /// </summary>
    public int Turn { get; init; }

    /// <summary>
    /// 当前玩家英雄 CardId (例如 "BG20_HERO_101")
    /// </summary>
    public string HeroCardId
    {
        get => _heroCardId ?? string.Empty;
        init => _heroCardId = value ?? string.Empty;
    }

    /// <summary>
    /// 英雄当前实时生命值
    /// </summary>
    public int Health { get; init; }

    /// <summary>
    /// 英雄当前护甲值
    /// </summary>
    public int Armor { get; init; }

    /// <summary>
    /// 英雄当前综合生存血线 (Health + Armor)
    /// </summary>
    [JsonIgnore]
    public int TotalHealth => Health + Armor;

    /// <summary>
    /// 当前酒馆星级等级 (1 ~ 7 星)
    /// </summary>
    public int TavernTier { get; init; }

    /// <summary>
    /// 升级至下一星级所需的铸币花费
    /// </summary>
    public int UpgradeCost { get; init; }

    /// <summary>
    /// 当前酒馆在售商品是否处于冻结状态
    /// </summary>
    public bool IsFrozen { get; init; }

    /// <summary>
    /// 本局对战禁选的种族位掩码 (Banned Races Mask)
    /// </summary>
    public MinionRaceMask BannedRaces { get; init; }

    #endregion

    #region 经济与战力仿真

    /// <summary>
    /// 当前回合铸币预算状况 (可用、上限、锁定)
    /// </summary>
    public GoldBudget Gold { get; init; }

    /// <summary>
    /// 当前盘面的战力模拟预测结果 (预测胜/平/负率及暴毙率 P_lethal)
    /// </summary>
    public CombatSimulationResult Simulation { get; init; }

    #endregion

    #region 实体卡槽集合 (只读不可变切片，深度隔离)

    /// <summary>
    /// 战场在场随从集合 (最大 7 席位)
    /// </summary>
    public ImmutableArray<MinionEntity> BoardMinions
    {
        get => _boardMinions.IsDefault ? ImmutableArray<MinionEntity>.Empty : _boardMinions;
        init => _boardMinions = value.IsDefault ? ImmutableArray<MinionEntity>.Empty : value;
    }

    /// <summary>
    /// 玩家手牌实体集合 (最大 10 张)
    /// </summary>
    public ImmutableArray<HandEntity> HandCards
    {
        get => _handCards.IsDefault ? ImmutableArray<HandEntity>.Empty : _handCards;
        init => _handCards = value.IsDefault ? ImmutableArray<HandEntity>.Empty : value;
    }

    /// <summary>
    /// 酒馆当前在售随从集合 (最大 7 席位)
    /// </summary>
    public ImmutableArray<TavernMinion> TavernMinions
    {
        get => _tavernMinions.IsDefault ? ImmutableArray<TavernMinion>.Empty : _tavernMinions;
        init => _tavernMinions = value.IsDefault ? ImmutableArray<TavernMinion>.Empty : value;
    }

    #endregion

    #region 便捷访问别名 (零 GC 别名映射，JsonIgnore 避免冗余序列化)

    /// <summary>
    /// 战场随从别名映射
    /// </summary>
    [JsonIgnore]
    public ImmutableArray<MinionEntity> Board => BoardMinions;

    /// <summary>
    /// 手牌实体别名映射
    /// </summary>
    [JsonIgnore]
    public ImmutableArray<HandEntity> Hand => HandCards;

    /// <summary>
    /// 酒馆在售别名映射
    /// </summary>
    [JsonIgnore]
    public ImmutableArray<TavernMinion> Tavern => TavernMinions;

    /// <summary>
    /// 战场是否已满员 (7 席位)
    /// </summary>
    [JsonIgnore]
    public bool IsBoardFull => BoardMinions.Length >= MaxBoardMinions;

    /// <summary>
    /// 手牌是否已满 (10 张)
    /// </summary>
    [JsonIgnore]
    public bool IsHandFull => HandCards.Length >= MaxHandCards;

    #endregion

    /// <summary>
    /// 默认安全初始快照构造函数 (零空引用保证)
    /// </summary>
    public BoardSnapshot()
    {
        Turn = 1;
        _heroCardId = string.Empty;
        Health = 30;
        Armor = 0;
        TavernTier = 1;
        UpgradeCost = 5;
        IsFrozen = false;
        BannedRaces = MinionRaceMask.None;
        Gold = new GoldBudget(3, 3, 0);
        Simulation = CombatSimulationResult.CreateDefaultSafePrior();
        _boardMinions = ImmutableArray<MinionEntity>.Empty;
        _handCards = ImmutableArray<HandEntity>.Empty;
        _tavernMinions = ImmutableArray<TavernMinion>.Empty;
    }

    /// <summary>
    /// 完整全量构造函数 (JSON 序列化主入口与显式构造)
    /// </summary>
    [JsonConstructor]
    public BoardSnapshot(
        int turn,
        string? heroCardId,
        int health,
        int armor,
        int tavernTier,
        int upgradeCost,
        bool isFrozen,
        MinionRaceMask bannedRaces,
        GoldBudget gold,
        CombatSimulationResult simulation,
        ImmutableArray<MinionEntity> boardMinions = default,
        ImmutableArray<HandEntity> handCards = default,
        ImmutableArray<TavernMinion> tavernMinions = default)
    {
        Turn = turn;
        _heroCardId = heroCardId ?? string.Empty;
        Health = health;
        Armor = armor;
        TavernTier = tavernTier;
        UpgradeCost = upgradeCost;
        IsFrozen = isFrozen;
        BannedRaces = bannedRaces;
        Gold = gold;
        Simulation = simulation;
        _boardMinions = boardMinions.IsDefault ? ImmutableArray<MinionEntity>.Empty : boardMinions;
        _handCards = handCards.IsDefault ? ImmutableArray<HandEntity>.Empty : handCards;
        _tavernMinions = tavernMinions.IsDefault ? ImmutableArray<TavernMinion>.Empty : tavernMinions;
    }

    #region 深拷贝与快速衍生 (Zero GC Derivation)

    /// <summary>
    /// 深度拷贝隔离 (值类型语义保证衍生快照与原始快照物理隔离)
    /// </summary>
    public BoardSnapshot DeepCopy() => this;

    public BoardSnapshot WithTurn(int turn) => this with { Turn = turn };
    public BoardSnapshot WithHero(string heroCardId, int health, int armor = 0) => this with { HeroCardId = heroCardId, Health = health, Armor = armor };
    public BoardSnapshot WithHealth(int health, int armor = 0) => this with { Health = health, Armor = armor };
    public BoardSnapshot WithGold(GoldBudget gold) => this with { Gold = gold };
    public BoardSnapshot WithGold(int available, int max = 10, int lockedInRoll = 0) => this with { Gold = new GoldBudget(available, max, lockedInRoll) };
    public BoardSnapshot WithSimulation(CombatSimulationResult simulation) => this with { Simulation = simulation };
    public BoardSnapshot WithBoardMinions(ImmutableArray<MinionEntity> minions) => this with { BoardMinions = minions };
    public BoardSnapshot WithHandCards(ImmutableArray<HandEntity> hand) => this with { HandCards = hand };
    public BoardSnapshot WithTavernMinions(ImmutableArray<TavernMinion> tavern) => this with { TavernMinions = tavern };
    public BoardSnapshot WithBoard(ImmutableArray<MinionEntity> minions) => this with { BoardMinions = minions };
    public BoardSnapshot WithHand(ImmutableArray<HandEntity> hand) => this with { HandCards = hand };
    public BoardSnapshot WithTavern(ImmutableArray<TavernMinion> tavern) => this with { TavernMinions = tavern };
    public BoardSnapshot WithTavernTier(int tier, int upgradeCost = 0) => this with { TavernTier = tier, UpgradeCost = upgradeCost };
    public BoardSnapshot WithFrozen(bool isFrozen) => this with { IsFrozen = isFrozen };
    public BoardSnapshot WithBannedRaces(MinionRaceMask bannedRaces) => this with { BannedRaces = bannedRaces };

    #endregion

    #region JSON 往返序列化支持 (T06 离线测试集与离线回放桥梁)

    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 将当前盘面快照序列化为格式化 JSON 文本
    /// </summary>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(this, options ?? DefaultJsonOptions);
    }

    /// <summary>
    /// 从 JSON 文本反序列化构建盘面快照
    /// </summary>
    public static BoardSnapshot FromJson(string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<BoardSnapshot>(json, options ?? DefaultJsonOptions);
    }

    #endregion

    public override string ToString()
    {
        return $"[Turn {Turn}] Hero:{HeroCardId} HP:{TotalHealth}({Health}+{Armor}) Tier:{TavernTier} " +
               $"Gold:{Gold.Available}/{Gold.Max} Board:{BoardMinions.Length}/{MaxBoardMinions} " +
               $"Hand:{HandCards.Length}/{MaxHandCards} Tavern:{TavernMinions.Length}/{MaxTavernMinions} " +
               $"P_lethal:{Simulation.PLethal:P1}";
    }
}
