namespace QuotaPeek.Core;

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

public static class CapsuleSelection
{
    public static int Cycle(int index, int steps, int count) => count == 0 ? -1 : ((index - steps) % count + count) % count;
    public static List<CapsulePreference> Normalize(IEnumerable<CapsulePreference>? preferences)
    {
        string[] known = ["clock", "quota", "system"];
        var result = (preferences ?? []).Where(p => known.Contains(p.Id)).DistinctBy(p => p.Id).ToList();
        foreach (var id in known.Where(id => result.All(p => p.Id != id))) result.Add(new() { Id = id });
        if (result.All(p => !p.Enabled)) result[0].Enabled = true;
        return result;
    }
}
