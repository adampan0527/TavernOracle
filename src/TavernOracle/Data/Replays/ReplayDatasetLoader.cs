using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TavernOracle.Data.State;

namespace TavernOracle.Data.Replays;

/// <summary>
/// 离线盘面对局回放数据加载服务 (T06)。
/// 支持在完全脱离炉石与 HDT 环境下，从程序集嵌入资源或本地磁盘极速加载盘面快照，
/// 并反序列化为 <see cref="BoardSnapshot"/> 强类型结构体。
/// 具备内存缓存与多路径自动探测回退机制，确保 100% 可用性。
/// </summary>
public sealed class ReplayDatasetLoader : IReplayDatasetLoader
{
    private static readonly Lazy<ReplayDatasetLoader> _defaultInstance = new(() => new ReplayDatasetLoader());

    /// <summary>
    /// 全局单例实例
    /// </summary>
    public static ReplayDatasetLoader Instance => _defaultInstance.Value;

    private readonly string? _customDirectory;
    private readonly Assembly _resourceAssembly;
    private readonly ConcurrentDictionary<string, BoardSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _discoveredNames = new();
    private readonly object _initLock = new();
    private bool _initialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// 构造数据加载服务
    /// </summary>
    /// <param name="customDirectory">可选的自定义数据根目录</param>
    /// <param name="resourceAssembly">用于查找嵌入资源的程序集，默认当前程序集</param>
    public ReplayDatasetLoader(string? customDirectory = null, Assembly? resourceAssembly = null)
    {
        _customDirectory = customDirectory;
        _resourceAssembly = resourceAssembly ?? typeof(ReplayDatasetLoader).Assembly;
    }

    #region 静态便捷入口 (Convenience Static API)

    /// <summary>
    /// 获取所有回放场景名称列表
    /// </summary>
    public static IReadOnlyList<string> GetNames() => Instance.GetReplayNames();

    /// <summary>
    /// 加载指定名称的快照
    /// </summary>
    public static BoardSnapshot Load(string name) => Instance.LoadReplay(name);

    /// <summary>
    /// 批量加载全部快照
    /// </summary>
    public static IReadOnlyDictionary<string, BoardSnapshot> LoadAll() => Instance.LoadAllReplays();

    /// <summary>
    /// 尝试加载快照
    /// </summary>
    public static bool TryLoad(string name, out BoardSnapshot snapshot) => Instance.TryLoadReplay(name, out snapshot);

    #endregion

    #region IReplayDatasetLoader 实现

    /// <inheritdoc />
    public IReadOnlyList<string> GetReplayNames()
    {
        EnsureInitialized();
        lock (_initLock)
        {
            return _discoveredNames.ToArray();
        }
    }

    /// <inheritdoc />
    public BoardSnapshot LoadReplay(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Replay name cannot be null or whitespace.", nameof(name));
        }
        string normalized = NormalizeName(name);

        if (_cache.TryGetValue(normalized, out var cached))
        {
            return cached;
        }

        if (TryLoadReplayInternal(normalized, out var loaded))
        {
            _cache[normalized] = loaded;
            return loaded;
        }

        throw new FileNotFoundException($"Replay snapshot '{name}' could not be located in embedded resources or search directories.");
    }

    /// <inheritdoc />
    public bool TryLoadReplay(string name, out BoardSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            snapshot = default;
            return false;
        }

        string normalized = NormalizeName(name);
        if (_cache.TryGetValue(normalized, out snapshot))
        {
            return true;
        }

        if (TryLoadReplayInternal(normalized, out snapshot))
        {
            _cache[normalized] = snapshot;
            return true;
        }

        snapshot = default;
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, BoardSnapshot> LoadAllReplays()
    {
        EnsureInitialized();
        var result = new Dictionary<string, BoardSnapshot>(StringComparer.OrdinalIgnoreCase);

        List<string> names;
        lock (_initLock)
        {
            names = new List<string>(_discoveredNames);
        }

        foreach (var name in names)
        {
            result[name] = LoadReplay(name);
        }

        return result;
    }

    #endregion

    #region 内部发现与解析加载

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            var nameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. 先加入预定义的 20 个标准场景名称
            foreach (var standardName in ReplayDatasetFactory.AllScenarioNames)
            {
                nameSet.Add(standardName);
            }

            // 2. 扫描程序集中的嵌入资源
            foreach (var resName in _resourceAssembly.GetManifestResourceNames())
            {
                if (resName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    (resName.Contains(".Replays.", StringComparison.OrdinalIgnoreCase) ||
                     resName.Contains("replays", StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = ExtractNameFromResource(resName);
                    if (!string.IsNullOrEmpty(candidate))
                    {
                        nameSet.Add(candidate);
                    }
                }
            }

            // 3. 扫描磁盘候选目录
            foreach (var dir in GetCandidateDirectories())
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                        {
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                            nameSet.Add(fileNameWithoutExt);
                        }
                    }
                    catch
                    {
                        // 忽略无权限或临时不可访问目录
                    }
                }
            }

            _discoveredNames.Clear();
            _discoveredNames.AddRange(nameSet);
            _discoveredNames.Sort(StringComparer.OrdinalIgnoreCase);
            _initialized = true;
        }
    }

    private bool TryLoadReplayInternal(string normalizedName, out BoardSnapshot snapshot)
    {
        // 尝试从嵌入式资源加载
        if (TryLoadFromEmbedded(normalizedName, out snapshot))
        {
            return true;
        }

        // 尝试从磁盘文件候选路径加载
        if (TryLoadFromFileSystem(normalizedName, out snapshot))
        {
            return true;
        }

        // 尝试从代码内置工厂兜底加载
        var allFactory = ReplayDatasetFactory.CreateAll();
        if (allFactory.TryGetValue(normalizedName, out snapshot))
        {
            return true;
        }

        snapshot = default;
        return false;
    }

    private bool TryLoadFromEmbedded(string normalizedName, out BoardSnapshot snapshot)
    {
        string targetEnd = $".{normalizedName}.json";
        var manifestNames = _resourceAssembly.GetManifestResourceNames();

        string? matchingResource = manifestNames.FirstOrDefault(
            n => n.EndsWith(targetEnd, StringComparison.OrdinalIgnoreCase));

        if (matchingResource != null)
        {
            using var stream = _resourceAssembly.GetManifestResourceStream(matchingResource);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string json = reader.ReadToEnd();
                snapshot = BoardSnapshot.FromJson(json, JsonOptions);
                return true;
            }
        }

        snapshot = default;
        return false;
    }

    private bool TryLoadFromFileSystem(string normalizedName, out BoardSnapshot snapshot)
    {
        string fileName = $"{normalizedName}.json";

        foreach (var dir in GetCandidateDirectories())
        {
            string fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
            {
                try
                {
                    string json = File.ReadAllText(fullPath);
                    snapshot = BoardSnapshot.FromJson(json, JsonOptions);
                    return true;
                }
                catch
                {
                    // 若当前文件存在但损坏，继续尝试其他候选目录或报错
                }
            }
        }

        snapshot = default;
        return false;
    }

    private IEnumerable<string> GetCandidateDirectories()
    {
        if (!string.IsNullOrWhiteSpace(_customDirectory))
        {
            yield return _customDirectory;
        }

        string baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "Data", "Replays");
        yield return Path.Combine(baseDir, "Replays");
        yield return Path.Combine(baseDir, "data", "replays");

        // 尝试从当前工作目录查找
        string currentDir = Directory.GetCurrentDirectory();
        yield return Path.Combine(currentDir, "data", "replays");
        yield return Path.Combine(currentDir, "tests", "TavernOracle.Tests", "Replays");
        yield return Path.Combine(currentDir, "src", "TavernOracle", "Data", "Replays");

        // 向上层级回退 (处理 xUnit 测试运行目录 bin/Debug/net8.0-windows)
        DirectoryInfo? cur = new DirectoryInfo(baseDir);
        for (int i = 0; i < 5 && cur != null; i++)
        {
            yield return Path.Combine(cur.FullName, "data", "replays");
            yield return Path.Combine(cur.FullName, "tests", "TavernOracle.Tests", "Replays");
            yield return Path.Combine(cur.FullName, "src", "TavernOracle", "Data", "Replays");
            cur = cur.Parent;
        }
    }

    private static string NormalizeName(string name)
    {
        string trimmed = name.Trim();
        if (trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^5];
        }
        return trimmed;
    }

    private static string ExtractNameFromResource(string resourceName)
    {
        // 资源命名格式如 "TavernOracle.Data.Replays.01_leapfrogger_early_transition.json"
        if (resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            string withoutExt = resourceName[..^5];
            int lastDot = withoutExt.LastIndexOf('.');
            return lastDot >= 0 ? withoutExt[(lastDot + 1)..] : withoutExt;
        }
        return string.Empty;
    }

    #endregion
}
