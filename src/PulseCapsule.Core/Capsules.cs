namespace PulseCapsule.Core;

public enum CapsuleStatus { Ready, Busy, Warning, Unavailable, Paused }
public sealed record CapsuleAction(string Id, string Title, Func<Task> Execute, bool Enabled = true);
public sealed record CapsuleAppearance(bool Centered = false, bool ShowStatus = true, double FontSize = 13, double DockFontSize = 12);

public interface ICapsule : IDisposable
{
    string Id { get; }
    string Title { get; }
    string PrimaryText { get; }
    string SecondaryText { get; }
    CapsuleStatus Status { get; }
    string Tooltip { get; }
    bool CanExpand { get; }
    object? ExpandedContent { get; }
    IReadOnlyList<CapsuleAction> Actions { get; }
    CapsuleAppearance Appearance { get; }
    string StatusColor { get; }
    string Footer { get; }
    event Action? Changed;
    Task Initialize();
    Task Refresh(bool force = false);
    void Pause();
    Task Resume();
}

public sealed record CapsulePreference
{
    public string Id { get; set; } = "clock";
    public bool Enabled { get; set; } = true;
}

public sealed record SystemPreferences
{
    public bool Temperature { get; set; } = true;
    public bool FanRpm { get; set; } = true;
    public bool FanControl { get; set; } = true;
}

public sealed record CapsuleModule(string Id, string Name, bool Enabled, IReadOnlyList<ProviderConfig> Providers);

public static class CapsuleSelection
{
    public const string ClockName = "时钟";
    public const string SystemName = "硬件监控";
    public static int Cycle(int index, int steps, int count) => count == 0 ? -1 : ((index - steps) % count + count) % count;
    public static AppSettings Normalize(AppSettings settings)
    {
        var preferences = (settings.Capsules ?? []).Where(p => p is not null).DistinctBy(p => p.Id).ToList();
        // Migrate the persisted family-wide quota gate into its source flags once.
        var providers = preferences.FirstOrDefault(p => p.Id == "quota") is { Enabled: false }
            ? settings.Providers.Select(p => p with { Enabled = false }).ToList() : settings.Providers;
        var groups = ProviderGroup.Group(providers);
        var bySource = groups.SelectMany(g => g.Providers.Select(p => (Id: "quota:" + p.Id, Group: g))).ToDictionary(p => p.Id, p => p.Group);
        var result = new List<CapsulePreference>();
        void Add(string id, bool enabled) { if (result.All(p => p.Id != id)) result.Add(new() { Id = id, Enabled = enabled }); }
        foreach (var preference in preferences)
        {
            if (preference.Id is "clock" or "system") Add(preference.Id, preference.Enabled);
            else if (preference.Id == "quota") foreach (var group in groups) Add(group.Id, group.Enabled);
            else if (bySource.TryGetValue(preference.Id, out var group)) Add(group.Id, group.Enabled);
        }
        if (preferences.Count == 0) Add("clock", true);
        foreach (var group in groups) Add(group.Id, group.Enabled);
        Add("clock", true); Add("system", true);
        if (result.All(p => !p.Enabled)) result.Single(p => p.Id == "clock").Enabled = true;
        var last = settings.LastCapsuleId;
        if (last is not null && bySource.TryGetValue(last, out var selected)) last = selected.Id;
        return settings with { Version = 2, Providers = providers, Capsules = result, LastCapsuleId = last };
    }

    public static IReadOnlyList<CapsuleModule> Modules(AppSettings settings)
    {
        settings = Normalize(settings);
        var groups = ProviderGroup.Group(settings.Providers).ToDictionary(g => g.Id);
        return settings.Capsules.Select(p => groups.TryGetValue(p.Id, out var group)
            ? new CapsuleModule(p.Id, group.Name, group.Enabled, group.Providers)
            : new CapsuleModule(p.Id, p.Id == "clock" ? ClockName : SystemName, p.Enabled, [])).ToList();
    }
}
