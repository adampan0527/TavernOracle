using System;
using System.Windows;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 随从站位锚点朝向枚举
/// </summary>
public enum AnchorPlacement
{
    /// <summary>
    /// 卡面上方（微标与站位提示）
    /// </summary>
    Top = 0,

    /// <summary>
    /// 卡面几何中心
    /// </summary>
    Center = 1,

    /// <summary>
    /// 卡面下方（属性身材或底部标识）
    /// </summary>
    Bottom = 2
}

/// <summary>
/// 炉石战棋卡面、顶部横条与仪表盘自适应锚点几何计算器。
/// 严格保证在 1080P、2K、4K、超宽屏（21:9）、16:10 及高 DPI 缩放下，
/// 坐标推导与吸附对齐误差严格 &lt; 2px。
/// </summary>
public sealed class BattlegroundsAnchorCalculator
{
    #region Reference Geometry Constants (Normalized to 16:9 Viewport)

    /// <summary>
    /// 1080P 基准随从卡牌宽度 (138px / 1920px = 0.071875)
    /// </summary>
    public const double CardWidthNormalized = 138.0 / 1920.0;

    /// <summary>
    /// 1080P 基准随从卡牌高度 (184px / 1080px = 0.17037037037037037)
    /// </summary>
    public const double CardHeightNormalized = 184.0 / 1080.0;

    /// <summary>
    /// 1080P 基准随从卡位水平间距 (152px / 1920px = 0.07916666666666666)
    /// </summary>
    public const double SlotSpacingNormalized = 152.0 / 1920.0;

    /// <summary>
    /// 酒馆在售区随从行中心垂直位置归一化比例 (419.04px / 1080px = 0.388)
    /// </summary>
    public const double TavernRowCenterYNormalized = 0.388;

    /// <summary>
    /// 我方战场随从行中心垂直位置归一化比例 (653.4px / 1080px = 0.605)
    /// </summary>
    public const double FriendlyRowCenterYNormalized = 0.605;

    /// <summary>
    /// 规范要求的随从卡面头顶微标默认垂直偏移距离 (15px)
    /// </summary>
    public const double DefaultBadgeOffsetY = 15.0;

    /// <summary>
    /// 顶部决策横条默认距视口顶部边距 (20px)
    /// </summary>
    public const double DefaultBannerTopMargin = 20.0;

    /// <summary>
    /// 战力仪表盘默认距视口左边距 (30px)
    /// </summary>
    public const double DefaultGaugeLeftMargin = 30.0;

    /// <summary>
    /// 战力仪表盘默认距视口顶边距 (40px)
    /// </summary>
    public const double DefaultGaugeTopMargin = 40.0;

    #endregion

    private readonly IGameWindowTracker? _tracker;
    private GameViewport _explicitViewport = GameViewport.Default1080P;

    #region Constructors

    /// <summary>
    /// 使用标准 1080P (100% DPI) 默认视口初始化锚点计算器
    /// </summary>
    public BattlegroundsAnchorCalculator()
    {
        _explicitViewport = GameViewport.Default1080P;
    }

    /// <summary>
    /// 使用指定的视口模型初始化锚点计算器
    /// </summary>
    public BattlegroundsAnchorCalculator(GameViewport viewport)
    {
        _explicitViewport = viewport.IsValid ? viewport : GameViewport.Default1080P;
    }

    /// <summary>
    /// 绑定至实时窗口追踪器，随游戏窗口动态吸附更新
    /// </summary>
    public BattlegroundsAnchorCalculator(IGameWindowTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    #endregion

    #region Properties

    /// <summary>
    /// 当前活跃计算所采用的视口模型
    /// </summary>
    public GameViewport Viewport
    {
        get
        {
            if (_tracker != null && _tracker.Viewport.IsValid)
            {
                return _tracker.Viewport;
            }

            return _explicitViewport;
        }
        set
        {
            _explicitViewport = value.IsValid ? value : GameViewport.Default1080P;
        }
    }

    #endregion

    #region Tavern Shop Minions (酒馆在售区随从 1~7 号卡位)

    /// <summary>
    /// 获取酒馆在售区指定卡位随从的完整矩形包围盒
    /// </summary>
    /// <param name="slotIndex">卡位索引 (1-based, 1 为最左，7 为最右)</param>
    /// <param name="totalMinions">当前在售随从总数 (默认 7)</param>
    /// <param name="space">目标坐标空间 (默认 RelativeDip)</param>
    public Rect GetTavernMinionBounds(int slotIndex, int totalMinions = 7, CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        ValidateSlotIndex(slotIndex, totalMinions);
        double normCenterX = CalculateNormalizedSlotCenterX(slotIndex, totalMinions);
        double normTopY = TavernRowCenterYNormalized - (CardHeightNormalized / 2.0);
        double normLeftX = normCenterX - (CardWidthNormalized / 2.0);

        return Viewport.NormalizedToRect(normLeftX, normTopY, CardWidthNormalized, CardHeightNormalized, space);
    }

    /// <summary>
    /// 获取酒馆在售区随从卡面顶部居中点
    /// </summary>
    public Point GetTavernMinionTopCenter(int slotIndex, int totalMinions = 7, CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        ValidateSlotIndex(slotIndex, totalMinions);
        double normCenterX = CalculateNormalizedSlotCenterX(slotIndex, totalMinions);
        double normTopY = TavernRowCenterYNormalized - (CardHeightNormalized / 2.0);

        return Viewport.NormalizedToPoint(normCenterX, normTopY, space);
    }

    /// <summary>
    /// 精确计算酒馆在售区随从（1~7 号卡位）卡面上方居中 15px 的微标挂载坐标点
    /// </summary>
    /// <param name="slotIndex">卡位索引 (1-based, 1 为最左)</param>
    /// <param name="totalMinions">在售随从总数 (默认 7)</param>
    /// <param name="badgeOffsetY">垂直偏移距离 (规范要求严格 15px)</param>
    /// <param name="space">目标坐标空间</param>
    /// <returns>微标挂载锚定点坐标 (X 为水平居中，Y 为卡面顶端上方 offset 处)</returns>
    public Point GetTavernMinionBadgeAnchor(
        int slotIndex,
        int totalMinions = 7,
        double badgeOffsetY = DefaultBadgeOffsetY,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        Point topCenter = GetTavernMinionTopCenter(slotIndex, totalMinions, space);
        double effectiveOffset = GetEffectiveOffset(badgeOffsetY, space);
        return new Point(topCenter.X, topCenter.Y - effectiveOffset);
    }

    /// <summary>
    /// 精确计算微标控件放置于随从卡面上方居中 15px 处的左上角 (Left, Top) 布局坐标
    /// </summary>
    /// <param name="slotIndex">卡位索引 (1-based)</param>
    /// <param name="badgeSize">微标控件的测算尺寸</param>
    /// <param name="totalMinions">在售随从总数 (默认 7)</param>
    /// <param name="badgeOffsetY">垂直偏移距离 (默认 15px)</param>
    /// <param name="space">目标坐标空间</param>
    /// <returns>微标控件放置的 (Left, Top) 坐标</returns>
    public Point GetTavernMinionBadgePosition(
        int slotIndex,
        Size badgeSize,
        int totalMinions = 7,
        double badgeOffsetY = DefaultBadgeOffsetY,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        Point topCenter = GetTavernMinionTopCenter(slotIndex, totalMinions, space);
        double effectiveOffset = GetEffectiveOffset(badgeOffsetY, space);

        double left = topCenter.X - (badgeSize.Width / 2.0);
        double top = topCenter.Y - effectiveOffset - badgeSize.Height;
        return new Point(left, top);
    }

    /// <summary>
    /// 精确获取微标在指定随从正上方居中 15px 处的完整包围盒
    /// </summary>
    public Rect GetTavernMinionBadgeBounds(
        int slotIndex,
        Size badgeSize,
        int totalMinions = 7,
        double badgeOffsetY = DefaultBadgeOffsetY,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        Point pos = GetTavernMinionBadgePosition(slotIndex, badgeSize, totalMinions, badgeOffsetY, space);
        return new Rect(pos.X, pos.Y, badgeSize.Width, badgeSize.Height);
    }

    #endregion

    #region Friendly Board Minions (我方战场随从 1~7 号卡位)

    /// <summary>
    /// 获取我方战场随从指定卡位的完整矩形包围盒
    /// </summary>
    public Rect GetFriendlyMinionBounds(int slotIndex, int totalMinions = 7, CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        ValidateSlotIndex(slotIndex, totalMinions);
        double normCenterX = CalculateNormalizedSlotCenterX(slotIndex, totalMinions);
        double normTopY = FriendlyRowCenterYNormalized - (CardHeightNormalized / 2.0);
        double normLeftX = normCenterX - (CardWidthNormalized / 2.0);

        return Viewport.NormalizedToRect(normLeftX, normTopY, CardWidthNormalized, CardHeightNormalized, space);
    }

    /// <summary>
    /// 获取我方战场随从几何中心点
    /// </summary>
    public Point GetFriendlyMinionCenter(int slotIndex, int totalMinions = 7, CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        ValidateSlotIndex(slotIndex, totalMinions);
        double normCenterX = CalculateNormalizedSlotCenterX(slotIndex, totalMinions);
        return Viewport.NormalizedToPoint(normCenterX, FriendlyRowCenterYNormalized, space);
    }

    /// <summary>
    /// 获取我方战场随从卡面顶部居中点
    /// </summary>
    public Point GetFriendlyMinionTopCenter(int slotIndex, int totalMinions = 7, CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        ValidateSlotIndex(slotIndex, totalMinions);
        double normCenterX = CalculateNormalizedSlotCenterX(slotIndex, totalMinions);
        double normTopY = FriendlyRowCenterYNormalized - (CardHeightNormalized / 2.0);
        return Viewport.NormalizedToPoint(normCenterX, normTopY, space);
    }

    /// <summary>
    /// 精确计算我方战场随从（1~7 号卡位）的站位指示锚点坐标
    /// </summary>
    /// <param name="slotIndex">卡位索引 (1-based, 1 为最左)</param>
    /// <param name="totalMinions">战场随从总数 (默认 7)</param>
    /// <param name="placement">锚点方位 (Top/Center/Bottom)</param>
    /// <param name="offset">相对卡面边界的外延间距 (默认 15px)</param>
    /// <param name="space">目标坐标空间</param>
    public Point GetFriendlyMinionAnchor(
        int slotIndex,
        int totalMinions = 7,
        AnchorPlacement placement = AnchorPlacement.Top,
        double offset = DefaultBadgeOffsetY,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        ValidateSlotIndex(slotIndex, totalMinions);
        double normCenterX = CalculateNormalizedSlotCenterX(slotIndex, totalMinions);
        double effectiveOffset = GetEffectiveOffset(offset, space);

        return placement switch
        {
            AnchorPlacement.Top => new Point(
                GetFriendlyMinionTopCenter(slotIndex, totalMinions, space).X,
                GetFriendlyMinionTopCenter(slotIndex, totalMinions, space).Y - effectiveOffset),

            AnchorPlacement.Center => GetFriendlyMinionCenter(slotIndex, totalMinions, space),

            AnchorPlacement.Bottom => new Point(
                GetFriendlyMinionTopCenter(slotIndex, totalMinions, space).X,
                GetFriendlyMinionBounds(slotIndex, totalMinions, space).Bottom + effectiveOffset),

            _ => GetFriendlyMinionTopCenter(slotIndex, totalMinions, space)
        };
    }

    /// <summary>
    /// 计算站位插入指示锚点（用于在两张随从之间或边缘指示插入位置）
    /// </summary>
    /// <param name="targetSlotIndex">目标插入槽位 (1 表示插入至最左侧，totalMinions+1 表示最右侧)</param>
    /// <param name="currentCount">当前已有随从数量</param>
    /// <param name="space">目标坐标空间</param>
    public Point GetFriendlySlotInsertAnchor(int targetSlotIndex, int currentCount, CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        int safeCount = MathEx.Clamp(currentCount, 1, 7);
        double normX;

        if (targetSlotIndex <= 1)
        {
            normX = CalculateNormalizedSlotCenterX(1, safeCount) - (SlotSpacingNormalized / 2.0);
        }
        else if (targetSlotIndex > safeCount)
        {
            normX = CalculateNormalizedSlotCenterX(safeCount, safeCount) + (SlotSpacingNormalized / 2.0);
        }
        else
        {
            double leftCenter = CalculateNormalizedSlotCenterX(targetSlotIndex - 1, safeCount);
            double rightCenter = CalculateNormalizedSlotCenterX(targetSlotIndex, safeCount);
            normX = (leftCenter + rightCenter) / 2.0;
        }

        return Viewport.NormalizedToPoint(normX, FriendlyRowCenterYNormalized, space);
    }

    #endregion

    #region Top Decision Banner (顶部决策横条自适应吸附)

    /// <summary>
    /// 精确计算顶部决策横条（Banner）自适应居中吸附的 (Left, Top) 布局坐标
    /// </summary>
    /// <param name="bannerWidth">横条当前测算或自适应宽度</param>
    /// <param name="topMargin">距 16:9 视口顶部的边距 (默认 20px)</param>
    /// <param name="space">目标坐标空间 (默认 RelativeDip)</param>
    public Point GetDecisionBannerPosition(
        double bannerWidth,
        double topMargin = DefaultBannerTopMargin,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        double viewportW;
        double originX;
        double originY;
        double effectiveTopMargin = GetEffectiveOffset(topMargin, space);

        switch (space)
        {
            case CoordinateSpace.ViewportRelativeDip:
                viewportW = Viewport.DipBounds.Width;
                originX = 0;
                originY = 0;
                break;

            case CoordinateSpace.RelativeDip:
                viewportW = Viewport.RelativeDipBounds.Width;
                originX = Viewport.RelativeDipBounds.X;
                originY = Viewport.RelativeDipBounds.Y;
                break;

            case CoordinateSpace.ScreenDip:
                viewportW = Viewport.DipBounds.Width;
                originX = Viewport.DipBounds.X;
                originY = Viewport.DipBounds.Y;
                break;

            case CoordinateSpace.ScreenPhysical:
                viewportW = Viewport.PhysicalBounds.Width;
                originX = Viewport.PhysicalBounds.X;
                originY = Viewport.PhysicalBounds.Y;
                break;

            case CoordinateSpace.RelativePhysical:
                viewportW = Viewport.RelativePhysicalBounds.Width;
                originX = Viewport.RelativePhysicalBounds.X;
                originY = Viewport.RelativePhysicalBounds.Y;
                break;

            case CoordinateSpace.ViewportRelativePhysical:
                viewportW = Viewport.PhysicalBounds.Width;
                originX = 0;
                originY = 0;
                break;

            default:
                viewportW = Viewport.RelativeDipBounds.Width;
                originX = Viewport.RelativeDipBounds.X;
                originY = Viewport.RelativeDipBounds.Y;
                break;
        }

        double left = originX + (viewportW - bannerWidth) / 2.0;
        double top = originY + effectiveTopMargin;
        return new Point(left, top);
    }

    /// <summary>
    /// 精确计算顶部决策横条（Banner）在指定坐标空间的完整矩形包围盒
    /// </summary>
    public Rect GetDecisionBannerBounds(
        double bannerWidth,
        double bannerHeight = 44.0,
        double topMargin = DefaultBannerTopMargin,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        Point pos = GetDecisionBannerPosition(bannerWidth, topMargin, space);
        return new Rect(pos.X, pos.Y, bannerWidth, bannerHeight);
    }

    #endregion

    #region Lethal Gauge (胜率与暴毙概率仪表盘自适应吸附)

    /// <summary>
    /// 精确计算战力与暴毙仪表盘（Gauge）自适应吸附的 (Left, Top) 布局坐标（默认视口左上角）
    /// </summary>
    /// <param name="gaugeWidth">仪表盘宽度 (默认 260px)</param>
    /// <param name="leftMargin">距 16:9 视口左侧边距 (默认 30px)</param>
    /// <param name="topMargin">距 16:9 视口顶部边距 (默认 40px)</param>
    /// <param name="space">目标坐标空间</param>
    public Point GetLethalGaugePosition(
        double gaugeWidth = 260.0,
        double leftMargin = DefaultGaugeLeftMargin,
        double topMargin = DefaultGaugeTopMargin,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        double originX;
        double originY;
        double effectiveLeftMargin = GetEffectiveOffset(leftMargin, space);
        double effectiveTopMargin = GetEffectiveOffset(topMargin, space);

        switch (space)
        {
            case CoordinateSpace.ViewportRelativeDip:
                originX = 0;
                originY = 0;
                break;

            case CoordinateSpace.RelativeDip:
                originX = Viewport.RelativeDipBounds.X;
                originY = Viewport.RelativeDipBounds.Y;
                break;

            case CoordinateSpace.ScreenDip:
                originX = Viewport.DipBounds.X;
                originY = Viewport.DipBounds.Y;
                break;

            case CoordinateSpace.ScreenPhysical:
                originX = Viewport.PhysicalBounds.X;
                originY = Viewport.PhysicalBounds.Y;
                break;

            case CoordinateSpace.RelativePhysical:
                originX = Viewport.RelativePhysicalBounds.X;
                originY = Viewport.RelativePhysicalBounds.Y;
                break;

            case CoordinateSpace.ViewportRelativePhysical:
                originX = 0;
                originY = 0;
                break;

            default:
                originX = Viewport.RelativeDipBounds.X;
                originY = Viewport.RelativeDipBounds.Y;
                break;
        }

        double left = originX + effectiveLeftMargin;
        double top = originY + effectiveTopMargin;
        return new Point(left, top);
    }

    /// <summary>
    /// 精确计算仪表盘在视口右上角吸附的 (Left, Top) 坐标
    /// </summary>
    public Point GetLethalGaugePositionTopRight(
        double gaugeWidth = 260.0,
        double rightMargin = DefaultGaugeLeftMargin,
        double topMargin = DefaultGaugeTopMargin,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        double viewportW;
        double originX;
        double originY;
        double effectiveRightMargin = GetEffectiveOffset(rightMargin, space);
        double effectiveTopMargin = GetEffectiveOffset(topMargin, space);

        switch (space)
        {
            case CoordinateSpace.ViewportRelativeDip:
                viewportW = Viewport.DipBounds.Width;
                originX = 0;
                originY = 0;
                break;

            case CoordinateSpace.RelativeDip:
                viewportW = Viewport.RelativeDipBounds.Width;
                originX = Viewport.RelativeDipBounds.X;
                originY = Viewport.RelativeDipBounds.Y;
                break;

            case CoordinateSpace.ScreenDip:
                viewportW = Viewport.DipBounds.Width;
                originX = Viewport.DipBounds.X;
                originY = Viewport.DipBounds.Y;
                break;

            case CoordinateSpace.ScreenPhysical:
                viewportW = Viewport.PhysicalBounds.Width;
                originX = Viewport.PhysicalBounds.X;
                originY = Viewport.PhysicalBounds.Y;
                break;

            case CoordinateSpace.RelativePhysical:
                viewportW = Viewport.RelativePhysicalBounds.Width;
                originX = Viewport.RelativePhysicalBounds.X;
                originY = Viewport.RelativePhysicalBounds.Y;
                break;

            case CoordinateSpace.ViewportRelativePhysical:
                viewportW = Viewport.PhysicalBounds.Width;
                originX = 0;
                originY = 0;
                break;

            default:
                viewportW = Viewport.RelativeDipBounds.Width;
                originX = Viewport.RelativeDipBounds.X;
                originY = Viewport.RelativeDipBounds.Y;
                break;
        }

        double left = originX + viewportW - gaugeWidth - effectiveRightMargin;
        double top = originY + effectiveTopMargin;
        return new Point(left, top);
    }

    /// <summary>
    /// 精确计算战力仪表盘的完整矩形包围盒
    /// </summary>
    public Rect GetLethalGaugeBounds(
        double gaugeWidth = 260.0,
        double gaugeHeight = 64.0,
        double leftMargin = DefaultGaugeLeftMargin,
        double topMargin = DefaultGaugeTopMargin,
        CoordinateSpace space = CoordinateSpace.RelativeDip)
    {
        Point pos = GetLethalGaugePosition(gaugeWidth, leftMargin, topMargin, space);
        return new Rect(pos.X, pos.Y, gaugeWidth, gaugeHeight);
    }

    #endregion

    #region Internal Coordinate Math Helpers

    /// <summary>
    /// 计算指定随从卡位的归一化水平中心坐标 (X 轴比例 [0.0, 1.0])
    /// </summary>
    public static double CalculateNormalizedSlotCenterX(int slotIndex, int totalMinions)
    {
        int safeCount = MathEx.Clamp(totalMinions, 1, 7);
        int safeIndex = MathEx.Clamp(slotIndex, 1, safeCount);

        // 中心对齐公式：以 0.5 为对称轴，按卡位间距均匀向两侧展开
        // delta = slotIndex - (totalMinions + 1) / 2.0
        double centerOffsetUnits = safeIndex - ((safeCount + 1) / 2.0);
        return 0.5 + (centerOffsetUnits * SlotSpacingNormalized);
    }

    private static void ValidateSlotIndex(int slotIndex, int totalMinions)
    {
        if (slotIndex < 1 || slotIndex > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "随从卡位索引必须在 1 至 7 之间。");
        }

        if (totalMinions < 1 || totalMinions > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalMinions),
                totalMinions,
                "随从总数必须在 1 至 7 之间。");
        }

        if (slotIndex > totalMinions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                $"卡位索引 ({slotIndex}) 不能超过随从总数 ({totalMinions})。");
        }
    }

    private double GetEffectiveOffset(double baseDipOffset, CoordinateSpace space)
    {
        if (space is CoordinateSpace.ScreenPhysical or CoordinateSpace.RelativePhysical or CoordinateSpace.ViewportRelativePhysical)
        {
            return baseDipOffset * Viewport.DpiScale;
        }

        return baseDipOffset;
    }

    #endregion
}
