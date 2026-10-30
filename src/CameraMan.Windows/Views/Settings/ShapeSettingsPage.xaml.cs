using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CameraMan.Windows.Models;
using CameraMan.Windows.Resources;
using CameraMan.Windows.ViewModels;
using CameraMan.Windows.Views.Controls;

namespace CameraMan.Windows.Views.Settings;

public partial class ShapeSettingsPage : UserControl
{
    private readonly List<BlobChip> _chips = [];
    private AppState? _state;

    public ShapeSettingsPage()
    {
        InitializeComponent();
        BuildOrganicChips();
        DataContextChanged += (_, e) => Attach(e.NewValue as AppState);
        Loaded += (_, _) => Attach(DataContext as AppState);
        Unloaded += (_, _) => Attach(null);
    }

    private void BuildOrganicChips()
    {
        foreach (var preset in OrganicBlob.Presets)
        {
            var chip = new BlobChip
            {
                Label = Strings.Get(preset.ResourceKey),
                Blob = preset.Blob,
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = Strings.Get(preset.ResourceKey),
            };
            chip.Click += (_, _) => _state?.SetOrganicPreset(preset.Blob);
            _chips.Add(chip);
            BlobPanel.Children.Add(chip);
        }

        var shuffle = new BlobChip
        {
            Label = Strings.Get("Blob_Shuffle"),
            Blob = null,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = Strings.Get("Blob_Shuffle"),
        };
        shuffle.Click += (_, _) => _state?.RandomizeOrganic();
        _chips.Add(shuffle);
        BlobPanel.Children.Add(shuffle);
    }

    private void Attach(AppState? state)
    {
        if (_state is not null)
        {
            _state.PropertyChanged -= OnStatePropertyChanged;
        }

        _state = state;
        if (_state is not null)
        {
            _state.PropertyChanged += OnStatePropertyChanged;
        }

        UpdateChipSelection();
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.OrganicBlob))
        {
            UpdateChipSelection();
        }
    }

    private void UpdateChipSelection()
    {
        if (_state is null)
        {
            return;
        }

        var current = _state.OrganicBlob;
        var isPreset = OrganicBlob.IsPreset(current);
        for (var i = 0; i < _chips.Count; i++)
        {
            if (i < OrganicBlob.Presets.Length)
            {
                _chips[i].IsSelected = OrganicBlob.Presets[i].Blob.Equals(current);
            }
            else
            {
                _chips[i].IsSelected = !isPreset;
            }
        }
    }
}
