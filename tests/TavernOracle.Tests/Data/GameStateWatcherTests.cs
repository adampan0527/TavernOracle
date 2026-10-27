using System;
using System.Collections.Generic;
using System.Diagnostics;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Data.StateMachine;
using TavernOracle.Priors;
using TavernOracle.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace TavernOracle.Tests.Data;

public class GameStateWatcherTests
{
    private readonly ITestOutputHelper _output;

    public GameStateWatcherTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InitialState_DefaultSnapshot_HasSafeDefaults()
    {
        // Arrange & Act
        using var watcher = new GameStateWatcher();

        // Assert
        Assert.NotNull(watcher);
        Assert.False(watcher.IsInGame);
        Assert.False(watcher.IsRunning);
        Assert.False(watcher.IsDegraded);
        Assert.Null(watcher.LastDegradationMessage);

        var snap = watcher.CurrentSnapshot;
        Assert.Equal(1, snap.Turn);
        Assert.Equal(30, snap.Health);
        Assert.Equal(0, snap.Armor);
        Assert.Equal(1, snap.TavernTier);
        Assert.Equal(5, snap.UpgradeCost);
        Assert.False(snap.IsFrozen);
        Assert.Equal(3, snap.Gold.Available);
        Assert.Equal(3, snap.Gold.Max);
        Assert.Empty(snap.BoardMinions);
        Assert.Empty(snap.HandCards);
        Assert.Empty(snap.TavernMinions);
    }

    [Fact]
    public void Lifecycle_GameStartAndEnd_DispatchesEventsAccurately()
    {
        // Arrange
        using var watcher = new GameStateWatcher();
        bool gameStartFired = false;
        bool gameEndFired = false;
        BoardSnapshot? receivedSnapshot = null;

        watcher.GameStarted += () => gameStartFired = true;
        watcher.GameEnded += () => gameEndFired = true;
        watcher.SnapshotChanged += snap => receivedSnapshot = snap;

        // Act - Game Start
        watcher.OnGameStart(
            heroCardId: "BG20_HERO_101",
            health: 35,
            armor: 5,
            bannedRaces: MinionRaceMask.Dragon | MinionRaceMask.Demon
        );

        // Assert
        Assert.True(watcher.IsInGame);
        Assert.True(gameStartFired);
        Assert.NotNull(receivedSnapshot);
        Assert.Equal("BG20_HERO_101", watcher.CurrentSnapshot.HeroCardId);
        Assert.Equal(35, watcher.CurrentSnapshot.Health);
        Assert.Equal(5, watcher.CurrentSnapshot.Armor);
        Assert.Equal(40, watcher.CurrentSnapshot.TotalHealth);
        Assert.Equal(MinionRaceMask.Dragon | MinionRaceMask.Demon, watcher.CurrentSnapshot.BannedRaces);

        // Act - Game End
        watcher.OnGameEnd();

        // Assert
        Assert.False(watcher.IsInGame);
        Assert.True(gameEndFired);
    }

    [Fact]
    public void ContinuousGameFlow_SimulateFullTurnSequence_AccuratelyUpdatesSnapshot()
    {
        // Arrange
        using var watcher = new GameStateWatcher();
        int snapshotChangedCount = 0;
        watcher.SnapshotChanged += _ => snapshotChangedCount++;

        // 1. 对局开始：Turn 1, 选定英雄
        watcher.OnGameStart(heroCardId: "BG20_HERO_101", health: 30, armor: 0);
        Assert.Equal(1, watcher.CurrentSnapshot.Turn);
        Assert.Equal(3, watcher.CurrentSnapshot.Gold.Available);
        Assert.Equal(5, watcher.CurrentSnapshot.UpgradeCost);

        // 2. 鲍勃酒馆上架 2 个 1 星随从：跳蛙 (BG21_000) 与 偏折 (BGS_071)
        watcher.OnEntityCreated(new HdtEntityDto(
            EntityId: 101,
            CardId: "BG21_000",
            Controller: 2,
            Zone: HdtZone.Play,
            ZonePosition: 0,
            Attack: 3,
            Health: 3,
            Cost: 3,
            Tier: 2,
            Race: MinionRaceMask.Beast
        ));

        watcher.OnEntityCreated(new HdtEntityDto(
            EntityId: 102,
            CardId: "BGS_071",
            Controller: 2,
            Zone: HdtZone.Play,
            ZonePosition: 1,
            Attack: 3,
            Health: 2,
            Cost: 3,
            Tier: 3,
            Race: MinionRaceMask.Mech
        ));

        Assert.Equal(2, watcher.CurrentSnapshot.TavernMinions.Length);
        Assert.Equal("BG21_000", watcher.CurrentSnapshot.TavernMinions[0].CardId);
        Assert.Equal("BGS_071", watcher.CurrentSnapshot.TavernMinions[1].CardId);

        // 3. 买入跳蛙骑士 (BG21_000): 扣除 3 金币，酒馆减少 1 个，手牌增加 1 张
        watcher.BuyMinion(101);

        var snapAfterBuy = watcher.CurrentSnapshot;
        Assert.Equal(0, snapAfterBuy.Gold.Available);
        Assert.Single(snapAfterBuy.TavernMinions);
        Assert.Equal("BGS_071", snapAfterBuy.TavernMinions[0].CardId);
        Assert.Single(snapAfterBuy.HandCards);
        Assert.Equal("BG21_000", snapAfterBuy.HandCards[0].CardId);
        Assert.Empty(snapAfterBuy.BoardMinions);

        // 4. 打出跳蛙骑士到战场第 0 槽位
        watcher.PlayMinion(101, targetSlot: 0);

        var snapAfterPlay = watcher.CurrentSnapshot;
        Assert.Empty(snapAfterPlay.HandCards);
        Assert.Single(snapAfterPlay.BoardMinions);
        Assert.Equal(101, snapAfterPlay.BoardMinions[0].EntityId);
        Assert.Equal("BG21_000", snapAfterPlay.BoardMinions[0].CardId);
        Assert.Equal(3, snapAfterPlay.BoardMinions[0].Attack);
        Assert.Equal(3, snapAfterPlay.BoardMinions[0].Health);
        Assert.Equal(MinionRaceMask.Beast, snapAfterPlay.BoardMinions[0].Race);
        Assert.True(snapAfterPlay.BoardMinions[0].Deathrattle);

        // 5. 冻结酒馆在售商品 (偏折机器人)
        watcher.Freeze(true);
        Assert.True(watcher.CurrentSnapshot.IsFrozen);
        Assert.True(watcher.CurrentSnapshot.TavernMinions[0].IsFrozen);

        // 6. 回合更替：进入第 2 回合
        watcher.OnTurnStart(2);

        var snapTurn2 = watcher.CurrentSnapshot;
        Assert.Equal(2, snapTurn2.Turn);
        Assert.Equal(4, snapTurn2.Gold.Available); // Turn 2 = 4 金币
        Assert.Equal(4, snapTurn2.Gold.Max);
        Assert.Equal(4, snapTurn2.UpgradeCost);   // 升级费用从 5 降为 4
        Assert.False(snapTurn2.IsFrozen);         // 回合开始后解除冻结状态
        Assert.Single(snapTurn2.TavernMinions);  // 冻结保留了偏折机器人
        Assert.Equal("BGS_071", snapTurn2.TavernMinions[0].CardId);

        // 7. 升级酒馆至 2 星：消耗 4 金币，酒馆变为 2 星，下次升 3 星初始 8 费或 7 费
        watcher.UpgradeTier();

        var snapTier2 = watcher.CurrentSnapshot;
        Assert.Equal(2, snapTier2.TavernTier);
        Assert.Equal(0, snapTier2.Gold.Available);
        Assert.Equal(8, snapTier2.UpgradeCost);

        // 8. 卖出战场上的跳蛙骑士：战场清空，金币 +1
        watcher.SellMinion(101);

        var snapAfterSell = watcher.CurrentSnapshot;
        Assert.Empty(snapAfterSell.BoardMinions);
        Assert.Equal(1, snapAfterSell.Gold.Available);

        Assert.True(snapshotChangedCount >= 8, $"Expected >= 8 snapshot dispatches, got {snapshotChangedCount}");
    }

    [Fact]
    public void EventDriven_ZoneAndTagChanges_MirrorsBoardCorrectly()
    {
        // Arrange
        using var watcher = new GameStateWatcher();
        watcher.OnGameStart("BG20_HERO_101", health: 30);

        // 1. 通过 OnEntityCreated 注入随从实体
        watcher.OnEntityCreated(new HdtEntityDto(
            EntityId: 201,
            CardId: "FP1_031", // 经典瑞文戴尔 (5星 1/7 亡语)
            Controller: 1,
            Zone: HdtZone.Hand,
            ZonePosition: 0
        ));

        Assert.Single(watcher.CurrentSnapshot.HandCards);
        Assert.Equal("FP1_031", watcher.CurrentSnapshot.HandCards[0].CardId);

        // 2. 模拟 HDT ZoneChange: Hand -> Play
        watcher.OnZoneChange(new HdtZoneChangeDto(
            EntityId: 201,
            CardId: "FP1_031",
            FromZone: HdtZone.Hand,
            ToZone: HdtZone.Play,
            ZonePosition: 0,
            Controller: 1
        ));

        Assert.Empty(watcher.CurrentSnapshot.HandCards);
        Assert.Single(watcher.CurrentSnapshot.BoardMinions);
        Assert.Equal("FP1_031", watcher.CurrentSnapshot.BoardMinions[0].CardId);
        Assert.True(watcher.CurrentSnapshot.BoardMinions[0].Deathrattle);

        // 3. 模拟 HDT TagChange: RESOURCES 与 RESOURCES_USED
        watcher.OnTagChange(new HdtTagChangeDto(EntityId: 0, TagName: "RESOURCES", Value: 10));
        watcher.OnTagChange(new HdtTagChangeDto(EntityId: 0, TagName: "RESOURCES_USED", Value: 4));

        Assert.Equal(10, watcher.CurrentSnapshot.Gold.Max);
        Assert.Equal(6, watcher.CurrentSnapshot.Gold.Available);

        // 4. 模拟护甲与扣血 TagChange
        watcher.OnTagChange(new HdtTagChangeDto(EntityId: 0, TagName: "ARMOR", Value: 8));
        watcher.OnTagChange(new HdtTagChangeDto(EntityId: 0, TagName: "DAMAGE", Value: 12));

        Assert.Equal(8, watcher.CurrentSnapshot.Armor);
        Assert.Equal(18, watcher.CurrentSnapshot.Health); // 30 - 12
        Assert.Equal(26, watcher.CurrentSnapshot.TotalHealth);
    }

    [Fact]
    public void MockGameEventSource_FullEventWiring_TriggersStateTransitions()
    {
        // Arrange
        using var mockSource = new MockGameEventSource();
        using var watcher = new GameStateWatcher(eventSource: mockSource);

        bool turnStartedFired = false;
        int receivedTurn = 0;
        watcher.TurnStarted += t =>
        {
            turnStartedFired = true;
            receivedTurn = t;
        };

        // Act - Trigger game start through mock event source
        mockSource.TriggerGameStart();
        Assert.True(watcher.IsInGame);

        // Act - Trigger turn start
        mockSource.TriggerTurnStart(3);
        Assert.True(turnStartedFired);
        Assert.Equal(3, receivedTurn);
        Assert.Equal(3, watcher.CurrentSnapshot.Turn);
        Assert.Equal(5, watcher.CurrentSnapshot.Gold.Available); // Turn 3 = 5 gold

        // Act - Trigger entity created
        mockSource.TriggerEntityCreated(new HdtEntityDto(
            EntityId: 301,
            CardId: "BGS_071",
            Controller: 1,
            Zone: HdtZone.Play,
            ZonePosition: 0,
            Attack: 3,
            Health: 2
        ));

        Assert.Single(watcher.CurrentSnapshot.BoardMinions);
        Assert.Equal("BGS_071", watcher.CurrentSnapshot.BoardMinions[0].CardId);

        // Act - Trigger game end
        mockSource.TriggerGameEnd();
        Assert.False(watcher.IsInGame);
    }

    [Fact]
    public void BobsBuddyBridge_SimulationCompleted_UpdatesSimulationInSnapshot()
    {
        // Arrange
        using var bridge = new BobsBuddyBridge();
        using var watcher = new GameStateWatcher(bobsBuddyBridge: bridge);

        // Act - Report simulation output from Bob's Buddy
        bridge.ReportSimulationOutput(
            winRate: 0.75,
            tieRate: 0.10,
            lossRate: 0.15,
            lethalRate: 0.04,
            avgDamageTaken: -4.5,
            simulationsCount: 5000
        );

        // Assert
        var snap = watcher.CurrentSnapshot;
        Assert.False(snap.Simulation.IsDegraded);
        Assert.Equal(0.75, snap.Simulation.WinRate, precision: 3);
        Assert.Equal(0.04, snap.Simulation.PLethal, precision: 3);
    }

    [Fact]
    public void FaultTolerance_CorruptedAndDirtyData_DegradesGracefullyWithoutCrashing()
    {
        // Arrange
        using var watcher = new GameStateWatcher();
        watcher.OnGameStart("BG20_HERO_101", 30);
        var initialSnapshot = watcher.CurrentSnapshot;

        // Act 1: 传入非法脏数据 (空 CardId，越界槽位，负数身材)
        watcher.OnEntityCreated(new HdtEntityDto(
            EntityId: -999,
            CardId: null!,
            Controller: 99,
            Zone: (HdtZone)999,
            ZonePosition: -50,
            Attack: -10,
            Health: -20
        ));

        // Assert 1: 不抛出异常，快照处于安全状态
        Assert.NotNull(watcher.CurrentSnapshot.HeroCardId);

        // Act 2: 模拟事件监听者抛出未捕获异常
        watcher.SnapshotChanged += _ => throw new InvalidOperationException("External subscriber crash");

        // 触发操作：买入、卖出、升本
        watcher.UpgradeTier();
        watcher.Freeze();

        // Assert 2: 状态机内部完好无损，绝不向外抛出异常
        Assert.True(watcher.CurrentSnapshot.TavernTier >= 1);
    }

    [Fact]
    public void PerformanceBenchmark_5000StateTransitions_AverageLatencyUnder2Milliseconds()
    {
        // Arrange
        using var watcher = new GameStateWatcher();
        watcher.OnGameStart("BG20_HERO_101", health: 30, armor: 5);

        const int iterations = 5000;
        var sw = Stopwatch.StartNew();

        // Act: 循环推进 5,000 次高频状态转移 (买入、打出、卖出、刷新、升本、回合推进)
        for (int i = 0; i < iterations; i++)
        {
            int entityId = 1000 + i;

            // 1. 创建在售随从
            watcher.OnEntityCreated(new HdtEntityDto(
                EntityId: entityId,
                CardId: "BG21_000",
                Controller: 2,
                Zone: HdtZone.Play,
                ZonePosition: 0,
                Attack: 3,
                Health: 3,
                Tier: 2,
                Race: MinionRaceMask.Beast
            ));

            // 2. 买入随从
            watcher.BuyMinion(entityId);

            // 3. 打出随从
            watcher.PlayMinion(entityId, targetSlot: 0);

            // 4. 卖出随从
            watcher.SellMinion(entityId);

            // 5. 周期性回合推进与升本
            if (i % 100 == 0)
            {
                watcher.OnTurnStart((i / 100) + 1);
                watcher.UpgradeTier();
            }
        }

        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        // 单次状态转移平均耗时 (包含 4 次快照产出：生成、买入、打出、卖出)
        int totalSnapshots = iterations * 4;
        double avgSnapshotMs = totalMs / totalSnapshots;

        _output.WriteLine($"[T05 Performance Benchmark] Total Time: {totalMs:F2} ms for {totalSnapshots} snapshots.");
        _output.WriteLine($"[T05 Performance Benchmark] Average Snapshot Latency: {avgSnapshotMs:F4} ms.");

        // Assert: 严格断言单次快照产出耗时 <= 2.0 ms
        Assert.True(avgSnapshotMs <= 2.0, $"Average latency {avgSnapshotMs:F4}ms exceeded 2.0ms budget!");
        Assert.True(avgSnapshotMs < 0.2, $"P99 optimization target violated: expected < 0.2ms, got {avgSnapshotMs:F4}ms");
    }
}
