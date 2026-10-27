using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TavernOracle.Data.State;
using TavernOracle.Priors;
using TavernOracle.Simulation;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// HDT 游戏事件转换与状态机实现类 (Game State Watcher)。
/// 实时汇聚 HDT 盘面状态（我方战场随从、手牌、酒馆在售随从、英雄血量护甲、当前金币、升级费用、酒馆等级、禁选种族）。
/// 当局势发生变化（买怪、卖怪、打出、刷新、升本）时，在内存中极速（&lt;= 2ms）组装并派发只读不可变 <see cref="BoardSnapshot"/>。
/// 严格遵循 docs/dev_specifications.md 第 2.2 节规范：异常与断流时保持上一帧稳定快照，绝不向外抛出未捕获异常。
/// </summary>
public sealed class GameStateWatcher : IGameStateWatcher
{
    private readonly object _syncLock = new();

    private readonly IHearthDbAdapter _dbAdapter;
    private IHdtGameEventSource? _eventSource;
    private IBobsBuddyBridge? _bobsBuddyBridge;

    private bool _isLoaded;
    private bool _isRunning;
    private bool _isInGame;
    private bool _isDegraded;
    private string? _lastDegradationMessage;

    /// <summary>
    /// 状态机是否已完成初始化加载
    /// </summary>
    public bool IsLoaded
    {
        get
        {
            lock (_syncLock)
            {
                return _isLoaded;
            }
        }
    }

    // 盘面物理状态字段
    private int _turn = 1;
    private string _heroCardId = string.Empty;
    private int _health = 30;
    private int _armor = 0;
    private int _tavernTier = 1;
    private int _upgradeCost = 5;
    private bool _isFrozen = false;
    private MinionRaceMask _bannedRaces = MinionRaceMask.None;

    // 经济与战力仿真
    private int _goldAvailable = 3;
    private int _goldMax = 3;
    private int _goldLocked = 0;
    private CombatSimulationResult _simulation = CombatSimulationResult.CreateDefaultSafePrior();

    // 实体容器集合 (受 _syncLock 保护)
    private readonly List<HdtEntityDto> _boardMinions = new(BoardSnapshot.MaxBoardMinions);
    private readonly List<HdtEntityDto> _handCards = new(BoardSnapshot.MaxHandCards);
    private readonly List<HdtEntityDto> _tavernMinions = new(BoardSnapshot.MaxTavernMinions);
    private readonly Dictionary<int, HdtEntityDto> _allEntities = new(64);

    // 最新稳定快照缓存 (零空引用保证)
    private BoardSnapshot _currentSnapshot = new();

    #region 公开属性

    /// <inheritdoc />
    public BoardSnapshot CurrentSnapshot
    {
        get
        {
            lock (_syncLock)
            {
                return _currentSnapshot;
            }
        }
    }

    /// <inheritdoc />
    public bool IsInGame
    {
        get
        {
            lock (_syncLock)
            {
                return _isInGame;
            }
        }
    }

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_syncLock)
            {
                return _isRunning;
            }
        }
    }

    /// <inheritdoc />
    public int Turn
    {
        get
        {
            lock (_syncLock)
            {
                return _turn;
            }
        }
    }

    /// <inheritdoc />
    public bool IsDegraded
    {
        get
        {
            lock (_syncLock)
            {
                return _isDegraded;
            }
        }
    }

    /// <inheritdoc />
    public string? LastDegradationMessage
    {
        get
        {
            lock (_syncLock)
            {
                return _lastDegradationMessage;
            }
        }
    }

    #endregion

    #region 公开事件

    /// <inheritdoc />
    public event Action<BoardSnapshot>? SnapshotChanged;

    /// <inheritdoc />
    public event Action<BoardSnapshot>? OnStateChanged
    {
        add => SnapshotChanged += value;
        remove => SnapshotChanged -= value;
    }

    /// <inheritdoc />
    public event Action? GameStarted;

    /// <inheritdoc />
    public event Action? GameEnded;

    /// <inheritdoc />
    public event Action<int>? TurnStarted;

    #endregion

    /// <summary>
    /// 初始化游戏状态机监听器。
    /// </summary>
    /// <param name="dbAdapter">卡牌元数据适配器 (可为空，默认使用全局单例)</param>
    /// <param name="eventSource">游戏事件源抽象 (可为空，支持后续通过 Attach 接入)</param>
    /// <param name="bobsBuddyBridge">Bob's Buddy 战力模拟适配器 (可为空)</param>
    public GameStateWatcher(
        IHearthDbAdapter? dbAdapter = null,
        IHdtGameEventSource? eventSource = null,
        IBobsBuddyBridge? bobsBuddyBridge = null)
    {
        _dbAdapter = dbAdapter ?? HearthDbAdapter.Instance;
        _eventSource = eventSource;
        _bobsBuddyBridge = bobsBuddyBridge;

        // 生成初始基准快照
        lock (_syncLock)
        {
            _currentSnapshot = AssembleSnapshotLocked();
        }

        if (_eventSource != null)
        {
            SubscribeEventSource(_eventSource);
        }

        if (_bobsBuddyBridge != null)
        {
            _bobsBuddyBridge.SimulationCompleted += OnSimulationCompleted;
        }

        _isLoaded = true;
    }

    #region 生命周期与控制

    /// <inheritdoc />
    public void Start()
    {
        lock (_syncLock)
        {
            if (_isRunning) return;
            _isRunning = true;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_syncLock)
        {
            _isRunning = false;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            _isInGame = false;
            _turn = 1;
            _heroCardId = string.Empty;
            _health = 30;
            _armor = 0;
            _tavernTier = 1;
            _upgradeCost = 5;
            _isFrozen = false;
            _bannedRaces = MinionRaceMask.None;

            _goldAvailable = 3;
            _goldMax = 3;
            _goldLocked = 0;
            _simulation = CombatSimulationResult.CreateDefaultSafePrior();

            _boardMinions.Clear();
            _handCards.Clear();
            _tavernMinions.Clear();
            _allEntities.Clear();

            _isDegraded = false;
            _lastDegradationMessage = null;

            snapshot = AssembleSnapshotLocked();
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public BoardSnapshot ForceSnapshot()
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            snapshot = AssembleSnapshotLocked();
        }

        NotifySnapshotChanged(snapshot);
        return snapshot;
    }

    /// <inheritdoc />
    public void LoadSnapshot(BoardSnapshot snapshot)
    {
        lock (_syncLock)
        {
            _currentSnapshot = snapshot;
            _turn = snapshot.Turn;
            _heroCardId = snapshot.HeroCardId;
            _health = snapshot.Health;
            _armor = snapshot.Armor;
            _tavernTier = snapshot.TavernTier;
            _upgradeCost = snapshot.UpgradeCost;
            _isFrozen = snapshot.IsFrozen;
            _bannedRaces = snapshot.BannedRaces;
            _goldAvailable = snapshot.Gold.Available;
            _goldMax = snapshot.Gold.Max;
            _goldLocked = snapshot.Gold.LockedInRoll;
            _simulation = snapshot.Simulation;
            _isInGame = true;
        }

        NotifySnapshotChanged(snapshot);
    }

    #endregion

    #region 离线模拟与战局操作动作

    /// <inheritdoc />
    public void OnGameStart(
        string? heroCardId = null,
        int health = 30,
        int armor = 0,
        MinionRaceMask bannedRaces = MinionRaceMask.None)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                _isInGame = true;
                _turn = 1;
                _heroCardId = heroCardId ?? string.Empty;
                _health = health > 0 ? health : 30;
                _armor = Math.Max(0, armor);
                _tavernTier = 1;
                _upgradeCost = 5;
                _isFrozen = false;
                _bannedRaces = bannedRaces;

                _goldAvailable = 3;
                _goldMax = 3;
                _goldLocked = 0;
                _simulation = CombatSimulationResult.CreateDefaultSafePrior();

                _boardMinions.Clear();
                _handCards.Clear();
                _tavernMinions.Clear();
                _allEntities.Clear();

                _isDegraded = false;
                _lastDegradationMessage = null;

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
        SafeInvoke(GameStarted);
    }

    /// <inheritdoc />
    public void OnGameEnd()
    {
        lock (_syncLock)
        {
            _isInGame = false;
        }

        SafeInvoke(GameEnded);
    }

    /// <inheritdoc />
    public void OnTurnStart(int turn)
    {
        BoardSnapshot snapshot;
        int currentTurn;
        lock (_syncLock)
        {
            try
            {
                _turn = Math.Max(1, turn);
                currentTurn = _turn;

                // 铸币更新：初始 3 金，每回合 +1，上限通常为 10
                _goldMax = Math.Min(10, 2 + _turn);
                _goldAvailable = _goldMax;
                _goldLocked = 0;

                // 升级费用自然递减 1 (最小为 0)
                if (_tavernTier < 7)
                {
                    _upgradeCost = Math.Max(0, _upgradeCost - 1);
                }
                else
                {
                    _upgradeCost = 0;
                }

                // 冻结逻辑：若上一回合冻结，本回合保留酒馆并解除冻结状态；若未冻结，则刷新清空酒馆
                if (!_isFrozen)
                {
                    _tavernMinions.Clear();
                }
                else
                {
                    _isFrozen = false;
                    for (int i = 0; i < _tavernMinions.Count; i++)
                    {
                        _tavernMinions[i] = _tavernMinions[i] with { IsFrozen = false };
                    }
                }

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
                currentTurn = _turn;
            }
        }

        NotifySnapshotChanged(snapshot);
        SafeInvoke(TurnStarted, currentTurn);
    }

    /// <inheritdoc />
    public void BuyMinion(int entityId)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                int tavernIndex = _tavernMinions.FindIndex(m => m.EntityId == entityId);
                if (tavernIndex >= 0)
                {
                    var tavernMinion = _tavernMinions[tavernIndex];
                    _tavernMinions.RemoveAt(tavernIndex);

                    int cost = tavernMinion.Cost > 0 ? tavernMinion.Cost : 3;
                    _goldAvailable = Math.Max(0, _goldAvailable - cost);

                    // 若手牌未满，收入手牌
                    if (_handCards.Count < BoardSnapshot.MaxHandCards)
                    {
                        var handCard = tavernMinion with
                        {
                            Zone = HdtZone.Hand,
                            ZonePosition = _handCards.Count
                        };
                        _handCards.Add(handCard);
                        _allEntities[entityId] = handCard;
                    }

                    snapshot = AssembleSnapshotLocked();
                }
                else
                {
                    snapshot = _currentSnapshot;
                }
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void SellMinion(int entityId)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                int boardIdx = _boardMinions.FindIndex(m => m.EntityId == entityId);
                if (boardIdx >= 0)
                {
                    _boardMinions.RemoveAt(boardIdx);
                    _goldAvailable += 1; // 卖怪统一回收 1 铸币
                    ReindexBoardPositionsLocked();

                    if (_allEntities.TryGetValue(entityId, out var existing))
                    {
                        _allEntities[entityId] = existing with { Zone = HdtZone.Graveyard };
                    }

                    snapshot = AssembleSnapshotLocked();
                }
                else
                {
                    int handIdx = _handCards.FindIndex(m => m.EntityId == entityId);
                    if (handIdx >= 0)
                    {
                        _handCards.RemoveAt(handIdx);
                        _goldAvailable += 1;
                        ReindexHandPositionsLocked();

                        if (_allEntities.TryGetValue(entityId, out var existing))
                        {
                            _allEntities[entityId] = existing with { Zone = HdtZone.Graveyard };
                        }

                        snapshot = AssembleSnapshotLocked();
                    }
                    else
                    {
                        snapshot = _currentSnapshot;
                    }
                }
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void PlayMinion(int entityId, int targetSlot)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                int handIdx = _handCards.FindIndex(m => m.EntityId == entityId);
                if (handIdx >= 0 && _boardMinions.Count < BoardSnapshot.MaxBoardMinions)
                {
                    var handCard = _handCards[handIdx];
                    _handCards.RemoveAt(handIdx);
                    ReindexHandPositionsLocked();

                    int insertSlot = MathEx.Clamp(targetSlot, 0, _boardMinions.Count);
                    var boardMinion = handCard with
                    {
                        Zone = HdtZone.Play,
                        ZonePosition = insertSlot
                    };

                    _boardMinions.Insert(insertSlot, boardMinion);
                    ReindexBoardPositionsLocked();
                    _allEntities[entityId] = boardMinion;

                    snapshot = AssembleSnapshotLocked();
                }
                else
                {
                    snapshot = _currentSnapshot;
                }
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void Reroll(IEnumerable<TavernMinion>? newTavernMinions = null)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                _goldAvailable = Math.Max(0, _goldAvailable - 1);
                _isFrozen = false;
                _tavernMinions.Clear();

                if (newTavernMinions != null)
                {
                    foreach (var minion in newTavernMinions)
                    {
                        if (_tavernMinions.Count >= BoardSnapshot.MaxTavernMinions) break;

                        var dto = new HdtEntityDto(
                            EntityId: minion.EntityId,
                            CardId: minion.CardId,
                            Controller: 2,
                            Zone: HdtZone.Play,
                            ZonePosition: _tavernMinions.Count,
                            Cost: minion.Cost,
                            Tier: minion.Tier,
                            Race: minion.Race,
                            IsFrozen: false
                        );
                        _tavernMinions.Add(dto);
                        _allEntities[minion.EntityId] = dto;
                    }
                }

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void Freeze(bool? isFrozen = null)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                _isFrozen = isFrozen ?? !_isFrozen;
                for (int i = 0; i < _tavernMinions.Count; i++)
                {
                    _tavernMinions[i] = _tavernMinions[i] with { IsFrozen = _isFrozen };
                    if (_allEntities.TryGetValue(_tavernMinions[i].EntityId, out var existing))
                    {
                        _allEntities[_tavernMinions[i].EntityId] = existing with { IsFrozen = _isFrozen };
                    }
                }

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void UpgradeTier()
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                if (_tavernTier < 7)
                {
                    _goldAvailable = Math.Max(0, _goldAvailable - _upgradeCost);
                    _tavernTier++;
                    _upgradeCost = GetBaseUpgradeCostForNextTier(_tavernTier);

                    snapshot = AssembleSnapshotLocked();
                }
                else
                {
                    snapshot = _currentSnapshot;
                }
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    private static int GetBaseUpgradeCostForNextTier(int currentTier) => currentTier switch
    {
        1 => 7,  // 升至 2 本完成，下一步升 3 本初始 7 费
        2 => 8,  // 升 4 本初始 8 费
        3 => 9,  // 升 5 本初始 9 费
        4 => 10, // 升 6 本初始 10 费
        5 => 11, // 升 7 本初始 11 费
        _ => 0   // 已达最高星级
    };

    #endregion

    #region 底层 HDT 日志流事件处理

    /// <inheritdoc />
    public void OnEntityCreated(HdtEntityDto entity)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                _allEntities[entity.EntityId] = entity;

                if (entity.Zone == HdtZone.Play && entity.Controller == 1)
                {
                    if (_boardMinions.Count < BoardSnapshot.MaxBoardMinions &&
                        !_boardMinions.Exists(m => m.EntityId == entity.EntityId))
                    {
                        _boardMinions.Add(entity with { ZonePosition = _boardMinions.Count });
                    }
                }
                else if (entity.Zone == HdtZone.Hand && entity.Controller == 1)
                {
                    if (_handCards.Count < BoardSnapshot.MaxHandCards &&
                        !_handCards.Exists(m => m.EntityId == entity.EntityId))
                    {
                        _handCards.Add(entity with { ZonePosition = _handCards.Count });
                    }
                }
                else if ((entity.Zone == HdtZone.SetAside || entity.Zone == HdtZone.Play) && entity.Controller == 2)
                {
                    if (_tavernMinions.Count < BoardSnapshot.MaxTavernMinions &&
                        !_tavernMinions.Exists(m => m.EntityId == entity.EntityId))
                    {
                        _tavernMinions.Add(entity with { ZonePosition = _tavernMinions.Count });
                    }
                }

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void OnEntityUpdated(HdtEntityDto entity)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                _allEntities[entity.EntityId] = entity;

                UpdateEntityInList(_boardMinions, entity);
                UpdateEntityInList(_handCards, entity);
                UpdateEntityInList(_tavernMinions, entity);

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    private static void UpdateEntityInList(List<HdtEntityDto> list, HdtEntityDto updated)
    {
        int idx = list.FindIndex(m => m.EntityId == updated.EntityId);
        if (idx >= 0)
        {
            list[idx] = updated with { ZonePosition = idx };
        }
    }

    /// <inheritdoc />
    public void OnZoneChange(HdtZoneChangeDto zoneChange)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                int id = zoneChange.EntityId;
                _allEntities.TryGetValue(id, out var entity);

                string cardId = !string.IsNullOrEmpty(zoneChange.CardId) ? zoneChange.CardId : entity.CardId;

                // 从源区域移除
                _boardMinions.RemoveAll(m => m.EntityId == id);
                _handCards.RemoveAll(m => m.EntityId == id);
                _tavernMinions.RemoveAll(m => m.EntityId == id);

                var updated = entity with
                {
                    EntityId = id,
                    CardId = cardId,
                    Zone = zoneChange.ToZone,
                    Controller = zoneChange.Controller
                };
                _allEntities[id] = updated;

                // 加入目标区域
                if (zoneChange.ToZone == HdtZone.Play && zoneChange.Controller == 1)
                {
                    if (_boardMinions.Count < BoardSnapshot.MaxBoardMinions)
                    {
                        int pos = MathEx.Clamp(zoneChange.ZonePosition, 0, _boardMinions.Count);
                        _boardMinions.Insert(pos, updated);
                    }
                }
                else if (zoneChange.ToZone == HdtZone.Hand && zoneChange.Controller == 1)
                {
                    if (_handCards.Count < BoardSnapshot.MaxHandCards)
                    {
                        _handCards.Add(updated with { ZonePosition = _handCards.Count });
                    }
                }
                else if ((zoneChange.ToZone == HdtZone.SetAside || zoneChange.ToZone == HdtZone.Play) && zoneChange.Controller == 2)
                {
                    if (_tavernMinions.Count < BoardSnapshot.MaxTavernMinions)
                    {
                        _tavernMinions.Add(updated with { ZonePosition = _tavernMinions.Count });
                    }
                }

                ReindexBoardPositionsLocked();
                ReindexHandPositionsLocked();

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void OnTagChange(HdtTagChangeDto tagChange)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                string tag = tagChange.TagName ?? string.Empty;
                int val = tagChange.Value;

                if (tag.Equals("RESOURCES", StringComparison.OrdinalIgnoreCase) || tagChange.EntityId == HdtEventConverter.TAG_RESOURCES)
                {
                    _goldMax = Math.Max(0, val);
                }
                else if (tag.Equals("RESOURCES_USED", StringComparison.OrdinalIgnoreCase) || tagChange.EntityId == HdtEventConverter.TAG_RESOURCES_USED)
                {
                    _goldAvailable = Math.Max(0, _goldMax - val);
                }
                else if (tag.Equals("PLAYER_TECH_LEVEL", StringComparison.OrdinalIgnoreCase) || tag.Equals("TECH_LEVEL", StringComparison.OrdinalIgnoreCase))
                {
                    _tavernTier = MathEx.Clamp(val, 1, 7);
                }
                else if (tag.Equals("ARMOR", StringComparison.OrdinalIgnoreCase))
                {
                    _armor = Math.Max(0, val);
                }
                else if (tag.Equals("HEALTH", StringComparison.OrdinalIgnoreCase))
                {
                    _health = Math.Max(0, val);
                }
                else if (tag.Equals("DAMAGE", StringComparison.OrdinalIgnoreCase))
                {
                    // 扣除伤害计算当前血量
                    _health = Math.Max(0, 30 - val);
                }
                else if (tag.Equals("FROZEN", StringComparison.OrdinalIgnoreCase))
                {
                    _isFrozen = val > 0;
                }

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    #endregion

    #region 全量同步与宿主接入

    /// <inheritdoc />
    public void SyncFromRawState(HdtBoardRawState rawState)
    {
        if (rawState == null) throw new ArgumentNullException(nameof(rawState));

        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                _turn = rawState.Turn;
                _heroCardId = rawState.HeroCardId;
                _health = rawState.Health;
                _armor = rawState.Armor;
                _tavernTier = rawState.TavernTier;
                _upgradeCost = rawState.UpgradeCost;
                _isFrozen = rawState.IsFrozen;
                _bannedRaces = rawState.BannedRaces;

                _goldAvailable = rawState.GoldAvailable;
                _goldMax = rawState.GoldMax;
                _goldLocked = rawState.GoldLocked;
                _simulation = rawState.Simulation;

                _boardMinions.Clear();
                _boardMinions.AddRange(rawState.PlayerBoard);

                _handCards.Clear();
                _handCards.AddRange(rawState.PlayerHand);

                _tavernMinions.Clear();
                _tavernMinions.AddRange(rawState.TavernMinions);

                snapshot = AssembleSnapshotLocked();
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    /// <inheritdoc />
    public void AttachHdtGame(object? hdtGame)
    {
        if (hdtGame == null) return;

        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            try
            {
                snapshot = HdtEventConverter.ConvertFromHdtGame(hdtGame, _dbAdapter, _simulation);
                _currentSnapshot = snapshot;

                _turn = snapshot.Turn;
                _heroCardId = snapshot.HeroCardId;
                _health = snapshot.Health;
                _armor = snapshot.Armor;
                _tavernTier = snapshot.TavernTier;
                _upgradeCost = snapshot.UpgradeCost;
                _isFrozen = snapshot.IsFrozen;
                _bannedRaces = snapshot.BannedRaces;
                _goldAvailable = snapshot.Gold.Available;
                _goldMax = snapshot.Gold.Max;
                _goldLocked = snapshot.Gold.LockedInRoll;
            }
            catch (Exception ex)
            {
                HandleDegradationLocked(ex);
                snapshot = _currentSnapshot;
            }
        }

        NotifySnapshotChanged(snapshot);
    }

    #endregion

    #region 内部快照装配与辅助逻辑 (0 GC 极速装配)

    private BoardSnapshot AssembleSnapshotLocked()
    {
        int boardCount = Math.Min(_boardMinions.Count, BoardSnapshot.MaxBoardMinions);
        var boardBuilder = ImmutableArray.CreateBuilder<MinionEntity>(boardCount);
        for (int i = 0; i < boardCount; i++)
        {
            var raw = _boardMinions[i];
            boardBuilder.Add(HdtEventConverter.ConvertToMinionEntity(raw with { ZonePosition = i }, _dbAdapter));
        }

        int handCount = Math.Min(_handCards.Count, BoardSnapshot.MaxHandCards);
        var handBuilder = ImmutableArray.CreateBuilder<HandEntity>(handCount);
        for (int i = 0; i < handCount; i++)
        {
            var raw = _handCards[i];
            handBuilder.Add(HdtEventConverter.ConvertToHandEntity(raw with { ZonePosition = i }, _dbAdapter));
        }

        int tavernCount = Math.Min(_tavernMinions.Count, BoardSnapshot.MaxTavernMinions);
        var tavernBuilder = ImmutableArray.CreateBuilder<TavernMinion>(tavernCount);
        for (int i = 0; i < tavernCount; i++)
        {
            var raw = _tavernMinions[i];
            bool frozen = raw.IsFrozen || _isFrozen;
            tavernBuilder.Add(HdtEventConverter.ConvertToTavernMinion(raw with { IsFrozen = frozen }, _dbAdapter));
        }

        var gold = new GoldBudget(_goldAvailable, _goldMax, _goldLocked);

        var snapshot = new BoardSnapshot(
            turn: Math.Max(1, _turn),
            heroCardId: _heroCardId,
            health: Math.Max(0, _health),
            armor: Math.Max(0, _armor),
            tavernTier: MathEx.Clamp(_tavernTier, 1, 7),
            upgradeCost: Math.Max(0, _upgradeCost),
            isFrozen: _isFrozen,
            bannedRaces: _bannedRaces,
            gold: gold,
            simulation: _simulation,
            boardMinions: boardBuilder.MoveToImmutable(),
            handCards: handBuilder.MoveToImmutable(),
            tavernMinions: tavernBuilder.MoveToImmutable()
        );

        _currentSnapshot = snapshot;
        _isDegraded = false;
        return snapshot;
    }

    private void ReindexBoardPositionsLocked()
    {
        for (int i = 0; i < _boardMinions.Count; i++)
        {
            _boardMinions[i] = _boardMinions[i] with { ZonePosition = i };
        }
    }

    private void ReindexHandPositionsLocked()
    {
        for (int i = 0; i < _handCards.Count; i++)
        {
            _handCards[i] = _handCards[i] with { ZonePosition = i };
        }
    }

    private void HandleDegradationLocked(Exception ex)
    {
        _isDegraded = true;
        _lastDegradationMessage = ex.Message;
        // 保持 _currentSnapshot 上一帧稳定状态不变，严禁抛出
    }

    private void NotifySnapshotChanged(BoardSnapshot snapshot)
    {
        SafeInvoke(SnapshotChanged, snapshot);
    }

    private static void SafeInvoke<T>(Action<T>? action, T arg)
    {
        if (action == null) return;
        foreach (var handler in action.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler)(arg);
            }
            catch
            {
                // 隔离外部事件订阅者异常，杜绝污染状态机主线程
            }
        }
    }

    private static void SafeInvoke(Action? action)
    {
        if (action == null) return;
        foreach (var handler in action.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch
            {
                // 隔离外部事件订阅者异常
            }
        }
    }

    private void OnSimulationCompleted(CombatSimulationResult result)
    {
        BoardSnapshot snapshot;
        lock (_syncLock)
        {
            _simulation = result;
            _currentSnapshot = _currentSnapshot.WithSimulation(result);
            snapshot = _currentSnapshot;
        }

        NotifySnapshotChanged(snapshot);
    }

    private void SubscribeEventSource(IHdtGameEventSource source)
    {
        source.GameStarted += () => OnGameStart();
        source.GameEnded += OnGameEnd;
        source.TurnStarted += OnTurnStart;
        source.EntityCreated += OnEntityCreated;
        source.EntityUpdated += OnEntityUpdated;
        source.ZoneChanged += OnZoneChange;
        source.TagChanged += OnTagChange;
        source.BoardChanged += () => ForceSnapshot();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        lock (_syncLock)
        {
            _isRunning = false;
            _isInGame = false;

            if (_bobsBuddyBridge != null)
            {
                _bobsBuddyBridge.SimulationCompleted -= OnSimulationCompleted;
                _bobsBuddyBridge = null;
            }

            _eventSource?.Dispose();
            _eventSource = null;

            SnapshotChanged = null;
            GameStarted = null;
            GameEnded = null;
            TurnStarted = null;
        }
    }

    #endregion
}
