using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CameraMan.Windows.Resources;
using CameraMan.Windows.Services;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Views.Settings;

/// <summary>
/// The single settings window. Pages are created once and reused; the sidebar follows System Settings with grouped
/// colored icons and a search field that matches page and row titles in both languages.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppState _state;
    private readonly Dictionary<SettingsPage, UserControl> _pages;
    private SettingsPage _current = SettingsPage.Camera;
    private bool _allowClose;

    public SettingsWindow(AppState state)
    {
        _state = state;
        DataContext = state;
        InitializeComponent();

        _pages = new Dictionary<SettingsPage, UserControl>
        {
            [SettingsPage.Camera] = new CameraSettingsPage { DataContext = state },
            [SettingsPage.Shape] = new ShapeSettingsPage { DataContext = state },
            [SettingsPage.Framing] = new FramingSettingsPage { DataContext = state },
            [SettingsPage.Image] = new ImageSettingsPage { DataContext = state },
            [SettingsPage.Border] = new BorderSettingsPage { DataContext = state },
            [SettingsPage.Window] = new WindowSettingsPage { DataContext = state },
            [SettingsPage.General] = new GeneralSettingsPage { DataContext = state },
            [SettingsPage.Shortcuts] = new ShortcutsSettingsPage { DataContext = state },
            [SettingsPage.About] = new AboutSettingsPage { DataContext = state },
        };

        ShowPage(SettingsPage.Camera);
        UpdateSearchHint();
    }

    public void ShowPage(SettingsPage page)
    {
        _current = page;
        PageHost.Content = _pages[page];
        RebuildNavigation();
    }

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowAppearance.ApplyDarkTitleBar(this, ThemeService.IsDark());
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _state.SaveNow();
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchHint();
        RebuildNavigation();
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SearchBox.Text = string.Empty;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            Hide();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void UpdateSearchHint() =>
        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;

    private void RebuildNavigation()
    {
        NavPanel.Children.Clear();
        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
        {
            for (var group = 0; group < SettingsPages.Sections.Length; group++)
            {
                if (group > 0)
                {
                    NavPanel.Children.Add(new Border { Height = 16 });
                }

                foreach (var page in SettingsPages.Sections[group])
                {
                    NavPanel.Children.Add(CreatePageButton(SettingsPages.Get(page), page == _current));
                }
            }

            return;
        }

        var any = false;
        foreach (var info in SettingsPages.All)
        {
            var pageMatches = Matches(info.TitleKey, query);
            var settingMatches = info.SearchKeys.Where(key => Matches(key, query)).ToList();
            if (!pageMatches && settingMatches.Count == 0)
            {
                continue;
            }

            any = true;
            NavPanel.Children.Add(CreatePageButton(info, info.Page == _current));
            foreach (var key in settingMatches)
            {
                NavPanel.Children.Add(CreateSearchResultButton(info.Page, key));
            }

            NavPanel.Children.Add(new Border { Height = 6 });
        }

        if (!any)
        {
            var empty = new TextBlock
            {
                Text = Strings.Get("Settings_NoResults"),
                FontSize = 13,
                Margin = new Thickness(4, 20, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            NavPanel.Children.Add(empty);
        }
    }

    private static bool Matches(string key, string query)
    {
        var current = Strings.Get(key);
        if (current.Contains(query, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        var english = Strings.Get(key, CultureInfo.GetCultureInfo("en"));
        return english.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private Button CreatePageButton(SettingsPageInfo info, bool selected)
    {
        var icon = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = info.Glyph,
                FontSize = 12.5,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                // Segoe MDL2 glyphs sit a hair low inside a square box; lift them.
                Margin = new Thickness(0, 0, 0, 2),
            },
        };
        icon.SetResourceReference(Border.BackgroundProperty, info.AccentResourceKey);
        ((TextBlock)icon.Child).SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");

        var label = new TextBlock
        {
            Text = Strings.Get(info.TitleKey),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (selected)
        {
            label.Foreground = Brushes.White;
        }
        else
        {
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Children.Add(icon);
        content.Children.Add(label);

        var button = new Button
        {
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 5, 6, 5),
            Margin = new Thickness(0, 0, 0, 2),
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Tag = selected ? "Selected" : "Unselected",
            Background = selected ? (Brush)FindResource("SelectionBackground") : Brushes.Transparent,
            Height = 36,
        };
        AutomationProperties.SetName(button, Strings.Get(info.TitleKey));
        button.Click += (_, _) => ShowPage(info.Page);
        return button;
    }

    private Button CreateSearchResultButton(SettingsPage page, string key)
    {
        var label = new TextBlock
        {
            Text = Strings.Get(key),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");

        var button = new Button
        {
            Content = label,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(40, 4, 6, 4),
            Margin = new Thickness(0),
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Cursor = Cursors.Hand,
            Height = 28,
        };
        AutomationProperties.SetName(button, Strings.Get(key));
        button.Click += (_, _) => ShowPage(page);
        return button;
    }
}
