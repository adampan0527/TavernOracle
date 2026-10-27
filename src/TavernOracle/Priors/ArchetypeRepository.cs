using System.Collections.Frozen;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TavernOracle.Priors;

/// <summary>
/// Root container DTO for deserializing archetypes.json.
/// </summary>
internal sealed class ArchetypeKnowledgeGraphDto
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("archetypes")]
    public List<ArchetypeDefinition> Archetypes { get; init; } = new();

    [JsonPropertyName("synergyPairs")]
    public List<SynergyPair> SynergyPairs { get; init; } = new();
}

/// <summary>
/// Production implementation of <see cref="IArchetypeRepository"/>.
/// Read-only, thread-safe, and designed for sub-0.1ms O(1) query latency.
/// </summary>
public sealed class ArchetypeRepository : IArchetypeRepository
{
    private static readonly Lazy<ArchetypeRepository> _defaultInstance = new(LoadDefault);

    public static ArchetypeRepository Instance => _defaultInstance.Value;

    private readonly IReadOnlyList<ArchetypeDefinition> _archetypes;
    private readonly FrozenDictionary<string, ArchetypeDefinition> _archetypesById;
    private readonly FrozenDictionary<string, EngineMinion> _minionsByCardId;
    private readonly FrozenDictionary<string, ArchetypeDefinition> _primaryArchetypeByCardId;
    private readonly FrozenDictionary<string, IReadOnlyList<ArchetypeDefinition>> _allArchetypesByCardId;
    private readonly SynergyMatrix _synergyMatrix;

    public SynergyMatrix SynergyMatrix => _synergyMatrix;

    private ArchetypeRepository(
        IReadOnlyList<ArchetypeDefinition> archetypes,
        IReadOnlyList<SynergyPair> synergyPairs)
    {
        _archetypes = archetypes;
        _archetypesById = archetypes.ToFrozenDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

        var minionDict = new Dictionary<string, EngineMinion>(StringComparer.OrdinalIgnoreCase);
        var cardToArchetypesMap = new Dictionary<string, List<ArchetypeDefinition>>(StringComparer.OrdinalIgnoreCase);
        var cardToBestArchMap = new Dictionary<string, (ArchetypeDefinition Arch, double Weight)>(StringComparer.OrdinalIgnoreCase);

        foreach (var arch in archetypes)
        {
            foreach (var minion in arch.CoreMinions)
            {
                if (!minionDict.ContainsKey(minion.CardId))
                {
                    minionDict[minion.CardId] = minion;
                }

                if (!cardToArchetypesMap.TryGetValue(minion.CardId, out var archList))
                {
                    archList = new List<ArchetypeDefinition>();
                    cardToArchetypesMap[minion.CardId] = archList;
                }
                archList.Add(arch);

                if (!cardToBestArchMap.TryGetValue(minion.CardId, out var best) || minion.SynergyWeight > best.Weight)
                {
                    cardToBestArchMap[minion.CardId] = (arch, minion.SynergyWeight);
                }
            }
        }

        _minionsByCardId = minionDict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _allArchetypesByCardId = cardToArchetypesMap.ToFrozenDictionary(
            k => k.Key,
            v => (IReadOnlyList<ArchetypeDefinition>)v.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        _primaryArchetypeByCardId = cardToBestArchMap.ToFrozenDictionary(
            k => k.Key,
            v => v.Value.Arch,
            StringComparer.OrdinalIgnoreCase);

        _synergyMatrix = new SynergyMatrix(archetypes, synergyPairs);
    }

    /// <summary>
    /// Loads the repository from an explicit JSON string.
    /// </summary>
    public static ArchetypeRepository FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON content cannot be null or whitespace.", nameof(json));
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        var dto = JsonSerializer.Deserialize<ArchetypeKnowledgeGraphDto>(json, options)
                  ?? throw new InvalidOperationException("Failed to deserialize ArchetypeKnowledgeGraphDto from JSON.");

        // Post-process enums and bitmasks
        var processedArchetypes = new List<ArchetypeDefinition>(dto.Archetypes.Count);
        foreach (var rawArch in dto.Archetypes)
        {
            var primaryMask = MinionRaceMaskExtensions.Parse(rawArch.PrimaryRaceString);
            var supportedMask = MinionRaceMask.None;
            if (rawArch.SupportedRacesStrings != null)
            {
                foreach (var s in rawArch.SupportedRacesStrings)
                {
                    supportedMask |= MinionRaceMaskExtensions.Parse(s);
                }
            }
            if (supportedMask == MinionRaceMask.None)
            {
                supportedMask = primaryMask;
            }

            var processedMinions = new List<EngineMinion>(rawArch.CoreMinions.Count);
            foreach (var rawMinion in rawMinionList(rawArch.CoreMinions))
            {
                var minionMask = MinionRaceMaskExtensions.Parse(rawMinion.Race);
                processedMinions.Add(new EngineMinion
                {
                    CardId = rawMinion.CardId,
                    Name = rawMinion.Name,
                    NameCn = rawMinion.NameCn,
                    Tier = rawMinion.Tier,
                    Race = rawMinion.Race,
                    RaceMask = minionMask,
                    IsCoreEngine = rawMinion.IsCoreEngine,
                    SynergyWeight = rawMinion.SynergyWeight,
                    Role = rawMinion.Role ?? "Engine",
                    Description = rawMinion.Description ?? string.Empty,
                    KeyMechanics = rawMinion.KeyMechanics ?? Array.Empty<string>()
                });
            }

            processedArchetypes.Add(new ArchetypeDefinition
            {
                Id = rawArch.Id,
                Name = rawArch.Name,
                NameCn = rawArch.NameCn,
                PrimaryRaceString = rawArch.PrimaryRaceString,
                PrimaryRace = primaryMask,
                SupportedRacesStrings = rawArch.SupportedRacesStrings ?? Array.Empty<string>(),
                SupportedRaces = supportedMask,
                Description = rawArch.Description ?? string.Empty,
                BaselineTierRequirement = rawArch.BaselineTierRequirement,
                KeyMechanics = rawArch.KeyMechanics ?? Array.Empty<string>(),
                TransitionSignposts = rawArch.TransitionSignposts ?? Array.Empty<string>(),
                CoreMinions = processedMinions
            });
        }

        return new ArchetypeRepository(processedArchetypes, dto.SynergyPairs);

        static IEnumerable<EngineMinion> rawMinionList(IReadOnlyList<EngineMinion>? list) => list ?? Array.Empty<EngineMinion>();
    }

    /// <summary>
    /// Loads the repository from a file on disk.
    /// </summary>
    public static ArchetypeRepository FromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Archetype knowledge graph file not found at: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return FromJson(json);
    }

    /// <summary>
    /// Loads the repository from the embedded assembly resource.
    /// </summary>
    public static ArchetypeRepository FromEmbedded()
    {
        var assembly = typeof(ArchetypeRepository).Assembly;
        const string resourceName = "TavernOracle.Priors.archetypes.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            // Search case-insensitively in manifest resource names
            var matched = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("archetypes.json", StringComparison.OrdinalIgnoreCase));

            if (matched == null)
            {
                throw new InvalidOperationException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
            }

            using var fallbackStream = assembly.GetManifestResourceStream(matched)!;
            using var reader = new StreamReader(fallbackStream);
            return FromJson(reader.ReadToEnd());
        }

        using var streamReader = new StreamReader(stream);
        return FromJson(streamReader.ReadToEnd());
    }

    private static ArchetypeRepository LoadDefault()
    {
        // Try embedded resource first
        try
        {
            return FromEmbedded();
        }
        catch
        {
            // Fallback to local files
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Priors", "archetypes.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "archetypes.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "data", "archetypes.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "TavernOracle", "Priors", "archetypes.json")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    return FromFile(path);
                }
            }

            throw new InvalidOperationException("Could not locate archetypes.json from embedded resource or disk paths.");
        }
    }

    public IReadOnlyList<ArchetypeDefinition> GetAllArchetypes() => _archetypes;

    public ArchetypeDefinition? GetArchetypeById(string archetypeId)
    {
        return _archetypesById.TryGetValue(archetypeId, out var arch) ? arch : null;
    }

    public IReadOnlyList<ArchetypeDefinition> GetArchetypesByRace(MinionRaceMask race)
    {
        return _synergyMatrix.GetArchetypesForRace(race);
    }

    public ArchetypeDefinition? GetPrimaryArchetypeForMinion(string cardId)
    {
        return _primaryArchetypeByCardId.TryGetValue(cardId, out var arch) ? arch : null;
    }

    public IReadOnlyList<ArchetypeDefinition> GetArchetypesForMinion(string cardId)
    {
        return _allArchetypesByCardId.TryGetValue(cardId, out var list) ? list : Array.Empty<ArchetypeDefinition>();
    }

    public double GetSynergyWeight(string archetypeId, string cardId)
    {
        return _synergyMatrix.GetSynergyWeight(archetypeId, cardId);
    }

    public double GetPairSynergy(string cardIdA, string cardIdB)
    {
        return _synergyMatrix.GetPairSynergy(cardIdA, cardIdB);
    }

    public bool IsCoreMinion(string cardId)
    {
        return _minionsByCardId.TryGetValue(cardId, out var minion) && minion.IsCoreEngine;
    }

    public EngineMinion? GetEngineMinion(string cardId)
    {
        return _minionsByCardId.TryGetValue(cardId, out var minion) ? minion : null;
    }

    public IReadOnlyList<EngineMinion> GetAllEngineMinions()
    {
        return _minionsByCardId.Values.ToArray();
    }

    public MinionRaceMask ConvertRaceStringToMask(string race)
    {
        return MinionRaceMaskExtensions.Parse(race);
    }
}
