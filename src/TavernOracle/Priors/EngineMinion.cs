using System.Text.Json.Serialization;

namespace TavernOracle.Priors;

/// <summary>
/// Represents a minion within the archetype knowledge graph that serves as an engine,
/// enabler, payoff, or core synergy pillar in Battlegrounds.
/// </summary>
public sealed class EngineMinion
{
    [JsonPropertyName("cardId")]
    public string CardId { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("nameCn")]
    public string NameCn { get; init; } = string.Empty;

    [JsonPropertyName("tier")]
    public int Tier { get; init; } = 1;

    [JsonPropertyName("race")]
    public string Race { get; init; } = string.Empty;

    [JsonIgnore]
    public MinionRaceMask RaceMask { get; init; }

    [JsonPropertyName("isCoreEngine")]
    public bool IsCoreEngine { get; init; }

    [JsonPropertyName("synergyWeight")]
    public double SynergyWeight { get; init; } = 1.0;

    [JsonPropertyName("role")]
    public string Role { get; init; } = "Engine";

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("keyMechanics")]
    public IReadOnlyList<string> KeyMechanics { get; init; } = Array.Empty<string>();

    public override string ToString() => $"[{Tier}★] {NameCn} ({Name}) - {Role} (w={SynergyWeight:F1})";
}
