using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TavernOracle.Presentation.Alignment;
using TavernOracle.Presentation.Overlay;
using Xunit;
using DpiChangedEventArgs = TavernOracle.Presentation.Alignment.DpiChangedEventArgs;

namespace TavernOracle.Tests.Presentation;

/// <summary>
/// 游戏窗口吸附与坐标变换对齐单元测试套件 (T19)。
/// 覆盖：
/// 1. 1920x1080 (100% DPI)、2560x1440 (125% DPI)、3840x2160 (150% DPI)、1280x720 典型分辨率下的卡位坐标推导；
/// 2. 16:9 经典、16:10、21:9 超宽屏黑边自适应 Letterbox / Pillarbox 计算；
/// 3. 酒馆在售区随从（1~7号卡位）卡面上方居中 15px 微标挂载点精度断言（误差严格 &lt; 2px）；
/// 4. 我方战场随从（1~7号卡位）站位指示锚点精度断言；
/// 5. 顶部决策横条 (Banner) 与仪表盘 (Gauge) 吸附对齐断言；
/// 6. 窗口移动、尺寸调整与 DPI 变更事件准确派发；
/// 7. 与 TransparentOverlayBase 的自动吸附集成联动。
/// </summary>
public class WindowAlignmentTests
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

    #region Mock Window Position Provider

    private sealed class MockWindowPositionProvider : IWindowPositionProvider
    {
        public IntPtr Handle { get; set; } = new IntPtr(0x12345);
        public RECT WindowRect { get; set; } = new RECT(100, 100, 2020, 1180);
        public RECT ClientRect { get; set; } = new RECT(0, 0, 1920, 1080);
        public POINT ClientScreenOrigin { get; set; } = new POINT(100, 100);
        public uint Dpi { get; set; } = 96;

        public IntPtr FindHearthstoneWindow() => Handle;

        public bool GetWindowRect(IntPtr hWnd, out RECT rect)
        {
            rect = WindowRect;
            return true;
        }

        public bool GetClientRect(IntPtr hWnd, out RECT rect)
        {
            rect = ClientRect;
            return true;
        }

        public bool ClientToScreen(IntPtr hWnd, ref POINT point)
        {
            point = new POINT(ClientScreenOrigin.X + point.X, ClientScreenOrigin.Y + point.Y);
            return true;
        }

        public uint GetDpiForWindow(IntPtr hWnd) => Dpi;
    }

    #endregion

    #region 1. Typical Resolutions & High-DPI Coordinate Derivation (< 2px Error)

    [Fact]
    public void Resolution_1080P_100DPI_TavernMinions_DerivesExactCoordinates()
    {
        // 1920x1080, 100% DPI (96 DPI)
        var clientRect = new Rect(0, 0, 1920, 1080);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        Assert.False(viewport.HasLetterbox);
        Assert.False(viewport.HasPillarbox);
        Assert.Equal(1.0, viewport.DpiScale);
        Assert.Equal(1920, viewport.DipBounds.Width);
        Assert.Equal(1080, viewport.DipBounds.Height);

        // 验证 1~7 号卡位酒馆随从
        double expectedCenterX4 = 960.0; // 第 4 卡位严格居中
        double expectedPitch = 152.0;

        for (int slot = 1; slot <= 7; slot++)
        {
            Rect bounds = calc.GetTavernMinionBounds(slot, 7);
            Point topCenter = calc.GetTavernMinionTopCenter(slot, 7);
            Point badgeAnchor = calc.GetTavernMinionBadgeAnchor(slot, 7, badgeOffsetY: 15.0);

            double expectedSlotCenterX = expectedCenterX4 + (slot - 4) * expectedPitch;

            // 断言卡牌中心与理论值误差 < 2px (实际上 < 0.001px)
            Assert.True(Math.Abs(bounds.X + bounds.Width / 2.0 - expectedSlotCenterX) < 2.0);
            Assert.True(Math.Abs(topCenter.X - expectedSlotCenterX) < 2.0);
            Assert.True(Math.Abs(badgeAnchor.X - expectedSlotCenterX) < 2.0);

            // 断言微标挂载点在卡面正上方严格 15px 处 (误差 < 2px)
            double distanceToTop = topCenter.Y - badgeAnchor.Y;
            Assert.True(Math.Abs(distanceToTop - 15.0) < 2.0);

            // 测试给定微标控件尺寸 (例如 120x32) 时的放置位置
            var badgeSize = new Size(120, 32);
            Point badgePos = calc.GetTavernMinionBadgePosition(slot, badgeSize, 7, badgeOffsetY: 15.0);
            Rect badgeBounds = calc.GetTavernMinionBadgeBounds(slot, badgeSize, 7, badgeOffsetY: 15.0);

            // 验证微标水平居中
            double badgeCenterX = badgePos.X + badgeSize.Width / 2.0;
            Assert.True(Math.Abs(badgeCenterX - expectedSlotCenterX) < 2.0);

            // 验证微标底端与随从顶端距离为 15px
            double badgeBottom = badgePos.Y + badgeSize.Height;
            Assert.True(Math.Abs(topCenter.Y - badgeBottom - 15.0) < 2.0);
            Assert.Equal(badgePos.X, badgeBounds.X);
            Assert.Equal(badgePos.Y, badgeBounds.Y);
        }
    }

    [Fact]
    public void Resolution_2K_1440P_125DPI_DerivesAccurateCoordinates_Under2px()
    {
        // 2560x1440, 125% DPI (120 DPI, scale = 1.25)
        var clientRect = new Rect(0, 0, 2560, 1440);
        var viewport = GameViewport.Calculate(clientRect, dpi: 120);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        Assert.Equal(1.25, viewport.DpiScale);
        Assert.False(viewport.HasLetterbox);
        Assert.False(viewport.HasPillarbox);

        // 验证 DIP 视口尺寸: 2560 / 1.25 = 2048, 1440 / 1.25 = 1152
        Assert.Equal(2048.0, viewport.DipBounds.Width);
        Assert.Equal(1152.0, viewport.DipBounds.Height);

        // 物理像素下的槽位中心理论值
        double physicalScale = 1440.0 / 1080.0; // 4/3
        double expectedPhysicalPitch = 152.0 * physicalScale;
        double expectedPhysicalCenter4 = 2560.0 / 2.0; // 1280

        for (int slot = 1; slot <= 7; slot++)
        {
            double expectedPhysCenterX = expectedPhysicalCenter4 + (slot - 4) * expectedPhysicalPitch;
            double expectedDipCenterX = expectedPhysCenterX / 1.25;

            // 在物理坐标空间校验
            Point physTopCenter = calc.GetTavernMinionTopCenter(slot, 7, CoordinateSpace.ScreenPhysical);
            Assert.True(Math.Abs(physTopCenter.X - expectedPhysCenterX) < 2.0,
                $"Slot {slot} physical X deviation {Math.Abs(physTopCenter.X - expectedPhysCenterX)} exceeds 2px");

            // 在 DIP 坐标空间校验
            Point dipTopCenter = calc.GetTavernMinionTopCenter(slot, 7, CoordinateSpace.RelativeDip);
            Assert.True(Math.Abs(dipTopCenter.X - expectedDipCenterX) < 2.0,
                $"Slot {slot} DIP X deviation {Math.Abs(dipTopCenter.X - expectedDipCenterX)} exceeds 2px");

            // 物理与 DIP 无损互转精度校验
            Point convertedPhys = viewport.NormalizedToPoint(
                BattlegroundsAnchorCalculator.CalculateNormalizedSlotCenterX(slot, 7),
                BattlegroundsAnchorCalculator.TavernRowCenterYNormalized,
                CoordinateSpace.ScreenPhysical);

            Point convertedDip = viewport.NormalizedToPoint(
                BattlegroundsAnchorCalculator.CalculateNormalizedSlotCenterX(slot, 7),
                BattlegroundsAnchorCalculator.TavernRowCenterYNormalized,
                CoordinateSpace.RelativeDip);

            Assert.True(Math.Abs(convertedPhys.X - convertedDip.X * 1.25) < 0.001);
            Assert.True(Math.Abs(convertedPhys.Y - convertedDip.Y * 1.25) < 0.001);
        }
    }

    [Fact]
    public void Resolution_4K_2160P_150DPI_DerivesAccurateCoordinates_Under2px()
    {
        // 3840x2160, 150% DPI (144 DPI, scale = 1.5)
        var clientRect = new Rect(0, 0, 3840, 2160);
        var viewport = GameViewport.Calculate(clientRect, dpi: 144);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        Assert.Equal(1.5, viewport.DpiScale);
        Assert.Equal(2.0, viewport.ScaleFactor); // 2160 / 1080 = 2.0
        Assert.Equal(2560.0, viewport.DipBounds.Width); // 3840 / 1.5 = 2560
        Assert.Equal(1440.0, viewport.DipBounds.Height); // 2160 / 1.5 = 1440

        double expectedPhysCenter4 = 3840.0 / 2.0; // 1920
        double expectedPhysPitch = 152.0 * 2.0; // 304

        for (int slot = 1; slot <= 7; slot++)
        {
            double expectedPhysCenterX = expectedPhysCenter4 + (slot - 4) * expectedPhysPitch;

            Point physTopCenter = calc.GetTavernMinionTopCenter(slot, 7, CoordinateSpace.ScreenPhysical);
            Assert.True(Math.Abs(physTopCenter.X - expectedPhysCenterX) < 2.0);

            // 验证头顶 15px 微标挂载在物理空间放大 1.5 倍（22.5px 物理）
            Point physBadgeAnchor = calc.GetTavernMinionBadgeAnchor(slot, 7, badgeOffsetY: 15.0, CoordinateSpace.ScreenPhysical);
            double physOffset = physTopCenter.Y - physBadgeAnchor.Y;
            Assert.True(Math.Abs(physOffset - 15.0 * 1.5) < 2.0);

            // 在 DIP 空间微标偏移保持 15 DIPs
            Point dipTopCenter = calc.GetTavernMinionTopCenter(slot, 7, CoordinateSpace.RelativeDip);
            Point dipBadgeAnchor = calc.GetTavernMinionBadgeAnchor(slot, 7, badgeOffsetY: 15.0, CoordinateSpace.RelativeDip);
            double dipOffset = dipTopCenter.Y - dipBadgeAnchor.Y;
            Assert.True(Math.Abs(dipOffset - 15.0) < 2.0);
        }
    }

    [Fact]
    public void Resolution_720P_100DPI_DerivesAccurateCoordinates_Under2px()
    {
        // 1280x720, 100% DPI (96 DPI)
        var clientRect = new Rect(0, 0, 1280, 720);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        double scale = 1280.0 / 1920.0; // 2/3
        double expectedCenter4 = 640.0;
        double expectedPitch = 152.0 * scale; // 101.3333

        for (int slot = 1; slot <= 7; slot++)
        {
            double expectedX = expectedCenter4 + (slot - 4) * expectedPitch;
            Point topCenter = calc.GetTavernMinionTopCenter(slot, 7);

            Assert.True(Math.Abs(topCenter.X - expectedX) < 2.0);
        }
    }

    #endregion

    #region 2. Aspect Ratio & Letterbox / Pillarbox Adaptation (< 2px Error)

    [Fact]
    public void AspectRatio_21x9_Ultrawide_AppliesPillarbox_CentersSymmetrically()
    {
        // 2560x1080 (21:9 超宽屏, 100% DPI)
        var clientRect = new Rect(0, 0, 2560, 1080);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        Assert.True(viewport.HasPillarbox);
        Assert.False(viewport.HasLetterbox);

        // 理论 16:9 宽度: 1080 * 16 / 9 = 1920
        // 单侧 Pillarbox: (2560 - 1920) / 2 = 320
        Assert.Equal(320.0, viewport.PillarboxWidth, 2);
        Assert.Equal(0.0, viewport.LetterboxHeight, 2);
        Assert.Equal(1920.0, viewport.PhysicalBounds.Width, 2);
        Assert.Equal(1080.0, viewport.PhysicalBounds.Height, 2);
        Assert.Equal(320.0, viewport.PhysicalBounds.X, 2);

        // 验证 16:9 画面的中心刚好对齐整个 2560 屏幕中心 (2560 / 2 = 1280)
        Point centerMinion = calc.GetTavernMinionTopCenter(4, 7, CoordinateSpace.RelativeDip);
        Assert.Equal(1280.0, centerMinion.X, 2);

        // 验证最左 (slot 1) 与最右 (slot 7) 距屏幕左右边沿严格对称
        Point slot1 = calc.GetTavernMinionTopCenter(1, 7, CoordinateSpace.RelativeDip);
        Point slot7 = calc.GetTavernMinionTopCenter(7, 7, CoordinateSpace.RelativeDip);

        double distLeft = slot1.X;
        double distRight = 2560.0 - slot7.X;
        Assert.True(Math.Abs(distLeft - distRight) < 2.0);
    }

    [Fact]
    public void AspectRatio_16x10_AppliesLetterbox_CentersVertically()
    {
        // 1920x1200 (16:10 比例, 100% DPI)
        var clientRect = new Rect(0, 0, 1920, 1200);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        Assert.False(viewport.HasPillarbox);
        Assert.True(viewport.HasLetterbox);

        // 理论 16:9 高度: 1920 * 9 / 16 = 1080
        // 单侧 Letterbox: (1200 - 1080) / 2 = 60
        Assert.Equal(0.0, viewport.PillarboxWidth, 2);
        Assert.Equal(60.0, viewport.LetterboxHeight, 2);
        Assert.Equal(1920.0, viewport.PhysicalBounds.Width, 2);
        Assert.Equal(1080.0, viewport.PhysicalBounds.Height, 2);
        Assert.Equal(60.0, viewport.PhysicalBounds.Y, 2);

        // 顶部横条吸附在 16:9 画面上方边距 20px 处，相对客户区 Y 坐标应为 60 + 20 = 80px
        Point bannerPos = calc.GetDecisionBannerPosition(bannerWidth: 800, topMargin: 20, CoordinateSpace.RelativeDip);
        Assert.Equal(80.0, bannerPos.Y, 2);
    }

    #endregion

    #region 3. Friendly Board Minions & Anchor Indicating

    [Fact]
    public void FriendlyBoardMinions_1to7Slots_CalculatesAnchorsAccurately()
    {
        var clientRect = new Rect(0, 0, 1920, 1080);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        for (int slot = 1; slot <= 7; slot++)
        {
            Rect bounds = calc.GetFriendlyMinionBounds(slot, 7);
            Point center = calc.GetFriendlyMinionCenter(slot, 7);
            Point topAnchor = calc.GetFriendlyMinionAnchor(slot, 7, AnchorPlacement.Top, offset: 15.0);
            Point bottomAnchor = calc.GetFriendlyMinionAnchor(slot, 7, AnchorPlacement.Bottom, offset: 15.0);

            // 几何对称性与中心对齐校验
            Assert.True(Math.Abs(bounds.X + bounds.Width / 2.0 - center.X) < 2.0);
            Assert.True(Math.Abs(bounds.Y + bounds.Height / 2.0 - center.Y) < 2.0);

            // 验证上方锚点在卡面顶部上方 15px
            Assert.True(Math.Abs(bounds.Top - topAnchor.Y - 15.0) < 2.0);

            // 验证下方锚点在卡面底部下方 15px
            Assert.True(Math.Abs(bottomAnchor.Y - bounds.Bottom - 15.0) < 2.0);
        }
    }

    [Fact]
    public void FriendlyBoardMinions_SlotInsertAnchors_PositionsBetweenMinions()
    {
        var clientRect = new Rect(0, 0, 1920, 1080);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        // 场景：场上有 3 只随从，计算插入到槽位 1、2、3、4 的位置
        Point insertAtLeft = calc.GetFriendlySlotInsertAnchor(1, currentCount: 3);
        Point insertBetween1And2 = calc.GetFriendlySlotInsertAnchor(2, currentCount: 3);
        Point insertBetween2And3 = calc.GetFriendlySlotInsertAnchor(3, currentCount: 3);
        Point insertAtRight = calc.GetFriendlySlotInsertAnchor(4, currentCount: 3);

        Point minion1 = calc.GetFriendlyMinionCenter(1, 3);
        Point minion2 = calc.GetFriendlyMinionCenter(2, 3);
        Point minion3 = calc.GetFriendlyMinionCenter(3, 3);

        // insertBetween1And2 必须在 minion1 和 minion2 之间严格居中
        double expectedMid12 = (minion1.X + minion2.X) / 2.0;
        Assert.True(Math.Abs(insertBetween1And2.X - expectedMid12) < 2.0);

        // insertBetween2And3 必须在 minion2 和 minion3 之间严格居中
        double expectedMid23 = (minion2.X + minion3.X) / 2.0;
        Assert.True(Math.Abs(insertBetween2And3.X - expectedMid23) < 2.0);

        // 插入点必须单调递增
        Assert.True(insertAtLeft.X < insertBetween1And2.X);
        Assert.True(insertBetween1And2.X < insertBetween2And3.X);
        Assert.True(insertBetween2And3.X < insertAtRight.X);
    }

    #endregion

    #region 4. Banner & Gauge Snapping Accuracy

    [Fact]
    public void DecisionBanner_SnappingCoordinates_CenteredAndAdaptive()
    {
        var clientRect = new Rect(100, 150, 1920, 1080);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        double bannerWidth = 800;
        Point relPos = calc.GetDecisionBannerPosition(bannerWidth, topMargin: 20, CoordinateSpace.RelativeDip);
        Rect relBounds = calc.GetDecisionBannerBounds(bannerWidth, bannerHeight: 44, topMargin: 20, CoordinateSpace.RelativeDip);

        // 水平严格居中: (1920 - 800) / 2 = 560
        Assert.Equal(560.0, relPos.X, 2);
        Assert.Equal(20.0, relPos.Y, 2);
        Assert.Equal(560.0, relBounds.X, 2);
        Assert.Equal(20.0, relBounds.Y, 2);

        // 屏幕绝对坐标校验: 100 + 560 = 660, 150 + 20 = 170
        Point screenPos = calc.GetDecisionBannerPosition(bannerWidth, topMargin: 20, CoordinateSpace.ScreenPhysical);
        Assert.Equal(660.0, screenPos.X, 2);
        Assert.Equal(170.0, screenPos.Y, 2);
    }

    [Fact]
    public void LethalGauge_SnappingCoordinates_TopLeftAndTopRight()
    {
        var clientRect = new Rect(50, 80, 1920, 1080);
        var viewport = GameViewport.Calculate(clientRect, dpi: 96);
        var calc = new BattlegroundsAnchorCalculator(viewport);

        // 左上角吸附
        Point topLeftPos = calc.GetLethalGaugePosition(gaugeWidth: 260, leftMargin: 30, topMargin: 40, CoordinateSpace.RelativeDip);
        Assert.Equal(30.0, topLeftPos.X, 2);
        Assert.Equal(40.0, topLeftPos.Y, 2);

        // 右上角吸附: 1920 - 260 - 30 = 1630
        Point topRightPos = calc.GetLethalGaugePositionTopRight(gaugeWidth: 260, rightMargin: 30, topMargin: 40, CoordinateSpace.RelativeDip);
        Assert.Equal(1630.0, topRightPos.X, 2);
        Assert.Equal(40.0, topRightPos.Y, 2);
    }

    #endregion

    #region 5. GameWindowTracker Events & DPI Changes

    [Fact]
    public void GameWindowTracker_MoveAndResize_DispatchesEventsAccurately()
    {
        var mockProvider = new MockWindowPositionProvider();
        using var tracker = new GameWindowTracker(mockProvider);

        var boundsEvents = new List<WindowBoundsChangedEventArgs>();
        var dpiEvents = new List<TavernOracle.Presentation.Alignment.DpiChangedEventArgs>();
        var viewportEvents = new List<GameViewportChangedEventArgs>();

        tracker.WindowBoundsChanged += (_, e) => boundsEvents.Add(e);
        tracker.DpiChanged += (_, e) => dpiEvents.Add(e);
        tracker.ViewportChanged += (_, e) => viewportEvents.Add(e);

        // 首次更新
        tracker.StartTracking(pollIntervalMs: 50);

        Assert.True(tracker.IsTracking);
        Assert.Equal(mockProvider.Handle, tracker.WindowHandle);
        Assert.Single(boundsEvents);
        Assert.Single(viewportEvents);

        // 模拟 1：仅移动窗口位置 (100, 100) -> (300, 400)，尺寸保持不变
        mockProvider.ClientScreenOrigin = new POINT(300, 400);
        mockProvider.WindowRect = new RECT(300, 400, 2220, 1480);
        tracker.UpdateNow();

        Assert.Equal(2, boundsEvents.Count);
        Assert.Equal(300, tracker.ClientRect.X);
        Assert.Equal(400, tracker.ClientRect.Y);
        Assert.Equal(1920, tracker.ClientRect.Width);
        Assert.Equal(1080, tracker.ClientRect.Height);

        // 模拟 2：无变化时不重复触发事件 (幂等性)
        tracker.UpdateNow();
        Assert.Equal(2, boundsEvents.Count);
        Assert.Empty(dpiEvents);

        // 模拟 3：调整分辨率到 2560x1440 且 DPI 升为 120 (125%)
        mockProvider.ClientRect = new RECT(0, 0, 2560, 1440);
        mockProvider.WindowRect = new RECT(300, 400, 2860, 1840);
        mockProvider.Dpi = 120;
        tracker.UpdateNow();

        Assert.Equal(3, boundsEvents.Count);
        Assert.Single(dpiEvents);
        Assert.Equal(120u, dpiEvents[0].NewDpi);
        Assert.Equal(1.25, dpiEvents[0].NewDpiScale);
        Assert.Equal(1.25, tracker.DpiScale);
        Assert.Equal(3, viewportEvents.Count);

        tracker.StopTracking();
        Assert.False(tracker.IsTracking);
    }

    #endregion

    #region 6. TransparentOverlay Snapping Integration (STA)

    [Fact]
    public void GameWindowTracker_AttachOverlay_SynchronizesBoundsAutomatically()
    {
        RunInSta(() =>
        {
            var mockProvider = new MockWindowPositionProvider
            {
                ClientScreenOrigin = new POINT(100, 150),
                ClientRect = new RECT(0, 0, 1920, 1080),
                Dpi = 96
            };

            using var tracker = new GameWindowTracker(mockProvider);
            using var overlay = new TransparentOverlayBase();

            tracker.StartTracking();
            tracker.AttachOverlay(overlay);

            // 验证 overlay 尺寸与位置自动同步至游戏客户区
            Assert.Equal(100.0, overlay.Left, 2);
            Assert.Equal(150.0, overlay.Top, 2);
            Assert.Equal(1920.0, overlay.Width, 2);
            Assert.Equal(1080.0, overlay.Height, 2);

            // 模拟窗口移动并放大至 2560x1440 @ 125% DPI
            mockProvider.ClientScreenOrigin = new POINT(200, 250);
            mockProvider.ClientRect = new RECT(0, 0, 2560, 1440);
            mockProvider.Dpi = 120;
            tracker.UpdateNow();

            // 验证以 DIP 为单位同步更新: Left=200/1.25=160, Top=250/1.25=200, W=2560/1.25=2048, H=1440/1.25=1152
            Assert.Equal(160.0, overlay.Left, 2);
            Assert.Equal(200.0, overlay.Top, 2);
            Assert.Equal(2048.0, overlay.Width, 2);
            Assert.Equal(1152.0, overlay.Height, 2);

            // 解除吸附
            tracker.DetachOverlay(overlay);
            Assert.DoesNotContain(overlay, tracker.AttachedOverlays);
        });
    }

    #endregion

    #region 7. Edge Cases & Validation

    [Fact]
    public void BattlegroundsAnchorCalculator_InvalidSlotIndex_ThrowsArgumentOutOfRangeException()
    {
        var calc = new BattlegroundsAnchorCalculator();

        Assert.Throws<ArgumentOutOfRangeException>(() => calc.GetTavernMinionBounds(0, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => calc.GetTavernMinionBounds(8, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => calc.GetTavernMinionBounds(5, 4)); // slot 5 > total 4
        Assert.Throws<ArgumentOutOfRangeException>(() => calc.GetFriendlyMinionBounds(-1, 7));
    }

    [Fact]
    public void GameViewport_EmptyOrZero_ReturnsEmptySafely()
    {
        var zeroRect = new Rect(0, 0, 0, 0);
        var vp = GameViewport.Calculate(zeroRect);

        Assert.False(vp.IsValid);
        Assert.True(vp.PhysicalBounds.IsEmpty);
    }

    #endregion
}
