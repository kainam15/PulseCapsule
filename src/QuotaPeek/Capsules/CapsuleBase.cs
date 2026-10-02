using System.Windows.Threading;

namespace QuotaPeek.Capsules;

public abstract class CapsuleBase : ICapsule
{
    private readonly DispatcherTimer? timer;
    private bool refreshing, disposed;
    protected bool Paused { get; private set; }
    public abstract string Id { get; }
    public abstract string Title { get; }
    public string PrimaryText { get; protected set; } = "加载中…";
    public string SecondaryText { get; protected set; } = "";
    public CapsuleStatus Status { get; protected set; } = CapsuleStatus.Busy;
    public string Tooltip { get; protected set; } = "";
    public virtual bool CanExpand => true;
    public object? ExpandedContent { get; protected set; }
    public virtual IReadOnlyList<CapsuleAction> Actions => [];
    public virtual CapsuleAppearance Appearance => new();
    public virtual string StatusColor => Status switch { CapsuleStatus.Unavailable => "#FFB1A6", CapsuleStatus.Warning => "#F8D98B", _ => "#A6EDCF" };
    public virtual string Footer => Paused ? "暂停刷新" : "自动刷新 · 数据保存在本机";
    public event Action? Changed;

    protected CapsuleBase(TimeSpan? interval = null)
    {
        if (interval is null) return;
        timer = new DispatcherTimer { Interval = interval.Value };
        timer.Tick += async (_, _) => await Refresh();
    }

    protected void Publish() => Changed?.Invoke();
    public virtual async Task Initialize() { await Refresh(); if (!disposed && !Paused) timer?.Start(); }
    public async Task Refresh(bool force = false)
    {
        if (refreshing || disposed || Paused) return;
        refreshing = true;
        try { await RefreshCore(force); }
        catch (Exception) { Status = CapsuleStatus.Unavailable; PrimaryText = "unavailable"; Tooltip = Title + " 暂时无法读取，将自动重试。"; }
        finally { refreshing = false; if (!disposed) Publish(); }
    }
    protected abstract Task RefreshCore(bool force);
    public virtual void Pause() { Paused = true; timer?.Stop(); }
    public virtual async Task Resume() { Paused = false; await Initialize(); }
    public virtual void Dispose() { disposed = true; timer?.Stop(); Changed = null; }
}
