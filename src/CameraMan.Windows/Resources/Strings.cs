using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace CameraMan.Windows.Resources;

/// <summary>Access to the English and Brazilian Portuguese resource catalogs.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new(
        "CameraMan.Windows.Resources.Strings",
        typeof(Strings).Assembly);

    public static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? Manager.GetString(key, CultureInfo.GetCultureInfo("en"))
        ?? key;

    /// <summary>Looks a key up in a specific language (used by the settings search to match English too).</summary>
    public static string Get(string key, CultureInfo culture) =>
        Manager.GetString(key, culture)
        ?? Manager.GetString(key, CultureInfo.GetCultureInfo("en"))
        ?? key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Applies the saved language preference. Must run before the first window is created.</summary>
    public static void ApplyLanguage(string preference)
    {
        var culture = preference switch
        {
            "pt-BR" => CultureInfo.GetCultureInfo("pt-BR"),
            "en" => CultureInfo.GetCultureInfo("en"),
            _ => CultureInfo.InstalledUICulture,
        };
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Strings.Get(Key);
}
