using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Hearthstone_Deck_Tracker.Plugins;
using TavernOracle.Contracts;
using TavernOracle.Data.State;
using TavernOracle.Data.StateMachine;
using TavernOracle.Decision;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Decision.Knapsack;
using TavernOracle.Decision.Positioning;
using TavernOracle.Presentation.Alignment;
using TavernOracle.Presentation.Controls;
using TavernOracle.Presentation.Settings;
using TavernOracle.Priors;
using PluginSettings = TavernOracle.Presentation.Settings.PluginSettings;

namespace TavernOracle
{
    /// <summary>
    /// Main entry point and lifecycle orchestrator for the TavernOracle HDT Plugin (T20).
    /// Discovered and instantiated by Hearthstone Deck Tracker's PluginManager via reflection.
    /// Assembles the event pipeline, GameStateWatcher state machine, Knapsack DP solver,
    /// heuristic minion position optimizer, and transparent HUD overlays into a unified,
    /// zero-crash, sub-15ms tactical advisor loop.
    /// Strictly complies with Blizzard's "Pencil &amp; Paper Rule" (zero mouse/key input simulation, zero memory injection).
    /// </summary>
    public class TavernOraclePlugin : IPlugin, ITavernOraclePlugin
    {
        private const int MaxBadgePoolCount = 14;

        private readonly object _syncLock = new();
        private bool _isLoaded;
        private DateTime? _lastUpdateTime;
        private ulong _updateTickCount;

        // Core Domain & Decision Services
        private readonly IGameStateWatcher _stateWatcher;
        private readonly Func<object?> _hdtGameProvider;
        private readonly bool _usesLiveHdt;
        private bool _liveGameActive;
        private readonly bool _isDefaultSolver;
        private readonly bool _isDefaultPipeline;
        private IKnapsackSolver _knapsackSolver;
        private readonly IMinionPositionOptimizer _positionOptimizer;
        private IDecisionPipeline _decisionPipeline;
        private readonly IGameWindowTracker _windowTracker;
        private readonly BattlegroundsAnchorCalculator _anchorCalculator;
        private readonly bool _isExplicitSettings;
        private readonly string? _settingsFilePath;
        private PluginSettings _settings;
        private SettingsViewModel? _settingsViewModel;
        private SettingsView? _settingsControl;
        private Window? _settingsWindow;

        // Presentation Layer Elements
        private Canvas? _hostCanvas;
        private Canvas? _containerCanvas;
        private DecisionBannerControl? _bannerControl;
        private LethalGaugeControl? _lethalGauge;
        private readonly List<MinionBadgeControl> _badgeControls = new(MaxBadgePoolCount);

        // Cached Decision States
        private ActionSequenceResult _lastActionResult = ActionSequenceResult.Empty;
        private PositioningResult _lastPositioningResult = PositioningResult.Empty;
        private BoardSnapshot _lastSnapshot = new();

        #region ITavernOraclePlugin Lifecycle Events

        /// <summary>
        /// Event triggered when the plugin completes initialization.
        /// </summary>
        public event Action? Loaded;

        /// <summary>
        /// Event triggered when the plugin is unloaded and torn down.
        /// </summary>
        public event Action? Unloaded;

        /// <summary>
        /// Event triggered on each periodic tick from HDT.
        /// </summary>
        public event Action? Updated;

        /// <summary>
        /// Event triggered when the plugin button is clicked in HDT Options.
        /// </summary>
        public event Action? ButtonPressed;

        #endregion

        #region IPlugin Metadata Properties

        /// <summary>
        /// Display name shown in HDT's plugin list.
        /// </summary>
        public string Name => "TavernOracle";

        /// <summary>
        /// Plugin description summarizing its tactical advisor purpose.
        /// </summary>
        public string Description => "酒馆战棋实时战术参谋长 HUD 辅助系统 (Real-time Tactical Advisor for Hearthstone Battlegrounds)";

        /// <summary>
        /// Text on the action button in HDT's plugin settings list.
        /// </summary>
        public string ButtonText => "设置";

        /// <summary>
        /// Author information.
        /// </summary>
        public string Author => "TavernOracle Team";

        /// <summary>
        /// Current assembly version.
        /// </summary>
        public Version Version => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);

        /// <summary>
        /// Top-level menu item for HDT's main window menu bar (or null if none).
        /// </summary>
#pragma warning disable CS8766
        public MenuItem? MenuItem => null;
#pragma warning restore CS8766

        #endregion

        #region ITavernOraclePlugin State Properties

        /// <summary>
        /// Whether the plugin is currently active and loaded.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                lock (_syncLock)
                {
                    return _isLoaded;
                }
            }
            private set
            {
                lock (_syncLock)
                {
                    _isLoaded = value;
                }
            }
        }

        /// <summary>
        /// Timestamp of the last processed update tick.
        /// </summary>
        public DateTime? LastUpdateTime
        {
            get
            {
                lock (_syncLock)
                {
                    return _lastUpdateTime;
                }
            }
            private set
            {
                lock (_syncLock)
                {
                    _lastUpdateTime = value;
                }
            }
        }

        /// <summary>
        /// Count of processed update ticks since load.
        /// </summary>
        public ulong UpdateTickCount
        {
            get
            {
                lock (_syncLock)
                {
                    return _updateTickCount;
                }
            }
            private set
            {
                lock (_syncLock)
                {
                    _updateTickCount = value;
                }
            }
        }

        #endregion

        #region Component Accessors (For Testing, Debugging & Integration)

        /// <summary>
        /// Attached game state watcher and state machine.
        /// </summary>
        public IGameStateWatcher StateWatcher => _stateWatcher;

        /// <summary>
        /// Attached branch-and-bound knapsack solver.
        /// </summary>
        public IKnapsackSolver KnapsackSolver => _knapsackSolver;

        /// <summary>
        /// Attached minion position heuristic optimizer.
        /// </summary>
        public IMinionPositionOptimizer PositionOptimizer => _positionOptimizer;

        /// <summary>
        /// Attached unified tactical decision pipeline.
        /// </summary>
        public IDecisionPipeline DecisionPipeline => _decisionPipeline;

        /// <summary>
        /// 玩家个性化配置与风控参数快照。
        /// </summary>
        public PluginSettings Settings => _settings;

        /// <summary>
        /// 玩家个性化配置视图模型。
        /// </summary>
        public SettingsViewModel SettingsViewModel => _settingsViewModel ??= new SettingsViewModel(_settings);

        /// <summary>
        /// 嵌入 HDT 插件选项界面的快捷控制面板控件。
        /// </summary>
        public SettingsView SettingsControl => _settingsControl ??= CreateSettingsControl();

        /// <summary>
        /// Attached game window alignment tracker.
        /// </summary>
        public IGameWindowTracker WindowTracker => _windowTracker;

        /// <summary>
        /// Geometric anchor coordinate calculator.
        /// </summary>
        public BattlegroundsAnchorCalculator AnchorCalculator => _anchorCalculator;

        /// <summary>
        /// HDT host overlay Canvas (e.g. Core.OverlayCanvas or mock host).
        /// </summary>
        public Canvas? HostCanvas => _hostCanvas;

        /// <summary>
        /// TavernOracle plugin root overlay container Canvas.
        /// </summary>
        public Canvas? ContainerCanvas => _containerCanvas;

        /// <summary>
        /// Top recommendation decision banner control.
        /// </summary>
        public DecisionBannerControl? BannerControl => _bannerControl;

        /// <summary>
        /// Win rate and lethal probability gauge control.
        /// </summary>
        public LethalGaugeControl? LethalGauge => _lethalGauge;

        /// <summary>
        /// Pool of pre-allocated minion badge overlay controls.
        /// </summary>
        public IReadOnlyList<MinionBadgeControl> BadgeControls => _badgeControls;

        /// <summary>
        /// The most recent optimal action sequence produced by the solver.
        /// </summary>
        public ActionSequenceResult LastActionResult => _lastActionResult;

        /// <summary>
        /// The most recent optimal board minion positioning layout.
        /// </summary>
        public PositioningResult LastPositioningResult => _lastPositioningResult;

        /// <summary>
        /// The most recent board snapshot processed by the decision loop.
        /// </summary>
        public BoardSnapshot LastSnapshot => _lastSnapshot;

        #endregion

        #region Assembly Probing & Dependency Resolution

        static TavernOraclePlugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolvePluginDependencies;
        }

        private static Assembly? ResolvePluginDependencies(object? sender, ResolveEventArgs args)
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(assemblyName)) return null;

                // HDT owns HearthDb. Resolve its public metadata assembly beside the host executable.
                if (assemblyName == "HearthDb")
                {
                    var hostFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HearthDb.dll");
                    if (System.IO.File.Exists(hostFile)) return Assembly.LoadFrom(hostFile);
                }

                var pluginDir = System.IO.Path.GetDirectoryName(typeof(TavernOraclePlugin).Assembly.Location);
                if (!string.IsNullOrEmpty(pluginDir))
                {
                    var targetFile = System.IO.Path.Combine(pluginDir, assemblyName + ".dll");
                    if (System.IO.File.Exists(targetFile))
                    {
                        return Assembly.LoadFrom(targetFile);
                    }
                }
            }
            catch
            {
                // Fall back to standard assembly probing
            }
            return null;
        }

        #endregion

        #region Constructors & Dependency Injection

        /// <summary>
        /// Default parameterless constructor required by HDT PluginManager reflection activation.
        /// </summary>
        public TavernOraclePlugin()
            : this(null, null, null, null, null, null, null, null)
        {
        }

        /// <summary>
        /// Overloaded constructor supporting dependency injection for unit and integration testing.
        /// </summary>
        public TavernOraclePlugin(
            IGameStateWatcher? stateWatcher = null,
            IKnapsackSolver? knapsackSolver = null,
            IMinionPositionOptimizer? positionOptimizer = null,
            IGameWindowTracker? windowTracker = null,
            Canvas? hostCanvas = null,
            IDecisionPipeline? decisionPipeline = null,
            PluginSettings? settings = null,
            string? settingsFilePath = null,
            Func<object?>? hdtGameProvider = null)
        {
            _isExplicitSettings = (settings != null);
            _settingsFilePath = settingsFilePath;
            _settings = settings ?? PluginSettings.Load(_settingsFilePath);
            _settingsViewModel = new SettingsViewModel(_settings, _settingsFilePath);
            _settingsViewModel.SettingsChanged += HandleSettingsChanged;

            _stateWatcher = stateWatcher ?? new GameStateWatcher();
            _usesLiveHdt = stateWatcher == null || hdtGameProvider != null;
            _hdtGameProvider = hdtGameProvider ?? (() => GetHdtCoreProperty("Game"));
            _isDefaultSolver = (knapsackSolver == null);

            if (_isDefaultSolver)
            {
                var riskValve = new RiskControlValve(
                    emergencyThreshold: _settings.LethalThreshold,
                    tempoMultiplier: _settings.TempoWeight);
                _knapsackSolver = new KnapsackSolver(riskValve: riskValve);
            }
            else
            {
                _knapsackSolver = knapsackSolver!;
            }

            _positionOptimizer = positionOptimizer ?? Decision.Positioning.MinionPositionOptimizer.Instance;
            _isDefaultPipeline = (decisionPipeline == null);
            _decisionPipeline = decisionPipeline ?? new DecisionPipeline(_knapsackSolver, _positionOptimizer);
            _windowTracker = windowTracker ?? new GameWindowTracker();
            _hostCanvas = hostCanvas;
            _anchorCalculator = new BattlegroundsAnchorCalculator(_windowTracker);
        }

        /// <summary>
        /// Dynamically attaches or switches the external host Canvas (e.g. Core.OverlayCanvas or mock host).
        /// </summary>
        public void SetHostCanvas(Canvas? hostCanvas)
        {
            lock (_syncLock)
            {
                if (ReferenceEquals(_hostCanvas, hostCanvas))
                {
                    return;
                }

                SafeRunOnUi(() =>
                {
                    if (_hostCanvas != null && _containerCanvas != null && _hostCanvas.Children.Contains(_containerCanvas))
                    {
                        _hostCanvas.Children.Remove(_containerCanvas);
                    }

                    _hostCanvas = hostCanvas;

                    if (_hostCanvas != null && _containerCanvas != null && !_hostCanvas.Children.Contains(_containerCanvas))
                    {
                        _hostCanvas.Children.Add(_containerCanvas);
                    }
                });
            }
        }

        #endregion

        #region Lifecycle Methods

        /// <summary>
        /// Called when HDT activates the plugin.
        /// Initializes services, registers event hooks, constructs UI controls, and prepares state.
        /// Idempotent: repeated calls while loaded have no side-effects.
        /// </summary>
        public void OnLoad()
        {
            lock (_syncLock)
            {
                if (_isLoaded)
                {
                    return;
                }

                _isLoaded = true;
                _updateTickCount = 0;
                _lastUpdateTime = null;

                // Load latest settings on activate (if not explicitly injected)
                if (!_isExplicitSettings)
                {
                    _settings = PluginSettings.Load(_settingsFilePath);
                }

                if (_settingsViewModel != null)
                {
                    _settingsViewModel.SettingsChanged -= HandleSettingsChanged;
                    _settingsViewModel.SettingsChanged += HandleSettingsChanged;
                    _settingsViewModel.UpdateFrom(_settings);
                }
                else
                {
                    _settingsViewModel = new SettingsViewModel(_settings, _settingsFilePath);
                    _settingsViewModel.SettingsChanged += HandleSettingsChanged;
                }

                // Apply decision parameters and HUD visibility
                ApplyDecisionSettings(_settings);

                // 1. Initialize presentation controls and mount to host canvas
                InitializeUI();
                ApplySettingsToHud(_settings);

                // 2. Subscribe to GameStateWatcher state changes and game events
                _stateWatcher.SnapshotChanged += HandleSnapshotChanged;
                _stateWatcher.GameStarted += HandleGameStarted;
                _stateWatcher.GameEnded += HandleGameEnded;
                _stateWatcher.TurnStarted += HandleTurnStarted;

                // 3. Subscribe to window geometry updates
                _windowTracker.ViewportChanged += HandleViewportChanged;
                _windowTracker.WindowBoundsChanged += HandleWindowBoundsChanged;

                try
                {
                    _windowTracker.StartTracking();
                }
                catch
                {
                    // Graceful degradation when window handle discovery is unavailable
                }

                // 4. Ensure state watcher is activated
                if (!_stateWatcher.IsRunning)
                {
                    try
                    {
                        _stateWatcher.Start();
                    }
                    catch
                    {
                        // Safe degradation
                    }
                }
                if (_usesLiveHdt) ShowLiveStatus("酒馆先知已加载", "等待酒馆招募阶段的盘面数据");
            }

            Loaded?.Invoke();
        }

        /// <summary>
        /// Called when HDT deactivates the plugin or HDT shuts down.
        /// Unregisters event hooks and cleans up HUD overlay resources.
        /// Idempotent: repeated calls while unloaded have no side-effects.
        /// </summary>
        public void OnUnload()
        {
            lock (_syncLock)
            {
                if (!_isLoaded)
                {
                    return;
                }

                _isLoaded = false;
                _liveGameActive = false;

                if (_settingsViewModel != null)
                {
                    _settingsViewModel.SettingsChanged -= HandleSettingsChanged;
                }

                SafeRunOnUi(() =>
                {
                    try
                    {
                        if (_settingsWindow != null && _settingsWindow.IsLoaded)
                        {
                            _settingsWindow.Close();
                            _settingsWindow = null;
                        }
                    }
                    catch
                    {
                        // Defensive degradation
                    }
                });

                // 1. Unsubscribe from state machine
                _stateWatcher.SnapshotChanged -= HandleSnapshotChanged;
                _stateWatcher.GameStarted -= HandleGameStarted;
                _stateWatcher.GameEnded -= HandleGameEnded;
                _stateWatcher.TurnStarted -= HandleTurnStarted;

                // 2. Unsubscribe and stop window tracker
                _windowTracker.ViewportChanged -= HandleViewportChanged;
                _windowTracker.WindowBoundsChanged -= HandleWindowBoundsChanged;

                try
                {
                    _windowTracker.StopTracking();
                }
                catch
                {
                    // Ignore tracker shutdown errors
                }

                // 3. Detach and teardown presentation controls
                TeardownUI();
            }

            Unloaded?.Invoke();
        }

        /// <summary>
        /// Called when the user clicks the plugin button in HDT Options.
        /// </summary>
        public void OnButtonPress()
        {
            OpenOrFocusSettingsWindow();
            ButtonPressed?.Invoke();
        }

        /// <summary>
        /// Called periodically (~100ms) on the HDT main loop.
        /// Coordinates tactical updates when the plugin is active.
        /// </summary>
        public void OnUpdate()
        {
            lock (_syncLock)
            {
                if (!_isLoaded)
                {
                    return;
                }

                _updateTickCount++;
                _lastUpdateTime = DateTime.UtcNow;

                // Drive window tracker polling and update anchor positioning
                try
                {
                    _windowTracker.UpdateNow();
                    if (_usesLiveHdt) SynchronizeLiveGame();
                }
                catch
                {
                    // Defensive degradation
                }
            }

            Updated?.Invoke();
        }

        #endregion

        #region Presentation Layer Initialization & Teardown

        private void InitializeUI()
        {
            // Auto-discover HDT Core.OverlayCanvas via reflection if not explicitly injected
            if (_hostCanvas == null)
            {
                _hostCanvas = TryGetHdtOverlayCanvas();
            }

            SafeRunOnUi(() =>
            {
                try
                {
                    if (_containerCanvas == null)
                    {
                        _containerCanvas = new Canvas
                        {
                            IsHitTestVisible = false,
                            Background = Brushes.Transparent,
                            ClipToBounds = false
                        };
                    }

                    if (_bannerControl == null)
                    {
                        _bannerControl = new DecisionBannerControl
                        {
                            Visibility = Visibility.Collapsed
                        };
                        _containerCanvas.Children.Add(_bannerControl);
                    }

                    if (_lethalGauge == null)
                    {
                        _lethalGauge = new LethalGaugeControl
                        {
                            Visibility = Visibility.Collapsed
                        };
                        _containerCanvas.Children.Add(_lethalGauge);
                    }

                    if (_badgeControls.Count == 0)
                    {
                        for (int i = 0; i < MaxBadgePoolCount; i++)
                        {
                            var badge = new MinionBadgeControl
                            {
                                Visibility = Visibility.Collapsed
                            };
                            _badgeControls.Add(badge);
                            _containerCanvas.Children.Add(badge);
                        }
                    }

                    // Attach to external host canvas if available (e.g. Core.OverlayCanvas or mock host)
                    if (_hostCanvas != null && !_hostCanvas.Children.Contains(_containerCanvas))
                    {
                        _hostCanvas.Children.Add(_containerCanvas);
                    }
                }
                catch
                {
                    // Defensive degradation for non-UI test environments
                }
            });
        }

        private void TeardownUI()
        {
            SafeRunOnUi(() =>
            {
                try
                {
                    if (_bannerControl != null)
                    {
                        _bannerControl.Visibility = Visibility.Collapsed;
                    }

                    if (_lethalGauge != null)
                    {
                        _lethalGauge.Visibility = Visibility.Collapsed;
                        _lethalGauge.Reset();
                    }

                    foreach (var badge in _badgeControls)
                    {
                        badge.StopGlowPulse();
                        badge.Visibility = Visibility.Collapsed;
                    }

                    if (_hostCanvas != null && _containerCanvas != null && _hostCanvas.Children.Contains(_containerCanvas))
                    {
                        _hostCanvas.Children.Remove(_containerCanvas);
                    }

                    if (_containerCanvas != null)
                    {
                        _containerCanvas.Children.Clear();
                    }

                    _bannerControl = null;
                    _lethalGauge = null;
                    _badgeControls.Clear();
                    _containerCanvas = null;
                }
                catch
                {
                    // Defensive degradation
                }
            });
        }

        #endregion

        #region Event Handlers & Decision Loop

        private void HandleGameStarted()
        {
            SafeRunOnUi(() =>
            {
                try
                {
                    _lethalGauge?.Reset();
                    RefreshHudPositions();
                }
                catch
                {
                    // Safe degradation
                }
            });
        }

        private void HandleGameEnded()
        {
            SafeRunOnUi(() =>
            {
                try
                {
                    if (_bannerControl != null)
                    {
                        _bannerControl.Visibility = Visibility.Collapsed;
                    }

                    if (_lethalGauge != null)
                    {
                        _lethalGauge.Visibility = Visibility.Collapsed;
                    }

                    foreach (var badge in _badgeControls)
                    {
                        badge.StopGlowPulse();
                        badge.Visibility = Visibility.Collapsed;
                    }
                }
                catch
                {
                    // Safe degradation
                }
            });
        }

        private void HandleTurnStarted(int turn)
        {
            // Turn advancement hook
        }

        private void HandleViewportChanged(object? sender, GameViewportChangedEventArgs e)
        {
            RefreshHudPositions();
        }

        private void HandleWindowBoundsChanged(object? sender, WindowBoundsChangedEventArgs e)
        {
            RefreshHudPositions();
        }

        /// <summary>
        /// Main tactical decision loop triggered whenever the game state watcher produces a new BoardSnapshot.
        /// Executes knapsack DP, position optimization, and updates the HUD presentation layer in &lt; 15ms.
        /// Zero unhandled exceptions guaranteed via structured defensive degradation.
        /// </summary>
        private void HandleSnapshotChanged(BoardSnapshot snapshot)
        {
            try
            {
                _lastSnapshot = snapshot;

                // 1. Solve optimal actions and positioning via unified DecisionPipeline (< 15ms)
                DecisionPipelineResult decision;
                try
                {
                    decision = _decisionPipeline.Execute(in snapshot);
                }
                catch
                {
                    decision = DecisionPipelineResult.Empty;
                }
                _lastActionResult = decision.ActionResult;
                _lastPositioningResult = decision.PositioningResult;
                var actionResult = decision.ActionResult;

                // 3. Dispatch UI updates to WPF Dispatcher
                SafeRunOnUi(() =>
                {
                    UpdateLethalGauge(in snapshot);
                    UpdateDecisionBanner(in snapshot, in actionResult);
                    UpdateMinionBadges(in snapshot, in actionResult);
                    RefreshHudPositions();
                });
            }
            catch
            {
                // Defensive degradation: Zero unhandled exceptions
            }
        }

        #endregion

        #region HUD Presentation Render Updates

        private void UpdateLethalGauge(in BoardSnapshot snapshot)
        {
            if (_lethalGauge == null || !_settings.IsOverlayEnabled || !_settings.IsLethalGaugeVisible ||
                (_usesLiveHdt && snapshot.Simulation.IsDegraded))
            {
                if (_lethalGauge != null)
                {
                    _lethalGauge.Visibility = Visibility.Collapsed;
                }
                return;
            }

            try
            {
                _lethalGauge.UpdateResult(snapshot.Simulation);
                _lethalGauge.Visibility = Visibility.Visible;
            }
            catch
            {
                // Defensive fallback
            }
        }

        private void UpdateDecisionBanner(in BoardSnapshot snapshot, in ActionSequenceResult actionResult)
        {
            if (_bannerControl == null || !_settings.IsOverlayEnabled || !_settings.IsBannerVisible)
            {
                if (_bannerControl != null)
                {
                    _bannerControl.Visibility = Visibility.Collapsed;
                }
                return;
            }

            try
            {
                // Determine alert level from simulation risk profile, configured threshold, and player health
                BannerAlertLevel alertLevel;
                double lethalRate = snapshot.Simulation.LethalRate;
                double threshold = _settings.LethalThreshold;

                if (lethalRate >= threshold || (snapshot.Health <= 10 && lethalRate >= threshold * (2.0 / 3.0)))
                {
                    alertLevel = BannerAlertLevel.Critical;
                }
                else if (lethalRate >= threshold * 0.5 || snapshot.Health <= 15)
                {
                    alertLevel = BannerAlertLevel.Warning;
                }
                else
                {
                    alertLevel = BannerAlertLevel.Normal;
                }

                string headline;
                string message;
                string? detail;

                if (actionResult.IsEmpty)
                {
                    headline = "【保持现状】";
                    message = "当前没有已评估的可行动作；普通金币回合结束清空";
                    detail = $"铸币: {snapshot.Gold.Available}/{snapshot.Gold.Max}";
                }
                else
                {
                    var firstAction = actionResult[0];
                    headline = alertLevel switch
                    {
                        BannerAlertLevel.Critical => "【绝地保命】",
                        BannerAlertLevel.Warning => "【战力预警】",
                        _ => "【本回合推荐】"
                    };

                    message = FormatActionText(firstAction, snapshot);

                    if (actionResult.Count > 1)
                    {
                        detail = $"首选: {firstAction.ActionType} | 启发式候选序列共 {actionResult.Count} 步 (预期收益 +{actionResult.TotalScore:F1})";
                    }
                    else
                    {
                        detail = $"动作: {firstAction.ActionType} | 收益: +{actionResult.TotalScore:F1} | 耗时: {actionResult.ElapsedMs:F2}ms";
                    }
                }

                _bannerControl.AlertLevel = alertLevel;
                _bannerControl.Headline = headline;
                _bannerControl.Message = message;
                _bannerControl.Detail = _usesLiveHdt
                    ? $"剩余 {snapshot.Gold.Available} 金 | 操作后自动更新建议"
                    : detail;
                foreach (var hand in snapshot.HandCards)
                    if (HandActionPlanner.ShouldHold(snapshot, hand))
                    {
                        _bannerControl.Detail += " | 留在手中：" + (hand.DisplayName ?? GetCardName(hand.CardId)) + "（手牌成长/手牌收益）";
                        break;
                    }
                foreach (var item in snapshot.TavernMinions)
                    if (item.IsTavernSpell && item.Cost <= snapshot.Gold.Available)
                    {
                        _bannerControl.Detail += " | 可购买法术尚未评估，刷新前请先判断";
                        break;
                    }
                bool hasSimulation = !_usesLiveHdt || !snapshot.Simulation.IsDegraded;
                _bannerControl.LethalRate = hasSimulation ? snapshot.Simulation.LethalRate : 0;
                _bannerControl.WinRate = hasSimulation ? snapshot.Simulation.WinRate : null;
                if (!hasSimulation) _bannerControl.Detail += " | 战力模拟未接入";
                _bannerControl.Visibility = Visibility.Visible;
            }
            catch
            {
                // Defensive fallback
            }
        }

        private void UpdateMinionBadges(in BoardSnapshot snapshot, in ActionSequenceResult actionResult)
        {
            if (!_settings.IsOverlayEnabled || !_settings.AreBadgesVisible)
            {
                for (int i = 0; i < _badgeControls.Count; i++)
                {
                    var badge = _badgeControls[i];
                    badge.StopGlowPulse();
                    badge.Visibility = Visibility.Collapsed;
                }
                return;
            }

            int activeBadgeIndex = 0;

            try
            {
                int tavernCount = snapshot.TavernMinions.Length;
                if (tavernCount > 0)
                {
                    for (int i = 0; i < tavernCount && activeBadgeIndex < _badgeControls.Count; i++)
                    {
                        var tavernMinion = snapshot.TavernMinions[i];
                        if (tavernMinion.IsTavernSpell) continue;
                        int slotIndex = i + 1; // 1-based slot index

                        // Check for duplicate copies on board or hand (Pairs & Triples)
                        int boardCopies = 0;
                        for (int b = 0; b < snapshot.BoardMinions.Length; b++)
                        {
                            if (!snapshot.BoardMinions[b].IsGolden &&
                                string.Equals(snapshot.BoardMinions[b].CardId, tavernMinion.CardId, StringComparison.OrdinalIgnoreCase))
                            {
                                boardCopies++;
                            }
                        }

                        int handCopies = 0;
                        for (int h = 0; h < snapshot.HandCards.Length; h++)
                        {
                            if (string.Equals(snapshot.HandCards[h].CardId, tavernMinion.CardId, StringComparison.OrdinalIgnoreCase))
                            {
                                handCopies++;
                            }
                        }

                        int totalCopies = boardCopies + handCopies;

                        // Check archetype key engine status
                        var engine = ArchetypeRepository.Instance.GetEngineMinion(tavernMinion.CardId);

                        // Check if recommended in solver action sequence
                        bool isRecommendedBuy = false;
                        for (int a = 0; a < actionResult.Count; a++)
                        {
                            var act = actionResult[a];
                            if (act.ActionType == ActionType.Buy &&
                                (act.SlotIndex == i || act.EntityId == tavernMinion.EntityId ||
                                 (!string.IsNullOrEmpty(act.CardId) && string.Equals(act.CardId, tavernMinion.CardId, StringComparison.OrdinalIgnoreCase))))
                            {
                                isRecommendedBuy = true;
                                break;
                            }
                        }

                        MinionBadgeViewModel? vm = null;

                        bool isNextBuy = !actionResult.IsEmpty && actionResult[0].ActionType == ActionType.Buy &&
                            actionResult[0].EntityId == tavernMinion.EntityId;
                        if (isNextBuy)
                        {
                            vm = MinionBadgeViewModel.CreateTempoFix($"首选买入 · 第{slotIndex}张", "从左向右数");
                        }
                        else if (totalCopies >= 2)
                        {
                            vm = MinionBadgeViewModel.CreateTriplePair("🔥 三连", "凑齐三连", enableGlow: true);
                        }
                        else if (totalCopies == 1)
                        {
                            vm = MinionBadgeViewModel.CreateTriplePair("🔥 对子", "已有1张", enableGlow: true);
                        }
                        else if (engine != null && (engine.IsCoreEngine || engine.SynergyWeight >= 2.0))
                        {
                            string title = !string.IsNullOrEmpty(engine.NameCn) ? engine.NameCn : "流派核心";
                            vm = MinionBadgeViewModel.CreateKeyEngine(title, engine.SynergyWeight, engine.Role);
                        }
                        else if (isRecommendedBuy)
                        {
                            vm = MinionBadgeViewModel.CreateTempoFix("即时推荐", "推荐买入", tempoScore: 3.0);
                        }

                        if (vm != null)
                        {
                            var badge = _badgeControls[activeBadgeIndex++];
                            var minionBounds = _anchorCalculator.GetTavernMinionBounds(slotIndex, tavernCount, CoordinateSpace.RelativeDip);
                            badge.ViewModel = vm;
                                                        var viewport = _anchorCalculator.Viewport;
                            double hostWidth = _hostCanvas?.ActualWidth ?? 0;
                            double hostHeight = _hostCanvas?.ActualHeight ?? 0;
                            double sourceWidth = viewport.PhysicalClientBounds.Width / viewport.DpiScale;
                            double sourceHeight = viewport.PhysicalClientBounds.Height / viewport.DpiScale;
                            if (hostWidth > 0 && hostHeight > 0 && sourceWidth > 0 && sourceHeight > 0)
                                minionBounds = new Rect(minionBounds.X * hostWidth / sourceWidth,
                                    minionBounds.Y * hostHeight / sourceHeight,
                                    minionBounds.Width * hostWidth / sourceWidth, minionBounds.Height * hostHeight / sourceHeight);
                            badge.AnchorToMinion(minionBounds, MinionBadgeControl.DefaultAnchorOffset + (slotIndex % 2 == 0 ? 36 : 0));
                            badge.Visibility = Visibility.Visible;

                            if (vm.EnableGlowPulse)
                            {
                                badge.StartGlowPulse();
                            }
                            else
                            {
                                badge.StopGlowPulse();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Defensive fallback
            }

            // Hide unused badges in the pool
            for (int i = activeBadgeIndex; i < _badgeControls.Count; i++)
            {
                var badge = _badgeControls[i];
                badge.StopGlowPulse();
                badge.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshHudPositions()
        {
            SafeRunOnUi(() =>
            {
                try
                {
                    if (_bannerControl != null && _bannerControl.Visibility == Visibility.Visible)
                    {
                        double width = _hostCanvas?.ActualWidth ?? 0;
                        if (width <= 0) width = _hostCanvas?.Width ?? 0;
                        if (double.IsNaN(width) || width <= 0) width = _anchorCalculator.Viewport.RelativeDipBounds.Width;
                        _bannerControl.UpdatePosition(width, topMargin: 125);

                    }

                    if (_lethalGauge != null && _lethalGauge.Visibility == Visibility.Visible)
                    {
                        double gaugeW = _lethalGauge.ActualWidth > 0 ? _lethalGauge.ActualWidth : 280.0;
                        var gaugePos = _anchorCalculator.GetLethalGaugePosition(gaugeW, leftMargin: 30.0, topMargin: 40.0, space: CoordinateSpace.RelativeDip);
                        Canvas.SetLeft(_lethalGauge, gaugePos.X);
                        Canvas.SetTop(_lethalGauge, gaugePos.Y);
                    }
                }
                catch
                {
                    // Defensive degradation
                }
            });
        }

        private static string FormatActionText(in CandidateAction action, in BoardSnapshot snapshot)
        {
            return action.ActionType switch
            {
                ActionType.Buy => $"买入从左数第 {action.SlotIndex + 1} 张：{GetShopName(action, snapshot)}",
                ActionType.Sell => $"卖出随从 (卡槽 #{action.SlotIndex + 1})",
                ActionType.Play => $"打出{GetHandName(action, snapshot)}，放在从左数第 {action.TargetPosition + 1} 位",
                ActionType.Magnetize => $"磁力合体：将{GetHandName(action, snapshot)}放在从左数第 {action.TargetPosition + 1} 位机械左侧",
                ActionType.UpgradeTier => $"升级酒馆至 {snapshot.TavernTier + 1} 星 (花费 {action.GoldCost} 金)",
                ActionType.Reroll => $"刷新酒馆 (剩余 {Math.Max(0, snapshot.Gold.Available - action.GoldCost)} 金)",
                ActionType.Freeze => "冻结酒馆在售随从",
                _ => action.ActionType.ToString()
            };
        }

        #endregion

        private static string GetHandName(in CandidateAction action, in BoardSnapshot snapshot) =>
            action.SlotIndex >= 0 && action.SlotIndex < snapshot.HandCards.Length &&
            !string.IsNullOrEmpty(snapshot.HandCards[action.SlotIndex].DisplayName)
                ? snapshot.HandCards[action.SlotIndex].DisplayName! : GetCardName(action.CardId);

        private static string GetShopName(in CandidateAction action, in BoardSnapshot snapshot) =>
            action.SlotIndex >= 0 && action.SlotIndex < snapshot.TavernMinions.Length &&
            !string.IsNullOrEmpty(snapshot.TavernMinions[action.SlotIndex].DisplayName)
                ? snapshot.TavernMinions[action.SlotIndex].DisplayName! : GetCardName(action.CardId);

        private static string GetCardName(string cardId) =>
            Data.HearthDbAdapter.Instance.TryGetCard(cardId, out var card) && !string.IsNullOrEmpty(card.Name)
                ? (card.LocalizedName ?? card.Name) : cardId;

        // HDT already reconstructs these public objects from Power.log. Never access the game process.
        private void SynchronizeLiveGame()
        {
            try
            {
                if (_hostCanvas == null)
                    SetHostCanvas(TryGetHdtOverlayCanvas());
                var game = _hdtGameProvider();
                if (game == null)
                {
                    if (_liveGameActive) _stateWatcher.OnGameEnd();
                    _liveGameActive = false;
                    ShowLiveStatus("酒馆先知等待数据", "尚未连接 HDT 盘面数据");
                    return;
                }
                var type = game.GetType();
                bool ReadFlag(string name) => type.GetProperty(name)?.GetValue(game) is true;
                if (ReadFlag("IsInMenu") || !ReadFlag("IsBattlegroundsMatch"))
                {
                    if (_liveGameActive) _stateWatcher.OnGameEnd();
                    _liveGameActive = false;
                    HandleGameEnded();
                    return;
                }
                if (ReadFlag("IsBattlegroundsCombatPhase"))
                {
                    HandleGameEnded(); // Clear shopping advice during combat.
                    return;
                }
                if (!_liveGameActive)
                {
                    _stateWatcher.OnGameStart();
                    _liveGameActive = true;
                }
                _stateWatcher.AttachHdtGame(game);
                if (string.IsNullOrEmpty(_lastSnapshot.HeroCardId) || _lastSnapshot.TavernMinions.Length == 0)
                    ShowLiveStatus("酒馆先知等待商店", "尚无可识别的酒馆商品，等待日志更新");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("[TavernOracle] HDT synchronization: {0}", ex);
                ShowLiveStatus("酒馆先知数据异常", "盘面同步失败，请重启 HDT 后重试");
            }
        }

        private void ShowLiveStatus(string headline, string message)
        {
            SafeRunOnUi(() =>
            {
                HandleGameEnded();
                if (_bannerControl == null || !_settings.IsOverlayEnabled || !_settings.IsBannerVisible) return;
                _bannerControl.Headline = headline;
                _bannerControl.Message = message;
                _bannerControl.Detail = "仅显示建议，所有操作由玩家手动执行";
                _bannerControl.LethalRate = 0;
                _bannerControl.WinRate = null;
                _bannerControl.Visibility = Visibility.Visible;
                RefreshHudPositions();
            });
        }

        private static object? GetHdtCoreProperty(string property)
        {
            var core = Type.GetType("Hearthstone_Deck_Tracker.API.Core, HearthstoneDeckTracker");
            return core?.GetProperty(property, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        #region Personalization Settings & Control Panel (T22)

        private SettingsView CreateSettingsControl()
        {
            SettingsView? control = null;
            SafeRunOnUi(() =>
            {
                try
                {
                    control = new SettingsView(SettingsViewModel);
                }
                catch
                {
                    // Defensive fallback
                }
            });
            return control ?? new SettingsView(SettingsViewModel);
        }

        private void OpenOrFocusSettingsWindow()
        {
            SafeRunOnUi(() =>
            {
                try
                {
                    if (_settingsWindow == null || !_settingsWindow.IsLoaded)
                    {
                        _settingsWindow = new Window
                        {
                            Title = "TavernOracle (酒馆先知) - 战术参谋长控制台",
                            Content = SettingsControl,
                            Width = 620,
                            Height = 720,
                            MinWidth = 480,
                            MinHeight = 500,
                            Background = Presentation.Theme.HudPalette.BackgroundDeepSpaceBrush,
                            Foreground = Presentation.Theme.HudPalette.TextPrimaryBrush,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen
                        };
                        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                        _settingsWindow.Show();
                    }
                    else
                    {
                        if (_settingsWindow.WindowState == WindowState.Minimized)
                        {
                            _settingsWindow.WindowState = WindowState.Normal;
                        }
                        _settingsWindow.Activate();
                        _settingsWindow.Focus();
                    }
                }
                catch
                {
                    // Defensive degradation
                }
            });
        }

        private void HandleSettingsChanged(object? sender, PluginSettings newSettings)
        {
            _settings = newSettings.Clone();
            ApplyDecisionSettings(_settings);
            ApplySettingsToHud(_settings);
        }

        private void ApplyDecisionSettings(PluginSettings settings)
        {
            lock (_syncLock)
            {
                if (_isDefaultSolver)
                {
                    var riskValve = new RiskControlValve(
                        emergencyThreshold: settings.LethalThreshold,
                        tempoMultiplier: settings.TempoWeight);
                    _knapsackSolver = new KnapsackSolver(riskValve: riskValve);

                    if (_isDefaultPipeline)
                    {
                        _decisionPipeline = new DecisionPipeline(_knapsackSolver, _positionOptimizer);
                    }
                }
            }
        }

        private void ApplySettingsToHud(PluginSettings settings)
        {
            SafeRunOnUi(() =>
            {
                try
                {
                    if (_containerCanvas != null)
                    {
                        _containerCanvas.Visibility = settings.IsOverlayEnabled ? Visibility.Visible : Visibility.Collapsed;
                    }

                    if (!settings.IsOverlayEnabled || !settings.IsBannerVisible)
                    {
                        if (_bannerControl != null)
                        {
                            _bannerControl.Visibility = Visibility.Collapsed;
                        }
                    }

                    if (!settings.IsOverlayEnabled || !settings.IsLethalGaugeVisible)
                    {
                        if (_lethalGauge != null)
                        {
                            _lethalGauge.Visibility = Visibility.Collapsed;
                        }
                    }

                    if (!settings.IsOverlayEnabled || !settings.AreBadgesVisible)
                    {
                        foreach (var badge in _badgeControls)
                        {
                            badge.StopGlowPulse();
                            badge.Visibility = Visibility.Collapsed;
                        }
                    }

                    // If overlay enabled and we have an active game state, refresh HUD immediately
                    if (settings.IsOverlayEnabled && _lastSnapshot.TavernTier > 0)
                    {
                        HandleSnapshotChanged(_lastSnapshot);
                    }
                }
                catch
                {
                    // Defensive degradation
                }
            });
        }

        #endregion

        #region Helpers & Threading

        private void SafeRunOnUi(Action action)
        {
            try
            {
                var dispatcher = _containerCanvas?.Dispatcher ?? _hostCanvas?.Dispatcher ?? Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
                {
                    dispatcher.Invoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
                // Defensive: Zero unhandled exceptions
            }
        }

        private static Canvas? TryGetHdtOverlayCanvas()
        {
            try
            {
                var coreType = Type.GetType("Hearthstone_Deck_Tracker.API.Core, HearthstoneDeckTracker")
                               ?? Type.GetType("Hearthstone_Deck_Tracker.API.Core")
                               ?? Type.GetType("HearthstoneDeckTracker.API.Core, HearthstoneDeckTracker")
                               ?? Type.GetType("HearthstoneDeckTracker.API.Core");
                if (coreType != null)
                {
                    var prop = coreType.GetProperty("OverlayCanvas", BindingFlags.Public | BindingFlags.Static);
                    if (prop != null)
                    {
                        return prop.GetValue(null) as Canvas;
                    }
                }
            }
            catch
            {
                // Safe offline / test fallback
            }
            return null;
        }

        #endregion
    }
}
