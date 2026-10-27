namespace TavernOracle.Decision.ActionGeneration;

using System;
using TavernOracle.Data;
using TavernOracle.Data.State;
using TavernOracle.Priors;

/// <summary>Conservative, explicit effect rules. Unsupported card text is never inferred as a rule.</summary>
public static class HandActionPlanner
{
    public static bool ShouldHold(in BoardSnapshot snapshot, in HandEntity card)
    {
        if (!card.IsMinion || snapshot.HandCards.Length >= 10 || snapshot.BoardMinions.Length < 4 ||
            snapshot.Health <= 10 || snapshot.Simulation.LethalRate > .30) return false;
        // Bream Counter grows in hand. Preserve a single copy while Murlocs can feed it.
        bool bream = card.CardId == "BG26_137" || card.CardId == "BG26_137_G";
        bool hasMurloc = false;
        bool handPayoff = false;
        foreach (var board in snapshot.BoardMinions)
        {
            hasMurloc |= (board.Race & MinionRaceMask.Murloc) != 0;
            handPayoff |= board.CardId == "BG26_354" || board.CardId == "BG26_354_G" ||
                          board.CardId == "BG26_360" || board.CardId == "BG26_360_G";
        }
        if (!(bream && hasMurloc) && !handPayoff) return false;
        // Keep the largest receiver; other minions remain available to develop the board.
        foreach (var other in snapshot.HandCards)
            if (other.IsMinion && other.EntityId != card.EntityId &&
                (other.Attack + other.Health > card.Attack + card.Health ||
                 (other.Attack + other.Health == card.Attack + card.Health && other.EntityId < card.EntityId))) return false;
        return true;
    }

    public static CandidateAction Plan(in BoardSnapshot snapshot, in HandEntity card, int handIndex)
    {
        var mechanics = card.Mechanics;
        int attack = card.Attack, health = card.Health;
        if (HearthDbAdapter.Instance.TryGetCard(card.CardId, out var meta))
        {
            if (!card.HasLiveStats) { attack = meta.BaseAttack; health = meta.BaseHealth; }
            mechanics |= meta.Mechanics;
        }
        // Empty early boards need independent attackers; full boards can still magnetize.
        if ((mechanics & CardMechanicsMask.Magnetic) != 0 && snapshot.BoardMinions.Length >= 3)
        {
            int target = -1;
            double best = double.NegativeInfinity;
            for (int i = 0; i < snapshot.BoardMinions.Length; i++)
            {
                var board = snapshot.BoardMinions[i];
                if ((board.Race & MinionRaceMask.Mech) == 0) continue;
                double value = board.Attack + board.Health + (board.DivineShield ? attack : 0) +
                    (board.Cleave ? attack * 2 : 0) + (board.Windfury ? attack : 0);
                if (value > best) { best = value; target = i; }
            }
            if (target >= 0)
                return CandidateAction.CreatePlay(card.EntityId, handIndex, target, card.Cost, card.CardId)
                    with { ActionType = ActionType.Magnetize, TargetEntityId = snapshot.BoardMinions[target].EntityId };
        }
        // Approximate combat order: stronger attackers first, fragile supports last.
        double opener = attack * ((mechanics & CardMechanicsMask.Cleave) != 0 ? 3 : 1) +
                         ((mechanics & CardMechanicsMask.DivineShield) != 0 ? 3 : 0);
        int position = 0;
        foreach (var board in snapshot.BoardMinions)
        {
            double other = board.Attack * (board.Cleave ? 3 : 1) + (board.DivineShield ? 3 : 0);
            if (other >= opener) position++;
        }
        return CandidateAction.CreatePlay(card.EntityId, handIndex, position, card.Cost, card.CardId);
    }
}
