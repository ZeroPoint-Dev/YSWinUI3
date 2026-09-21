using System;
using System.IO;
using System.Text.Json;

namespace YSWinUI3.Settings;

public sealed class YSSettingsStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public YSSettingsStore(string applicationName, string fileName = "settings.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            applicationName);
        _filePath = Path.Combine(folder, fileName);
    }

    public T Load<T>() where T : new()
    {
        try
        {
            if (!File.Exists(_filePath)) return new T();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(_filePath), _jsonOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    public void Save<T>(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? folder = Path.GetDirectoryName(_filePath);
        if (folder is not null) Directory.CreateDirectory(folder);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, _jsonOptions));
    }
}
