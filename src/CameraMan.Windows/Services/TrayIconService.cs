using System.ComponentModel;
using System.Drawing;
using CameraMan.Windows.Models;
using CameraMan.Windows.Resources;
using CameraMan.Windows.ViewModels;
using Forms = System.Windows.Forms;

namespace CameraMan.Windows.Services;

/// <summary>
/// The notification-area icon and its menu: show/hide, settings, restore defaults, size, space slots, screen corner,
/// camera selection, always-on-top and quit. The menu is rebuilt when it opens, so it always reflects the live state.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly AppState _state;
    private readonly CameraCoordinator _camera;
    private readonly Action _toggleCamera;
    private readonly Action _openSettings;
    private readonly Action _restoreDefaults;
    private readonly Action _quit;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Icon _iconImage;
    private bool _disposed;

    public TrayIconService(
        AppState state,
        CameraCoordinator camera,
        Action toggleCamera,
        Action openSettings,
        Action restoreDefaults,
        Action quit)
    {
        _state = state;
        _camera = camera;
        _toggleCamera = toggleCamera;
        _openSettings = openSettings;
        _restoreDefaults = restoreDefaults;
        _quit = quit;

        _iconImage = LoadIcon();
        _menu = new Forms.ContextMenuStrip();
        _menu.Opening += OnMenuOpening;
        _icon = new Forms.NotifyIcon
        {
            Text = Strings.Get("Tray_Tooltip"),
            Icon = _iconImage,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => _openSettings();
        _state.PropertyChanged += OnStatePropertyChanged;
        Rebuild();
    }

    /// <summary>Rebuilds the menu now (used after show/hide from a shortcut or a settings change).</summary>
    public void Refresh()
    {
        if (!_disposed)
        {
            Rebuild();
        }
    }

    private void OnMenuOpening(object? sender, CancelEventArgs e)
    {
        Rebuild();
        _ = RefreshDevicesAsync();
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            await _camera.RefreshDevicesAsync();
        }
        catch (Exception)
        {
            // The menu still works with the device list it already has; the Camera page offers a manual retry.
        }
    }

    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        _menu.Items.Clear();

        _menu.Items.Add(Item(
            _state.IsWindowVisible ? Strings.Get("Tray_HideCamera") : Strings.Get("Tray_ShowCamera"),
            _toggleCamera));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Item(Strings.Get("Tray_Settings"), _openSettings, bold: true));
        _menu.Items.Add(Item(Strings.Get("Tray_RestoreDefaults"), _restoreDefaults));
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var sizeMenu = new Forms.ToolStripMenuItem(Strings.Get("Tray_WindowSize"));
        foreach (var preset in WindowSizePresetExtensions.All)
        {
            var item = CheckItem(
                Strings.Get(preset.ResourceKey()),
                _state.WindowSizePreset == preset,
                () => _state.WindowSizePreset = preset);
            sizeMenu.DropDownItems.Add(item);
        }

        _menu.Items.Add(sizeMenu);

        var slot1 = new Forms.ToolStripMenuItem(Strings.Get("Tray_SpaceSlot1"));
        foreach (var preset in WindowSizePresetExtensions.All)
        {
            slot1.DropDownItems.Add(CheckItem(
                Strings.Get(preset.ResourceKey()),
                _state.SpaceSlot1 == preset,
                () => _state.SpaceSlot1 = preset));
        }

        _menu.Items.Add(slot1);

        var slot2 = new Forms.ToolStripMenuItem(Strings.Get("Tray_SpaceSlot2"));
        foreach (var preset in WindowSizePresetExtensions.All)
        {
            slot2.DropDownItems.Add(CheckItem(
                Strings.Get(preset.ResourceKey()),
                _state.SpaceSlot2 == preset,
                () => _state.SpaceSlot2 = preset));
        }

        _menu.Items.Add(slot2);

        var edgeMenu = new Forms.ToolStripMenuItem(Strings.Get("Tray_ScreenEdge"));
        foreach (var edge in WindowSizePresetExtensions.Edges)
        {
            edgeMenu.DropDownItems.Add(CheckItem(
                Strings.Get(edge.ResourceKey()),
                _state.ScreenEdge == edge,
                () => _state.ScreenEdge = edge));
        }

        _menu.Items.Add(edgeMenu);

        var cameraMenu = new Forms.ToolStripMenuItem(Strings.Get("Tray_Camera"));
        if (_state.VideoDevices.Count == 0)
        {
            cameraMenu.DropDownItems.Add(new Forms.ToolStripMenuItem(Strings.Get("Tray_NoCameras")) { Enabled = false });
        }
        else
        {
            foreach (var device in _state.VideoDevices)
            {
                var id = device.Id;
                cameraMenu.DropDownItems.Add(CheckItem(
                    device.Name,
                    string.Equals(id, _state.SelectedDeviceId, StringComparison.Ordinal),
                    () => _ = SelectCameraAsync(id)));
            }
        }

        cameraMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        cameraMenu.DropDownItems.Add(Item(Strings.Get("Tray_RefreshCameras"), () => _ = RefreshDevicesAsync()));
        _menu.Items.Add(cameraMenu);

        _menu.Items.Add(CheckItem(
            Strings.Get("Tray_AlwaysOnTop"),
            _state.AlwaysOnTop,
            () => _state.AlwaysOnTop = !_state.AlwaysOnTop));

        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Item(Strings.Get("Tray_Quit"), _quit));

        _icon.Text = Strings.Get("Tray_Tooltip");
    }

    private async Task SelectCameraAsync(string id)
    {
        try
        {
            await _camera.SelectCameraAsync(id);
        }
        catch (Exception ex)
        {
            _state.ShowToast(ex.Message);
        }
    }

    private static Forms.ToolStripMenuItem Item(string text, Action action, bool bold = false)
    {
        var item = new Forms.ToolStripMenuItem(text);
        if (bold)
        {
            item.Font = new Font(item.Font, System.Drawing.FontStyle.Bold);
        }

        item.Click += (_, _) => action();
        return item;
    }

    private static Forms.ToolStripMenuItem CheckItem(string text, bool isChecked, Action action)
    {
        var item = new Forms.ToolStripMenuItem(text) { Checked = isChecked };
        item.Click += (_, _) => action();
        return item;
    }

    private static Icon LoadIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var extracted = Icon.ExtractAssociatedIcon(path);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to the generic application icon.
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (e.PropertyName is nameof(AppState.IsWindowVisible)
            or nameof(AppState.WindowSizePreset)
            or nameof(AppState.AlwaysOnTop)
            or nameof(AppState.SelectedDeviceId))
        {
            Rebuild();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _state.PropertyChanged -= OnStatePropertyChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _iconImage.Dispose();
    }
}
