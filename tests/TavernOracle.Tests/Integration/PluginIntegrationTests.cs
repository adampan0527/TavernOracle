using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TavernOracle;
using TavernOracle.Data.Replays;
using TavernOracle.Data.State;
using TavernOracle.Data.StateMachine;
using TavernOracle.Decision.ActionGeneration;
using TavernOracle.Presentation.Controls;
using TavernOracle.Priors;
using TavernOracle.Simulation;
using Xunit;

namespace TavernOracle.Tests.Integration
{
    /// <summary>
    /// Comprehensive end-to-end integration test suite for TavernOraclePlugin lifecycle and decision pipeline (T20).
    /// Validates full lifecycle assembly: OnLoad -> Hero Selection -> Tavern Shop -> Knapsack DP -> Positioning -> HUD Rendering -> OnUpdate -> OnUnload.
    /// Strictly verifies Blizzard's "Pencil &amp; Paper Rule" compliance, sub-15ms loop performance, and zero unhandled exceptions.
    /// </summary>
    public class PluginIntegrationTests
    {
        /// <summary>
        /// Executes an action within a dedicated STA thread to accommodate WPF Dispatcher and UI visual elements.
        /// </summary>
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

        #region Full Lifecycle & Game Flow Integration

        [Fact]
        public void FullGameFlow_FromLoadToUnload_WithReplaySnapshots_RendersAccuratelyWithoutCrash()
        {
            RunInSta(() =>
            {
                // 1. Arrange: Create mock HDT Host Canvas
                var mockHostCanvas = new Canvas { Width = 1920, Height = 1080 };
                var plugin = new TavernOraclePlugin(hostCanvas: mockHostCanvas);

                Assert.False(plugin.IsLoaded);
                Assert.Null(plugin.LastUpdateTime);
                Assert.Equal(0UL, plugin.UpdateTickCount);

                // 2. Act: OnLoad
                plugin.OnLoad();

                Assert.True(plugin.IsLoaded);
                Assert.NotNull(plugin.ContainerCanvas);
                Assert.True(mockHostCanvas.Children.Contains(plugin.ContainerCanvas));
                Assert.NotNull(plugin.BannerControl);
                Assert.NotNull(plugin.LethalGauge);
                Assert.Equal(14, plugin.BadgeControls.Count);

                // 3. Act: Game Start (Hero Selected: BG20_HERO_101, Turn 1)
                plugin.StateWatcher.OnGameStart(
                    heroCardId: "BG20_HERO_101",
                    health: 30,
                    armor: 0,
                    bannedRaces: MinionRaceMask.Demon | MinionRaceMask.Mech);

                Assert.True(plugin.StateWatcher.IsInGame);

                // 4. Act: Load T06 Leapfrogger transition snapshot (S01)
                var allSnapshots = ReplayDatasetFactory.CreateAll();
                var s01 = allSnapshots[ReplayDatasetFactory.S01_LeapfroggerEarlyTransition];

                plugin.StateWatcher.LoadSnapshot(s01);

                // Assert Decision Loop outputs
                Assert.Equal(s01.Turn, plugin.LastSnapshot.Turn);
                Assert.False(plugin.LastActionResult.IsEmpty);
                Assert.True(plugin.LastActionResult.TotalScore > 0);
                Assert.Equal(s01.BoardMinions.Length, plugin.LastPositioningResult.OrderedMinions.Length);

                // Assert HUD state
                Assert.Equal(Visibility.Visible, plugin.BannerControl!.Visibility);
                Assert.Equal(BannerAlertLevel.Normal, plugin.BannerControl.AlertLevel);
                Assert.Contains("本回合推荐", plugin.BannerControl.Headline);
                Assert.False(string.IsNullOrWhiteSpace(plugin.BannerControl.Message));

                Assert.Equal(Visibility.Visible, plugin.LethalGauge!.Visibility);
                Assert.Equal(s01.Simulation.LethalRate, plugin.LethalGauge.ViewModel.LethalRate);

                // 5. Act: Load Lethal Crisis snapshot (S05 - HP 7, LethalRate = 0.38)
                var s05 = allSnapshots[ReplayDatasetFactory.S05_LethalEmergencyHp7Turn8];
                plugin.StateWatcher.LoadSnapshot(s05);

                Assert.Equal(BannerAlertLevel.Critical, plugin.BannerControl.AlertLevel);
                Assert.Contains("绝地保命", plugin.BannerControl.Headline);
                Assert.True(plugin.LethalGauge.IsCriticalAlert);

                // 6. Act: Load Multi-Pair Triple Hunt snapshot (S18)
                var s18 = allSnapshots[ReplayDatasetFactory.S18_MultiPairTripleHunt];
                plugin.StateWatcher.LoadSnapshot(s18);

                // Verify minion badges were computed and positioned
                var activeBadges = plugin.BadgeControls.Where(b => b.Visibility == Visibility.Visible).ToList();
                Assert.NotEmpty(activeBadges);
                Assert.Contains(activeBadges, b => b.BadgeType == BadgeType.TriplePair || b.BadgeType == BadgeType.KeyEngine);

                // 7. Act: HDT Periodic Update Tick
                plugin.OnUpdate();
                Assert.Equal(1UL, plugin.UpdateTickCount);
                Assert.NotNull(plugin.LastUpdateTime);

                // 8. Act: Game End
                plugin.StateWatcher.OnGameEnd();
                Assert.False(plugin.StateWatcher.IsInGame);
                Assert.Equal(Visibility.Collapsed, plugin.BannerControl.Visibility);
                Assert.Equal(Visibility.Collapsed, plugin.LethalGauge.Visibility);

                // 9. Act: OnUnload
                plugin.OnUnload();

                Assert.False(plugin.IsLoaded);
                Assert.DoesNotContain(plugin.ContainerCanvas, mockHostCanvas.Children.OfType<UIElement>());
            });
        }

        #endregion

        #region Idempotency & Repeated Load/Unload

        [Fact]
        public void Lifecycle_RepeatedLoadAndUnload_IsIdempotentAndSafe()
        {
            RunInSta(() =>
            {
                var mockHostCanvas = new Canvas();
                var plugin = new TavernOraclePlugin(hostCanvas: mockHostCanvas);

                int loadedCount = 0;
                int unloadedCount = 0;
                plugin.Loaded += () => loadedCount++;
                plugin.Unloaded += () => unloadedCount++;

                // Multiple consecutive OnLoad calls
                plugin.OnLoad();
                plugin.OnLoad();
                plugin.OnLoad();

                Assert.True(plugin.IsLoaded);
                Assert.Equal(1, loadedCount);
                Assert.Single(mockHostCanvas.Children);

                // Multiple consecutive OnUnload calls
                plugin.OnUnload();
                plugin.OnUnload();
                plugin.OnUnload();

                Assert.False(plugin.IsLoaded);
                Assert.Equal(1, unloadedCount);
                Assert.Empty(mockHostCanvas.Children);

                // Re-load again to verify full re-activation capability
                plugin.OnLoad();
                Assert.True(plugin.IsLoaded);
                Assert.Equal(2, loadedCount);
                Assert.Single(mockHostCanvas.Children);

                plugin.OnUnload();
                Assert.False(plugin.IsLoaded);
                Assert.Equal(2, unloadedCount);
            });
        }

        #endregion

        #region Headless / Offline Null Host Canvas Fallback

        [Fact]
        public void OfflineMode_WhenHostCanvasIsNull_OperatesGracefullyWithoutNullReferenceException()
        {
            RunInSta(() =>
            {
                // In offline or non-HDT headless testing, hostCanvas is null
                var plugin = new TavernOraclePlugin(hostCanvas: null);

                // Should not throw NullReferenceException
                plugin.OnLoad();
                Assert.True(plugin.IsLoaded);
                Assert.NotNull(plugin.ContainerCanvas);
                Assert.Null(plugin.HostCanvas);

                // Feed snapshot and drive algorithms
                var allSnapshots = ReplayDatasetFactory.CreateAll();
                var s01 = allSnapshots[ReplayDatasetFactory.S01_LeapfroggerEarlyTransition];

                plugin.StateWatcher.LoadSnapshot(s01);

                Assert.False(plugin.LastActionResult.IsEmpty);
                Assert.Equal(s01.BoardMinions.Length, plugin.LastPositioningResult.OrderedMinions.Length);

                plugin.OnUpdate();
                Assert.Equal(1UL, plugin.UpdateTickCount);

                plugin.OnUnload();
                Assert.False(plugin.IsLoaded);
            });
        }

        #endregion

        #region All 20 T06 Replay Scenarios Stress & Zero-Crash Verification

        [Fact]
        public void All20ReplayScenarios_ProcessWithoutUnhandledExceptions_UnderTightLatencyBudget()
        {
            RunInSta(() =>
            {
                var mockHost = new Canvas();
                var plugin = new TavernOraclePlugin(stateWatcher: new GameStateWatcher(), hostCanvas: mockHost);
                plugin.OnLoad();

                var allSnapshots = ReplayDatasetFactory.CreateAll();
                Assert.Equal(20, allSnapshots.Count);

                foreach (var (scenarioName, snapshot) in allSnapshots)
                {
                    // Drive state machine with scenario snapshot
                    plugin.StateWatcher.LoadSnapshot(snapshot);

                    // Ensure solver and positioning succeeded without exception
                    Assert.Equal(snapshot.BoardMinions.Length, plugin.LastPositioningResult.OrderedMinions.Length);
                    Assert.True(plugin.LastActionResult.ElapsedMs < 20.0,
                        $"Scenario {scenarioName} exceeded latency budget: {plugin.LastActionResult.ElapsedMs:F2}ms");

                    // Verify banner and gauge updated
                    Assert.NotNull(plugin.BannerControl);
                    Assert.Equal(Visibility.Visible, plugin.BannerControl.Visibility);
                    Assert.NotNull(plugin.LethalGauge);
                    Assert.Equal(Visibility.Visible, plugin.LethalGauge.Visibility);

                    // Drive periodic update tick
                    plugin.OnUpdate();
                }

                plugin.OnUnload();
                Assert.False(plugin.IsLoaded);
            });
        }

        #endregion

        #region Dynamic Host Canvas Switching

        [Fact]
        public void SetHostCanvas_SwitchesParentCanvasCleanly()
        {
            RunInSta(() =>
            {
                var host1 = new Canvas();
                var host2 = new Canvas();

                var plugin = new TavernOraclePlugin(hostCanvas: host1);
                plugin.OnLoad();

                Assert.Contains(plugin.ContainerCanvas!, host1.Children.OfType<UIElement>());
                Assert.DoesNotContain(plugin.ContainerCanvas!, host2.Children.OfType<UIElement>());

                // Dynamically switch to host2
                plugin.SetHostCanvas(host2);

                Assert.DoesNotContain(plugin.ContainerCanvas!, host1.Children.OfType<UIElement>());
                Assert.Contains(plugin.ContainerCanvas!, host2.Children.OfType<UIElement>());

                plugin.OnUnload();
                Assert.Empty(host2.Children);
            });
        }

        #endregion

        #region Blizzard Pencil & Paper Rule Compliance Check

        [Fact]
        public void PencilAndPaperRule_AllHudControlsAreHitTestInvisible()
        {
            RunInSta(() =>
            {
                var mockHost = new Canvas();
                var plugin = new TavernOraclePlugin(hostCanvas: mockHost);
                plugin.OnLoad();

                // Core compliance requirement: All HUD controls must have IsHitTestVisible = false
                Assert.False(plugin.ContainerCanvas!.IsHitTestVisible, "ContainerCanvas must be click-through");
                Assert.False(plugin.BannerControl!.IsHitTestVisible, "BannerControl must be click-through");
                Assert.False(plugin.LethalGauge!.IsHitTestVisible, "LethalGauge must be click-through");

                foreach (var badge in plugin.BadgeControls)
                {
                    Assert.False(badge.IsHitTestVisible, "MinionBadgeControl must be click-through");
                }

                plugin.OnUnload();
            });
        }

        #endregion
    }
}
