using System.ComponentModel;
using System.Reflection;

namespace TavernOracle.Simulation;

/// <summary>
/// 基于反射解耦的 HDT BobsBuddyInvoker 动态适配器。
/// 在不强引用 HDT 实体库的前提下，能够跨版本安全反射读取 HDT 内部状态与模拟输出。
/// </summary>
public sealed class ReflectionBobsBuddyInvokerAdapter : IBobsBuddyInvokerAdapter
{
    private object? _invoker;
    private INotifyPropertyChanged? _notifyPropertyChanged;
    private readonly object _syncLock = new();
    private bool _disposed;

    public bool IsActive => _invoker != null && !_disposed;

    public BobsBuddySimulationState State
    {
        get
        {
            if (_invoker == null) return BobsBuddySimulationState.Initial;
            return ExtractState(_invoker);
        }
    }

    public event Action<BobsBuddySimulationState>? StateChanged;
    public event Action<CombatSimulationResult>? OutputUpdated;

    public ReflectionBobsBuddyInvokerAdapter(object? invoker = null)
    {
        if (invoker != null)
        {
            Attach(invoker);
        }
    }

    public void Attach(object invoker)
    {
        lock (_syncLock)
        {
            Detach();
            _invoker = invoker;

            if (invoker is INotifyPropertyChanged notify)
            {
                _notifyPropertyChanged = notify;
                _notifyPropertyChanged.PropertyChanged += OnPropertyChanged;
            }
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || _invoker == null) return;

        try
        {
            var propertyName = e.PropertyName ?? string.Empty;

            if (propertyName.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                var newState = ExtractState(_invoker);
                StateChanged?.Invoke(newState);
            }
            else if (propertyName.Equals("Output", StringComparison.OrdinalIgnoreCase) ||
                     propertyName.Equals("Simulation", StringComparison.OrdinalIgnoreCase) ||
                     propertyName.Contains("Rate", StringComparison.OrdinalIgnoreCase))
            {
                if (TryPollLatestData(out var result, out _))
                {
                    OutputUpdated?.Invoke(result);
                }
            }
        }
        catch
        {
            // 忽略反射异常，确保 HDT 内部异常不波及插件主进程
        }
    }

    public bool TryPollLatestData(out CombatSimulationResult result, out BobsBuddySimulationState state)
    {
        state = BobsBuddySimulationState.Initial;
        result = default;

        if (_invoker == null || _disposed)
            return false;

        try
        {
            state = ExtractState(_invoker);
            var outputObj = ExtractOutputObject(_invoker);

            if (outputObj == null)
            {
                return false;
            }

            double winRate = ReadDoubleProperty(outputObj, "WinRate", "winRate") ?? 0.0;
            double tieRate = ReadDoubleProperty(outputObj, "TieRate", "tieRate") ?? 0.0;
            double lossRate = ReadDoubleProperty(outputObj, "LossRate", "lossRate") ?? 0.0;

            // 提取暴毙概率：优先读取 myLethalRate (我方死亡率)，其次读取 lethalRate
            double lethalRate = ReadDoubleProperty(outputObj, "MyLethalRate", "myLethalRate", "LethalRate", "lethalRate") ?? 0.0;
            double avgDamage = ReadDoubleProperty(outputObj, "MyAvgDamageTaken", "myAvgDamageTaken", "DamageTaken", "damageTaken", "AverageDamageTaken") ?? 0.0;
            int simCount = ReadIntProperty(outputObj, "Simulations", "simulations", "SimulationsCount", "SimulationCount") ?? 0;

            result = new CombatSimulationResult(
                winRate: winRate,
                tieRate: tieRate,
                lossRate: lossRate,
                lethalRate: lethalRate,
                averageDamageTaken: avgDamage,
                simulationsCount: simCount,
                isDegraded: false,
                statusMessage: "Extracted from HDT BobsBuddy"
            );

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Detach()
    {
        lock (_syncLock)
        {
            if (_notifyPropertyChanged != null)
            {
                _notifyPropertyChanged.PropertyChanged -= OnPropertyChanged;
                _notifyPropertyChanged = null;
            }
            _invoker = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }

    private static BobsBuddySimulationState ExtractState(object target)
    {
        try
        {
            var prop = target.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return BobsBuddySimulationState.Initial;

            var val = prop.GetValue(target);
            if (val == null) return BobsBuddySimulationState.Initial;

            var str = val.ToString() ?? string.Empty;

            if (str.Contains("CombatRunning", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Combat", StringComparison.OrdinalIgnoreCase) ||
                str.Contains("Running", StringComparison.OrdinalIgnoreCase))
            {
                return BobsBuddySimulationState.CombatRunning;
            }

            if (str.Contains("SimulationDone", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                str.Contains("Completed", StringComparison.OrdinalIgnoreCase))
            {
                return BobsBuddySimulationState.SimulationDone;
            }

            if (str.Contains("CombatEnded", StringComparison.OrdinalIgnoreCase) ||
                str.Equals("Ended", StringComparison.OrdinalIgnoreCase) ||
                str.Contains("GameOver", StringComparison.OrdinalIgnoreCase))
            {
                return BobsBuddySimulationState.CombatEnded;
            }

            return BobsBuddySimulationState.Initial;
        }
        catch
        {
            return BobsBuddySimulationState.Initial;
        }
    }

    private static object? ExtractOutputObject(object target)
    {
        var type = target.GetType();
        var outputProp = type.GetProperty("Output", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                         ?? type.GetProperty("Simulation", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                         ?? type.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (outputProp != null)
        {
            var val = outputProp.GetValue(target);
            if (val != null) return val;
        }

        // 如果 target 本身就具备 winRate / lossRate 字段/属性，则返回自身
        var winProp = type.GetProperty("WinRate", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (winProp != null) return target;

        return null;
    }

    private static double? ReadDoubleProperty(object target, params string[] candidateNames)
    {
        var type = target.GetType();
        foreach (var name in candidateNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var val = prop.GetValue(target);
                if (val != null && double.TryParse(val.ToString(), out double d))
                {
                    return d;
                }
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var val = field.GetValue(target);
                if (val != null && double.TryParse(val.ToString(), out double d))
                {
                    return d;
                }
            }
        }
        return null;
    }

    private static int? ReadIntProperty(object target, params string[] candidateNames)
    {
        var type = target.GetType();
        foreach (var name in candidateNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                var val = prop.GetValue(target);
                if (val != null && int.TryParse(val.ToString(), out int i))
                {
                    return i;
                }
            }

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var val = field.GetValue(target);
                if (val != null && int.TryParse(val.ToString(), out int i))
                {
                    return i;
                }
            }
        }
        return null;
    }
}
