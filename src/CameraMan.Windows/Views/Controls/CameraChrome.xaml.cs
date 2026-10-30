using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CameraMan.Windows.Camera;
using CameraMan.Windows.Models;
using CameraMan.Windows.Rendering;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Views.Controls;

/// <summary>
/// Draws the camera frame clipped/masked to the current shape, plus the outer border and shadow. Frames are pulled
/// from the capture buffer at display refresh rate; only the newest frame is used, so a slow UI never builds a queue.
/// </summary>
public partial class CameraChrome : UserControl
{
    public static readonly DependencyProperty FrameSourceProperty = DependencyProperty.Register(
        nameof(FrameSource),
        typeof(FrameBuffer),
        typeof(CameraChrome),
        new PropertyMetadata(null, OnFrameSourceChanged));

    private readonly DispatcherTimer _breathingTimer;
    private AppState? _state;
    private readonly byte[] _scratch = [];
    private byte[] _frameBytes = [];
    private WriteableBitmap? _bitmap;
    private long _lastVersion = -1;
    private double _breathingTime;
    private bool _renderHooked;

    public CameraChrome()
    {
        InitializeComponent();
        _breathingTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(66),
        };
        _breathingTimer.Tick += (_, _) =>
        {
            _breathingTime += 0.066;
            RebuildChrome();
            UpdateBorderAndShadow();
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        // Resizing the window (or changing the border/shadow padding, which also changes this
        // control's size) has to refresh the border and shadow outlines too, not just the surface.
        SizeChanged += (_, _) =>
        {
            RebuildChrome();
            UpdateBorderAndShadow();
        };
        DataContextChanged += OnDataContextChanged;
    }

    public FrameBuffer? FrameSource
    {
        get => (FrameBuffer?)GetValue(FrameSourceProperty);
        set => SetValue(FrameSourceProperty, value);
    }

    private static void OnFrameSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CameraChrome chrome)
        {
            chrome._lastVersion = -1;
            chrome._bitmap = null;
            chrome.Surface.Frame = null;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_state is not null)
        {
            _state.PropertyChanged -= OnStatePropertyChanged;
        }

        _state = e.NewValue as AppState;
        if (_state is not null)
        {
            _state.PropertyChanged += OnStatePropertyChanged;
        }

        RebuildChrome();
        UpdateBorderAndShadow();
        UpdateBreathingTimer();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_renderHooked)
        {
            CompositionTarget.Rendering += OnRendering;
            _renderHooked = true;
        }

        RebuildChrome();
        UpdateBorderAndShadow();
        UpdateBreathingTimer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_renderHooked)
        {
            CompositionTarget.Rendering -= OnRendering;
            _renderHooked = false;
        }

        _breathingTimer.Stop();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateBreathingTimer();
        if (IsVisible)
        {
            _lastVersion = -1;
        }
    }

    private void OnStatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AppState.ShapeType):
            case nameof(AppState.ActiveCornerRadius):
            case nameof(AppState.OrganicBlob):
            case nameof(AppState.SoftEdge):
            case nameof(AppState.OrganicBreathing):
            case nameof(AppState.ReduceMotion):
            case nameof(AppState.Scale):
            case nameof(AppState.OffsetX):
            case nameof(AppState.OffsetY):
            case nameof(AppState.FlipHorizontal):
            case nameof(AppState.FlipVertical):
                RebuildChrome();
                UpdateBorderAndShadow();
                UpdateBreathingTimer();
                break;
            case nameof(AppState.ShowBorder):
            case nameof(AppState.BorderWidth):
            case nameof(AppState.BorderColorHex):
            case nameof(AppState.BorderUseGradient):
            case nameof(AppState.BorderGradientStartHex):
            case nameof(AppState.BorderGradientEndHex):
            case nameof(AppState.ShowShadow):
            case nameof(AppState.BorderShadowRadius):
            case nameof(AppState.BorderShadowColorHex):
                UpdateBorderAndShadow();
                break;
            case nameof(AppState.CameraStatus):
                UpdateFrameVisibility();
                break;
        }
    }

    private void UpdateBreathingTimer()
    {
        var shouldBreathe = _state is not null &&
                            _state.ShapeType == ShapeType.Organic &&
                            _state.OrganicBreathing &&
                            !_state.ReduceMotion &&
                            IsVisible;
        if (shouldBreathe && !_breathingTimer.IsEnabled)
        {
            _breathingTimer.Start();
        }
        else if (!shouldBreathe && _breathingTimer.IsEnabled)
        {
            _breathingTimer.Stop();
            _breathingTime = 0;
            RebuildChrome();
        }
    }

    private void RebuildChrome()
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (_state is null || width < 2 || height < 2)
        {
            return;
        }

        var bounds = new Rect(0, 0, width, height);
        var geometry = ShapeGeometryFactory.Create(
            _state.ShapeType,
            bounds,
            _state.ActiveCornerRadius,
            _state.OrganicBlob,
            _breathingTime);

        Surface.Geometry = geometry;
        Surface.Zoom = _state.Scale;
        Surface.OffsetX = _state.OffsetX;
        Surface.OffsetY = _state.OffsetY;
        Surface.FlipHorizontal = _state.FlipHorizontal;
        Surface.FlipVertical = _state.FlipVertical;

        // The fade lives entirely inside the outline: alpha 0 at the border growing to 1 at
        // SoftEdge px inward, at a constant rate along the whole outline (see BuildSoftMask).
        // It is pushed as a drawing-context opacity mask instead of UIElement.OpacityMask so it
        // applies to the frame drawing itself, in the element's own coordinate space.
        Surface.MaskBrush = _state.SoftEdge > 0 ? BuildSoftMask(geometry, _state.SoftEdge) : null;

        Surface.InvalidateVisual();
    }

    /// <summary>
    /// Distance-field feather: rasterises the shape with a margin, measures every pixel's
    /// distance to the outline (two-pass chamfer transform) and maps it linearly to alpha, so
    /// the fade starts at zero exactly on the contour, grows with the pixel distance and keeps
    /// the same width along curves, corners and blob waves. The margin keeps outside pixels
    /// around the shape even when its outline runs along the window edge — without them the
    /// distance field would collapse into a hard edge exactly where the shape touches the
    /// window. The finished mask is cropped back to the element bounds.
    /// </summary>
    private Brush BuildSoftMask(Geometry geometry, double softEdge)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        // Half device resolution: the feather is linear, so bilinear upsampling reproduces it
        // exactly while keeping the distance transform cheap even at large sizes.
        const double fieldScale = 0.5;
        var gridPixelInDips = 1d / (dpi.DpiScaleX * fieldScale);
        var rampPixels = (float)(softEdge / gridPixelInDips);
        // Raster margin (grid px): empty pixels around the shape seed the distance field.
        const int padPx = 8;
        var windowPixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX * fieldScale));
        var windowPixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY * fieldScale));
        var pixelWidth = windowPixelWidth + padPx * 2;
        var pixelHeight = windowPixelHeight + padPx * 2;
        var padDips = padPx * gridPixelInDips;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(padDips, padDips));
            dc.DrawGeometry(Brushes.White, null, geometry);
            dc.Pop();
        }

        var coverage = new RenderTargetBitmap(
            pixelWidth, pixelHeight, 96 * dpi.DpiScaleX * fieldScale, 96 * dpi.DpiScaleY * fieldScale, PixelFormats.Pbgra32);
        coverage.Render(visual);
        coverage.Freeze();

        var stride = pixelWidth * 4;
        var pixels = new byte[stride * pixelHeight];
        coverage.CopyPixels(pixels, stride, 0);

        SoftMaskField.Apply(pixels, pixelWidth, pixelHeight, rampPixels);

        var mask = BitmapSource.Create(
            pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        mask.Freeze();

        var cropped = new CroppedBitmap(mask, new Int32Rect(padPx, padPx, windowPixelWidth, windowPixelHeight));
        cropped.Freeze();

        var brush = new ImageBrush(cropped)
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            // The camera frame is drawn cover-fit and overflows the element, so a relative brush
            // viewport would be sized by that overflowing drawing box and the fade would slide off
            // the shape's sides. An absolute viewport pinned to the layout box keeps the feather
            // exactly on the outline, independent of how the frame overflows.
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, ActualWidth, ActualHeight),
            TileMode = TileMode.None,
        };
        brush.Freeze();
        return brush;
    }

    private void UpdateBorderAndShadow()
    {
        if (_state is null)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 2 || height < 2)
        {
            // No room for the camera: drop the outlines instead of leaving the previous (larger)
            // geometry on screen while the window shrinks.
            BorderPath.Visibility = Visibility.Collapsed;
            ShadowPath.Visibility = Visibility.Collapsed;
            return;
        }

        var showBorder = _state.ShowBorder && _state.BorderWidth > 0 && _state.SoftEdge <= 0;
        var borderThickness = Math.Max(0, _state.BorderWidth);
        // Half of the centred stroke sits outside the contour.
        var borderExtent = showBorder ? borderThickness / 2 : 0;
        BorderPath.Data = CreateOutlineGeometry(borderExtent, width, height);
        PlaceOutline(BorderPath, borderExtent, width, height);
        BorderPath.Visibility = showBorder ? Visibility.Visible : Visibility.Collapsed;
        BorderPath.StrokeThickness = borderThickness;
        if (showBorder)
        {
            if (_state.BorderUseGradient)
            {
                var gradient = new LinearGradientBrush(
                    _state.BorderGradientStartColor,
                    _state.BorderGradientEndColor,
                    new Point(0, 0),
                    new Point(1, 1));
                gradient.Freeze();
                BorderPath.Stroke = gradient;
            }
            else
            {
                var solid = new SolidColorBrush(_state.BorderColor);
                solid.Freeze();
                BorderPath.Stroke = solid;
            }
        }

        var showShadow = _state.ShowShadow && _state.BorderShadowRadius > 0;
        var shadowThickness =
            (_state.ShowBorder && _state.BorderWidth > 0 ? _state.BorderWidth : 0) +
            _state.BorderShadowRadius * 2;
        // Room for the stroke's outer half plus the blur spread.
        var shadowExtent = showShadow ? (shadowThickness / 2) + _state.BorderShadowRadius : 0;
        ShadowPath.Data = CreateOutlineGeometry(shadowExtent, width, height);
        PlaceOutline(ShadowPath, shadowExtent, width, height);
        ShadowPath.Visibility = showShadow ? Visibility.Visible : Visibility.Collapsed;
        if (showShadow)
        {
            var stroke = new SolidColorBrush(_state.BorderShadowColor);
            stroke.Freeze();
            ShadowPath.Stroke = stroke;
            ShadowPath.StrokeThickness = shadowThickness;
            ShadowPath.Effect = new BlurEffect
            {
                Radius = _state.BorderShadowRadius,
                KernelType = KernelType.Gaussian,
                // The halo only re-renders when the outline changes (or while the blob breathes),
                // and the cheaper sampling is indistinguishable at these radii.
                RenderingBias = RenderingBias.Performance,
            };
        }
    }

    /// <summary>Shape outline drawn <paramref name="extent"/> px inside its element, so a stroke of
    /// twice that width stays inside the element bounds.</summary>
    private Geometry CreateOutlineGeometry(double extent, double width, double height)
    {
        return ShapeGeometryFactory.Create(
            _state!.ShapeType,
            new Rect(extent, extent, width, height),
            _state.ActiveCornerRadius,
            _state.OrganicBlob,
            _breathingTime);
    }

    /// <summary>Gives the outline a slot big enough for its overflow and offsets it so the contour
    /// still lands exactly on the chrome bounds.</summary>
    private static void PlaceOutline(System.Windows.Shapes.Path path, double extent, double width, double height)
    {
        path.Width = width + extent * 2;
        path.Height = height + extent * 2;
        Canvas.SetLeft(path, -extent);
        Canvas.SetTop(path, -extent);
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!IsVisible || _state is null || FrameSource is null)
        {
            return;
        }

        var info = FrameSource.LatestInfo;
        if (info is null || info.Value.Version == _lastVersion)
        {
            return;
        }

        var required = info.Value.Stride * info.Value.Height;
        if (_frameBytes.Length < required)
        {
            _frameBytes = new byte[required];
        }

        if (!FrameSource.TryCopyLatest(_frameBytes, out var copied))
        {
            return;
        }

        _lastVersion = copied.Version;
        EnsureBitmap(copied.Width, copied.Height);
        if (_bitmap is null)
        {
            return;
        }

        _bitmap.WritePixels(
            new Int32Rect(0, 0, copied.Width, copied.Height),
            _frameBytes,
            copied.Stride,
            0);
        Surface.Frame = _bitmap;
        Surface.InvalidateVisual();
        UpdateFrameVisibility();
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is not null && _bitmap.PixelWidth == width && _bitmap.PixelHeight == height)
        {
            return;
        }

        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
    }

    private void UpdateFrameVisibility()
    {
        if (_state is null)
        {
            return;
        }

        var show = _state.CameraStatus == CameraStatus.Connected;
        Surface.Frame = show ? _bitmap : null;
        Surface.InvalidateVisual();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RebuildChrome();
        UpdateBorderAndShadow();
    }
}
/// <summary>Draws one frame, cover-scaled, with zoom/offset/mirror inside the shape geometry.</summary>
public sealed class CameraShapeSurface : FrameworkElement
{
    public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(
        nameof(Geometry), typeof(Geometry), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(ImageSource), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffsetXProperty = DependencyProperty.Register(
        nameof(OffsetX), typeof(double), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffsetYProperty = DependencyProperty.Register(
        nameof(OffsetY), typeof(double), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FlipHorizontalProperty = DependencyProperty.Register(
        nameof(FlipHorizontal), typeof(bool), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FlipVerticalProperty = DependencyProperty.Register(
        nameof(FlipVertical), typeof(bool), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaskBrushProperty = DependencyProperty.Register(
        nameof(MaskBrush), typeof(Brush), typeof(CameraShapeSurface),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public CameraShapeSurface()
    {
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        IsHitTestVisible = false;
    }

    public Geometry? Geometry
    {
        get => (Geometry?)GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public ImageSource? Frame
    {
        get => (ImageSource?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double OffsetX
    {
        get => (double)GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public double OffsetY
    {
        get => (double)GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    public bool FlipHorizontal
    {
        get => (bool)GetValue(FlipHorizontalProperty);
        set => SetValue(FlipHorizontalProperty, value);
    }

    public bool FlipVertical
    {
        get => (bool)GetValue(FlipVerticalProperty);
        set => SetValue(FlipVerticalProperty, value);
    }

    /// <summary>Feather brush for the soft edge; when null the frame is clipped to the geometry.</summary>
    public Brush? MaskBrush
    {
        get => (Brush?)GetValue(MaskBrushProperty);
        set => SetValue(MaskBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth;
        var height = ActualHeight;
        var geometry = Geometry;
        if (width <= 0 || height <= 0 || geometry is null)
        {
            return;
        }

        var maskBrush = MaskBrush;
        if (maskBrush is null)
        {
            drawingContext.PushClip(geometry);
        }
        else
        {
            // Pushed on the drawing context (not via UIElement.OpacityMask) so the brush maps into
            // the element's own drawing space. The cover-scaled frame overflows the bounds, and
            // UIElement.OpacityMask sizes its brush by that overflowing content box, which slid the
            // fade off the shape's sides.
            drawingContext.PushOpacityMask(maskBrush);
        }

        var frame = Frame;
        if (frame is null)
        {
            drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
        }
        else
        {
            var pixelWidth = frame is BitmapSource bitmap ? bitmap.PixelWidth : frame.Width;
            var pixelHeight = frame is BitmapSource bitmap2 ? bitmap2.PixelHeight : frame.Height;
            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                drawingContext.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));
            }
            else
            {
                var zoom = Math.Max(1, Zoom);
                var scale = Math.Max(width / pixelWidth, height / pixelHeight) * zoom;
                var drawWidth = pixelWidth * scale;
                var drawHeight = pixelHeight * scale;
                var centerX = width / 2 + width * OffsetX / 100;
                var centerY = height / 2 - height * OffsetY / 100;

                var flipped = FlipHorizontal || FlipVertical;
                if (flipped)
                {
                    drawingContext.PushTransform(new ScaleTransform(
                        FlipHorizontal ? -1 : 1,
                        FlipVertical ? -1 : 1,
                        centerX,
                        centerY));
                }

                drawingContext.DrawImage(
                    frame,
                    new Rect(centerX - drawWidth / 2, centerY - drawHeight / 2, drawWidth, drawHeight));
                if (flipped)
                {
                    drawingContext.Pop();
                }
            }
        }

        drawingContext.Pop();
    }
}
