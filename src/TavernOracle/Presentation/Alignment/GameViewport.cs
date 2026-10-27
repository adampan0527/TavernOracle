using System;
using System.Windows;

namespace TavernOracle.Presentation.Alignment;

/// <summary>
/// 炉石传说战棋 16:9 核心活跃视口几何模型。
/// 封装自适应 Letterbox (上下黑边) / Pillarbox (左右黑边) 及多分辨率高 DPI 缩放变换。
/// </summary>
public readonly struct GameViewport : IEquatable<GameViewport>
{
    /// <summary>
    /// 经典炉石 16:9 基准宽高比 (1.7777777777777778)
    /// </summary>
    public const double StandardAspectRatio = 16.0 / 9.0;

    /// <summary>
    /// 标准参考分辨率宽度 (1920px)
    /// </summary>
    public const double ReferenceWidth = 1920.0;

    /// <summary>
    /// 标准参考分辨率高度 (1080px)
    /// </summary>
    public const double ReferenceHeight = 1080.0;

    /// <summary>
    /// 空视口实例
    /// </summary>
    public static readonly GameViewport Empty = new(Rect.Empty, Rect.Empty, Rect.Empty, Rect.Empty, Rect.Empty, 0, 0, 96, 1.0, 0.0, 0.0);

    /// <summary>
    /// 标准 1080P (100% DPI, 96 DPI) 默认视口实例
    /// </summary>
    public static readonly GameViewport Default1080P = Calculate(new Rect(0, 0, ReferenceWidth, ReferenceHeight), 96);

    #region Properties

    /// <summary>
    /// 目标窗口客户区屏幕绝对矩形 (原生物理像素)
    /// </summary>
    public Rect PhysicalClientBounds { get; }

    /// <summary>
    /// 16:9 活跃游戏画面的屏幕绝对矩形 (原生物理像素，剔除黑边)
    /// </summary>
    public Rect PhysicalBounds { get; }

    /// <summary>
    /// 16:9 活跃游戏画面的屏幕绝对矩形 (WPF 设备无关像素 DIP)
    /// </summary>
    public Rect DipBounds { get; }

    /// <summary>
    /// 相对窗口客户区左上角的 16:9 活跃视口矩形 (WPF 设备无关像素 DIP)
    /// </summary>
    public Rect RelativeDipBounds { get; }

    /// <summary>
    /// 相对窗口客户区左上角的 16:9 活跃视口矩形 (原生物理像素)
    /// </summary>
    public Rect RelativePhysicalBounds { get; }

    /// <summary>
    /// 左右黑边单侧宽度 (原生物理像素，Pillarbox)
    /// </summary>
    public double PillarboxWidth { get; }

    /// <summary>
    /// 上下黑边单侧高度 (原生物理像素，Letterbox)
    /// </summary>
    public double LetterboxHeight { get; }

    /// <summary>
    /// 左右黑边单侧宽度 (WPF 设备无关像素 DIP)
    /// </summary>
    public double PillarboxWidthDip => DpiScale > 0 ? PillarboxWidth / DpiScale : 0;

    /// <summary>
    /// 上下黑边单侧高度 (WPF 设备无关像素 DIP)
    /// </summary>
    public double LetterboxHeightDip => DpiScale > 0 ? LetterboxHeight / DpiScale : 0;

    /// <summary>
    /// 是否存在左右黑边 (针对 21:9 超宽屏等)
    /// </summary>
    public bool HasPillarbox => PillarboxWidth > 0.5;

    /// <summary>
    /// 是否存在上下黑边 (针对 16:10、4:3 等宽高比)
    /// </summary>
    public bool HasLetterbox => LetterboxHeight > 0.5;

    /// <summary>
    /// 视口所在监视器的 DPI (例如 96, 120, 144, 192)
    /// </summary>
    public uint Dpi { get; }

    /// <summary>
    /// DPI 缩放比例因子 (例如 1.0, 1.25, 1.5, 2.0)
    /// </summary>
    public double DpiScale { get; }

    /// <summary>
    /// 客户区实际宽高比 (Width / Height)
    /// </summary>
    public double AspectRatio { get; }

    /// <summary>
    /// 相对 1080P 基准分辨率的缩放系数 (PhysicalBounds.Height / 1080.0)
    /// </summary>
    public double ScaleFactor { get; }

    /// <summary>
    /// 视口是否有效（非空且具备正宽高）
    /// </summary>
    public bool IsValid => !PhysicalBounds.IsEmpty && PhysicalBounds.Width > 0 && PhysicalBounds.Height > 0;

    #endregion

    #region Constructor

    private GameViewport(
        Rect physicalClientBounds,
        Rect physicalBounds,
        Rect dipBounds,
        Rect relativeDipBounds,
        Rect relativePhysicalBounds,
        double pillarboxWidth,
        double letterboxHeight,
        uint dpi,
        double dpiScale,
        double aspectRatio,
        double scaleFactor)
    {
        PhysicalClientBounds = physicalClientBounds;
        PhysicalBounds = physicalBounds;
        DipBounds = dipBounds;
        RelativeDipBounds = relativeDipBounds;
        RelativePhysicalBounds = relativePhysicalBounds;
        PillarboxWidth = pillarboxWidth;
        LetterboxHeight = letterboxHeight;
        Dpi = dpi;
        DpiScale = dpiScale;
        AspectRatio = aspectRatio;
        ScaleFactor = scaleFactor;
    }

    #endregion

    #region Factory & Calculation

    /// <summary>
    /// 根据客户区物理矩形与 DPI 计算自适应 16:9 活跃视口与黑边
    /// </summary>
    /// <param name="clientRectPhysical">窗口客户区屏幕物理矩形</param>
    /// <param name="dpi">当前屏幕 DPI (默认 96)</param>
    /// <returns>计算完备的视口几何模型</returns>
    public static GameViewport Calculate(Rect clientRectPhysical, uint dpi = 96)
    {
        if (clientRectPhysical.IsEmpty || clientRectPhysical.Width <= 0 || clientRectPhysical.Height <= 0)
        {
            return Empty;
        }

        double clientW = clientRectPhysical.Width;
        double clientH = clientRectPhysical.Height;
        double currentRatio = clientW / clientH;

        double pillarbox = 0.0;
        double letterbox = 0.0;
        double activeW = clientW;
        double activeH = clientH;

        const double ratioEpsilon = 1e-4;

        if (Math.Abs(currentRatio - StandardAspectRatio) <= ratioEpsilon)
        {
            // 完美匹配 16:9
            activeW = clientW;
            activeH = clientH;
        }
        else if (currentRatio > StandardAspectRatio)
        {
            // 比 16:9 更宽 (例如 21:9 超宽屏、32:9 带鱼屏) -> 左右居中，产生 Pillarbox 黑边
            activeH = clientH;
            activeW = clientH * StandardAspectRatio;
            pillarbox = (clientW - activeW) / 2.0;
        }
        else
        {
            // 比 16:9 更高 (例如 16:10、4:3) -> 上下居中，产生 Letterbox 黑边
            activeW = clientW;
            activeH = clientW / StandardAspectRatio;
            letterbox = (clientH - activeH) / 2.0;
        }

        double activeX = clientRectPhysical.X + pillarbox;
        double activeY = clientRectPhysical.Y + letterbox;

        var physicalBounds = new Rect(activeX, activeY, activeW, activeH);
        var relativePhysicalBounds = new Rect(pillarbox, letterbox, activeW, activeH);

        double dpiScale = Math.Max(0.5, (dpi > 0 ? dpi : 96) / 96.0);

        var dipBounds = new Rect(
            activeX / dpiScale,
            activeY / dpiScale,
            activeW / dpiScale,
            activeH / dpiScale);

        var relativeDipBounds = new Rect(
            pillarbox / dpiScale,
            letterbox / dpiScale,
            activeW / dpiScale,
            activeH / dpiScale);

        double scaleFactor = activeH / ReferenceHeight;

        return new GameViewport(
            physicalClientBounds: clientRectPhysical,
            physicalBounds: physicalBounds,
            dipBounds: dipBounds,
            relativeDipBounds: relativeDipBounds,
            relativePhysicalBounds: relativePhysicalBounds,
            pillarboxWidth: pillarbox,
            letterboxHeight: letterbox,
            dpi: dpi > 0 ? dpi : 96,
            dpiScale: dpiScale,
            aspectRatio: currentRatio,
            scaleFactor: scaleFactor);
    }

    #endregion

    #region Coordinate Transformations

    /// <summary>
    /// 将 16:9 视口内归一化坐标 (0.0~1.0, 0.0~1.0) 转换为指定坐标空间中的点
    /// </summary>
    /// <param name="normalizedX">归一化 X 轴比例 [0.0, 1.0]</param>
    /// <param name="normalizedY">归一化 Y 轴比例 [0.0, 1.0]</param>
    /// <param name="targetSpace">目标坐标空间</param>
    /// <returns>目标空间内的绝对坐标点</returns>
    public Point NormalizedToPoint(double normalizedX, double normalizedY, CoordinateSpace targetSpace = CoordinateSpace.RelativeDip)
    {
        return targetSpace switch
        {
            CoordinateSpace.ViewportRelativeDip => new Point(
                normalizedX * DipBounds.Width,
                normalizedY * DipBounds.Height),

            CoordinateSpace.RelativeDip => new Point(
                RelativeDipBounds.X + normalizedX * RelativeDipBounds.Width,
                RelativeDipBounds.Y + normalizedY * RelativeDipBounds.Height),

            CoordinateSpace.ScreenDip => new Point(
                DipBounds.X + normalizedX * DipBounds.Width,
                DipBounds.Y + normalizedY * DipBounds.Height),

            CoordinateSpace.ScreenPhysical => new Point(
                PhysicalBounds.X + normalizedX * PhysicalBounds.Width,
                PhysicalBounds.Y + normalizedY * PhysicalBounds.Height),

            CoordinateSpace.RelativePhysical => new Point(
                RelativePhysicalBounds.X + normalizedX * RelativePhysicalBounds.Width,
                RelativePhysicalBounds.Y + normalizedY * RelativePhysicalBounds.Height),

            CoordinateSpace.ViewportRelativePhysical => new Point(
                normalizedX * PhysicalBounds.Width,
                normalizedY * PhysicalBounds.Height),

            _ => new Point(
                RelativeDipBounds.X + normalizedX * RelativeDipBounds.Width,
                RelativeDipBounds.Y + normalizedY * RelativeDipBounds.Height)
        };
    }

    /// <summary>
    /// 将归一化矩形转换为指定坐标空间内的绝对矩形
    /// </summary>
    public Rect NormalizedToRect(
        double normX,
        double normY,
        double normWidth,
        double normHeight,
        CoordinateSpace targetSpace = CoordinateSpace.RelativeDip)
    {
        var origin = NormalizedToPoint(normX, normY, targetSpace);

        double width;
        double height;

        if (targetSpace is CoordinateSpace.ScreenPhysical or CoordinateSpace.RelativePhysical or CoordinateSpace.ViewportRelativePhysical)
        {
            width = normWidth * PhysicalBounds.Width;
            height = normHeight * PhysicalBounds.Height;
        }
        else
        {
            width = normWidth * DipBounds.Width;
            height = normHeight * DipBounds.Height;
        }

        return new Rect(origin.X, origin.Y, width, height);
    }

    #endregion

    #region Equality & HashCode

    public bool Equals(GameViewport other)
    {
        return PhysicalBounds.Equals(other.PhysicalBounds) &&
               PhysicalClientBounds.Equals(other.PhysicalClientBounds) &&
               Dpi == other.Dpi;
    }

    public override bool Equals(object? obj) => obj is GameViewport other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(PhysicalBounds, PhysicalClientBounds, Dpi);

    public static bool operator ==(GameViewport left, GameViewport right) => left.Equals(right);

    public static bool operator !=(GameViewport left, GameViewport right) => !left.Equals(right);

    #endregion
}
