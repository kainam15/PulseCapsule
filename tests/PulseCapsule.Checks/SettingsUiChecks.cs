using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PulseCapsule;
using PulseCapsule.Core;
using PulseCapsule.Services;
using PulseCapsule.Shell;
using PulseCapsule.UI;
using PulseCapsule.Capsules.Quota;

// Executes production WPF templates and handlers without showing a window or touching real accounts.
// Pixel scale is a render check; it is NOT a claim of desktop DPI/input verification.
static class SettingsUiChecks
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { RunSta(); } catch (Exception e) { failure = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw new Exception("WPF UI regression failed", failure);
    }
    private static void RunSta()
    {
        var checks = 0;
        void Check(string name, bool ok) { if (!ok) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        var directory = Path.GetFullPath(Path.Combine(".artifacts", "settings-ui-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
        var app = new App(); app.InitializeComponent();
        var store = new SettingsStore(directory);
        var providers = Enumerable.Range(0, 14).Select(i => ProviderConfig.Create(ProviderType.Manual) with { Id = "ui-fixture-" + i, Name = "长名称账户 " + i + " ABCDEFGHIJKLMNOPQRSTUVWXYZ", ManualRemaining = 123 }).ToList();
        var config = new AppSettings { Providers = providers, Capsules = [new() { Id = "clock" }, new() { Id = "quota", Enabled = false }, new() { Id = "system", Enabled = false }] };
        store.Save(config);
        var history = new HistoryStore(directory);
        using var monitor = new QuotaMonitor(config, new CredentialStore("ui-checks-" + Guid.NewGuid().ToString("N")), history, true);
        using var host = new CapsuleHost(config, () => store.Save(app.Settings));
        void Set(string name, object value) => typeof(App).GetProperty(name)!.SetValue(app, value);
        Set("Store", store); Set("Monitor", monitor); Set("Credentials", new CredentialStore("ui-checks-unused")); Set("Host", host);
        var window = new SettingsWindow(app);
        var root = (FrameworkElement)window.Content;
        ((Grid)root).Background = (Brush)app.Resources["Canvas"];
        T Find<T>(string name) where T : FrameworkElement => (T)window.FindName(name);
        void Click(string name)
        {
            if (Find<ButtonBase>(name) is RadioButton tab) tab.IsChecked = true;
            else Find<ButtonBase>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        void Layout() { root.Measure(new Size(694, 502)); root.Arrange(new Rect(0, 0, 694, 502)); root.UpdateLayout(); }
        bool Inside(FrameworkElement element)
        {
            var point = element.TranslatePoint(new Point(), root);
            return element.ActualHeight > 0 && point.Y >= 0 && point.Y + element.ActualHeight <= 502 && point.X + element.ActualWidth <= 694;
        }
        void Render(string name, double scale = 1)
        {
            Layout(); var bitmap = new RenderTargetBitmap((int)(694 * scale), (int)(502 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name + ".png")); png.Save(file);
        }
        Layout();
        Check("minimum client area keeps all navigation and fixed actions reachable", new[] { "SourcesNav", "CapsuleSettingsButton", "DesktopNav", "AddButton", "SaveButton", "DiscardButton" }.All(n => Inside(Find<FrameworkElement>(n))));
        Check("account list scrolls independently of the form", Find<ListBox>("ProviderList").ActualHeight < 14 * 40 && Find<ScrollViewer>("EditorScroll").ScrollableHeight > 0);
        Render("sources-minimum"); Render("sources-minimum-200-render", 2);
        Find<TextBox>("IntervalInput").Text = "invalid";
        Click("SaveButton"); Layout();
        Check("invalid interval is attached to its field and does not persist", FieldFeedback.GetError(Find<TextBox>("IntervalInput")).Contains("1–1440") && store.Load().Providers[0].RefreshMinutes == 60);
        Render("field-error");
        Click("DiscardButton");
        Check("discard restores saved values and clears dirty state", Find<TextBox>("IntervalInput").Text == "60" && Find<TextBlock>("UnsavedMarker").Visibility == Visibility.Collapsed);
        Click("DesktopNav"); Layout();
        var beforeSnapshot = monitor.Snapshots[providers[0].Id];
        Find<CheckBox>("NotifyCheck").IsChecked = false; Click("NotifyCheck");
        Check("desktop preference immediately persists without resetting snapshots", !store.Load().Notifications && ReferenceEquals(beforeSnapshot, monitor.Snapshots[providers[0].Id]));
        Render("desktop-minimum");
        Directory.CreateDirectory(store.FilePath + ".tmp");
        Find<CheckBox>("NotifyCheck").IsChecked = true; Click("NotifyCheck");
        Check("failed preference save restores checkbox and stored state", Find<CheckBox>("NotifyCheck").IsChecked == false && !store.Load().Notifications && Find<TextBlock>("StatusText").Text.Contains("未保存"));
        Directory.Delete(store.FilePath + ".tmp");
        Click("SourcesNav");
        Find<TextBox>("NameInput").Text = "新账户名称"; Click("SaveButton");
        Check("saving an account preserves the latest global preference", store.Load().Providers[0].Name == "新账户名称" && !store.Load().Notifications);
        Click("CapsuleSettingsButton"); Layout(); Render("capsules-minimum");
        Find<ComboBox>("RotateSeconds").SelectedItem = 30;
        Check("rotation interval is saved on selection", store.Load().CarouselSeconds == 30);
        var list = Find<ListBox>("CapsuleList"); list.SelectedIndex = 2;
        typeof(SettingsWindow).GetMethod("MoveCapsule", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [-1]);
        Check("capsule reordering immediately persists", store.Load().Capsules[1].Id == "system");
        Layout();
        var only = Descendants<CheckBox>(list).First(c => (string)c.Content == "时间"); only.IsChecked = false;
        only.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check("last enabled capsule cannot be turned off", store.Load().Capsules.Any(p => p.Enabled) && Find<TextBlock>("StatusText").Text.Contains("至少"));
        var view = new QuotaExpandedView(_ => { }, _ => Task.CompletedTask, _ => { }, id => app.Settings.CardDetailsExpanded.GetValueOrDefault(id), app.SetCardDetails)
            { Foreground = (Brush)app.Resources["Text"] };
        var now = DateTimeOffset.UtcNow;
        var wget = ProviderConfig.Create(ProviderType.WgetCloud) with { Id = "wget-fixture" };
        QuotaSnapshot Point(decimal remaining, int hours) => new() { ProviderId = wget.Id, Status = SnapshotStatus.Ok, Currency = "GB", FetchedAt = now.AddHours(hours), Remaining = remaining, Used = 12, Total = 100, EstimatedDays = 42.6m, Message = "套餐到期：27-05-15" };
        List<CardViewModel> Cards() => [new(wget, Point(88, 0), [Point(100, -48), Point(94, -24), Point(88, 0)]), new(ProviderConfig.Create(ProviderType.Codex), new() { ProviderId = "codex-fixture", Kind = QuotaKind.RateWindow, Windows = [new("5 小时", 25, 300, now.AddHours(1)), new("每周", 50, 10080, now.AddDays(2))] }, [])];
        var cardRoot = new Grid { Width = 318, Background = (Brush)app.Resources["Canvas"] }; cardRoot.Children.Add(view);
        void CardLayout() { cardRoot.Measure(new Size(318, double.PositiveInfinity)); cardRoot.Arrange(new Rect(0, 0, 318, cardRoot.DesiredSize.Height)); cardRoot.UpdateLayout(); }
        view.Update(Cards()); CardLayout();
        var details = Descendants<Expander>(view).First(x => x.Visibility == Visibility.Visible); var shortHeight = cardRoot.ActualHeight;
        Check("details start collapsed while Codex windows are visible", !details.IsExpanded && Descendants<TextBlock>(view).Any(t => t.Text == "每周"));
        details.IsExpanded = true; CardLayout();
        Check("expanding details persists per source and increases content height", store.Load().CardDetailsExpanded.GetValueOrDefault(wget.Id) && cardRoot.ActualHeight > shortHeight);
        view.Update(Cards()); CardLayout();
        Check("detail state survives snapshot replacement", Descendants<Expander>(view).First(x => x.Visibility == Visibility.Visible).IsExpanded);
        Directory.CreateDirectory(store.FilePath + ".tmp");
        Descendants<Expander>(view).First(x => x.Visibility == Visibility.Visible).IsExpanded = false;
        Check("failed detail-state save restores expanded content", Descendants<Expander>(view).First(x => x.Visibility == Visibility.Visible).IsExpanded && store.Load().CardDetailsExpanded[wget.Id]);
        Directory.Delete(store.FilePath + ".tmp");
        var sourceConfig = new AppSettings { Providers = providers.Take(2).ToList() };
        using (var sourceMonitor = new QuotaMonitor(sourceConfig, new CredentialStore("ui-refresh-checks"), history, false))
        {
            sourceMonitor.RefreshProviderAsync(providers[0].Id).GetAwaiter().GetResult();
            Check("single-source refresh leaves other sources untouched", sourceMonitor.Snapshots.ContainsKey(providers[0].Id) && !sourceMonitor.Snapshots.ContainsKey(providers[1].Id));
            var firstRefresh = sourceMonitor.Snapshots[providers[0].Id];
            sourceMonitor.ApplyPreferences(sourceConfig with { Notifications = false });
            sourceMonitor.RefreshProviderAsync(providers[0].Id).GetAwaiter().GetResult();
            Check("preference save preserves forced-refresh debounce", ReferenceEquals(firstRefresh, sourceMonitor.Snapshots[providers[0].Id]));
        }
        var bitmap = new RenderTargetBitmap(318, (int)Math.Ceiling(cardRoot.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(cardRoot);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(Path.Combine(directory, "cards-expanded.png"))) encoder.Save(file);
        window.Close();
        Console.WriteLine($"{checks} WPF checks passed. Artifacts: {directory}");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
