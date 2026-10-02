using QuotaPeek.Services;
using QuotaPeek.UI;

namespace QuotaPeek.Capsules.Quota;

// Each enabled provider is an ordinary capsule. The existing grouped detail view is shared by the family.
public sealed class QuotaCapsule : CapsuleBase
{
    private readonly QuotaMonitor monitor;
    private readonly ProviderConfig config;
    private readonly QuotaExpandedView view;
    private string color = "#A6EDCF";
    public override string Id => "quota:" + config.Id;
    public override string Title => config.Name;
    public override string StatusColor => color;
    public override string Footer => monitor.Demo ? "演示数据 · 仅用于预览" : monitor.Paused ? "暂停刷新" : monitor.IsRefreshing ? "正在同步…" : monitor.Warning ?? base.Footer;
    public QuotaCapsule(ProviderConfig config, QuotaMonitor monitor, Action openSettings) : base(TimeSpan.FromSeconds(15))
    {
        this.config = config; this.monitor = monitor;
        view = new(openSettings); ExpandedContent = view;
        monitor.Changed += Update;
        Update();
    }
    private void Update()
    {
        try
        {
            var cards = monitor.Settings.Providers.Where(p => p.Enabled)
                .Select(p => new CardViewModel(p, monitor.Snapshots.GetValueOrDefault(p.Id), monitor.History(p.Id))).ToList();
            view.Update(cards);
            var card = cards.FirstOrDefault(c => c.Config.Id == config.Id);
            PrimaryText = config.Name; SecondaryText = card?.PrimaryValue ?? "—";
            color = card?.StatusBrush.ToString() ?? "#94A8B9";
            Status = card?.Snapshot?.Status switch { SnapshotStatus.Ok => CapsuleStatus.Ready, SnapshotStatus.Stale => CapsuleStatus.Warning, _ => CapsuleStatus.Unavailable };
            Tooltip = string.Join("\n", cards.Select(c => c.Name + " " + c.PrimaryValue + " · " + c.StatusText));
            Publish();
        }
        catch { Status = CapsuleStatus.Unavailable; PrimaryText = config.Name; SecondaryText = "unavailable"; Publish(); }
    }
    protected override async Task RefreshCore(bool force) { await monitor.RefreshAsync(force); Update(); }
    public override void Pause() { base.Pause(); monitor.SetPaused(true); }
    public override async Task Resume() { monitor.SetPaused(false); await base.Resume(); }
    public override void Dispose() { monitor.Changed -= Update; base.Dispose(); }
}
