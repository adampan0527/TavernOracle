namespace TavernOracle.Simulation;

/// <summary>
/// Bob's Buddy 战力模拟桥接适配器。
/// 负责桥接 HDT 内置蒙特卡洛战力模拟机制，向决策引擎与 HUD 实时派发胜率、平局率、败率及暴毙概率 (P_lethal)。
/// 具备自动优雅降级机制 (脱机或无数据时回退至默认安全先验值 P_lethal = 0.10)。
/// </summary>
public sealed class BobsBuddyBridge : IBobsBuddyBridge, IDisposable
{
    private readonly object _syncLock = new();

    private BobsBuddySimulationState _currentState = BobsBuddySimulationState.Initial;
    private CombatSimulationResult _latestResult = CombatSimulationResult.CreateDefaultSafePrior("Initial unsimulated state");
    private bool _hasReceivedValidSimulation;
    private bool _isOfflineMode;
    private IBobsBuddyInvokerAdapter? _invokerAdapter;
    private bool _disposed;

    /// <inheritdoc />
    public BobsBuddySimulationState CurrentState
    {
        get
        {
            lock (_syncLock)
            {
                return _currentState;
            }
        }
        private set
        {
            Action<BobsBuddySimulationState>? handler = null;
            lock (_syncLock)
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    handler = StateChanged;
                }
            }
            handler?.Invoke(value);
        }
    }

    /// <inheritdoc />
    public CombatSimulationResult LatestResult
    {
        get
        {
            lock (_syncLock)
            {
                // 脱机模式或无有效模拟数据时，始终优雅降级返回默认安全先验
                if (_isOfflineMode || !_hasReceivedValidSimulation)
                {
                    return CombatSimulationResult.CreateDefaultSafePrior(_isOfflineMode ? "Forced Offline Mode" : "No live simulation data");
                }
                return _latestResult;
            }
        }
        private set
        {
            Action<CombatSimulationResult>? handler = null;
            lock (_syncLock)
            {
                _latestResult = value;
                _hasReceivedValidSimulation = !value.IsDegraded;
                handler = SimulationCompleted;
            }
            handler?.Invoke(value);
        }
    }

    /// <inheritdoc />
    public bool IsOfflineMode
    {
        get
        {
            lock (_syncLock)
            {
                return _isOfflineMode;
            }
        }
        set
        {
            lock (_syncLock)
            {
                _isOfflineMode = value;
            }
        }
    }

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            lock (_syncLock)
            {
                if (_isOfflineMode || _disposed) return false;
                return (_invokerAdapter != null && _invokerAdapter.IsActive) || _hasReceivedValidSimulation;
            }
        }
    }

    /// <inheritdoc />
    public event Action<CombatSimulationResult>? SimulationCompleted;

    /// <inheritdoc />
    public event Action<BobsBuddySimulationState>? StateChanged;

    /// <summary>
    /// 初始化 Bob's Buddy 桥接适配器
    /// </summary>
    /// <param name="invokerAdapter">可选的底层 Invoker 适配实现</param>
    /// <param name="isOfflineMode">是否直接初始化为脱机模式</param>
    public BobsBuddyBridge(IBobsBuddyInvokerAdapter? invokerAdapter = null, bool isOfflineMode = false)
    {
        _isOfflineMode = isOfflineMode;
        if (invokerAdapter != null)
        {
            SetAdapterInternal(invokerAdapter);
        }
    }

    /// <inheritdoc />
    public void ReportSimulationResult(CombatSimulationResult result)
    {
        if (_disposed) return;
        LatestResult = result;
    }

    /// <inheritdoc />
    public void ReportSimulationOutput(
        double winRate,
        double tieRate,
        double lossRate,
        double lethalRate,
        double avgDamageTaken = 0.0,
        int simulationsCount = 0)
    {
        if (_disposed) return;

        var result = new CombatSimulationResult(
            winRate: winRate,
            tieRate: tieRate,
            lossRate: lossRate,
            lethalRate: lethalRate,
            averageDamageTaken: avgDamageTaken,
            simulationsCount: simulationsCount,
            isDegraded: false,
            statusMessage: "Reported via ReportSimulationOutput"
        );

        ReportSimulationResult(result);
    }

    /// <inheritdoc />
    public void UpdateState(BobsBuddySimulationState newState)
    {
        if (_disposed) return;
        CurrentState = newState;
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_syncLock)
        {
            _hasReceivedValidSimulation = false;
            _latestResult = CombatSimulationResult.CreateDefaultSafePrior("Reset to default safe prior");
        }
        CurrentState = BobsBuddySimulationState.Initial;
    }

    /// <inheritdoc />
    public CombatSimulationResult GetEffectiveResult()
    {
        return LatestResult;
    }

    /// <inheritdoc />
    public void AttachHdtInvoker(object? invoker)
    {
        if (_disposed) return;

        if (invoker == null)
        {
            DetachAdapterInternal();
            return;
        }

        var adapter = new ReflectionBobsBuddyInvokerAdapter(invoker);
        SetAdapterInternal(adapter);
    }

    private void SetAdapterInternal(IBobsBuddyInvokerAdapter adapter)
    {
        lock (_syncLock)
        {
            DetachAdapterInternal();
            _invokerAdapter = adapter;
            _invokerAdapter.StateChanged += OnAdapterStateChanged;
            _invokerAdapter.OutputUpdated += OnAdapterOutputUpdated;

            if (_invokerAdapter.TryPollLatestData(out var result, out var state))
            {
                _currentState = state;
                _latestResult = result;
                _hasReceivedValidSimulation = true;
            }
            else
            {
                _currentState = _invokerAdapter.State;
            }
        }
    }

    private void DetachAdapterInternal()
    {
        if (_invokerAdapter != null)
        {
            _invokerAdapter.StateChanged -= OnAdapterStateChanged;
            _invokerAdapter.OutputUpdated -= OnAdapterOutputUpdated;
            _invokerAdapter.Dispose();
            _invokerAdapter = null;
        }
    }

    private void OnAdapterStateChanged(BobsBuddySimulationState newState)
    {
        UpdateState(newState);
    }

    private void OnAdapterOutputUpdated(CombatSimulationResult result)
    {
        ReportSimulationResult(result);
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_disposed) return;
            _disposed = true;
            DetachAdapterInternal();
            SimulationCompleted = null;
            StateChanged = null;
        }
    }
}
