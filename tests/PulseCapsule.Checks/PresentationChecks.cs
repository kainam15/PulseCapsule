using System.Windows;
using PulseCapsule.Core;
using PulseCapsule.UI;

static class PresentationChecks
{
    public static void Run(Action<string, bool> check)
    {
        var now = DateTimeOffset.UtcNow;
        check("relative update retains unknown timestamps", DisplayText.RelativeUpdate(null, now) == "尚未读取数据");
        check("relative update uses elapsed minutes", DisplayText.RelativeUpdate(now.AddMinutes(-3), now) == "3 分钟前更新");
        check("plan date includes full year without altering failure text", DisplayText.PlanDates("套餐到期：27-05-15 10:15；重置日期：2026-11-17") == "套餐到期：2027年5月15日 10:15；重置日期：2026年11月17日"
            && DisplayText.PlanDates("连接失败 401") == "连接失败 401");
        var provider = ProviderConfig.Create(ProviderType.WgetCloud);
        QuotaSnapshot Point(decimal balance, int hours) => new() { ProviderId = provider.Id, Currency = "GB", Remaining = balance, FetchedAt = now.AddHours(hours), Status = SnapshotStatus.Ok };
        var snapshot = Point(999.8m, 0) with { EstimatedDays = 42.6m, Message = "套餐到期：27-05-15" };
        var card = new CardViewModel(provider, snapshot, [Point(999.9m, -1), snapshot, Point(1000m, -2)]);
        check("forecast rounds to whole days with estimation context", card.Forecast == "约 43 天 · 按近期消耗估算");
        check("healthy subscription dates belong in details", card.Message is null && card.DetailMessage == "套餐到期：2027年5月15日" && card.DetailsVisibility == Visibility.Visible);
        check("trend sorts samples and reports real change", card.TrendSummary.Contains("−0.20 GB") && card.TrendPoints[0].Label.Contains("1,000.00 GB"));
        check("small trend changes do not occupy full chart height", card.Trend.Max(p => p.Y) - card.Trend.Min(p => p.Y) < 0.1);
        check("every chart point has an exact time and value", card.TrendPoints.Count == 3 && card.TrendPoints.All(p => p.Label.Contains("年") && p.Label.Contains("GB")));
        check("insufficient and duplicate trend samples are hidden", new CardViewModel(provider, snapshot, [snapshot, snapshot, snapshot]).TrendVisibility == Visibility.Collapsed);
        var stale = new CardViewModel(provider, snapshot with { Status = SnapshotStatus.Stale, Message = "连接失败，保留上次数据" }, []);
        check("stale errors remain visible with retry and original timestamp", stale.Message!.Contains("连接失败") && stale.RetryVisibility == Visibility.Visible && stale.UpdatedExact == card.UpdatedExact);
        var codex = new CardViewModel(ProviderConfig.Create(ProviderType.Codex), new() { ProviderId = "codex-fixture", Kind = QuotaKind.RateWindow, Windows = [new("5 小时", 20, 300, now.AddHours(1)), new("每周", 40, 10080, now.AddDays(2))] }, []);
        check("Codex windows and resets stay outside details", codex.Windows.Count == 2 && codex.Windows.All(w => w.Reset.Contains("重置")) && codex.DetailsVisibility == Visibility.Collapsed);
    }
}
