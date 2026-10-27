using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace TavernOracle.Priors;

/// <summary>
/// Model for pairwise synergistic interactions between specific Battlegrounds cards.
/// </summary>
public sealed class SynergyPair
{
    [JsonPropertyName("cardIdA")]
    public string CardIdA { get; init; } = string.Empty;

    [JsonPropertyName("cardIdB")]
    public string CardIdB { get; init; } = string.Empty;

    [JsonPropertyName("synergyScore")]
    public double SynergyScore { get; init; } = 1.0;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    public override string ToString() => $"{CardIdA} + {CardIdB} => {SynergyScore:F1} ({Reason})";
}

/// <summary>
/// Precomputed immutable matrix for zero-allocation O(1) synergy queries across archetypes,
/// card pairs, and race bitmasks.
/// </summary>
public sealed class SynergyMatrix
{
    public readonly struct ArchCardKey : IEquatable<ArchCardKey>
    {
        public readonly string ArchetypeId;
        public readonly string CardId;

        public ArchCardKey(string archetypeId, string cardId)
        {
            ArchetypeId = archetypeId;
            CardId = cardId;
        }

        public bool Equals(ArchCardKey other) =>
            string.Equals(ArchetypeId, other.ArchetypeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(CardId, other.CardId, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object? obj) => obj is ArchCardKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(ArchetypeId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(CardId));
    }

    // (archetypeId, cardId) -> synergy weight (Zero allocation struct key)
    private readonly IReadOnlyDictionary<ArchCardKey, double> _archetypeCardSynergy;

    // Symmetric pair lookup: OrderCards(cardIdA, cardIdB) -> synergy score
    private readonly IReadOnlyDictionary<string, SynergyPair> _pairSynergies;

    // Fast cardId -> list of pair synergies
    private readonly IReadOnlyDictionary<string, IReadOnlyList<SynergyPair>> _cardPartners;

    // Fast CardId -> set of Archetype IDs
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _cardArchetypeIds;

    // Race mask direct index array (powers of two and combinations up to 2048)
    private readonly IReadOnlyDictionary<MinionRaceMask, IReadOnlyList<ArchetypeDefinition>> _raceToArchetypes;

    public IReadOnlyList<SynergyPair> AllPairs { get; }

    public SynergyMatrix(
        IReadOnlyList<ArchetypeDefinition> archetypes,
        IReadOnlyList<SynergyPair> synergyPairs)
    {
        AllPairs = synergyPairs ?? Array.Empty<SynergyPair>();

        // Build (archetypeId, cardId) -> weight
        var archCardDict = new Dictionary<ArchCardKey, double>();
        var cardArchDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var arch in archetypes)
        {
            foreach (var minion in arch.CoreMinions)
            {
                var key = new ArchCardKey(arch.Id, minion.CardId);
                archCardDict[key] = minion.SynergyWeight;

                if (!cardArchDict.TryGetValue(minion.CardId, out var archList))
                {
                    archList = new List<string>();
                    cardArchDict[minion.CardId] = archList;
                }
                if (!archList.Contains(arch.Id, StringComparer.OrdinalIgnoreCase))
                {
                    archList.Add(arch.Id);
                }
            }
        }
        _archetypeCardSynergy = archCardDict.ToFrozenDictionary();
        _cardArchetypeIds = cardArchDict.ToFrozenDictionary(
            k => k.Key,
            v => (IReadOnlyList<string>)v.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        // Build Pair synergies
        var pairDict = new Dictionary<string, SynergyPair>(StringComparer.OrdinalIgnoreCase);
        var partnerDict = new Dictionary<string, List<SynergyPair>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in AllPairs)
        {
            var pairKey = MakePairKey(pair.CardIdA, pair.CardIdB);
            pairDict[pairKey] = pair;

            AddPartner(partnerDict, pair.CardIdA, pair);
            AddPartner(partnerDict, pair.CardIdB, pair);
        }
        _pairSynergies = pairDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _cardPartners = partnerDict.ToFrozenDictionary(
            k => k.Key,
            v => (IReadOnlyList<SynergyPair>)v.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        // Build Race bitmask fast mapping
        var raceMap = new Dictionary<MinionRaceMask, IReadOnlyList<ArchetypeDefinition>>();
        // Seed single tribe flags
        var tribeFlags = (MinionRaceMask[])Enum.GetValues(typeof(MinionRaceMask));
        foreach (var flag in tribeFlags)
        {
            if (flag == MinionRaceMask.None) continue;
            var matching = archetypes.Where(a => a.PrimaryRace.Intersects(flag) || a.SupportedRaces.Intersects(flag)).ToArray();
            raceMap[flag] = matching;
        }
        _raceToArchetypes = raceMap.ToFrozenDictionary();
    }

    private static void AddPartner(Dictionary<string, List<SynergyPair>> dict, string cardId, SynergyPair pair)
    {
        if (!dict.TryGetValue(cardId, out var list))
        {
            list = new List<SynergyPair>();
            dict[cardId] = list;
        }
        list.Add(pair);
    }

    private static string BuildKey(string archetypeId, string cardId) => $"{archetypeId}::{cardId}";

    private static string MakePairKey(string cardA, string cardB)
    {
        return string.Compare(cardA, cardB, StringComparison.OrdinalIgnoreCase) < 0
            ? $"{cardA}::{cardB}"
            : $"{cardB}::{cardA}";
    }

    /// <summary>
    /// Gets the synergy weight of a card within a specific archetype in O(1).
    /// </summary>
    public double GetSynergyWeight(string archetypeId, string cardId)
    {
        return _archetypeCardSynergy.TryGetValue(new ArchCardKey(archetypeId, cardId), out var weight) ? weight : 0.0;
    }

    /// <summary>
    /// Checks whether a card has explicit synergy with the specified archetype.
    /// </summary>
    public bool HasArchetypeSynergy(string archetypeId, string cardId)
    {
        return _archetypeCardSynergy.ContainsKey(new ArchCardKey(archetypeId, cardId));
    }

    /// <summary>
    /// Gets the pairwise synergy score between two cards in O(1). Returns 1.0 (baseline) if no explicit synergy.
    /// </summary>
    public double GetPairSynergy(string cardIdA, string cardIdB)
    {
        if (string.Equals(cardIdA, cardIdB, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (_cardPartners.TryGetValue(cardIdA, out var partners))
        {
            for (int i = 0; i < partners.Count; i++)
            {
                var pair = partners[i];
                if (string.Equals(pair.CardIdA, cardIdB, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.CardIdB, cardIdB, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.SynergyScore;
                }
            }
        }

        return 1.0;
    }

    /// <summary>
    /// Gets the detailed pair record between two cards if it exists.
    /// </summary>
    public SynergyPair? GetPairDetails(string cardIdA, string cardIdB)
    {
        if (_cardPartners.TryGetValue(cardIdA, out var partners))
        {
            for (int i = 0; i < partners.Count; i++)
            {
                var pair = partners[i];
                if (string.Equals(pair.CardIdA, cardIdB, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pair.CardIdB, cardIdB, StringComparison.OrdinalIgnoreCase))
                {
                    return pair;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns all synergistic partner cards for the given card.
    /// </summary>
    public IReadOnlyList<SynergyPair> GetSynergisticPartners(string cardId)
    {
        return _cardPartners.TryGetValue(cardId, out var list) ? list : Array.Empty<SynergyPair>();
    }

    /// <summary>
    /// Returns archetype IDs containing the minion.
    /// </summary>
    public IReadOnlyList<string> GetArchetypeIdsForCard(string cardId)
    {
        return _cardArchetypeIds.TryGetValue(cardId, out var list) ? list : Array.Empty<string>();
    }

    /// <summary>
    /// Fast O(1) lookup of archetypes supporting or based on the specified minion race mask.
    /// </summary>
    public IReadOnlyList<ArchetypeDefinition> GetArchetypesForRace(MinionRaceMask raceMask)
    {
        if (raceMask == MinionRaceMask.None)
        {
            return Array.Empty<ArchetypeDefinition>();
        }

        if (_raceToArchetypes.TryGetValue(raceMask, out var list))
        {
            return list;
        }

        // For composite masks (e.g. Mech | Beast dual tribe), union matches
        var composite = new HashSet<ArchetypeDefinition>();
        foreach (var kvp in _raceToArchetypes)
        {
            if (raceMask.Intersects(kvp.Key))
            {
                foreach (var arch in kvp.Value)
                {
                    composite.Add(arch);
                }
            }
        }
        return composite.ToArray();
    }
}
