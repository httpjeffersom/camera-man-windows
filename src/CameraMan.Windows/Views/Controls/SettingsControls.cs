using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CameraMan.Windows.Models;

namespace CameraMan.Windows.Views.Controls;

public sealed class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Footer
    {
        get => (string)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }
}

public sealed class SettingsRow : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(System.Windows.Media.Brush), typeof(SettingsRow),
        new PropertyMetadata(System.Windows.Media.Brushes.SlateGray));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Icon
    {
        get => (string)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public System.Windows.Media.Brush IconBrush
    {
        get => (System.Windows.Media.Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }
}

public sealed class ColorPickerButton : Button
{
    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor),
        typeof(System.Windows.Media.Color),
        typeof(ColorPickerButton),
        new FrameworkPropertyMetadata(
            System.Windows.Media.Colors.White,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedColorChanged));

    public System.Windows.Media.Color SelectedColor
    {
        get => (System.Windows.Media.Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerButton button)
        {
            var color = (System.Windows.Media.Color)e.NewValue;
            button.Background = new System.Windows.Media.SolidColorBrush(color);
            button.ToolTip = ColorCode.ToHex(color);
        }
    }

    protected override void OnClick()
    {
        base.OnClick();
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(SelectedColor.A, SelectedColor.R, SelectedColor.G, SelectedColor.B),
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SelectedColor = ColorCode.WithChannelsOf(
                SelectedColor,
                System.Windows.Media.Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B));
        }
    }
}

/// <summary>A live shape preview used by the settings cards and organic presets.</summary>
public sealed class ShapeThumbnail : FrameworkElement
{
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(ShapeType), typeof(ShapeThumbnail),
        new FrameworkPropertyMetadata(ShapeType.Circle, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(ShapeThumbnail),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BlobProperty = DependencyProperty.Register(
        nameof(Blob), typeof(OrganicBlob), typeof(ShapeThumbnail),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ShapeType Shape
    {
        get => (ShapeType)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public OrganicBlob? Blob
    {
        get => (OrganicBlob?)GetValue(BlobProperty);
        set => SetValue(BlobProperty, value);
    }

    protected override void OnRender(System.Windows.Media.DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 8 || height < 8)
        {
            return;
        }

        // The shape fills the frame almost completely: only a hair of margin for anti-aliasing.
        var inset = Math.Min(width, height) * 0.02;
        var bounds = new Rect(inset, inset, width - inset * 2, height - inset * 2);
        var geometry = ShapeGeometryFactory.Create(
            Shape, bounds, CornerRadius, Blob ?? OrganicBlob.Default);
        var gradient = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(100, 116, 139),
            System.Windows.Media.Color.FromRgb(51, 65, 85),
            new Point(0, 0),
            new Point(0, 1));
        gradient.Freeze();

        drawingContext.PushClip(geometry);
        drawingContext.DrawRectangle(gradient, null, bounds);

        // Solid person silhouette (SF Symbols person.fill style), anchored to the frame bottom.
        var unit = Math.Min(width, height);
        var personCx = bounds.X + bounds.Width / 2;
        var bodyW = unit * 0.47;
        var bodyH = unit * 0.205;
        var headD = unit * 0.23;
        var bodyTop = bounds.Bottom - bodyH;
        var body = new System.Windows.Media.RectangleGeometry(
            new Rect(personCx - bodyW / 2, bodyTop, bodyW, bodyH + unit * 0.02),
            bodyW / 2,
            bodyW / 2);
        var head = new System.Windows.Media.EllipseGeometry(
            new Point(personCx, bodyTop - unit * 0.012 - headD / 2), headD / 2, headD / 2);
        var person = new System.Windows.Media.GeometryGroup();
        person.Children.Add(body);
        person.Children.Add(head);
        var personBrush = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(217, 255, 255, 255));
        personBrush.Freeze();
        drawingContext.DrawGeometry(personBrush, null, person);
        drawingContext.Pop();

        var stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(42, 255, 255, 255));
        stroke.Freeze();
        drawingContext.DrawGeometry(null, new System.Windows.Media.Pen(stroke, 1), geometry);
    }
}

public sealed class ShapeCard : RadioButton
{
    public static readonly DependencyProperty ShapeProperty = DependencyProperty.Register(
        nameof(Shape), typeof(ShapeType), typeof(ShapeCard), new PropertyMetadata(ShapeType.Circle));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(ShapeCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BlobProperty = DependencyProperty.Register(
        nameof(Blob), typeof(OrganicBlob), typeof(ShapeCard), new PropertyMetadata(null));

    public static readonly DependencyProperty CornerRadiusValueProperty = DependencyProperty.Register(
        nameof(CornerRadiusValue), typeof(double), typeof(ShapeCard), new PropertyMetadata(0.0));

    public ShapeType Shape
    {
        get => (ShapeType)GetValue(ShapeProperty);
        set => SetValue(ShapeProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public OrganicBlob? Blob
    {
        get => (OrganicBlob?)GetValue(BlobProperty);
        set => SetValue(BlobProperty, value);
    }

    public double CornerRadiusValue
    {
        get => (double)GetValue(CornerRadiusValueProperty);
        set => SetValue(CornerRadiusValueProperty, value);
    }
}

public sealed class BlobChip : Button
{
    public static readonly DependencyProperty BlobProperty = DependencyProperty.Register(
        nameof(Blob), typeof(OrganicBlob), typeof(BlobChip),
        new PropertyMetadata(null, OnBlobChanged));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(BlobChip), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(BlobChip), new PropertyMetadata(false));

    public OrganicBlob? Blob
    {
        get => (OrganicBlob?)GetValue(BlobProperty);
        set => SetValue(BlobProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Wires the template's glyph to the blob without relying on template bindings, which
    /// resolve unreliably inside data triggers for this control.</summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyBlobState();
    }

    private static void OnBlobChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((BlobChip)d).ApplyBlobState();
    }

    private void ApplyBlobState()
    {
        var isShuffle = Blob is null;
        if (GetTemplateChild("BlobGlyph") is BlobGlyph glyph)
        {
            glyph.Blob = Blob;
            glyph.Visibility = isShuffle ? Visibility.Collapsed : Visibility.Visible;
        }

        if (GetTemplateChild("ShuffleGlyph") is System.Windows.Controls.TextBlock shuffle)
        {
            shuffle.Visibility = isShuffle ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

/// <summary>A flat, solid organic silhouette shown inside an organic preset chip (like SwiftUI's BlobChip).</summary>
public sealed class BlobGlyph : FrameworkElement
{
    public static readonly DependencyProperty BlobProperty = DependencyProperty.Register(
        nameof(Blob), typeof(OrganicBlob), typeof(BlobGlyph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(
        nameof(FillBrush), typeof(Brush), typeof(BlobGlyph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public OrganicBlob? Blob
    {
        get => (OrganicBlob?)GetValue(BlobProperty);
        set => SetValue(BlobProperty, value);
    }

    public Brush? FillBrush
    {
        get => (Brush?)GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth;
        var height = ActualHeight;
        if (width < 4 || height < 4)
        {
            return;
        }

        var bounds = new Rect(0, 0, width, height);
        var geometry = ShapeGeometryFactory.Create(
            ShapeType.Organic, bounds, 0, Blob ?? OrganicBlob.Default);
        // The macOS chip fills the blob with the page colour at 85% opacity.
        drawingContext.PushOpacity(0.85);
        drawingContext.DrawGeometry(FillBrush, null, geometry);
        drawingContext.Pop();
    }
}

/// <summary>Two-way equality between an enum property and a radio button.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not null && parameter is not null && value.Equals(parameter);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}

/// <summary>A row of key caps, as shown on the Shortcuts page. Keys are separated by commas.</summary>
public sealed class KeyChord : System.Windows.Controls.WrapPanel
{
    public static readonly DependencyProperty KeysProperty = DependencyProperty.Register(
        nameof(Keys), typeof(string), typeof(KeyChord),
        new PropertyMetadata(string.Empty, OnKeysChanged));

    public string Keys
    {
        get => (string)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KeyChord chord)
        {
            chord.Rebuild();
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        var keys = (Keys ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var key in keys)
        {
            var text = new System.Windows.Controls.TextBlock
            {
                Text = key,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Padding = new Thickness(0),
            };
            text.SetResourceReference(System.Windows.Controls.TextBlock.FontFamilyProperty, "UiFont");
            text.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrimary");

            var border = new System.Windows.Controls.Border
            {
                CornerRadius = new CornerRadius(5),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 4, 0),
                Child = text,
            };
            border.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "ControlBackground");
            border.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "ControlBorder");
            Children.Add(border);
        }
    }
}

public static class NameConverters
{
    public static string ShapeName(ShapeType shape, bool shortName) =>
        Resources.Strings.Get(shortName ? shape.ShortResourceKey() : shape.ResourceKey());

    public static string SizeName(WindowSizePreset preset) => Resources.Strings.Get(preset.ResourceKey());

    public static string EdgeName(ScreenEdge edge) => Resources.Strings.Get(edge.ResourceKey());
}

public sealed class ShapeNameConverter : IValueConverter
{
    public bool ShortName { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ShapeType shape ? NameConverters.ShapeName(shape, ShortName) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class SizeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is WindowSizePreset preset ? NameConverters.SizeName(preset) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class EdgeNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ScreenEdge edge ? NameConverters.EdgeName(edge) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Frames a settings page so every page shares the same content width and left edge.
/// The content is capped at 640 px and aligned to the left inside the scroll viewport.
/// </summary>
public sealed class SettingsPageFrame : Decorator
{
    private const double MaxContentWidth = 640;
    private static readonly Thickness ContentPadding = new(32, 18, 32, 28);

    public SettingsPageFrame()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (BindingOperations.GetBinding(this, WidthProperty) is not null)
        {
            return;
        }

        var viewer = FindScrollViewer();
        if (viewer is not null)
        {
            // Track the full scroll viewer width (including the scrollbar) so the
            // content width does not change when a page starts or stops scrolling.
            SetBinding(WidthProperty, new Binding(nameof(FrameworkElement.ActualWidth)) { Source = viewer });
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var outerWidth = GetOuterWidth(constraint.Width);
        var frameWidth = Math.Min(outerWidth, MaxContentWidth + ContentPadding.Left + ContentPadding.Right);
        var childWidth = Math.Max(0, frameWidth - ContentPadding.Left - ContentPadding.Right);
        var childHeight = double.IsInfinity(constraint.Height)
            ? double.PositiveInfinity
            : Math.Max(0, constraint.Height - ContentPadding.Top - ContentPadding.Bottom);

        Child?.Measure(new Size(childWidth, childHeight));

        var height = (Child?.DesiredSize.Height ?? 0) + ContentPadding.Top + ContentPadding.Bottom;
        return new Size(frameWidth, height);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var outerWidth = GetOuterWidth(arrangeSize.Width);
        var frameWidth = Math.Min(outerWidth, MaxContentWidth + ContentPadding.Left + ContentPadding.Right);
        var childWidth = Math.Max(0, frameWidth - ContentPadding.Left - ContentPadding.Right);
        var childHeight = Math.Max(0, arrangeSize.Height - ContentPadding.Top - ContentPadding.Bottom);

        Child?.Arrange(new Rect(ContentPadding.Left, ContentPadding.Top, childWidth, childHeight));
        return arrangeSize;
    }

    private double GetOuterWidth(double fallback)
    {
        var width = double.IsNaN(Width) ? fallback : Width;
        return double.IsInfinity(width) ? MaxContentWidth + ContentPadding.Left + ContentPadding.Right : width;
    }

    private ScrollViewer? FindScrollViewer()
    {
        for (DependencyObject? current = this; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ScrollViewer viewer)
            {
                return viewer;
            }
        }

        return null;
    }
}

public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null;
        if (Invert)
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>
/// Border/shadow padding for the chrome margin: <c>values[0]</c> is the padding and
/// <c>values[1] x values[2]</c> is the window size. The padding is capped so it can never eat the
/// whole camera — a small window with a thick border and a large shadow keeps at least
/// <see cref="MinCameraSize"/> px of shape instead of collapsing to nothing.
/// </summary>
public sealed class BorderPaddingToThicknessConverter : IMultiValueConverter
{
    private const double MinCameraSize = 48;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3)
        {
            return new Thickness(0);
        }

        var padding = values[0] is double d ? d : 0;
        var width = values[1] is double w ? w : 0;
        var height = values[2] is double h ? h : 0;
        var shortest = Math.Min(width, height);
        if (shortest > 0)
        {
            padding = Math.Min(padding, Math.Max(0, (shortest - MinCameraSize) / 2));
        }

        return new Thickness(padding);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is System.Windows.Media.Color color
            ? new System.Windows.Media.SolidColorBrush(color)
            : System.Windows.Media.Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

/// <summary>ShowBorder &amp;&amp; BorderWidth &gt; 0 &amp;&amp; SoftEdge == 0.</summary>
public sealed class BorderVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3)
        {
            return Visibility.Collapsed;
        }

        var show = values[0] is true;
        var width = values[1] is double w ? w : 0;
        var soft = values[2] is double s ? s : 0;
        return show && width > 0 && soft <= 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
