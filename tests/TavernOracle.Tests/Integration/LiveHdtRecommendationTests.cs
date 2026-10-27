using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TavernOracle.Data.StateMachine;

namespace TavernOracle.Tests.Integration;

public sealed class LiveHdtRecommendationTests
{
    // Matches the installed GameV2 API, including enum GetTag and a shuffled Board.
    public enum Tag { Resources = 26, Used = 25, Temp = 295, Tech = 1377, Cost = 48, Health = 45 }
    public sealed class Entity
    {
        public int Id { get; set; }
        public string CardId { get; set; } = "BG21_000";
        public bool IsMinion { get; set; } = true;
        public bool IsBattlegroundsSpell { get; set; }
        public bool IsInPlay { get; set; } = true;
        public int ZonePosition { get; set; }
        public int Attack => 3;
        public int Health => 3;
        public Dictionary<Tag, int> Tags { get; } = new();
        public int GetTag(Tag tag) => Tags.TryGetValue(tag, out int value) ? value : 0;
    }
    public sealed class Player
    {
        public Entity Hero { get; } = new() { IsMinion = false, CardId = "BG20_HERO_101" };
        public List<Entity> Board { get; } = new();
        public List<Entity> Hand { get; } = new();
        public List<Entity> PlayerEntities { get; } = new();
    }
    public sealed class Game
    {
        public bool IsInMenu { get; set; }
        public bool IsBattlegroundsMatch { get; set; } = true;
        public bool IsBattlegroundsCombatPhase { get; set; }
        public int PlayerTechLevel => 3;
        public Player Player { get; } = new();
        public Player Opponent { get; } = new();
        public Entity PlayerEntity { get; } = new() { IsMinion = false };
        public int GetTurnNumber() => 4;
    }
    private static Game CreateGame()
    {
        var game = new Game();
        game.PlayerEntity.Tags[Tag.Resources] = 6;
        game.PlayerEntity.Tags[Tag.Used] = 6;
        game.Player.Hero.Tags[Tag.Health] = 30;
        var upgrade = new Entity { IsMinion = false, CardId = "TB_BaconShopTechUp04_Button" };
        upgrade.Tags[Tag.Cost] = 8;
        game.Player.PlayerEntities.Add(upgrade);
        game.Opponent.Board.Add(new Entity { Id = 12, ZonePosition = 2 });
        game.Opponent.Board.Add(new Entity { Id = 11, ZonePosition = 1 });
        return game;
    }

    [Fact]
    public void NativeGame_UsesActualZeroGoldTierAndVisibleSlotOrder()
    {
        var snapshot = HdtEventConverter.ConvertFromHdtGame(CreateGame());
        Assert.Equal(0, snapshot.Gold.Available);
        Assert.Equal(6, snapshot.Gold.Max);
        Assert.Equal(3, snapshot.TavernTier);
        Assert.Equal(4, snapshot.Turn);
        Assert.Equal(8, snapshot.UpgradeCost);
        Assert.Equal(11, snapshot.TavernMinions[0].EntityId);
    }

    [Fact]
    public void OverlayContainer_DoesNotClipAwayItsChildren()
    {
        RunSta(() =>
        {
            var host = new Canvas { Width = 1920, Height = 1080 };
            var plugin = new TavernOraclePlugin(hostCanvas: host);
            try
            {
                plugin.OnLoad();
                host.Measure(new Size(1920, 1080));
                host.Arrange(new Rect(0, 0, 1920, 1080));
                Assert.True(!plugin.ContainerCanvas!.ClipToBounds ||
                    plugin.ContainerCanvas.RenderSize.Width > 0);
            }
            finally { plugin.OnUnload(); }
        });
    }

    [Fact]
    public void LiveTick_ShowsBuyBadge_UpdatesGold_AndClearsAdviceDuringCombatAndMenu()
    {
        RunSta(() =>
        {
            var game = CreateGame();
            game.PlayerEntity.Tags[Tag.Resources] = 3;
            game.PlayerEntity.Tags[Tag.Used] = 0;
            var host = new Canvas { Width = 1920, Height = 1080 };
            var plugin = new TavernOraclePlugin(hostCanvas: host, hdtGameProvider: () => game);
            try
            {
                plugin.OnLoad();
                plugin.OnUpdate();
                Assert.Equal(2, plugin.LastSnapshot.TavernMinions.Length);
                Assert.Equal(Visibility.Visible, plugin.BannerControl!.Visibility);
                Assert.Contains("买入从左数第", plugin.BannerControl.Message);
                Assert.Contains(plugin.BadgeControls, badge => badge.Visibility == Visibility.Visible);
                Assert.Contains(plugin.BadgeControls, badge => badge.Visibility == Visibility.Visible &&
                    badge.ViewModel!.Title.Contains("首选买入"));
                Assert.False(plugin.ContainerCanvas!.IsHitTestVisible);
                Assert.Equal(Visibility.Collapsed, plugin.LethalGauge!.Visibility);
                Assert.Contains("战力模拟未接入", plugin.BannerControl.Detail!);
                var renderPath = Environment.GetEnvironmentVariable("TAVERNORACLE_RENDER_PATH");
                if (!string.IsNullOrEmpty(renderPath))
                {
                    host.Background = new SolidColorBrush(Color.FromRgb(22, 27, 34));
                    host.Measure(new Size(1920, 1080));
                    host.Arrange(new Rect(0, 0, 1920, 1080));
                    host.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(host);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(renderPath);
                    encoder.Save(output);
                }

                game.PlayerEntity.Tags[Tag.Used] = 3;
                game.Opponent.Board.Clear();
                game.Opponent.Board.Add(new Entity { Id = 13, ZonePosition = 1, CardId = "BG_EX1_008" });
                plugin.OnUpdate();
                Assert.Equal(0, plugin.LastSnapshot.Gold.Available);
                Assert.Equal(13, plugin.LastSnapshot.TavernMinions[0].EntityId);
                Assert.DoesNotContain(plugin.LastActionResult.AsSpan().ToArray(), action =>
                    action.ActionType == TavernOracle.Decision.ActionGeneration.ActionType.Buy);

                game.IsBattlegroundsCombatPhase = true;
                plugin.OnUpdate();
                Assert.All(plugin.BadgeControls, badge => Assert.Equal(Visibility.Collapsed, badge.Visibility));
                Assert.Equal(Visibility.Collapsed, plugin.BannerControl.Visibility);
                game.IsBattlegroundsCombatPhase = false;
                game.IsInMenu = true;
                plugin.OnUpdate();
                Assert.False(plugin.StateWatcher.IsInGame);
            }
            finally { plugin.OnUnload(); }
        });
    }

    [Fact]
    public void MissingLiveHost_ShowsDiagnosticInsteadOfSilentEmptyOverlay()
    {
        RunSta(() =>
        {
            var plugin = new TavernOraclePlugin(hostCanvas: new Canvas(), hdtGameProvider: () => null);
            try
            {
                plugin.OnLoad();
                plugin.OnUpdate();
                Assert.Equal(Visibility.Visible, plugin.BannerControl!.Visibility);
                Assert.Contains("尚未连接", plugin.BannerControl.Message);
                Assert.All(plugin.BadgeControls, badge => Assert.Equal(Visibility.Collapsed, badge.Visibility));
            }
            finally { plugin.OnUnload(); }
        });
    }

    [Fact]
    public void NativeShop_PreservesSpellSlotButDoesNotRecommendItAsAMinion()
    {
        var game = CreateGame();
        game.PlayerEntity.Tags[Tag.Used] = 0;
        game.Opponent.Board.Add(new Entity
        {
            Id = 14, CardId = "BG_TEST_SPELL", ZonePosition = 3,
            IsMinion = false, IsBattlegroundsSpell = true
        });
        game.Opponent.Board.Add(new Entity { Id = 99, IsMinion = false });
        var snapshot = HdtEventConverter.ConvertFromHdtGame(game);
        Assert.Equal(3, snapshot.TavernMinions.Length);
        Assert.True(snapshot.TavernMinions[2].IsTavernSpell);
        var actions = new TavernOracle.Decision.ActionGeneration.CandidateActionGenerator().Generate(snapshot);
        Assert.DoesNotContain(actions.AsSpan().ToArray(), action => action.EntityId == 14);
        var result = new TavernOracle.Decision.Knapsack.KnapsackSolver().Solve(snapshot);
        Assert.DoesNotContain(result.AsSpan().ToArray(), action => action.EntityId == 14);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
