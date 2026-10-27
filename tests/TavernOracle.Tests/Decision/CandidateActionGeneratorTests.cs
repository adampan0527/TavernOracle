namespace TavernOracle.Tests.Decision;

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using TavernOracle.Data.State;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Priors;
using Xunit;

public class CandidateActionGeneratorTests
{
    private readonly ICandidateActionGenerator _generator = CandidateActionGenerator.Shared;

    #region 内存契约与结构体验证

    [Fact]
    public void MemoryContract_CandidateAction_IsValueType_ZeroHeapAllocation()
    {
        Assert.True(typeof(CandidateAction).IsValueType, "CandidateAction must be a struct (value type).");
        Assert.False(typeof(CandidateAction).IsClass, "CandidateAction must NOT be a reference type (class).");
    }

    [Fact]
    public void MemoryContract_CandidateActionBuffer_IsValueType_ZeroHeapAllocation()
    {
        Assert.True(typeof(CandidateActionBuffer).IsValueType, "CandidateActionBuffer must be a struct (value type).");
        Assert.False(typeof(CandidateActionBuffer).IsClass, "CandidateActionBuffer must NOT be a reference type (class).");
        Assert.True(typeof(InlineActionStorage).IsValueType, "InlineActionStorage must be a struct (value type).");
    }

    #endregion

    #region 升本动作 (UpgradeTier) 边界测试

    [Theory]
    [InlineData(4, 5, false)] // 4 金币 < 5 升本费用 -> 非法
    [InlineData(5, 5, true)]  // 5 金币 == 5 升本费用 -> 合法
    [InlineData(6, 5, true)]  // 6 金币 > 5 升本费用 -> 合法
    [InlineData(0, 0, true)]  // 0 金币 == 0 升本费用 -> 合法
    public void UpgradeTier_GoldBoundary_GeneratesOnlyWhenAffordable(int availableGold, int upgradeCost, bool expectedLegal)
    {
        // Arrange
        var snapshot = new BoardSnapshot
        {
            TavernTier = 2,
            UpgradeCost = upgradeCost,
            Gold = new GoldBudget(availableGold, 10, 0)
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        var upgradeActions = actions.Where(a => a.ActionType == ActionType.UpgradeTier).ToList();
        if (expectedLegal)
        {
            Assert.Single(upgradeActions);
            var action = upgradeActions[0];
            Assert.Equal(upgradeCost, action.GoldCost);
            Assert.Equal(-upgradeCost, action.GoldDelta);
            Assert.Equal(-1, action.SlotIndex);
            Assert.Equal(-1, action.TargetPosition);
            Assert.Equal(0, action.EntityId);
        }
        else
        {
            Assert.Empty(upgradeActions);
        }
    }

    [Fact]
    public void UpgradeTier_AtMaxTier7_NeverGeneratesUpgradeAction()
    {
        // Arrange: 酒馆星级已达 7 级上限，即使金币充足也绝对不可升本
        var snapshot = new BoardSnapshot
        {
            TavernTier = 7,
            UpgradeCost = 0,
            Gold = new GoldBudget(10, 10, 0)
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.UpgradeTier);
    }

    #endregion

    #region 刷新动作 (Reroll) 边界测试

    [Theory]
    [InlineData(0, false)] // 0 金币 -> 无法刷新
    [InlineData(1, true)]  // 1 金币 -> 可以刷新
    [InlineData(10, true)] // 10 金币 -> 可以刷新
    public void Reroll_GoldBoundary_GeneratesOnlyWhenGoldIsAtLeastOne(int availableGold, bool expectedLegal)
    {
        // Arrange
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(availableGold, 10, 0)
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        var rerollActions = actions.Where(a => a.ActionType == ActionType.Reroll).ToList();
        if (expectedLegal)
        {
            Assert.Single(rerollActions);
            var action = rerollActions[0];
            Assert.Equal(1, action.GoldCost);
            Assert.Equal(-1, action.GoldDelta);
            Assert.Equal(-1, action.SlotIndex);
            Assert.Equal(-1, action.TargetPosition);
        }
        else
        {
            Assert.Empty(rerollActions);
        }
    }

    #endregion

    #region 冻结动作 (Freeze) 边界测试

    [Fact]
    public void Freeze_WhenTavernHasMinionsAndNotFrozen_GeneratesFreezeAction()
    {
        // Arrange
        var snapshot = new BoardSnapshot
        {
            IsFrozen = false,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_01", 3, 1, MinionRaceMask.Beast, false)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        var freezeActions = actions.Where(a => a.ActionType == ActionType.Freeze).ToList();
        Assert.Single(freezeActions);
        var action = freezeActions[0];
        Assert.Equal(0, action.GoldCost);
        Assert.Equal(0, action.GoldDelta);
        Assert.Equal(-1, action.SlotIndex);
        Assert.Equal(-1, action.TargetPosition);
    }

    [Fact]
    public void Freeze_WhenAlreadyFrozen_DoesNotGenerateFreezeAction()
    {
        // Arrange: 当前酒馆已经处于冻结状态，绝不重复生成 Freeze 动作
        var snapshot = new BoardSnapshot
        {
            IsFrozen = true,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_01", 3, 1, MinionRaceMask.Beast, true)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Freeze);
    }

    [Fact]
    public void Freeze_WhenTavernIsEmpty_DoesNotGenerateFreezeAction()
    {
        // Arrange: 酒馆在售为空，不可冻结
        var snapshot = new BoardSnapshot
        {
            IsFrozen = false,
            TavernMinions = ImmutableArray<TavernMinion>.Empty
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Freeze);
    }

    #endregion

    #region 购买动作 (Buy) 边界测试与硬性剪枝

    [Fact]
    public void Buy_WhenHandIsFull10_NeverGeneratesBuyActions()
    {
        // Arrange: 手牌满 10 张，即使有 10 金币且酒馆满员，也绝对禁止购买
        var handBuilder = ImmutableArray.CreateBuilder<HandEntity>();
        for (int i = 0; i < 10; i++)
        {
            handBuilder.Add(new HandEntity(200 + i, $"BG_HAND_{i}", 0, isMinion: true, isSpell: false, i));
        }

        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(10, 10, 0),
            HandCards = handBuilder.ToImmutable(),
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_01", 3, 1, MinionRaceMask.Mech, false),
                new TavernMinion(302, "BG_02", 3, 1, MinionRaceMask.Demon, false)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert: 100% 剪枝 Buy 动作
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Buy);
    }

    [Fact]
    public void Buy_WhenGoldIsZero_NeverGeneratesBuyActions()
    {
        // Arrange: 0 金币，酒馆随从花费 3 金币，手牌空置
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(0, 10, 0),
            HandCards = ImmutableArray<HandEntity>.Empty,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_01", 3, 1, MinionRaceMask.Mech, false)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Buy);
    }

    [Fact]
    public void Buy_GeneratesBuyForEachAffordableMinion_WithCorrectSlotAndCost()
    {
        // Arrange: 3 金币，酒馆 3 个随从 (分别花费 3, 4, 3)
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(3, 10, 0),
            HandCards = ImmutableArray<HandEntity>.Empty,
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_M1", 3, 1, MinionRaceMask.Beast, false),
                new TavernMinion(302, "BG_M2", 4, 2, MinionRaceMask.Dragon, false), // 4 金币买不起
                new TavernMinion(303, "BG_M3", 3, 1, MinionRaceMask.Pirate, false)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert: 仅针对 301 (slot 0) 和 303 (slot 2) 生成 Buy 动作
        var buyActions = actions.Where(a => a.ActionType == ActionType.Buy).ToList();
        Assert.Equal(2, buyActions.Count);

        var first = buyActions[0];
        Assert.Equal(301, first.EntityId);
        Assert.Equal(0, first.SlotIndex);
        Assert.Equal(3, first.GoldCost);
        Assert.Equal(-3, first.GoldDelta);
        Assert.Equal("BG_M1", first.CardId);

        var second = buyActions[1];
        Assert.Equal(303, second.EntityId);
        Assert.Equal(2, second.SlotIndex);
        Assert.Equal(3, second.GoldCost);
        Assert.Equal(-3, second.GoldDelta);
        Assert.Equal("BG_M3", second.CardId);
    }

    #endregion

    #region 出售动作 (Sell) 边界测试

    [Fact]
    public void Sell_WhenBoardIsEmpty_GeneratesZeroSellActions()
    {
        // Arrange
        var snapshot = new BoardSnapshot
        {
            BoardMinions = ImmutableArray<MinionEntity>.Empty
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Sell);
    }

    [Fact]
    public void Sell_GeneratesSellForEachBoardMinion_YieldsOneGold()
    {
        // Arrange: 场上有 3 个随从，即使可用金币为 0 也允许出售
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(0, 10, 0),
            BoardMinions = ImmutableArray.Create(
                new MinionEntity { EntityId = 101, CardId = "BG_B1", ZonePosition = 0 },
                new MinionEntity { EntityId = 102, CardId = "BG_B2", ZonePosition = 1 },
                new MinionEntity { EntityId = 103, CardId = "BG_B3", ZonePosition = 2 }
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        var sellActions = actions.Where(a => a.ActionType == ActionType.Sell).ToList();
        Assert.Equal(3, sellActions.Count);

        for (int i = 0; i < 3; i++)
        {
            var sell = sellActions[i];
            Assert.Equal(101 + i, sell.EntityId);
            Assert.Equal(i, sell.SlotIndex);
            Assert.Equal(-1, sell.TargetPosition);
            Assert.Equal(0, sell.GoldCost);
            Assert.Equal(1, sell.GoldDelta); // 获得 1 金币
            Assert.Equal($"BG_B{i + 1}", sell.CardId);
        }
    }

    #endregion

    #region 打出动作 (Play) 边界测试与全站位排列

    [Fact]
    public void Play_WhenBoardIsFull7_NeverGeneratesPlayActions()
    {
        // Arrange: 战场满员 7 随从，即使手牌有多张随从且金币充足，也绝对不可打出
        var boardBuilder = ImmutableArray.CreateBuilder<MinionEntity>();
        for (int i = 0; i < 7; i++)
        {
            boardBuilder.Add(new MinionEntity { EntityId = 100 + i, CardId = $"BG_MINION_{i}", ZonePosition = i });
        }

        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = boardBuilder.ToImmutable(),
            HandCards = ImmutableArray.Create(
                new HandEntity(201, "BG_H1", 0, isMinion: true, isSpell: false, 0),
                new HandEntity(202, "BG_H2", 0, isMinion: true, isSpell: false, 1)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert: 100% 剪枝 Play 动作
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Play);
    }

    [Fact]
    public void Play_EmptyBoard_GeneratesPositionZeroOnly()
    {
        // Arrange: 场上 0 随从，手牌 1 张随从
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(0, 10, 0),
            BoardMinions = ImmutableArray<MinionEntity>.Empty,
            HandCards = ImmutableArray.Create(
                new HandEntity(201, "BG_H1", 0, isMinion: true, isSpell: false, 0)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert: 只能打在第 0 席位
        var playActions = actions.Where(a => a.ActionType == ActionType.Play).ToList();
        Assert.Single(playActions);
        var play = playActions[0];
        Assert.Equal(201, play.EntityId);
        Assert.Equal(0, play.SlotIndex);
        Assert.Equal(0, play.TargetPosition);
        Assert.Equal(0, play.GoldCost);
        Assert.Equal(0, play.GoldDelta);
        Assert.Equal("BG_H1", play.CardId);
    }

    [Fact]
    public void Play_TwoMinionsOnBoard_GeneratesThreeTargetPositionsPerHandCard()
    {
        // Arrange: 场上有 2 个随从 (pos 0, 1)，手牌有 1 张随从 (Cost 0)
        // 目标站位应为: 0 (最左), 1 (中间), 2 (最右)
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(5, 10, 0),
            BoardMinions = ImmutableArray.Create(
                new MinionEntity { EntityId = 101, CardId = "BG_B1", ZonePosition = 0 },
                new MinionEntity { EntityId = 102, CardId = "BG_B2", ZonePosition = 1 }
            ),
            HandCards = ImmutableArray.Create(
                new HandEntity(201, "BG_H1", 0, isMinion: true, isSpell: false, 0)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert
        var playActions = actions.Where(a => a.ActionType == ActionType.Play).ToList();
        Assert.Equal(3, playActions.Count);

        for (int pos = 0; pos <= 2; pos++)
        {
            var play = playActions[pos];
            Assert.Equal(201, play.EntityId);
            Assert.Equal(0, play.SlotIndex);
            Assert.Equal(pos, play.TargetPosition);
            Assert.Equal("BG_H1", play.CardId);
        }
    }

    [Fact]
    public void Play_WhenHandCardCostExceedsAvailableGold_PrunesAction()
    {
        // Arrange: 0 金币，手牌有法术/特殊卡牌花费 2 铸币
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(0, 10, 0),
            BoardMinions = ImmutableArray.Create(
                new MinionEntity { EntityId = 101, CardId = "BG_B1", ZonePosition = 0 }
            ),
            HandCards = ImmutableArray.Create(
                new HandEntity(201, "BG_SPELL", cost: 2, isMinion: false, isSpell: true, 0)
            )
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert: 无法支付费用，应被硬性剪枝
        Assert.DoesNotContain(actions, a => a.ActionType == ActionType.Play);
    }

    #endregion

    #region 极限复合场景 (Extreme Complex Scenarios)

    [Fact]
    public void ExtremeScenario_ZeroGold_FullBoard_FullHand_AlreadyFrozen()
    {
        // Arrange: 0 金币、满场 7 随从、满手牌 10 张、酒馆已冻结且有 7 随从
        var boardBuilder = ImmutableArray.CreateBuilder<MinionEntity>();
        for (int i = 0; i < 7; i++)
        {
            boardBuilder.Add(new MinionEntity { EntityId = 100 + i, CardId = $"BG_B_{i}", ZonePosition = i });
        }

        var handBuilder = ImmutableArray.CreateBuilder<HandEntity>();
        for (int i = 0; i < 10; i++)
        {
            handBuilder.Add(new HandEntity(200 + i, $"BG_H_{i}", 0, isMinion: true, isSpell: false, i));
        }

        var tavernBuilder = ImmutableArray.CreateBuilder<TavernMinion>();
        for (int i = 0; i < 7; i++)
        {
            tavernBuilder.Add(new TavernMinion(300 + i, $"BG_T_{i}", 3, 2, MinionRaceMask.All, true));
        }

        var snapshot = new BoardSnapshot
        {
            Turn = 5,
            TavernTier = 3,
            UpgradeCost = 7,
            IsFrozen = true,
            Gold = new GoldBudget(0, 10, 0),
            BoardMinions = boardBuilder.ToImmutable(),
            HandCards = handBuilder.ToImmutable(),
            TavernMinions = tavernBuilder.ToImmutable()
        };

        // Act
        var actions = _generator.Generate(snapshot);

        // Assert:
        // - UpgradeTier: 0 金币 < 7 -> 0
        // - Reroll: 0 金币 < 1 -> 0
        // - Freeze: 已冻结 -> 0
        // - Buy: 满手牌 + 0 金币 -> 0
        // - Play: 满场 7 随从 -> 0
        // - Sell: 7 个战场随从各可卖出 -> 7
        // 总合法动作必须严格为 7，全部为 Sell
        Assert.Equal(7, actions.Count);
        Assert.All(actions, a => Assert.Equal(ActionType.Sell, a.ActionType));
    }

    [Fact]
    public void StandardTurn_ExactActionsCount_Verification()
    {
        // Arrange: 典型实战盘面
        // 回合 3, 5 金币, 2 本 (升本需 6 金币), 未冻结
        // 酒馆 3 随从 (费用全为 3)
        // 战场 2 随从
        // 手牌 1 随从 (Cost 0)
        var snapshot = new BoardSnapshot
        {
            Turn = 3,
            TavernTier = 2,
            UpgradeCost = 6,
            IsFrozen = false,
            Gold = new GoldBudget(5, 10, 0),
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_T1", 3, 2, MinionRaceMask.Murloc, false),
                new TavernMinion(302, "BG_T2", 3, 2, MinionRaceMask.Beast, false),
                new TavernMinion(303, "BG_T3", 3, 1, MinionRaceMask.Naga, false)
            ),
            BoardMinions = ImmutableArray.Create(
                new MinionEntity { EntityId = 101, CardId = "BG_B1", ZonePosition = 0 },
                new MinionEntity { EntityId = 102, CardId = "BG_B2", ZonePosition = 1 }
            ),
            HandCards = ImmutableArray.Create(
                new HandEntity(201, "BG_H1", 0, isMinion: true, isSpell: false, 0)
            )
        };

        // Act
        var buffer = new CandidateActionBuffer();
        _generator.Generate(in snapshot, ref buffer);

        // Assert:
        // 1. UpgradeTier: 5 < 6 -> 0
        // 2. Reroll: 5 >= 1 -> 1
        // 3. Freeze: 未冻结且有 3 随从 -> 1
        // 4. Buy: 手牌 1 < 10, 金币 5 >= 3, 3 个在售随从皆买得起 -> 3
        // 5. Sell: 场上 2 随从 -> 2
        // 6. Play: 场上 2 随从 (< 7)，手牌 1 随从 -> positions 0, 1, 2 -> 3
        // 总数 = 0 + 1 + 1 + 3 + 2 + 3 = 10
        Assert.Equal(10, buffer.Count);
        Assert.Equal(10, buffer.AsSpan().Length);

        // 测试 Span 重载
        var destArray = new CandidateAction[16];
        Span<CandidateAction> spanDest = destArray;
        int count = _generator.Generate(snapshot, spanDest);
        Assert.Equal(10, count);

        // 验证动作集无重复槽位
        var buySlots = buffer.Where(a => a.ActionType == ActionType.Buy).Select(a => a.SlotIndex).ToList();
        Assert.Equal(new[] { 0, 1, 2 }, buySlots);

        var sellSlots = buffer.Where(a => a.ActionType == ActionType.Sell).Select(a => a.SlotIndex).ToList();
        Assert.Equal(new[] { 0, 1 }, sellSlots);

        var playPositions = buffer.Where(a => a.ActionType == ActionType.Play).Select(a => a.TargetPosition).ToList();
        Assert.Equal(new[] { 0, 1, 2 }, playPositions);
    }

    #endregion

    #region 性能压测与零 GC 验证 (Zero GC & Extreme Performance)

    [Fact]
    public void Performance_TenThousandRuns_AverageLatencyUnderHalfMillisecond_And_ZeroGC()
    {
        // Arrange: 构造高复杂度代表性盘面
        var boardBuilder = ImmutableArray.CreateBuilder<MinionEntity>();
        for (int i = 0; i < 5; i++)
        {
            boardBuilder.Add(new MinionEntity { EntityId = 100 + i, CardId = $"BG_B_{i}", Attack = 3 + i, Health = 4 + i, ZonePosition = i });
        }

        var handBuilder = ImmutableArray.CreateBuilder<HandEntity>();
        for (int i = 0; i < 3; i++)
        {
            handBuilder.Add(new HandEntity(200 + i, $"BG_H_{i}", 0, isMinion: true, isSpell: false, i));
        }

        var tavernBuilder = ImmutableArray.CreateBuilder<TavernMinion>();
        for (int i = 0; i < 6; i++)
        {
            tavernBuilder.Add(new TavernMinion(300 + i, $"BG_T_{i}", 3, 3, MinionRaceMask.Dragon, false));
        }

        var snapshot = new BoardSnapshot
        {
            Turn = 8,
            TavernTier = 4,
            UpgradeCost = 8,
            IsFrozen = false,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = boardBuilder.ToImmutable(),
            HandCards = handBuilder.ToImmutable(),
            TavernMinions = tavernBuilder.ToImmutable()
        };

        const int iterations = 10_000;
        var generator = CandidateActionGenerator.Shared;
        var buffer = new CandidateActionBuffer();

        // 预热 (Warm-up) JIT 编译
        for (int i = 0; i < 500; i++)
        {
            generator.Generate(in snapshot, ref buffer);
        }

        // 强制回收，确保基准内存纯净
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startTimestamp = Stopwatch.GetTimestamp();

        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        int checksum = 0;
        for (int i = 0; i < iterations; i++)
        {
            generator.Generate(in snapshot, ref buffer);
            checksum += buffer.Count;
        }

        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long endTimestamp = Stopwatch.GetTimestamp();
        int gen0After = GC.CollectionCount(0);
        int gen1After = GC.CollectionCount(1);
        int gen2After = GC.CollectionCount(2);

        // 校验计算有效性 (防止 JIT 优化循环)
        Assert.True(checksum > 0, "Checksum must be positive.");

        // 时延断言：单次生成耗时严格 <= 0.5 ms (10,000 次总耗时应远小于 5000 ms，通常 < 50 ms)
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);
        double totalMilliseconds = elapsed.TotalMilliseconds;
        double averageLatencyMs = totalMilliseconds / iterations;

        Assert.True(averageLatencyMs <= 0.5,
            $"Average latency {averageLatencyMs:F4} ms exceeded limit 0.5 ms (Total: {totalMilliseconds:F2} ms for {iterations} iterations)");

        // 零 GC 断言：使用栈上缓冲区循环 10,000 次，当前线程分配托管堆字节增量为 0，GC 触发次数为 0
        long allocatedBytes = allocatedAfter - allocatedBefore;
        Assert.Equal(0, allocatedBytes);
        Assert.Equal(gen0Before, gen0After);
        Assert.Equal(gen1Before, gen1After);
        Assert.Equal(gen2Before, gen2After);
    }

    #endregion

    #region 缓冲区与结构体工具类单元测试 (Buffer & Helpers)

    [Fact]
    public void CandidateActionBuffer_Operations_Clear_Count_ToArray_CopyTo()
    {
        var buffer = new CandidateActionBuffer();
        Assert.True(buffer.IsEmpty);
        Assert.Empty(buffer);
        Assert.Equal(128, buffer.Capacity);

        buffer.Add(CandidateAction.CreateReroll());
        buffer.Add(CandidateAction.CreateFreeze());
        Assert.False(buffer.IsEmpty);
        Assert.Equal(2, buffer.Count);
        Assert.Equal(ActionType.Reroll, buffer[0].ActionType);
        Assert.Equal(ActionType.Freeze, buffer[1].ActionType);

        var array = buffer.ToArray();
        Assert.Equal(2, array.Length);
        Assert.Equal(ActionType.Reroll, array[0].ActionType);
        Assert.Equal(ActionType.Freeze, array[1].ActionType);

        var dest = new CandidateAction[2];
        buffer.CopyTo(dest);
        Assert.Equal(ActionType.Reroll, dest[0].ActionType);
        Assert.Equal(ActionType.Freeze, dest[1].ActionType);

        // 测试索引越界异常
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[2]);

        // 测试 Clear
        buffer.Clear();
        Assert.True(buffer.IsEmpty);
        Assert.Empty(buffer);
    }

    [Fact]
    public void CandidateActionBuffer_ExceedingCapacity_ThrowsInvalidOperationException()
    {
        var buffer = new CandidateActionBuffer();
        for (int i = 0; i < CandidateActionBuffer.MaxCapacity; i++)
        {
            buffer.Add(CandidateAction.CreateReroll());
        }

        Assert.Equal(CandidateActionBuffer.MaxCapacity, buffer.Count);
        Assert.Throws<InvalidOperationException>(() => buffer.Add(CandidateAction.CreateReroll()));
    }

    [Fact]
    public void CandidateActionGenerator_GenerateToSpan_WhenDestinationTooSmall_ThrowsArgumentException()
    {
        var snapshot = new BoardSnapshot
        {
            Gold = new GoldBudget(10, 10, 0),
            TavernMinions = ImmutableArray.Create(
                new TavernMinion(301, "BG_1", 3, 1, MinionRaceMask.All, false)
            )
        };

        // 仅分配 1 个动作空间，但至少会生成 UpgradeTier, Reroll, Freeze, Buy 共 4 个动作
        var tooSmallSpan = new CandidateAction[1];
        Assert.Throws<ArgumentException>(() => _generator.Generate(snapshot, tooSmallSpan));
    }

    [Fact]
    public void CandidateAction_ToString_FormatsCorrectly()
    {
        var upgrade = CandidateAction.CreateUpgradeTier(7);
        var reroll = CandidateAction.CreateReroll();
        var freeze = CandidateAction.CreateFreeze();
        var buy = CandidateAction.CreateBuy(301, 0, 3, "BG20_001");
        var sell = CandidateAction.CreateSell(101, 1, "BG20_002");
        var play = CandidateAction.CreatePlay(201, 2, 3, 0, "BG20_003");

        Assert.Contains("UpgradeTier", upgrade.ToString());
        Assert.Contains("Reroll", reroll.ToString());
        Assert.Contains("Freeze", freeze.ToString());
        Assert.Contains("Buy", buy.ToString());
        Assert.Contains("Sell", sell.ToString());
        Assert.Contains("Play", play.ToString());
    }

    [Fact]
    public void HandBoundary_NineCards_CanBuy_TenCards_CannotBuy()
    {
        // 9 张手牌 -> 仍可购买
        var nineCards = Enumerable.Range(0, 9)
            .Select(i => new HandEntity(200 + i, $"BG_H_{i}", 0, true, false, i))
            .ToImmutableArray();

        var snapshot9 = new BoardSnapshot
        {
            Gold = new GoldBudget(10, 10, 0),
            HandCards = nineCards,
            TavernMinions = ImmutableArray.Create(new TavernMinion(301, "BG_T1", 3, 1, MinionRaceMask.All, false))
        };

        var actions9 = _generator.Generate(snapshot9);
        Assert.Contains(actions9, a => a.ActionType == ActionType.Buy);

        // 10 张手牌 -> 满手牌硬性剪枝，禁止购买
        var tenCards = nineCards.Add(new HandEntity(209, "BG_H_9", 0, true, false, 9));
        var snapshot10 = snapshot9 with { HandCards = tenCards };

        var actions10 = _generator.Generate(snapshot10);
        Assert.DoesNotContain(actions10, a => a.ActionType == ActionType.Buy);
    }

    [Fact]
    public void BoardBoundary_SixMinions_CanPlay_SevenMinions_CannotPlay()
    {
        // 6 随从战场 -> 还可以打出随从至 0..6 (7 个位置)
        var sixBoard = Enumerable.Range(0, 6)
            .Select(i => new MinionEntity { EntityId = 100 + i, CardId = $"BG_B_{i}", ZonePosition = i })
            .ToImmutableArray();

        var snapshot6 = new BoardSnapshot
        {
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = sixBoard,
            HandCards = ImmutableArray.Create(new HandEntity(201, "BG_H1", 0, true, false, 0))
        };

        var actions6 = _generator.Generate(snapshot6);
        var playActions6 = actions6.Where(a => a.ActionType == ActionType.Play).ToList();
        Assert.Equal(7, playActions6.Count); // 0, 1, 2, 3, 4, 5, 6 共 7 个位置

        // 7 随从战场 -> 满场硬性剪枝，禁止打出随从
        var sevenBoard = sixBoard.Add(new MinionEntity { EntityId = 106, CardId = "BG_B_6", ZonePosition = 6 });
        var snapshot7 = snapshot6 with { BoardMinions = sevenBoard };

        var actions7 = _generator.Generate(snapshot7);
        Assert.DoesNotContain(actions7, a => a.ActionType == ActionType.Play);
    }

    #endregion
}
