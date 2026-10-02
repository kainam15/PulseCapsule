using PulseCapsule.Capsules;
using System.Windows.Threading;

namespace PulseCapsule.Shell;

public sealed class CapsuleHost : IDisposable
{
    private List<ICapsule> capsules = [];
    private readonly Dictionary<ICapsule, Action> subscriptions = [];
    private readonly DispatcherTimer carousel = new();
    private readonly Action remember;
    private AppSettings settings;
    private bool paused, disposed;
    public IReadOnlyList<ICapsule> Capsules => capsules;
    public ICapsule? Current { get; private set; }
    public event Action? Changed;
    public event Action<string, string>? Notification;
    public CapsuleHost(AppSettings settings, Action remember)
    {
        this.settings = settings; this.remember = remember;
        carousel.Tick += (_, _) => Cycle(-1);
    }
    public void Notify(string title, string message) => Notification?.Invoke(title, message);
    public async Task Configure(AppSettings value, List<ICapsule> items)
    {
        settings = value;
        var id = Current?.Id ?? settings.LastCapsuleId;
        foreach (var old in capsules)
        {
            if (subscriptions.Remove(old, out var subscription)) old.Changed -= subscription;
            if (!items.Contains(old)) { try { old.Dispose(); } catch { } }
        }
        capsules = items;
        foreach (var capsule in capsules)
        {
            Action subscription = () => { if (Current == capsule) OnChanged(); };
            subscriptions[capsule] = subscription;
            capsule.Changed += subscription;
        }
        Current = capsules.FirstOrDefault(c => c.Id == id) ?? capsules.FirstOrDefault();
        if (settings.LastCapsuleId != Current?.Id) { settings.LastCapsuleId = Current?.Id; remember(); }
        carousel.Stop();
        carousel.Interval = TimeSpan.FromSeconds(Math.Clamp(settings.CarouselSeconds, 5, 3600));
        if (settings.AutoRotate && !paused) carousel.Start();
        OnChanged();
        foreach (var capsule in capsules.ToArray())
        {
            if (paused) capsule.Pause(); else await Guard(capsule, capsule.Initialize);
        }
    }
    public void Cycle(int steps)
    {
        if (capsules.Count < 2) return;
        var index = CapsuleSelection.Cycle(Math.Max(0, capsules.IndexOf(Current!)), steps, capsules.Count);
        Current = capsules[index]; settings.LastCapsuleId = Current.Id;
        remember(); OnChanged();
    }
    public Task Refresh(bool force = false) => Task.WhenAll(capsules.ToArray().Select(c => Guard(c, () => c.Refresh(force))));
    public async Task RunAction(string id)
    {
        var capsule = Current;
        var action = capsule?.Actions.FirstOrDefault(a => a.Id == id);
        if (capsule is not null && action is { Enabled: true }) await Guard(capsule, action.Execute);
        OnChanged();
    }
    public void Pause()
    {
        paused = true; carousel.Stop();
        foreach (var capsule in capsules) { try { capsule.Pause(); } catch { } }
        OnChanged();
    }
    public async Task Resume()
    {
        paused = false;
        foreach (var capsule in capsules.ToArray()) await Guard(capsule, capsule.Resume);
        if (settings.AutoRotate) carousel.Start();
        OnChanged();
    }
    private async Task Guard(ICapsule capsule, Func<Task> action)
    {
        try { await action(); }
        catch
        {
            var index = capsules.IndexOf(capsule);
            if (index < 0) return;
            if (subscriptions.Remove(capsule, out var subscription)) capsule.Changed -= subscription;
            try { capsule.Dispose(); } catch { }
            var unavailable = new UnavailableCapsule(capsule.Id, capsule.Title);
            capsules[index] = unavailable;
            if (Current == capsule) Current = unavailable;
            OnChanged();
        }
    }
    private void OnChanged() { if (!disposed) Changed?.Invoke(); }
    public void Dispose()
    {
        disposed = true; carousel.Stop();
        foreach (var capsule in capsules) { try { capsule.Dispose(); } catch { } }
        capsules.Clear();
        subscriptions.Clear();
    }
    private sealed class UnavailableCapsule : CapsuleBase
    {
        public override string Id { get; }
        public override string Title { get; }
        public UnavailableCapsule(string id, string title)
        {
            Id = id; Title = title; PrimaryText = title; SecondaryText = "unavailable"; Status = CapsuleStatus.Unavailable;
            Tooltip = title + " 暂时不可用";
            ExpandedContent = new System.Windows.Controls.TextBlock { Text = Tooltip, Margin = new System.Windows.Thickness(0, 12, 0, 20) };
        }
        protected override Task RefreshCore(bool force) { PrimaryText = Title; SecondaryText = "unavailable"; Status = CapsuleStatus.Unavailable; return Task.CompletedTask; }
    }
}
