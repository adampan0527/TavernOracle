using System;
using System.Collections.Immutable;
using System.Text.Json;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Priors;
using TavernOracle.Simulation;
using Xunit;

namespace TavernOracle.Tests.Data;

public class BoardSnapshotTests
{
    [Fact]
    public void AllDtoTypes_AreValueTypes_ZeroGCPromise()
    {
        // 核心 DTO 契约：所有实体与快照必须全部为值类型 (struct / readonly record struct)，杜绝堆内存垃圾
        Assert.True(typeof(MinionEntity).IsValueType, "MinionEntity must be a struct (value type)");
        Assert.True(typeof(HandEntity).IsValueType, "HandEntity must be a struct (value type)");
        Assert.True(typeof(TavernMinion).IsValueType, "TavernMinion must be a struct (value type)");
        Assert.True(typeof(GoldBudget).IsValueType, "GoldBudget must be a struct (value type)");
        Assert.True(typeof(TavernTier).IsValueType, "TavernTier must be a struct (value type)");
        Assert.True(typeof(BoardSnapshot).IsValueType, "BoardSnapshot must be a struct (value type)");
        Assert.True(typeof(CombatSimulationResult).IsValueType, "CombatSimulationResult must be a struct (value type)");
    }

    [Fact]
    public void MinionEntity_KeywordMechanicsBitmask_AccurateHelperProperties()
    {
        // Arrange
        var mechanics = CardMechanicsMask.Taunt |
                        CardMechanicsMask.DivineShield |
                        CardMechanicsMask.Poisonous |
                        CardMechanicsMask.Reborn |
                        CardMechanicsMask.Windfury;

        var minion = new MinionEntity(
            entityId: 101,
            cardId: "BG21_000",
            attack: 7,
            health: 8,
            tier: 4,
            race: MinionRaceMask.Beast,
            mechanics: mechanics,
            isGolden: true,
            zonePosition: 2
        );

        // Assert
        Assert.Equal(101, minion.EntityId);
        Assert.Equal("BG21_000", minion.CardId);
        Assert.Equal(7, minion.Attack);
        Assert.Equal(8, minion.Health);
        Assert.Equal(4, minion.Tier);
        Assert.Equal(MinionRaceMask.Beast, minion.Race);
        Assert.True(minion.IsGolden);
        Assert.Equal(2, minion.ZonePosition);

        // Helper bitmask properties
        Assert.True(minion.Taunt);
        Assert.True(minion.DivineShield);
        Assert.True(minion.Poisonous);
        Assert.False(minion.Venomous);
        Assert.True(minion.HasPoisonOrVenom);
        Assert.True(minion.Reborn);
        Assert.True(minion.Windfury);
        Assert.False(minion.MegaWindfury);
        Assert.False(minion.Magnetic);
        Assert.False(minion.Deathrattle);
        Assert.False(minion.Stealth);
        Assert.False(minion.Spellcraft);
    }

    [Fact]
    public void HandEntity_And_TavernMinion_Properties_AssignCorrectly()
    {
        // HandEntity
        var handMinion = new HandEntity(
            entityId: 201,
            cardId: "BG20_001",
            cost: 0,
            isMinion: true,
            isSpell: false,
            zonePosition: 0
        );

        Assert.Equal(201, handMinion.EntityId);
        Assert.Equal("BG20_001", handMinion.CardId);
        Assert.Equal(0, handMinion.Cost);
        Assert.True(handMinion.IsMinion);
        Assert.False(handMinion.IsSpell);
        Assert.Equal(0, handMinion.ZonePosition);

        // TavernMinion
        var tavernMinion = new TavernMinion(
            entityId: 301,
            cardId: "BG20_002",
            cost: 3,
            tier: 3,
            race: MinionRaceMask.Mech | MinionRaceMask.Demon,
            isFrozen: true
        );

        Assert.Equal(301, tavernMinion.EntityId);
        Assert.Equal("BG20_002", tavernMinion.CardId);
        Assert.Equal(3, tavernMinion.Cost);
        Assert.Equal(3, tavernMinion.Tier);
        Assert.Equal(MinionRaceMask.Mech | MinionRaceMask.Demon, tavernMinion.Race);
        Assert.True(tavernMinion.IsFrozen);
    }

    [Fact]
    public void GoldBudget_Calculations_NetSpendableAndCanAfford()
    {
        var gold = new GoldBudget(available: 8, max: 10, lockedInRoll: 2);

        Assert.Equal(8, gold.Available);
        Assert.Equal(10, gold.Max);
        Assert.Equal(2, gold.LockedInRoll);
        Assert.Equal(6, gold.NetSpendable);
        Assert.True(gold.CanAfford(3));
        Assert.True(gold.CanAfford(8));
        Assert.False(gold.CanAfford(9));
        Assert.True(gold.CanAffordRoll);
        Assert.True(gold.CanAffordMinion);

        var brokeGold = new GoldBudget(0, 10, 0);
        Assert.False(brokeGold.CanAffordRoll);
        Assert.False(brokeGold.CanAffordMinion);
        Assert.Equal(0, brokeGold.NetSpendable);
    }

    [Fact]
    public void TavernTier_ImplicitConversionAndComparison_WorksSeamlessly()
    {
        TavernTier tier3 = 3;
        int tierInt = tier3;

        Assert.Equal(3, tierInt);
        Assert.Equal(3, tier3.Value);
        Assert.True(tier3 == 3);
        Assert.True(tier3 < 5);
        Assert.Equal("Tier 3", tier3.ToString());
    }

    [Fact]
    public void BoardSnapshot_DefaultConstructor_NoNullReferences()
    {
        // 验证默认构造快照具有完备的非空安全保证
        var snapshot = new BoardSnapshot();

        Assert.Equal(1, snapshot.Turn);
        Assert.NotNull(snapshot.HeroCardId);
        Assert.Equal(string.Empty, snapshot.HeroCardId);
        Assert.Equal(30, snapshot.Health);
        Assert.Equal(0, snapshot.Armor);
        Assert.Equal(30, snapshot.TotalHealth);
        Assert.Equal(1, snapshot.TavernTier);
        Assert.Equal(5, snapshot.UpgradeCost);
        Assert.False(snapshot.IsFrozen);
        Assert.Equal(MinionRaceMask.None, snapshot.BannedRaces);
        Assert.Equal(3, snapshot.Gold.Available);
        Assert.True(snapshot.Simulation.IsDegraded);

        // 集合绝不为 null，且不会触发 Default ImmutableArray 异常
        Assert.False(snapshot.BoardMinions.IsDefault);
        Assert.Empty(snapshot.BoardMinions);
        Assert.False(snapshot.HandCards.IsDefault);
        Assert.Empty(snapshot.HandCards);
        Assert.False(snapshot.TavernMinions.IsDefault);
        Assert.Empty(snapshot.TavernMinions);

        // 别名访问
        Assert.Empty(snapshot.Board);
        Assert.Empty(snapshot.Hand);
        Assert.Empty(snapshot.Tavern);
        Assert.False(snapshot.IsBoardFull);
        Assert.False(snapshot.IsHandFull);
    }

    [Fact]
    public void BoardSnapshot_ImmutabilityAndDeepCopyIndependence_Verified()
    {
        // Arrange
        var board = ImmutableArray.Create(
            new MinionEntity(1, "BG21_000", 2, 2, 1, MinionRaceMask.Beast, CardMechanicsMask.None, false, 0),
            new MinionEntity(2, "BG21_001", 3, 4, 2, MinionRaceMask.Mech, CardMechanicsMask.Taunt, false, 1)
        );
        var hand = ImmutableArray.Create(
            new HandEntity(10, "BG_SPELL_01", 1, false, true, 0)
        );
        var tavern = ImmutableArray.Create(
            new TavernMinion(20, "BG21_002", 3, 2, MinionRaceMask.Murloc, false)
        );

        var original = new BoardSnapshot(
            turn: 5,
            heroCardId: "BG_HERO_101",
            health: 28,
            armor: 4,
            tavernTier: 3,
            upgradeCost: 6,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Demon | MinionRaceMask.Dragon,
            gold: new GoldBudget(7, 10, 1),
            simulation: new CombatSimulationResult(0.65, 0.10, 0.25, 0.05),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );

        // Act: 衍生新快照并执行深拷贝
        var derived = original with
        {
            Turn = 6,
            Health = 22,
            Armor = 0,
            Gold = new GoldBudget(10, 10, 0),
            BoardMinions = original.BoardMinions.Add(
                new MinionEntity(3, "BG21_003", 5, 5, 3, MinionRaceMask.Elemental, CardMechanicsMask.DivineShield, false, 2)
            ),
            HandCards = original.HandCards.Clear(),
            TavernMinions = ImmutableArray<TavernMinion>.Empty,
            IsFrozen = true
        };

        var deepCopyOfDerived = derived.DeepCopy();

        // Assert: 原始快照 100% 深度隔离且不可变
        Assert.Equal(5, original.Turn);
        Assert.Equal(28, original.Health);
        Assert.Equal(4, original.Armor);
        Assert.Equal(32, original.TotalHealth);
        Assert.Equal(7, original.Gold.Available);
        Assert.Equal(1, original.Gold.LockedInRoll);
        Assert.False(original.IsFrozen);
        Assert.Equal(2, original.BoardMinions.Length);
        Assert.Single(original.HandCards);
        Assert.Single(original.TavernMinions);

        // 衍生快照值验证
        Assert.Equal(6, derived.Turn);
        Assert.Equal(22, derived.Health);
        Assert.Equal(0, derived.Armor);
        Assert.Equal(22, derived.TotalHealth);
        Assert.Equal(10, derived.Gold.Available);
        Assert.Equal(0, derived.Gold.LockedInRoll);
        Assert.True(derived.IsFrozen);
        Assert.Equal(3, derived.BoardMinions.Length);
        Assert.Empty(derived.HandCards);
        Assert.Empty(derived.TavernMinions);

        // 深拷贝副本与衍生快照完全一致
        Assert.Equal(derived, deepCopyOfDerived);
    }

    [Fact]
    public void BoardSnapshot_JsonRoundTripSerialization_PreservesAllFields()
    {
        // Arrange: 构造完整丰富的快照样本
        var board = ImmutableArray.Create(
            new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Taunt | CardMechanicsMask.Reborn, false, 0),
            new MinionEntity(2, "BG21_001_G", 8, 10, 4, MinionRaceMask.Mech | MinionRaceMask.All, CardMechanicsMask.DivineShield | CardMechanicsMask.Windfury, true, 1)
        );

        var hand = ImmutableArray.Create(
            new HandEntity(10, "BG_SPELL_01", 2, false, true, 0),
            new HandEntity(11, "BG21_005", 0, true, false, 1)
        );

        var tavern = ImmutableArray.Create(
            new TavernMinion(20, "BG21_010", 3, 3, MinionRaceMask.Murloc, false),
            new TavernMinion(21, "BG21_011", 3, 4, MinionRaceMask.Pirate, true)
        );

        var original = new BoardSnapshot(
            turn: 7,
            heroCardId: "BG_HERO_102",
            health: 24,
            armor: 5,
            tavernTier: 4,
            upgradeCost: 7,
            isFrozen: true,
            bannedRaces: MinionRaceMask.Undead | MinionRaceMask.Naga,
            gold: new GoldBudget(9, 10, 0),
            simulation: new CombatSimulationResult(
                winRate: 0.72,
                tieRate: 0.08,
                lossRate: 0.20,
                lethalRate: 0.03,
                averageDamageTaken: -4.5,
                simulationsCount: 5000,
                isDegraded: false,
                statusMessage: "Accurate simulation"
            ),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );

        // Act: 序列化为 JSON 并反序列化还原
        string json = original.ToJson();
        var restored = BoardSnapshot.FromJson(json);

        // Assert: 基础字段完全一致
        Assert.Equal(original.Turn, restored.Turn);
        Assert.Equal(original.HeroCardId, restored.HeroCardId);
        Assert.Equal(original.Health, restored.Health);
        Assert.Equal(original.Armor, restored.Armor);
        Assert.Equal(original.TotalHealth, restored.TotalHealth);
        Assert.Equal(original.TavernTier, restored.TavernTier);
        Assert.Equal(original.UpgradeCost, restored.UpgradeCost);
        Assert.Equal(original.IsFrozen, restored.IsFrozen);
        Assert.Equal(original.BannedRaces, restored.BannedRaces);

        // 经济与仿真结果一致
        Assert.Equal(original.Gold.Available, restored.Gold.Available);
        Assert.Equal(original.Gold.Max, restored.Gold.Max);
        Assert.Equal(original.Gold.LockedInRoll, restored.Gold.LockedInRoll);
        Assert.Equal(original.Simulation.WinRate, restored.Simulation.WinRate, precision: 4);
        Assert.Equal(original.Simulation.TieRate, restored.Simulation.TieRate, precision: 4);
        Assert.Equal(original.Simulation.LossRate, restored.Simulation.LossRate, precision: 4);
        Assert.Equal(original.Simulation.LethalRate, restored.Simulation.LethalRate, precision: 4);
        Assert.Equal(original.Simulation.PLethal, restored.Simulation.PLethal, precision: 4);
        Assert.Equal(original.Simulation.AverageDamageTaken, restored.Simulation.AverageDamageTaken, precision: 4);
        Assert.Equal(original.Simulation.SimulationsCount, restored.Simulation.SimulationsCount);
        Assert.Equal(original.Simulation.IsDegraded, restored.Simulation.IsDegraded);

        // 集合一致性
        Assert.Equal(original.BoardMinions.Length, restored.BoardMinions.Length);
        for (int i = 0; i < original.BoardMinions.Length; i++)
        {
            Assert.Equal(original.BoardMinions[i], restored.BoardMinions[i]);
        }

        Assert.Equal(original.HandCards.Length, restored.HandCards.Length);
        for (int i = 0; i < original.HandCards.Length; i++)
        {
            Assert.Equal(original.HandCards[i], restored.HandCards[i]);
        }

        Assert.Equal(original.TavernMinions.Length, restored.TavernMinions.Length);
        for (int i = 0; i < original.TavernMinions.Length; i++)
        {
            Assert.Equal(original.TavernMinions[i], restored.TavernMinions[i]);
        }
    }

    [Fact]
    public void Performance_ConstructAndDeepCopy10000Snapshots_ZeroGen0Collections()
    {
        // Arrange: 预先创建不可变切片数据，在测试循环外部完成准备
        var board = ImmutableArray.Create(
            new MinionEntity(1, "BG21_000", 3, 3, 2, MinionRaceMask.Beast, CardMechanicsMask.Taunt, false, 0),
            new MinionEntity(2, "BG21_001", 5, 4, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, 1)
        );
        var hand = ImmutableArray.Create(
            new HandEntity(10, "BG_SPELL_01", 2, false, true, 0)
        );
        var tavern = ImmutableArray.Create(
            new TavernMinion(20, "BG21_002", 3, 2, MinionRaceMask.Murloc, false)
        );

        var baseSnapshot = new BoardSnapshot(
            turn: 5,
            heroCardId: "BG_HERO_01",
            health: 30,
            armor: 5,
            tavernTier: 3,
            upgradeCost: 6,
            isFrozen: false,
            bannedRaces: MinionRaceMask.Dragon,
            gold: new GoldBudget(10, 10, 0),
            simulation: new CombatSimulationResult(0.70, 0.10, 0.20, 0.02),
            boardMinions: board,
            handCards: hand,
            tavernMinions: tavern
        );

        // JIT 预热与垃圾回收就绪
        for (int w = 0; w < 50; w++)
        {
            var warmup = baseSnapshot with { Turn = w };
            _ = warmup.DeepCopy();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long initialGen0 = GC.CollectionCount(0);
        long initialBytes = GC.GetAllocatedBytesForCurrentThread();

        // Act: 循环构造新快照并执行深拷贝 10,000 次
        BoardSnapshot sink = baseSnapshot;
        for (int i = 0; i < 10000; i++)
        {
            // 构造新快照 (使用值传递，0 堆分配)
            var constructed = new BoardSnapshot(
                turn: i,
                heroCardId: "BG_HERO_01",
                health: 30 - (i % 15),
                armor: 5,
                tavernTier: 3,
                upgradeCost: 6,
                isFrozen: (i & 1) == 0,
                bannedRaces: MinionRaceMask.Dragon,
                gold: new GoldBudget(10 - (i % 5), 10, 0),
                simulation: baseSnapshot.Simulation,
                boardMinions: board,
                handCards: hand,
                tavernMinions: tavern
            );

            // 衍生与深拷贝
            var derived = constructed with
            {
                Health = constructed.Health + 1,
                Turn = constructed.Turn + 1
            };

            var copy = derived.DeepCopy();
            sink = copy;
        }

        long gen0Diff = GC.CollectionCount(0) - initialGen0;
        long bytesAllocated = GC.GetAllocatedBytesForCurrentThread() - initialBytes;

        // Assert: 验证 10,000 次操作完全在栈上执行，Gen0 回收增量为 0，且堆分配为 0 字节
        Assert.Equal(10000, sink.Turn);
        Assert.Equal(0, gen0Diff);
        Assert.Equal(0, bytesAllocated);
    }

    [Fact]
    public void FluentDerivations_WorkCorrectly()
    {
        var snapshot = new BoardSnapshot();

        var updated = snapshot
            .WithTurn(8)
            .WithHero("BG22_HERO_999", 20, 5)
            .WithTavernTier(5, 9)
            .WithGold(8, 10, 1)
            .WithFrozen(true)
            .WithBannedRaces(MinionRaceMask.Murloc);

        Assert.Equal(8, updated.Turn);
        Assert.Equal("BG22_HERO_999", updated.HeroCardId);
        Assert.Equal(20, updated.Health);
        Assert.Equal(5, updated.Armor);
        Assert.Equal(25, updated.TotalHealth);
        Assert.Equal(5, updated.TavernTier);
        Assert.Equal(9, updated.UpgradeCost);
        Assert.Equal(8, updated.Gold.Available);
        Assert.Equal(1, updated.Gold.LockedInRoll);
        Assert.True(updated.IsFrozen);
        Assert.Equal(MinionRaceMask.Murloc, updated.BannedRaces);
    }
}
