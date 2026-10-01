using System;
using System.IO;
using System.Text.Json;

namespace WhalePet;

public sealed class Settings
{
    public bool Hungry { get; set; }
    public DateTimeOffset? LastMealCheck { get; set; }
    public double Width { get; set; } = 250;
    public double? X { get; set; }
    public double? Y { get; set; }
    public bool StartupConfigured { get; set; }
}

public sealed class Storage
{
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhaleMaidPet");
    public string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new Settings(); }
    }
    public void Save(Settings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, SettingsPath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Log(e); }
    }
    public void Log(Exception error)
    {
        try { Directory.CreateDirectory(DirectoryPath); File.AppendAllText(Path.Combine(DirectoryPath, "errors.log"), $"{DateTimeOffset.Now:O} {error}\n"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
