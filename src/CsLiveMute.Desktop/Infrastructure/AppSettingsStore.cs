using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CsLiveMute.Core.Models;

namespace CsLiveMute.Desktop.Infrastructure;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static AppSettingsStore()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly string _settingsPath;

    public AppSettingsStore()
    {
        var appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CsLiveMute");

        Directory.CreateDirectory(appDataFolder);
        _settingsPath = Path.Combine(appDataFolder, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = AppSettings.CreateDefault();
            await SaveAsync(defaults);
            return defaults;
        }

        await using var stream = File.OpenRead(_settingsPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions);
        return settings ?? AppSettings.CreateDefault();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions);
    }
}
