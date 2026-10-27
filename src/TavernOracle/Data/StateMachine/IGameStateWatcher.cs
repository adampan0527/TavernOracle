using System;
using System.Collections.Generic;
using TavernOracle.Data.State;
using TavernOracle.Priors;

namespace TavernOracle.Data.StateMachine;

/// <summary>
/// HDT 游戏事件转换与状态机契约接口 (Game State Watcher Interface)。
/// 负责将 HDT 底层日志事件流转为内部不可变盘面快照 <see cref="BoardSnapshot"/>。
/// 严格遵守 15ms 时延闭环（本环节 &lt;= 2ms）与零异常崩溃降级原则。
/// </summary>
public interface IGameStateWatcher : IDisposable
{
    /// <summary>
    /// 当前最新稳定的盘面不可变快照
    /// </summary>
    BoardSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// 当前是否处于酒馆战棋对局中
    /// </summary>
    bool IsInGame { get; }

    /// <summary>
    /// 状态机监听服务是否处于激活运行状态
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 当前对局回合数 (从第 1 回合起算)
    /// </summary>
    int Turn { get; }

    /// <summary>
    /// 当前状态机是否处于断流降级兜底状态
    /// </summary>
    bool IsDegraded { get; }

    /// <summary>
    /// 最近一次降级警告或异常描述信息
    /// </summary>
    string? LastDegradationMessage { get; }

    /// <summary>
    /// 盘面局势发生变更时派发最新不可变快照事件
    /// </summary>
    event Action<BoardSnapshot>? SnapshotChanged;

    /// <summary>
    /// 状态机盘面发生变更时派发最新快照事件别名 (对齐 SnapshotChanged)
    /// </summary>
    event Action<BoardSnapshot>? OnStateChanged;

    /// <summary>
    /// 对局开始事件
    /// </summary>
    event Action? GameStarted;

    /// <summary>
    /// 对局结束事件
    /// </summary>
    event Action? GameEnded;

    /// <summary>
    /// 回合更替开始事件 (参数为当前回合数)
    /// </summary>
    event Action<int>? TurnStarted;

    #region 生命周期控制

    /// <summary>
    /// 启动状态机监听
    /// </summary>
    void Start();

    /// <summary>
    /// 停止状态机监听
    /// </summary>
    void Stop();

    /// <summary>
    /// 重置状态机到初始安全状态
    /// </summary>
    void Reset();

    /// <summary>
    /// 强制从内部实时状态重新组装并派发一次完整快照
    /// </summary>
    BoardSnapshot ForceSnapshot();

    /// <summary>
    /// 直接载入外部盘面快照 (支持离线测试、回放与决策推导)
    /// </summary>
    void LoadSnapshot(BoardSnapshot snapshot);

    #endregion

    #region 离线模拟与战局动作注入 (支持离线测试与决策搜索回放)

    /// <summary>
    /// 触发对局开始
    /// </summary>
    void OnGameStart(
        string? heroCardId = null,
        int health = 30,
        int armor = 0,
        MinionRaceMask bannedRaces = MinionRaceMask.None);

    /// <summary>
    /// 触发对局结束
    /// </summary>
    void OnGameEnd();

    /// <summary>
    /// 触发回合更替
    /// </summary>
    void OnTurnStart(int turn);

    /// <summary>
    /// 执行买入随从动作 (将酒馆在售怪移入手牌并扣除铸币)
    /// </summary>
    void BuyMinion(int entityId);

    /// <summary>
    /// 执行卖出随从动作 (将战场或手牌随从移入墓地并增加 1 铸币)
    /// </summary>
    void SellMinion(int entityId);

    /// <summary>
    /// 执行打出随从动作 (将手牌随从置入战场指定卡位)
    /// </summary>
    void PlayMinion(int entityId, int targetSlot);

    /// <summary>
    /// 执行刷新酒馆在售动作 (扣除 1 铸币并更新随从列表)
    /// </summary>
    void Reroll(IEnumerable<TavernMinion>? newTavernMinions = null);

    /// <summary>
    /// 执行冻结/解冻酒馆动作
    /// </summary>
    void Freeze(bool? isFrozen = null);

    /// <summary>
    /// 执行升级酒馆等级动作
    /// </summary>
    void UpgradeTier();

    #endregion

    #region 底层 HDT 日志流事件接入

    /// <summary>
    /// 接收实体创建事件
    /// </summary>
    void OnEntityCreated(HdtEntityDto entity);

    /// <summary>
    /// 接收实体更新事件
    /// </summary>
    void OnEntityUpdated(HdtEntityDto entity);

    /// <summary>
    /// 接收实体区域流转事件
    /// </summary>
    void OnZoneChange(HdtZoneChangeDto zoneChange);

    /// <summary>
    /// 接收游戏标签变更事件
    /// </summary>
    void OnTagChange(HdtTagChangeDto tagChange);

    #endregion

    #region HDT 宿主同步支持

    /// <summary>
    /// 从全量原始状态直接同步盘面
    /// </summary>
    void SyncFromRawState(HdtBoardRawState rawState);

    /// <summary>
    /// 挂载并同步 HDT 原生 Game 实例 (如 Core.Game)
    /// </summary>
    void AttachHdtGame(object? hdtGame);

    #endregion
}
