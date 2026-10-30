namespace CameraMan.Windows.Views.Settings;

public enum SettingsPage
{
    Camera,
    Shape,
    Framing,
    Image,
    Border,
    Window,
    General,
    Shortcuts,
    About,
}

public sealed record SettingsPageInfo(
    SettingsPage Page,
    string TitleKey,
    string Glyph,
    string AccentResourceKey,
    string[] SearchKeys);

/// <summary>Order, icons and searchable row titles of the nine pages, mirroring the macOS settings sidebar.</summary>
public static class SettingsPages
{
    public static readonly SettingsPageInfo[] All =
    [
        new(SettingsPage.Camera, "Page_Camera", "\uE714", "AccentCamera",
            ["Camera_Source", "Camera_VideoEffectsTitle", "Camera_Status", "Camera_Format"]),
        new(SettingsPage.Shape, "Page_Shape", "\uEA3A", "AccentShape",
            ["Shape_Circle", "Shape_Organic", "Shape_Square", "Shape_Vertical", "Shape_Horizontal",
             "Shape_SoftEdge", "Shape_CornerRadius", "Blob_Shuffle", "Shape_Breathing"]),
        new(SettingsPage.Framing, "Page_Framing", "\uE7A8", "AccentFraming",
            ["Framing_Horizontal", "Framing_Vertical", "Framing_Zoom", "Framing_Mirror", "Framing_Flip"]),
        new(SettingsPage.Image, "Page_Image", "\uE790", "AccentImage",
            ["Image_Brightness", "Image_Contrast", "Image_Saturation"]),
        new(SettingsPage.Border, "Page_Border", "\uE790", "AccentBorder",
            ["Border_Show", "Border_Width", "Border_Gradient", "Border_Color", "Border_StartColor",
             "Border_EndColor", "Shadow_Show", "Shadow_Radius", "Shadow_Color"]),
        new(SettingsPage.Window, "Page_Window", "\uE737", "AccentWindow",
            ["Window_Size", "Window_Corner", "Window_Slot1", "Window_Slot2", "Window_AlwaysOnTop"]),
        new(SettingsPage.General, "Page_General", "\uE713", "AccentGeneral",
            ["General_OpenAtLogin", "General_Language", "General_Restore"]),
        new(SettingsPage.Shortcuts, "Page_Shortcuts", "\uE765", "AccentShortcuts",
            ["Shortcuts_GlobalToggle", "Shortcuts_ShowHide", "Shortcuts_Global", "Shortcuts_Window", "Shortcuts_Menu"]),
        new(SettingsPage.About, "Page_About", "\uE946", "AccentAbout", []),
    ];

    public static readonly SettingsPage[][] Sections =
    [
        [SettingsPage.Camera, SettingsPage.Shape, SettingsPage.Framing, SettingsPage.Image, SettingsPage.Border],
        [SettingsPage.Window, SettingsPage.General, SettingsPage.Shortcuts, SettingsPage.About],
    ];

    public static SettingsPageInfo Get(SettingsPage page) => All.First(info => info.Page == page);
}
