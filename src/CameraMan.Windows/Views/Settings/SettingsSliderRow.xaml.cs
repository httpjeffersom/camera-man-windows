using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CameraMan.Windows.Resources;

namespace CameraMan.Windows.Views.Settings;

public enum SliderFormat
{
    Points,
    Percent,
    Zoom,
    Signed,
    Decimal,
    OffOrPoints,
}

/// <summary>A settings row with a value label and a thick slider underneath, matching the macOS component.</summary>
public partial class SettingsSliderRow : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsSliderRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(SettingsSliderRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(string), typeof(SettingsSliderRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconBrushProperty = DependencyProperty.Register(
        nameof(IconBrush), typeof(Brush), typeof(SettingsSliderRow), new PropertyMetadata(Brushes.SlateGray));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SettingsSliderRow),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(SettingsSliderRow),
        new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SettingsSliderRow),
        new PropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(SettingsSliderRow),
        new PropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(
        nameof(LargeChange), typeof(double), typeof(SettingsSliderRow), new PropertyMetadata(5.0));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(SliderFormat), typeof(SettingsSliderRow),
        new PropertyMetadata(SliderFormat.Points, OnValueChanged));

    public SettingsSliderRow()
    {
        InitializeComponent();
        UpdateText();
    }

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

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    public SliderFormat Format
    {
        get => (SliderFormat)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsSliderRow row)
        {
            row.UpdateText();
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsSliderRow row && row.Slider is not null)
        {
            row.Slider.IsSnapToTickEnabled = row.Step > 0;
        }
    }

    private void UpdateText()
    {
        if (ValueText is null)
        {
            return;
        }

        ValueText.Text = Format switch
        {
            SliderFormat.Percent => string.Format(CultureInfo.CurrentCulture, "{0:0}%", Value),
            SliderFormat.Zoom => string.Format(CultureInfo.CurrentCulture, "{0:0.0}\u00D7", Value),
            SliderFormat.Signed => string.Format(CultureInfo.CurrentCulture, "{0:+0.00;-0.00;0.00}", Value),
            SliderFormat.Decimal => string.Format(CultureInfo.CurrentCulture, "{0:0.0}", Value),
            SliderFormat.OffOrPoints => Math.Abs(Value) < 0.001
                ? Strings.Get("Common_Off")
                : Strings.Format("Common_Points", Math.Round(Value)),
            _ => Strings.Format("Common_Points", Math.Round(Value)),
        };
    }
}
