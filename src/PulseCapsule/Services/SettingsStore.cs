using System.Text.Json;
using System.Text.Json.Serialization;

namespace PulseCapsule.Services;

public sealed class SettingsStore
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public string? Warning { get; private set; }
    public bool NeedsUpgrade { get; private set; }
    public SettingsStore(string directoryPath) { DirectoryPath = directoryPath; Directory.CreateDirectory(directoryPath); }

    public AppSettings Load()
    {
        if (!File.Exists(FilePath)) return CapsuleSelection.Normalize(new());
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            var settings = document.Deserialize<AppSettings>(Json) ?? throw new JsonException();
            if (!document.RootElement.TryGetProperty("Version", out _)) settings.Version = 1;
            if (settings.Version is not (1 or 2) || settings.Providers is null || settings.Providers.Any(p => string.IsNullOrWhiteSpace(p.Id)) || settings.Providers.Select(p => p.Id).Distinct().Count() != settings.Providers.Count)
                throw new JsonException();
            foreach (var provider in settings.Providers) provider.RefreshMinutes = Math.Clamp(provider.RefreshMinutes, 1, 1440);
            NeedsUpgrade = settings.Version == 1;
            settings.System ??= new();
            settings.CardDetailsExpanded ??= [];
            settings.CarouselSeconds = Math.Clamp(settings.CarouselSeconds, 5, 3600);
            return CapsuleSelection.Normalize(settings);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            var backup = FilePath + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            File.Copy(FilePath, backup, false);
            Warning = "设置文件无法读取，原文件已备份；当前使用默认设置。";
            return CapsuleSelection.Normalize(new());
        }
    }

    public void Save(AppSettings settings)
    {
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(CapsuleSelection.Normalize(settings), Json));
        File.Move(temporary, FilePath, true);
    }
}
