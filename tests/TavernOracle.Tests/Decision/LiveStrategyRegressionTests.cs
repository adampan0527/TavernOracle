namespace TavernOracle.Tests.Decision;

using System.Collections.Immutable;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Priors;
using Xunit;

public class LiveStrategyRegressionTests
{
    private static BoardSnapshot Empty(int gold = 0) => new BoardSnapshot().WithGold(gold).WithTavernTier(2, int.MaxValue);
    private static MinionEntity Mech(int id) => new(id, "BGS_071", 10, 10, 3, MinionRaceMask.Mech, CardMechanicsMask.DivineShield, false, id - 1);

    [Fact]
    public void SpareGold_ProducesOneRefreshThenWaitsForNewShop()
    {
        var result = KnapsackSolver.Instance.Solve(Empty(2));
        Assert.Single(result);
        Assert.Equal(ActionType.Reroll, result[0].Type);
        Assert.Empty(KnapsackSolver.Instance.Solve(Empty()));
    }

    [Fact]
    public void Magnetic_CanAttachOnFullBoardWithoutSelling()
    {
        var board = ImmutableArray.Create(Mech(1), Mech(2), Mech(3), Mech(4), Mech(5), Mech(6), Mech(7));
        var card = new HandEntity(20, "MAGNETIC_TEST", 0, true, false, 0)
        { HasLiveStats = true, Attack = 3, Health = 4, Race = MinionRaceMask.Mech, Mechanics = CardMechanicsMask.Magnetic };
        var result = KnapsackSolver.Instance.Solve(Empty().WithBoard(board).WithHand(ImmutableArray.Create(card)));
        Assert.Equal(ActionType.Magnetize, result[0].Type);
        Assert.Contains(board, m => m.EntityId == result[0].TargetEntityId);
        Assert.DoesNotContain(result, a => a.Type == ActionType.Sell);
    }

    [Fact]
    public void HandGrowthCard_IsHeldWhenBoardIsEstablished_ButPlayedToFillEmptyBoard()
    {
        var card = new HandEntity(20, "BG26_137", 0, true, false, 0) { HasLiveStats = true, Attack = 6, Health = 6 };
        var snapshot = Empty().WithBoard(ImmutableArray.Create(Mech(1) with { Race = MinionRaceMask.Murloc }, Mech(2), Mech(3), Mech(4)))
            .WithHand(ImmutableArray.Create(card));
        Assert.DoesNotContain(KnapsackSolver.Instance.Solve(snapshot), a => a.EntityId == 20 && a.Type == ActionType.Play);
        Assert.Contains(KnapsackSolver.Instance.Solve(snapshot.WithBoard(ImmutableArray<MinionEntity>.Empty)), a => a.EntityId == 20 && a.Type == ActionType.Play);
    }

    [Fact]
    public void WeakSupport_IsPlacedBehindStrongAttacker()
    {
        var card = new HandEntity(20, "SUPPORT_TEST", 0, true, false, 0) { HasLiveStats = true, Attack = 1, Health = 2 };
        var result = KnapsackSolver.Instance.Solve(Empty().WithBoard(ImmutableArray.Create(Mech(1))).WithHand(ImmutableArray.Create(card)));
        Assert.Equal(1, result[0].TargetPosition);
    }

    [Fact]
    public void NativeConversion_PreservesBuffedStatsNamesAndSpellType()
    {
        var raw = new TavernOracle.Data.StateMachine.HdtEntityDto(1, "UNKNOWN", 1,
            TavernOracle.Data.StateMachine.HdtZone.Hand, 0, Attack: 31, Health: 42,
            DisplayName: "测试随从", IsMinionCard: true);
        var hand = TavernOracle.Data.StateMachine.HdtEventConverter.ConvertToHandEntity(raw);
        Assert.True(hand.HasLiveStats);
        Assert.Equal(31, hand.Attack);
        Assert.Equal(42, hand.Health);
        Assert.Equal("测试随从", hand.DisplayName);
        var spell = TavernOracle.Data.StateMachine.HdtEventConverter.ConvertToHandEntity(raw with { IsMinionCard = false });
        Assert.False(spell.IsMinion);
        Assert.True(spell.IsSpell);
    }

    [Fact]
    public void AffordableUnsupportedSpell_BlocksBlindRefresh()
    {
        var snapshot = Empty(2).WithTavern(ImmutableArray.Create(
            new TavernMinion(3, "SPELL", 2, 1, MinionRaceMask.None) { IsTavernSpell = true }));
        Assert.DoesNotContain(KnapsackSolver.Instance.Solve(snapshot), a => a.Type == ActionType.Reroll);
    }
}
