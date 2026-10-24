using PulseCapsule.Services;
using PulseCapsule.UI;

namespace PulseCapsule.Capsules.Quota;

// One supplier is one selectable module; each underlying source retains its own data.
public sealed class QuotaCapsule : CapsuleBase
{
    private readonly QuotaMonitor monitor;
    private readonly CapsuleModule module;
    private readonly QuotaExpandedView view;
    private string color = "#A6EDCF";
    public override string Id => module.Id;
    public override string Title => module.Name;
    public override string StatusColor => color;
    public override string Footer => monitor.Demo ? "演示数据 · 仅用于预览" : monitor.Paused ? "暂停刷新" : monitor.IsRefreshing ? "正在同步…" : monitor.Warning ?? base.Footer;
    public bool Matches(CapsuleModule value) => module.Id == value.Id && module.Name == value.Name
        && module.Providers.SequenceEqual(value.Providers, ReferenceEqualityComparer.Instance);
    public QuotaCapsule(CapsuleModule module, QuotaMonitor monitor, Action<string> openSettings, Action<string> disable, Action<string, bool> saveDetails) : base(TimeSpan.FromSeconds(15))
    {
        this.module = module; this.monitor = monitor;
        view = new(openSettings, monitor.RefreshProviderAsync, disable,
            id => monitor.Settings.CardDetailsExpanded.GetValueOrDefault(id), saveDetails); ExpandedContent = view;
        monitor.Changed += Update;
        Update();
    }
    private void Update()
    {
        try
        {
            var cards = CapsuleSelection.Modules(monitor.Settings).SelectMany(m => m.Providers).Where(p => p.Enabled)
                .Select(p => new CardViewModel(p, monitor.Snapshots.GetValueOrDefault(p.Id), monitor.History(p.Id))).ToList();
            view.Update(cards);
            var primary = module.Providers.FirstOrDefault(p => p.Enabled);
            var card = cards.FirstOrDefault(c => c.Config.Id == primary?.Id);
            PrimaryText = module.Name; SecondaryText = card?.PrimaryValue ?? "—";
            color = card?.StatusBrush.ToString() ?? "#94A8B9";
            Status = card?.Snapshot?.Status switch { SnapshotStatus.Ok => CapsuleStatus.Ready, SnapshotStatus.Stale => CapsuleStatus.Warning, _ => CapsuleStatus.Unavailable };
            Tooltip = string.Join("\n", cards.Select(c => c.Name + " " + c.PrimaryValue + " · " + c.StatusText));
            Publish();
        }
        catch { Status = CapsuleStatus.Unavailable; PrimaryText = module.Name; SecondaryText = "unavailable"; Publish(); }
    }
    protected override async Task RefreshCore(bool force) { await monitor.RefreshAsync(force); Update(); }
    public override void Pause() { base.Pause(); monitor.SetPaused(true); }
    public override async Task Resume() { monitor.SetPaused(false); await base.Resume(); }
    public override void Dispose() { monitor.Changed -= Update; base.Dispose(); }
}
