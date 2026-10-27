using System.Text.Json.Serialization;

namespace TavernOracle.Priors;

/// <summary>
/// Defines an endgame composition / strategy archetype in Hearthstone Battlegrounds.
/// Contains core engine minion dependencies, key mechanics, and tribe affiliation bitmasks.
/// </summary>
public sealed class ArchetypeDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("nameCn")]
    public string NameCn { get; init; } = string.Empty;

    [JsonPropertyName("primaryRace")]
    public string PrimaryRaceString { get; init; } = string.Empty;

    [JsonIgnore]
    public MinionRaceMask PrimaryRace { get; init; }

    [JsonPropertyName("supportedRaces")]
    public IReadOnlyList<string> SupportedRacesStrings { get; init; } = Array.Empty<string>();

    [JsonIgnore]
    public MinionRaceMask SupportedRaces { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("baselineTierRequirement")]
    public int BaselineTierRequirement { get; init; } = 3;

    [JsonPropertyName("keyMechanics")]
    public IReadOnlyList<string> KeyMechanics { get; init; } = Array.Empty<string>();

    [JsonPropertyName("transitionSignposts")]
    public IReadOnlyList<string> TransitionSignposts { get; init; } = Array.Empty<string>();

    [JsonPropertyName("coreMinions")]
    public IReadOnlyList<EngineMinion> CoreMinions { get; init; } = Array.Empty<EngineMinion>();

    public override string ToString() => $"{NameCn} ({Name}) [{Id}]";
}
