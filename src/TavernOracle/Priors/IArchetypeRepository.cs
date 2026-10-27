namespace TavernOracle.Priors;

/// <summary>
/// Service interface for querying archetype definitions, minion role categorizations,
/// pairwise synergies, and tribe bitmask intersections in sub-millisecond O(1) time.
/// </summary>
public interface IArchetypeRepository
{
    /// <summary>
    /// Gets all registered endgame composition archetypes.
    /// </summary>
    IReadOnlyList<ArchetypeDefinition> GetAllArchetypes();

    /// <summary>
    /// Retrieves an archetype definition by its unique identifier (e.g. "Beasts_Leapfrogger").
    /// </summary>
    ArchetypeDefinition? GetArchetypeById(string archetypeId);

    /// <summary>
    /// Performs an O(1) bitmask query to retrieve all archetypes matching or supporting a race tribe.
    /// </summary>
    IReadOnlyList<ArchetypeDefinition> GetArchetypesByRace(MinionRaceMask race);

    /// <summary>
    /// Returns the primary/highest-weight archetype associated with the given minion Card ID.
    /// </summary>
    ArchetypeDefinition? GetPrimaryArchetypeForMinion(string cardId);

    /// <summary>
    /// Returns all archetypes that include the specified minion.
    /// </summary>
    IReadOnlyList<ArchetypeDefinition> GetArchetypesForMinion(string cardId);

    /// <summary>
    /// Returns the synergy weight of a minion within a given archetype (defaults to 0.0 if not affiliated).
    /// </summary>
    double GetSynergyWeight(string archetypeId, string cardId);

    /// <summary>
    /// Returns the pairwise synergy coefficient between two cards (defaults to 1.0 baseline).
    /// </summary>
    double GetPairSynergy(string cardIdA, string cardIdB);

    /// <summary>
    /// Checks whether a minion is marked as a core engine card for any archetype.
    /// </summary>
    bool IsCoreMinion(string cardId);

    /// <summary>
    /// Retrieves the EngineMinion definition for a card ID.
    /// </summary>
    EngineMinion? GetEngineMinion(string cardId);

    /// <summary>
    /// Retrieves all registered engine minions across all archetypes.
    /// </summary>
    IReadOnlyList<EngineMinion> GetAllEngineMinions();

    /// <summary>
    /// Converts a race string or alias into its corresponding <see cref="MinionRaceMask"/>.
    /// </summary>
    MinionRaceMask ConvertRaceStringToMask(string race);

    /// <summary>
    /// Accesses the underlying precomputed synergy matrix.
    /// </summary>
    SynergyMatrix SynergyMatrix { get; }
}
