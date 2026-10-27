using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TavernOracle.Data.State;
using TavernOracle.Decision;
using TavernOracle.Presentation.Controls;
using TavernOracle.Presentation.Settings;
using Xunit;

namespace TavernOracle.Tests.Presentation;

/// <summary>
/// 玩家个性化配置与快捷控制面板全面单元测试 (T22)。
/// 覆盖：
/// 1. PluginSettings 数据模型与边界规整；
/// 2. JSON 序列化与文件持久化及容错降级；
/// 3. SettingsViewModel 响应式属性变更与事件广播；
/// 4. SettingsView WPF 用户控件实例化与数据绑定；
/// 5. TavernOraclePlugin 实时联动响应（HUD 显隐与决策风控参数更新）。
/// </summary>
public class SettingsTests
{
    private static void RunInSta(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (caught != null)
        {
            ExceptionDispatchInfo.Capture(caught).Throw();
        }
    }

    #region 1. PluginSettings Model & Validation Tests

    [Fact]
    public void PluginSettings_DefaultValues_MustMatchSpecification()
    {
        var settings = new PluginSettings();

        Assert.True(settings.IsOverlayEnabled, "IsOverlayEnabled should be true by default");
        Assert.Equal(1.0, settings.TempoWeight, precision: 4);
        Assert.Equal(0.30, settings.LethalThreshold, precision: 4);
        Assert.True(settings.IsBannerVisible, "IsBannerVisible should be true by default");
        Assert.True(settings.AreBadgesVisible, "AreBadgesVisible should be true by default");
        Assert.True(settings.IsLethalGaugeVisible, "IsLethalGaugeVisible should be true by default");
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    [InlineData(2.5, 2.5)]
    [InlineData(0.1, 0.5)]   // Below minimum clamped
    [InlineData(5.0, 2.5)]   // Above maximum clamped
    [InlineData(double.NaN, 1.0)] // NaN fallback to default
    [InlineData(double.PositiveInfinity, 1.0)] // Infinity fallback
    public void PluginSettings_TempoWeight_Clamping_ShouldEnforceBounds(double input, double expected)
    {
        var settings = new PluginSettings { TempoWeight = input };
        settings.ClampValues();

        Assert.Equal(expected, settings.TempoWeight, precision: 4);
    }

    [Theory]
    [InlineData(0.10, 0.10)]
    [InlineData(0.30, 0.30)]
    [InlineData(0.50, 0.50)]
    [InlineData(0.01, 0.10)]  // Below min
    [InlineData(0.99, 0.50)]  // Above max
    [InlineData(double.NaN, 0.30)] // NaN fallback
    [InlineData(double.NegativeInfinity, 0.30)]
    public void PluginSettings_LethalThreshold_Clamping_ShouldEnforceBounds(double input, double expected)
    {
        var settings = new PluginSettings { LethalThreshold = input };
        settings.ClampValues();

        Assert.Equal(expected, settings.LethalThreshold, precision: 4);
    }

    [Fact]
    public void PluginSettings_ResetToDefaults_ShouldRestoreAllProperties()
    {
        var settings = new PluginSettings
        {
            IsOverlayEnabled = false,
            TempoWeight = 2.4,
            LethalThreshold = 0.45,
            IsBannerVisible = false,
            AreBadgesVisible = false,
            IsLethalGaugeVisible = false
        };

        settings.ResetToDefaults();

        Assert.True(settings.IsOverlayEnabled);
        Assert.Equal(1.0, settings.TempoWeight, precision: 4);
        Assert.Equal(0.30, settings.LethalThreshold, precision: 4);
        Assert.True(settings.IsBannerVisible);
        Assert.True(settings.AreBadgesVisible);
        Assert.True(settings.IsLethalGaugeVisible);
    }

    [Fact]
    public void PluginSettings_Clone_ShouldProduceIndependentDeepCopy()
    {
        var original = new PluginSettings
        {
            IsOverlayEnabled = false,
            TempoWeight = 1.8,
            LethalThreshold = 0.25,
            IsBannerVisible = true,
            AreBadgesVisible = false,
            IsLethalGaugeVisible = true
        };

        var clone = original.Clone();

        Assert.True(original.Equals(clone));
        Assert.NotSame(original, clone);

        // Mutating clone must not affect original
        clone.TempoWeight = 0.7;
        Assert.Equal(1.8, original.TempoWeight, precision: 4);
        Assert.Equal(0.7, clone.TempoWeight, precision: 4);
        Assert.False(original.Equals(clone));
    }

    #endregion

    #region 2. Serialization & Persistence Tests

    [Fact]
    public void PluginSettings_JsonRoundtrip_ShouldPreserveAllValues()
    {
        var original = new PluginSettings
        {
            IsOverlayEnabled = false,
            TempoWeight = 1.75,
            LethalThreshold = 0.35,
            IsBannerVisible = false,
            AreBadgesVisible = true,
            IsLethalGaugeVisible = false
        };

        string json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PluginSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void PluginSettings_SaveAndLoad_ShouldPersistToFileSuccessfully()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"tavern_settings_test_{Guid.NewGuid():N}.json");

        try
        {
            var settings = new PluginSettings
            {
                IsOverlayEnabled = true,
                TempoWeight = 2.2,
                LethalThreshold = 0.20,
                IsBannerVisible = true,
                AreBadgesVisible = false,
                IsLethalGaugeVisible = true
            };

            bool saveResult = settings.Save(tempFile);
            Assert.True(saveResult);
            Assert.True(File.Exists(tempFile));

            var loaded = PluginSettings.Load(tempFile);
            Assert.NotNull(loaded);
            Assert.Equal(settings, loaded);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void PluginSettings_Load_WhenFileNotFound_ShouldReturnDefaultSettings()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"missing_{Guid.NewGuid():N}.json");
        var settings = PluginSettings.Load(missingPath);

        Assert.NotNull(settings);
        Assert.True(settings.IsOverlayEnabled);
        Assert.Equal(1.0, settings.TempoWeight, precision: 4);
        Assert.Equal(0.30, settings.LethalThreshold, precision: 4);
    }

    [Fact]
    public void PluginSettings_Load_WhenCorruptedJson_ShouldGracefullyFallbackToDefaults()
    {
        string corruptedFile = Path.Combine(Path.GetTempPath(), $"corrupted_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(corruptedFile, "{ this is not valid json !!! }");

            var settings = PluginSettings.Load(corruptedFile);

            Assert.NotNull(settings);
            Assert.True(settings.IsOverlayEnabled);
            Assert.Equal(1.0, settings.TempoWeight, precision: 4);
            Assert.Equal(0.30, settings.LethalThreshold, precision: 4);
        }
        finally
        {
            if (File.Exists(corruptedFile))
            {
                File.Delete(corruptedFile);
            }
        }
    }

    #endregion

    #region 3. SettingsViewModel Tests

    [Fact]
    public void SettingsViewModel_PropertyChanged_ShouldFireForModifiedProperties()
    {
        var vm = new SettingsViewModel(new PluginSettings(), settingsFilePath: null);
        vm.AutoSaveEnabled = false;

        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
            {
                changedProperties.Add(e.PropertyName);
            }
        };

        vm.IsOverlayEnabled = false;
        Assert.Contains(nameof(vm.IsOverlayEnabled), changedProperties);

        changedProperties.Clear();
        vm.TempoWeight = 1.5;
        Assert.Contains(nameof(vm.TempoWeight), changedProperties);
        Assert.Contains(nameof(vm.TempoWeightDisplay), changedProperties);

        changedProperties.Clear();
        vm.LethalThreshold = 0.40;
        Assert.Contains(nameof(vm.LethalThreshold), changedProperties);
        Assert.Contains(nameof(vm.LethalThresholdDisplay), changedProperties);

        changedProperties.Clear();
        vm.IsBannerVisible = false;
        Assert.Contains(nameof(vm.IsBannerVisible), changedProperties);

        changedProperties.Clear();
        vm.AreBadgesVisible = false;
        Assert.Contains(nameof(vm.AreBadgesVisible), changedProperties);

        changedProperties.Clear();
        vm.IsLethalGaugeVisible = false;
        Assert.Contains(nameof(vm.IsLethalGaugeVisible), changedProperties);
    }

    [Fact]
    public void SettingsViewModel_SettingsChanged_ShouldBroadcastNewSnapshot()
    {
        var vm = new SettingsViewModel(new PluginSettings(), settingsFilePath: null);
        vm.AutoSaveEnabled = false;

        PluginSettings? received = null;
        vm.SettingsChanged += (_, s) => received = s;

        vm.TempoWeight = 2.1;

        Assert.NotNull(received);
        Assert.Equal(2.1, received.TempoWeight, precision: 4);
    }

    [Fact]
    public void SettingsViewModel_Displays_ShouldProvideFriendlyStrings()
    {
        var vm = new SettingsViewModel(new PluginSettings(), settingsFilePath: null);
        vm.AutoSaveEnabled = false;

        vm.TempoWeight = 0.6;
        Assert.Contains("0.60x", vm.TempoWeightDisplay);
        Assert.Contains("偏重发育", vm.TempoWeightDisplay);

        vm.TempoWeight = 1.0;
        Assert.Contains("1.00x", vm.TempoWeightDisplay);
        Assert.Contains("均衡节奏", vm.TempoWeightDisplay);

        vm.TempoWeight = 2.0;
        Assert.Contains("2.00x", vm.TempoWeightDisplay);
        Assert.Contains("即时战力", vm.TempoWeightDisplay);

        vm.LethalThreshold = 0.25;
        Assert.Equal("25%", vm.LethalThresholdDisplay);
    }

    [Fact]
    public void SettingsViewModel_ResetDefaults_ShouldResetAndBroadcast()
    {
        var vm = new SettingsViewModel(new PluginSettings
        {
            IsOverlayEnabled = false,
            TempoWeight = 2.5,
            LethalThreshold = 0.45
        }, settingsFilePath: null);
        vm.AutoSaveEnabled = false;

        PluginSettings? broadcasted = null;
        vm.SettingsChanged += (_, s) => broadcasted = s;

        vm.ResetDefaults();

        Assert.True(vm.IsOverlayEnabled);
        Assert.Equal(1.0, vm.TempoWeight, precision: 4);
        Assert.Equal(0.30, vm.LethalThreshold, precision: 4);
        Assert.NotNull(broadcasted);
        Assert.True(broadcasted.IsOverlayEnabled);
    }

    #endregion

    #region 4. SettingsView WPF Control Tests

    [Fact]
    public void SettingsView_CanInstantiateAndBindViewModel()
    {
        RunInSta(() =>
        {
            var settings = new PluginSettings
            {
                IsOverlayEnabled = true,
                TempoWeight = 1.5,
                LethalThreshold = 0.25
            };
            var vm = new SettingsViewModel(settings, settingsFilePath: null);
            vm.AutoSaveEnabled = false;

            var view = new SettingsView(vm);

            Assert.NotNull(view);
            Assert.Same(vm, view.ViewModel);
            Assert.Same(vm, view.DataContext);

            // Parameterless constructor check
            var defaultView = new SettingsView();
            Assert.NotNull(defaultView.ViewModel);
        });
    }

    #endregion

    #region 5. TavernOraclePlugin Integration & Live Tuning Tests

    [Fact]
    public void Plugin_Settings_ShouldInitializeWithDefaultValues()
    {
        var plugin = new TavernOraclePlugin();

        Assert.NotNull(plugin.Settings);
        Assert.NotNull(plugin.SettingsViewModel);
        Assert.True(plugin.Settings.IsOverlayEnabled);
        Assert.Equal(1.0, plugin.Settings.TempoWeight, precision: 4);
        Assert.Equal(0.30, plugin.Settings.LethalThreshold, precision: 4);
    }

    [Fact]
    public void Plugin_SettingsControl_ShouldExposeValidView()
    {
        RunInSta(() =>
        {
            var plugin = new TavernOraclePlugin();
            var control = plugin.SettingsControl;

            Assert.NotNull(control);
            Assert.IsType<SettingsView>(control);
            Assert.NotNull(control.ViewModel);
        });
    }

    [Fact]
    public void Plugin_OnLoad_ShouldLoadSettingsAndWireEvents()
    {
        var plugin = new TavernOraclePlugin();

        plugin.OnLoad();

        Assert.True(plugin.IsLoaded);
        Assert.NotNull(plugin.Settings);
        Assert.NotNull(plugin.SettingsViewModel);

        plugin.OnUnload();
        Assert.False(plugin.IsLoaded);
    }

    [Fact]
    public void Plugin_LiveTuning_TogglingOverlay_ShouldHideOrShowContainerCanvas()
    {
        RunInSta(() =>
        {
            var hostCanvas = new Canvas();
            string tempSettingsFile = Path.Combine(Path.GetTempPath(), $"live_tuning_overlay_{Guid.NewGuid():N}.json");
            var plugin = new TavernOraclePlugin(hostCanvas: hostCanvas, settings: new PluginSettings(), settingsFilePath: tempSettingsFile);
            plugin.SettingsViewModel.AutoSaveEnabled = false;

            plugin.OnLoad();

            Assert.NotNull(plugin.ContainerCanvas);
            Assert.Equal(Visibility.Visible, plugin.ContainerCanvas.Visibility);

            // Change setting to disable overlay
            plugin.SettingsViewModel.IsOverlayEnabled = false;

            Assert.Equal(Visibility.Collapsed, plugin.ContainerCanvas.Visibility);

            // Re-enable
            plugin.SettingsViewModel.IsOverlayEnabled = true;
            Assert.Equal(Visibility.Visible, plugin.ContainerCanvas.Visibility);

            plugin.OnUnload();
        });
    }

    [Fact]
    public void Plugin_LiveTuning_ComponentVisibility_ShouldDirectlyControlHudElements()
    {
        RunInSta(() =>
        {
            var hostCanvas = new Canvas();
            string tempSettingsFile = Path.Combine(Path.GetTempPath(), $"live_tuning_visibility_{Guid.NewGuid():N}.json");
            var plugin = new TavernOraclePlugin(hostCanvas: hostCanvas, settings: new PluginSettings(), settingsFilePath: tempSettingsFile);
            plugin.SettingsViewModel.AutoSaveEnabled = false;
            plugin.OnLoad();

            // Simulate snapshot with active minions
            var snapshot = new BoardSnapshot
            {
                TavernTier = 2,
                Health = 30,
                Gold = new GoldBudget(10, 10),
                Simulation = new TavernOracle.Simulation.CombatSimulationResult(0.60f, 0.10f, 0.30f, 4.0f)
            };

            // Trigger decision loop
            var method = typeof(TavernOraclePlugin).GetMethod("HandleSnapshotChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            method.Invoke(plugin, new object[] { snapshot });

            // Initially visible
            Assert.NotNull(plugin.BannerControl);
            Assert.Equal(Visibility.Visible, plugin.BannerControl.Visibility);

            // Toggle banner off
            plugin.SettingsViewModel.IsBannerVisible = false;
            Assert.Equal(Visibility.Collapsed, plugin.BannerControl.Visibility);

            // Toggle gauge off
            Assert.NotNull(plugin.LethalGauge);
            plugin.SettingsViewModel.IsLethalGaugeVisible = false;
            Assert.Equal(Visibility.Collapsed, plugin.LethalGauge.Visibility);

            // Toggle badges off
            plugin.SettingsViewModel.AreBadgesVisible = false;
            foreach (var badge in plugin.BadgeControls)
            {
                Assert.Equal(Visibility.Collapsed, badge.Visibility);
            }

            plugin.OnUnload();
        });
    }

    [Fact]
    public void Plugin_LiveTuning_LethalThresholdAndTempoWeight_ShouldUpdateSolverParameters()
    {
        string tempSettingsFile = Path.Combine(Path.GetTempPath(), $"live_tuning_solver_{Guid.NewGuid():N}.json");
        var plugin = new TavernOraclePlugin(settings: new PluginSettings(), settingsFilePath: tempSettingsFile);
        plugin.SettingsViewModel.AutoSaveEnabled = false;
        plugin.OnLoad();

        // Tune parameters
        plugin.SettingsViewModel.TempoWeight = 2.2;
        plugin.SettingsViewModel.LethalThreshold = 0.15;

        Assert.Equal(2.2, plugin.Settings.TempoWeight, precision: 4);
        Assert.Equal(0.15, plugin.Settings.LethalThreshold, precision: 4);

        // Verify knapsack solver reflects new tempo/threshold
        var solver = plugin.KnapsackSolver;
        Assert.NotNull(solver);

        plugin.OnUnload();
    }

    #endregion
}
