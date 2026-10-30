using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Threading;
using CameraMan.Windows.Models;
using CameraMan.Windows.Services;

namespace CameraMan.Windows.ViewModels;

/// <summary>
/// The single observable state shared by the camera window, the settings pages and the tray menu. Setters clamp to the
/// supported range and schedule a debounced JSON save, mirroring the macOS app's "save as you change" behaviour.
/// </summary>
public sealed class AppState : INotifyPropertyChanged
{
    public const double MinOffset = -50;
    public const double MaxOffset = 50;
    public const double MinScale = 1.0;
    public const double MaxScale = 2.0;
    public const double MaxBorderWidth = 20;
    public const double MaxShadowRadius = 24;
    public const double MaxCornerRadius = 150;
    public const double MaxSoftEdge = 40;

    private readonly SettingsStore _store;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Dictionary<string, PresetPlacement> _placements;
    private bool _loading;
    private bool _saving;

    private string? _selectedDeviceId;
    private CameraStatus _cameraStatus = CameraStatus.Loading;
    private string? _cameraFormat;
    private string? _cameraErrorMessage;
    private string? _selectedDeviceName;

    private ShapeType _shapeType = ShapeType.Circle;
    private double _squareCornerRadius;
    private double _verticalCornerRadius;
    private double _horizontalCornerRadius;
    private OrganicBlob _organicBlob = OrganicBlob.Default;
    private bool _organicBreathing;
    private double _softEdge;

    private double _offsetX;
    private double _offsetY;
    private double _scale = 1;
    private bool _flipHorizontal;
    private bool _flipVertical;

    private bool _showBorder;
    private double _borderWidth;
    private string _borderColorHex = "#FF3B82F6";
    private bool _borderUseGradient;
    private string _borderGradientStartHex = "#FF3B82F6";
    private string _borderGradientEndHex = "#FF8B5CF6";
    private bool _showShadow;
    private double _borderShadowRadius;
    private string _borderShadowColorHex = "#80000000";

    private double _brightness;
    private double _contrast = 1;
    private double _saturation = 1;

    private WindowSizePreset _windowSizePreset = WindowSizePreset.Md;
    private ScreenEdge _screenEdge = ScreenEdge.TopLeft;
    private WindowSizePreset _spaceSlot1 = WindowSizePreset.Sm;
    private WindowSizePreset _spaceSlot2 = WindowSizePreset.Lg;
    private bool _alwaysOnTop = true;

    private bool _globalShortcutsEnabled = true;
    private bool _openAtLogin;
    private string _language = "system";
    private string? _globalHotKeyError;
    private string? _startupError;
    private bool _reduceMotion;

    private string? _toastMessage;
    private bool _isWindowVisible = true;
    private bool _settingsOpen;

    public AppState(AppSettings settings, SettingsStore store)
    {
        _store = store;
        _placements = BuildPlacementMap(settings.Placements);

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(450),
        };
        _saveTimer.Tick += (_, _) => SaveNow();

        _toastTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastMessage = null;
        };

        Load(settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the window size, corner, topmost state or chrome padding changed.</summary>
    public event EventHandler? LayoutSettingsChanged;

    public event EventHandler? GlobalShortcutsSettingChanged;

    public ObservableCollection<CameraDevice> VideoDevices { get; } = [];

    /// <summary>Localized choices for the language picker. Applied on the next launch.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("system", Resources.Strings.Get("General_LanguageSystem")),
        new("en", Resources.Strings.Get("General_LanguageEnglish")),
        new("pt-BR", Resources.Strings.Get("General_LanguagePortuguese")),
    ];

    /// <summary>Number of real cameras, excluding the "System default" choice shown first in the picker.</summary>
    public int DeviceCount => Math.Max(0, VideoDevices.Count - 1);

    public bool HasDevices => DeviceCount > 0;

    /// <summary>Localized subtitle for the camera picker: "N available" or "No camera found".</summary>
    public string CameraSourceSubtitle => HasDevices
        ? Resources.Strings.Format("Camera_AvailableCount", DeviceCount)
        : Resources.Strings.Get("Camera_NoCameraFound");

    /// <summary>Localized, human-readable state shown on the Camera page.</summary>
    public string CameraStatusText => CameraStatus switch
    {
        CameraStatus.Connected => Resources.Strings.Get("Camera_Connected"),
        CameraStatus.Loading => Resources.Strings.Get("Camera_Loading"),
        CameraStatus.PermissionDenied => Resources.Strings.Get("Camera_PermissionDenied"),
        CameraStatus.Disconnected => Resources.Strings.Get("Camera_Disconnected"),
        CameraStatus.Error => CameraErrorMessage ?? Resources.Strings.Get("Camera_Error"),
        _ => Resources.Strings.Get("Camera_NotFound"),
    };

    public bool ShowSoftEdgeBorderNote => SoftEdge > 0 && ShowBorder;

    public bool ShowBorderGradientOptions => ShowBorder && BorderUseGradient;

    public bool ShowBorderSolidColor => ShowBorder && !BorderUseGradient;

    public string? SelectedDeviceId
    {
        get => _selectedDeviceId;
        set
        {
            if (Set(ref _selectedDeviceId, value))
            {
                ScheduleSave();
            }
        }
    }

    public string? SelectedDeviceName
    {
        get => _selectedDeviceName;
        private set => Set(ref _selectedDeviceName, value);
    }

    public CameraStatus CameraStatus
    {
        get => _cameraStatus;
        private set
        {
            if (Set(ref _cameraStatus, value))
            {
                OnPropertyChanged(nameof(IsCameraLoading));
                OnPropertyChanged(nameof(IsCameraAvailable));
                OnPropertyChanged(nameof(IsCameraUnavailable));
                OnPropertyChanged(nameof(IsPermissionDenied));
                OnPropertyChanged(nameof(CameraStatusText));
            }
        }
    }

    public bool IsCameraLoading => CameraStatus == CameraStatus.Loading;

    public bool IsCameraAvailable => CameraStatus == CameraStatus.Connected;

    public bool IsCameraUnavailable =>
        CameraStatus is CameraStatus.NotFound or CameraStatus.Disconnected or CameraStatus.Error;

    public bool IsPermissionDenied => CameraStatus == CameraStatus.PermissionDenied;

    public string? CameraFormat
    {
        get => _cameraFormat;
        private set => Set(ref _cameraFormat, value);
    }

    public string? CameraErrorMessage
    {
        get => _cameraErrorMessage;
        private set
        {
            if (Set(ref _cameraErrorMessage, value))
            {
                OnPropertyChanged(nameof(CameraStatusText));
            }
        }
    }

    public ShapeType ShapeType
    {
        get => _shapeType;
        set
        {
            if (Set(ref _shapeType, value))
            {
                OnPropertyChanged(nameof(ShowCornerRadius));
                OnPropertyChanged(nameof(ActiveCornerRadius));
                OnPropertyChanged(nameof(IsOrganic));
                ScheduleSave();
            }
        }
    }

    public bool IsOrganic => ShapeType == ShapeType.Organic;

    /// <summary>The corner radius only applies to the square and the two rectangles.</summary>
    public bool ShowCornerRadius => ShapeType is ShapeType.Square or ShapeType.VerticalRectangle or ShapeType.HorizontalRectangle;

    /// <summary>The radius of the shape that is currently active; the slider edits the active value.</summary>
    public double ActiveCornerRadius
    {
        get => ShapeType switch
        {
            ShapeType.VerticalRectangle => VerticalCornerRadius,
            ShapeType.HorizontalRectangle => HorizontalCornerRadius,
            _ => SquareCornerRadius,
        };
        set
        {
            switch (ShapeType)
            {
                case ShapeType.VerticalRectangle:
                    VerticalCornerRadius = value;
                    break;
                case ShapeType.HorizontalRectangle:
                    HorizontalCornerRadius = value;
                    break;
                default:
                    SquareCornerRadius = value;
                    break;
            }
        }
    }

    public double SquareCornerRadius
    {
        get => _squareCornerRadius;
        set
        {
            if (SetClamped(ref _squareCornerRadius, value, 0, MaxCornerRadius)
                && ShapeType == ShapeType.Square)
            {
                OnPropertyChanged(nameof(ActiveCornerRadius));
            }
        }
    }

    public double VerticalCornerRadius
    {
        get => _verticalCornerRadius;
        set
        {
            if (SetClamped(ref _verticalCornerRadius, value, 0, MaxCornerRadius)
                && ShapeType == ShapeType.VerticalRectangle)
            {
                OnPropertyChanged(nameof(ActiveCornerRadius));
            }
        }
    }

    public double HorizontalCornerRadius
    {
        get => _horizontalCornerRadius;
        set
        {
            if (SetClamped(ref _horizontalCornerRadius, value, 0, MaxCornerRadius)
                && ShapeType == ShapeType.HorizontalRectangle)
            {
                OnPropertyChanged(nameof(ActiveCornerRadius));
            }
        }
    }

    public OrganicBlob OrganicBlob
    {
        get => _organicBlob;
        set => Set(ref _organicBlob, value ?? OrganicBlob.Default);
    }

    public bool OrganicBreathing
    {
        get => _organicBreathing;
        set => Set(ref _organicBreathing, value);
    }

    public double SoftEdge
    {
        get => _softEdge;
        set
        {
            if (SetClamped(ref _softEdge, value, 0, MaxSoftEdge))
            {
                OnPropertyChanged(nameof(ShowSoftEdgeBorderNote));
            }
        }
    }

    public double OffsetX
    {
        get => _offsetX;
        set => SetClamped(ref _offsetX, value, MinOffset, MaxOffset);
    }

    public double OffsetY
    {
        get => _offsetY;
        set => SetClamped(ref _offsetY, value, MinOffset, MaxOffset);
    }

    public double Scale
    {
        get => _scale;
        set => SetClamped(ref _scale, value, MinScale, MaxScale);
    }

    public bool FlipHorizontal
    {
        get => _flipHorizontal;
        set => Set(ref _flipHorizontal, value);
    }

    public bool FlipVertical
    {
        get => _flipVertical;
        set => Set(ref _flipVertical, value);
    }

    public bool ShowBorder
    {
        get => _showBorder;
        set
        {
            if (Set(ref _showBorder, value))
            {
                OnPropertyChanged(nameof(BorderPadding));
                OnPropertyChanged(nameof(ShowSoftEdgeBorderNote));
                OnPropertyChanged(nameof(ShowBorderGradientOptions));
                OnPropertyChanged(nameof(ShowBorderSolidColor));
                LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double BorderWidth
    {
        get => _borderWidth;
        set
        {
            if (SetClamped(ref _borderWidth, value, 0, MaxBorderWidth))
            {
                OnPropertyChanged(nameof(BorderPadding));
                LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string BorderColorHex
    {
        get => _borderColorHex;
        set
        {
            if (Set(ref _borderColorHex, ColorCode.Normalize(value, "#FF3B82F6")))
            {
                OnPropertyChanged(nameof(BorderColor));
            }
        }
    }

    public Color BorderColor
    {
        get => ColorCode.Parse(_borderColorHex, Color.FromRgb(59, 130, 246));
        set => BorderColorHex = ColorCode.ToHex(value);
    }

    public bool BorderUseGradient
    {
        get => _borderUseGradient;
        set
        {
            if (Set(ref _borderUseGradient, value))
            {
                OnPropertyChanged(nameof(ShowBorderGradientOptions));
                OnPropertyChanged(nameof(ShowBorderSolidColor));
            }
        }
    }

    public string BorderGradientStartHex
    {
        get => _borderGradientStartHex;
        set
        {
            if (Set(ref _borderGradientStartHex, ColorCode.Normalize(value, "#FF3B82F6")))
            {
                OnPropertyChanged(nameof(BorderGradientStartColor));
            }
        }
    }

    public Color BorderGradientStartColor
    {
        get => ColorCode.Parse(_borderGradientStartHex, Color.FromRgb(59, 130, 246));
        set => BorderGradientStartHex = ColorCode.ToHex(value);
    }

    public string BorderGradientEndHex
    {
        get => _borderGradientEndHex;
        set
        {
            if (Set(ref _borderGradientEndHex, ColorCode.Normalize(value, "#FF8B5CF6")))
            {
                OnPropertyChanged(nameof(BorderGradientEndColor));
            }
        }
    }

    public Color BorderGradientEndColor
    {
        get => ColorCode.Parse(_borderGradientEndHex, Color.FromRgb(139, 92, 246));
        set => BorderGradientEndHex = ColorCode.ToHex(value);
    }

    public bool ShowShadow
    {
        get => _showShadow;
        set
        {
            if (Set(ref _showShadow, value))
            {
                OnPropertyChanged(nameof(BorderPadding));
                LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double BorderShadowRadius
    {
        get => _borderShadowRadius;
        set
        {
            if (SetClamped(ref _borderShadowRadius, value, 0, MaxShadowRadius))
            {
                OnPropertyChanged(nameof(BorderPadding));
                LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string BorderShadowColorHex
    {
        get => _borderShadowColorHex;
        set
        {
            if (Set(ref _borderShadowColorHex, ColorCode.Normalize(value, "#80000000")))
            {
                OnPropertyChanged(nameof(BorderShadowColor));
            }
        }
    }

    public Color BorderShadowColor
    {
        get => ColorCode.Parse(_borderShadowColorHex, Color.FromArgb(128, 0, 0, 0));
        set => BorderShadowColorHex = ColorCode.ToHex(value);
    }

    /// <summary>Extra margin around the camera surface so a border and the shadow blur are not clipped.</summary>
    public double BorderPadding
    {
        get
        {
            var padding = 0.0;
            if (ShowBorder && BorderWidth > 0)
            {
                padding += BorderWidth;
            }

            if (ShowShadow && BorderShadowRadius > 0)
            {
                padding += 2 * BorderShadowRadius;
            }

            return padding;
        }
    }

    public double Brightness
    {
        get => _brightness;
        set => SetClamped(ref _brightness, value, -0.5, 0.5);
    }

    public double Contrast
    {
        get => _contrast;
        set => SetClamped(ref _contrast, value, 0.5, 2);
    }

    public double Saturation
    {
        get => _saturation;
        set => SetClamped(ref _saturation, value, 0, 2);
    }

    public WindowSizePreset WindowSizePreset
    {
        get => _windowSizePreset;
        set
        {
            if (!Set(ref _windowSizePreset, value))
            {
                return;
            }

            // Switching size restores the corner that preset was last placed at.
            var edge = GetPlacement(value).ScreenEdge;
            if (_screenEdge != edge)
            {
                _screenEdge = edge;
                OnPropertyChanged(nameof(ScreenEdge));
            }

            ScheduleSave();
            LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ScreenEdge ScreenEdge
    {
        get => _screenEdge;
        set
        {
            if (!Set(ref _screenEdge, value))
            {
                return;
            }

            var placement = CurrentPlacement();
            placement.ScreenEdge = value;
            placement.OriginX = null;
            placement.OriginY = null;
            ScheduleSave();
            LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public WindowSizePreset SpaceSlot1
    {
        get => _spaceSlot1;
        set
        {
            if (Set(ref _spaceSlot1, value))
            {
                ScheduleSave();
            }
        }
    }

    public WindowSizePreset SpaceSlot2
    {
        get => _spaceSlot2;
        set
        {
            if (Set(ref _spaceSlot2, value))
            {
                ScheduleSave();
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (Set(ref _alwaysOnTop, value))
            {
                OnPropertyChanged(nameof(EffectiveAlwaysOnTop));
                LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// While the settings window is open the camera temporarily stops being topmost, so the window the user is
    /// editing is never hidden behind the floating camera. The saved <see cref="AlwaysOnTop"/> value is untouched.
    /// </summary>
    public bool EffectiveAlwaysOnTop => _alwaysOnTop && !_settingsOpen;

    public bool SettingsOpen
    {
        get => _settingsOpen;
        set
        {
            if (Set(ref _settingsOpen, value))
            {
                OnPropertyChanged(nameof(EffectiveAlwaysOnTop));
            }
        }
    }

    public bool GlobalShortcutsEnabled
    {
        get => _globalShortcutsEnabled;
        set
        {
            if (Set(ref _globalShortcutsEnabled, value))
            {
                GlobalShortcutsSettingChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool OpenAtLogin
    {
        get => _openAtLogin;
        set => Set(ref _openAtLogin, value);
    }

    public string Language
    {
        get => _language;
        set
        {
            if (Set(ref _language, value is "en" or "pt-BR" ? value : "system"))
            {
                ScheduleSave();
            }
        }
    }

    public string? GlobalHotKeyError
    {
        get => _globalHotKeyError;
        set => Set(ref _globalHotKeyError, value);
    }

    public string? StartupError
    {
        get => _startupError;
        set => Set(ref _startupError, value);
    }

    /// <summary>
    /// Mirrors the Windows animation setting. It is system state, not a user preference, so changing it must not
    /// schedule a settings write.
    /// </summary>
    public bool ReduceMotion
    {
        get => _reduceMotion;
        set
        {
            if (_reduceMotion == value)
            {
                return;
            }

            _reduceMotion = value;
            OnPropertyChanged(nameof(ReduceMotion));
        }
    }

    public string? ToastMessage
    {
        get => _toastMessage;
        private set => Set(ref _toastMessage, value);
    }

    public bool IsWindowVisible
    {
        get => _isWindowVisible;
        set => Set(ref _isWindowVisible, value);
    }

    public void ShowToast(string message)
    {
        ToastMessage = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public void ReplaceDevices(IEnumerable<CameraDevice> devices)
    {
        var list = devices.ToList();
        VideoDevices.Clear();

        // The macOS picker starts with "System default"; an empty id means "let MediaCapture choose".
        VideoDevices.Add(new CameraDevice(string.Empty, Resources.Strings.Get("Camera_SystemDefault")));
        foreach (var device in list)
        {
            VideoDevices.Add(device);
        }

        OnPropertyChanged(nameof(DeviceCount));
        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(CameraSourceSubtitle));
        SelectedDeviceName = DeviceNameFor(SelectedDeviceId);
    }

    public void SetCameraState(CameraStatus status, string? format = null, string? message = null)
    {
        CameraStatus = status;
        CameraFormat = format;
        CameraErrorMessage = message;
        SelectedDeviceName = DeviceNameFor(SelectedDeviceId);
    }

    private string? DeviceNameFor(string? id) =>
        string.IsNullOrEmpty(id)
            ? Resources.Strings.Get("Camera_SystemDefault")
            : VideoDevices.FirstOrDefault(d => d.Id == id)?.Name;

    public PresetPlacement GetPlacement(WindowSizePreset preset) =>
        _placements.TryGetValue(AppSettings.KeyFor(preset), out var placement) ? placement.Clone() : new PresetPlacement();

    public void SetWindowSizePreset(WindowSizePreset preset) => WindowSizePreset = preset;

    public void SetScreenEdge(ScreenEdge edge) => ScreenEdge = edge;

    /// <summary>Records the physical rectangle after the user drags or resizes the camera window.</summary>
    public void SetWindowRectForCurrentPreset(int x, int y, int width, int height, string? monitorDeviceName)
    {
        var placement = CurrentPlacement();
        placement.OriginX = x;
        placement.OriginY = y;
        placement.MonitorDeviceName = monitorDeviceName;
        if (WindowSizePreset != WindowSizePreset.Full)
        {
            placement.WidthPx = width;
            placement.HeightPx = height;
        }

        ScheduleSave();
    }

    public WindowSizePreset SpaceToggleTargetPreset() =>
        WindowSizePreset == SpaceSlot1 ? SpaceSlot2 : SpaceSlot1;

    public void AdjustOffset(double dx, double dy)
    {
        OffsetX += dx;
        OffsetY += dy;
    }

    public void ZoomIn() => Scale = Math.Round(Math.Min(Scale + 0.1, MaxScale), 2);

    public void ZoomOut() => Scale = Math.Round(Math.Max(Scale - 0.1, MinScale), 2);

    public void ResetZoom() => Scale = 1;

    public void ResetFraming()
    {
        OffsetX = 0;
        OffsetY = 0;
        Scale = 1;
    }

    public void ResetImage()
    {
        Brightness = 0;
        Contrast = 1;
        Saturation = 1;
    }

    public void CycleShape()
    {
        var shapes = Enum.GetValues<ShapeType>();
        var index = Array.IndexOf(shapes, ShapeType);
        ShapeType = shapes[(index + 1) % shapes.Length];
    }

    public void SetOrganicPreset(OrganicBlob blob)
    {
        OrganicBlob = blob;
        ScheduleSave();
    }

    public void RandomizeOrganic()
    {
        OrganicBlob = OrganicBlob.CreateRandom();
        ScheduleSave();
    }

    public void RestoreDefaults()
    {
        var defaults = new AppSettings();
        var keepLanguage = Language;
        var keepLogin = OpenAtLogin;
        _placements.Clear();
        foreach (var preset in WindowSizePresetExtensions.All)
        {
            _placements[AppSettings.KeyFor(preset)] = new PresetPlacement { ScreenEdge = ScreenEdge.TopLeft };
        }

        _loading = true;
        try
        {
            SelectedDeviceId = string.Empty;
            ShapeType = defaults.ShapeType;
            SquareCornerRadius = defaults.SquareCornerRadius ?? defaults.ShapeCornerRadius;
            VerticalCornerRadius = defaults.VerticalCornerRadius ?? defaults.ShapeCornerRadius;
            HorizontalCornerRadius = defaults.HorizontalCornerRadius ?? defaults.ShapeCornerRadius;
            OrganicBlob = defaults.OrganicBlob.Clone();
            OrganicBreathing = defaults.OrganicBreathing;
            SoftEdge = defaults.SoftEdge;
            OffsetX = defaults.OffsetX;
            OffsetY = defaults.OffsetY;
            Scale = defaults.Scale;
            FlipHorizontal = defaults.FlipHorizontal;
            FlipVertical = defaults.FlipVertical;
            ShowBorder = defaults.ShowBorder;
            BorderWidth = defaults.BorderWidth;
            BorderColorHex = defaults.BorderColorHex;
            BorderUseGradient = defaults.BorderUseGradient;
            BorderGradientStartHex = defaults.BorderGradientStartHex;
            BorderGradientEndHex = defaults.BorderGradientEndHex;
            ShowShadow = defaults.ShowShadow;
            BorderShadowRadius = defaults.BorderShadowRadius;
            BorderShadowColorHex = defaults.BorderShadowColorHex;
            Brightness = defaults.Brightness;
            Contrast = defaults.Contrast;
            Saturation = defaults.Saturation;
            WindowSizePreset = defaults.WindowSizePreset;
            ScreenEdge = defaults.ScreenEdge;
            SpaceSlot1 = defaults.SpaceSlot1;
            SpaceSlot2 = defaults.SpaceSlot2;
            AlwaysOnTop = defaults.AlwaysOnTop;
            GlobalShortcutsEnabled = defaults.GlobalShortcutsEnabled;
            Language = keepLanguage;
            OpenAtLogin = keepLogin;
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(BorderPadding));
        ScheduleSave();
        LayoutSettingsChanged?.Invoke(this, EventArgs.Empty);
        GlobalShortcutsSettingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Flushes pending changes immediately (exit, settings window close).</summary>
    public void SaveNow()
    {
        _saveTimer.Stop();
        if (_loading || _saving)
        {
            return;
        }

        try
        {
            _saving = true;
            _store.Save(CaptureSettings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Saving is best effort; the app keeps running with its in-memory state.
        }
        finally
        {
            _saving = false;
        }
    }

    private AppSettings CaptureSettings() => new()
    {
        SelectedDeviceId = SelectedDeviceId,
        ShapeType = ShapeType,
        ShapeCornerRadius = SquareCornerRadius,
        SquareCornerRadius = SquareCornerRadius,
        VerticalCornerRadius = VerticalCornerRadius,
        HorizontalCornerRadius = HorizontalCornerRadius,
        OrganicBlob = OrganicBlob.Clone(),
        OrganicBreathing = OrganicBreathing,
        SoftEdge = SoftEdge,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        Scale = Scale,
        FlipHorizontal = FlipHorizontal,
        FlipVertical = FlipVertical,
        ShowBorder = ShowBorder,
        BorderWidth = BorderWidth,
        BorderColorHex = BorderColorHex,
        BorderUseGradient = BorderUseGradient,
        BorderGradientStartHex = BorderGradientStartHex,
        BorderGradientEndHex = BorderGradientEndHex,
        ShowShadow = ShowShadow,
        BorderShadowRadius = BorderShadowRadius,
        BorderShadowColorHex = BorderShadowColorHex,
        Brightness = Brightness,
        Contrast = Contrast,
        Saturation = Saturation,
        WindowSizePreset = WindowSizePreset,
        ScreenEdge = ScreenEdge,
        Placements = _placements.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.OrdinalIgnoreCase),
        SpaceSlot1 = SpaceSlot1,
        SpaceSlot2 = SpaceSlot2,
        AlwaysOnTop = AlwaysOnTop,
        GlobalShortcutsEnabled = GlobalShortcutsEnabled,
        OpenAtLogin = OpenAtLogin,
        Language = Language,
    };

    private void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            // Empty (not null) selects the "System default" entry in the picker.
            _selectedDeviceId = settings.SelectedDeviceId ?? string.Empty;
            _shapeType = settings.ShapeType;
            _squareCornerRadius = settings.SquareCornerRadius ?? settings.ShapeCornerRadius;
            _verticalCornerRadius = settings.VerticalCornerRadius ?? settings.ShapeCornerRadius;
            _horizontalCornerRadius = settings.HorizontalCornerRadius ?? settings.ShapeCornerRadius;
            _organicBlob = settings.OrganicBlob.Clone();
            _organicBreathing = settings.OrganicBreathing;
            _softEdge = settings.SoftEdge;
            _offsetX = settings.OffsetX;
            _offsetY = settings.OffsetY;
            _scale = settings.Scale;
            _flipHorizontal = settings.FlipHorizontal;
            _flipVertical = settings.FlipVertical;
            _showBorder = settings.ShowBorder;
            _borderWidth = settings.BorderWidth;
            _borderColorHex = settings.BorderColorHex;
            _borderUseGradient = settings.BorderUseGradient;
            _borderGradientStartHex = settings.BorderGradientStartHex;
            _borderGradientEndHex = settings.BorderGradientEndHex;
            _showShadow = settings.ShowShadow;
            _borderShadowRadius = settings.BorderShadowRadius;
            _borderShadowColorHex = settings.BorderShadowColorHex;
            _brightness = settings.Brightness;
            _contrast = settings.Contrast;
            _saturation = settings.Saturation;
            _windowSizePreset = settings.WindowSizePreset;
            _screenEdge = settings.ScreenEdge;
            _spaceSlot1 = settings.SpaceSlot1;
            _spaceSlot2 = settings.SpaceSlot2;
            _alwaysOnTop = settings.AlwaysOnTop;
            _globalShortcutsEnabled = settings.GlobalShortcutsEnabled;
            _openAtLogin = settings.OpenAtLogin;
            _language = settings.Language;
        }
        finally
        {
            _loading = false;
        }
    }

    private PresetPlacement CurrentPlacement()
    {
        var key = AppSettings.KeyFor(WindowSizePreset);
        if (!_placements.TryGetValue(key, out var placement))
        {
            placement = new PresetPlacement { ScreenEdge = ScreenEdge };
            _placements[key] = placement;
        }

        return placement;
    }

    /// <summary>
    /// Copies the saved placements into a case-insensitive map. A hand-edited file can hold the same preset twice
    /// ("md" and "MD"); <c>ToDictionary</c> would throw on that and the app would never start, so the first key wins.
    /// </summary>
    private static Dictionary<string, PresetPlacement> BuildPlacementMap(Dictionary<string, PresetPlacement> source)
    {
        var placements = new Dictionary<string, PresetPlacement>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            placements.TryAdd(pair.Key, pair.Value?.Clone() ?? new PresetPlacement());
        }

        return placements;
    }

    private bool SetClamped(ref double field, double value, double min, double max, [CallerMemberName] string? propertyName = null)
    {
        if (!double.IsFinite(value))
        {
            return false;
        }

        var clamped = Math.Clamp(value, min, max);
        if (Math.Abs(field - clamped) < 1e-9)
        {
            return false;
        }

        field = clamped;
        OnPropertyChanged(propertyName);
        ScheduleSave();
        return true;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (!_loading)
        {
            ScheduleSave();
        }

        return true;
    }

    private void ScheduleSave()
    {
        if (_loading)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
