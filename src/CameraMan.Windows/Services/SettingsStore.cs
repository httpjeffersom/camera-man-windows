using System.Text.Json;
using System.Text.Json.Serialization;
using CameraMan.Windows.Models;

namespace CameraMan.Windows.Services;

/// <summary>
/// Loads and saves the JSON settings file atomically. A corrupt or unreadable file falls back to defaults instead of
/// preventing the app from starting; the bad file is preserved next to the new one for inspection.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public SettingsStore(string? directory = null)
    {
        Directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CameraMan");
        FilePath = Path.Combine(Directory, "settings.json");
    }

    public string Directory { get; }

    public string FilePath { get; }

    public AppSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A settings file must never stop the app from starting: anything from a truncated write to a
            // hand-edited file with a null wave falls back to defaults and keeps the bad file for inspection.
            warning = ex.Message;
            TryPreserveCorruptFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Version = AppSettings.CurrentVersion;
        System.IO.Directory.CreateDirectory(Directory);
        var temporary = FilePath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(temporary, json);
        File.Move(temporary, FilePath, overwrite: true);
    }

    public void Delete()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    private void TryPreserveCorruptFile()
    {
        try
        {
            var backup = FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(FilePath, backup, overwrite: true);
        }
        catch (IOException)
        {
            // Best effort only.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort only.
        }
    }
}
