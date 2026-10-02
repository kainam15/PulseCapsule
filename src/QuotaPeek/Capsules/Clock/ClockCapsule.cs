using System.Windows;
using System.Windows.Controls;

namespace QuotaPeek.Capsules.Clock;

public sealed class ClockCapsule : CapsuleBase
{
    private readonly TextBlock detail = new() { FontSize = 28, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 25, 0, 30) };
    public override string Id => "clock";
    public override string Title => "Clock";
    public override CapsuleAppearance Appearance => new(true, false, 15, 14);
    public override string Footer => "本机时间 · 不显示秒";
    public ClockCapsule() : base(TimeSpan.FromSeconds(1)) => ExpandedContent = detail;
    protected override Task RefreshCore(bool force)
    {
        var now = DateTime.Now;
        PrimaryText = $"{now:yyyy-MM-dd} {now:HH:mm}";
        Tooltip = now.ToString("yyyy-MM-dd dddd") + "\n本机时间";
        detail.Text = $"{now:HH:mm}\n{now:yyyy-MM-dd dddd}";
        Status = CapsuleStatus.Ready;
        return Task.CompletedTask;
    }
}
