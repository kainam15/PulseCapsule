using System.IO;
using System.Reflection;
using System.Text.Json;
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
        var honeApi = ProviderConfig.Create(ProviderType.Hone) with { Id = "ui-hone-api", Enabled = false };
        var honeWallet = ProviderConfig.Create(ProviderType.HoneWallet) with { Id = "ui-hone-wallet", BaseUrl = "https://HONE.vvvv.ee:443/v1/" };
        providers.InsertRange(2, [honeApi, honeWallet]);
        var config = CapsuleSelection.Normalize(new AppSettings { Providers = providers, Capsules = [new() { Id = "clock" }, new() { Id = "system", Enabled = false }] });
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
        double clientHeight = 502;
        void Layout() { root.Measure(new Size(694, clientHeight)); root.Arrange(new Rect(0, 0, 694, clientHeight)); root.UpdateLayout(); }
        bool Inside(FrameworkElement element)
        {
            var point = element.TranslatePoint(new Point(), root);
            return element.ActualHeight > 0 && point.Y >= 0 && point.Y + element.ActualHeight <= 502 && point.X + element.ActualWidth <= 694;
        }
        void Render(string name, double scale = 1)
        {
            Layout(); var bitmap = new RenderTargetBitmap((int)(694 * scale), (int)(clientHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(Path.Combine(directory, name + ".png")); png.Save(file);
        }
        Layout();
        Check("source editor no longer contains an enable switch", window.FindName("EnabledCheck") is null);
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
        Click("CapsuleSettingsButton"); Layout(); Render("modules-minimum");
        var list = Find<ListBox>("ModuleList");
        Check("management uses named modules without family rows or removed controls", list.Items.Cast<CapsuleModule>().Any(m => m.Name == "Hone")
            && list.Items.Cast<CapsuleModule>().Any(m => m.Name == "时钟") && list.Items.Cast<CapsuleModule>().Any(m => m.Name == "硬件监控")
            && !list.Items.Cast<CapsuleModule>().Any(m => m.Id == "quota")
            && new[] { "CapsuleProviders", "TemperatureCheck", "FanRpmCheck", "FanControlCheck" }.All(n => window.FindName(n) is null)
            && !Descendants<Button>(Find<FrameworkElement>("CapsulesPage")).Any());
        Check("rotation stays below the module list and inside minimum window", Inside(Find<FrameworkElement>("RotationSettings"))
            && Find<FrameworkElement>("RotationSettings").TranslatePoint(new Point(), root).Y >= list.TranslatePoint(new Point(0, list.ActualHeight), root).Y);
        var originalSystem = JsonSerializer.Serialize(store.Load().System);
        Find<ComboBox>("RotateSeconds").SelectedItem = 30;
        Check("rotation saves without rewriting hidden hardware preferences", store.Load().CarouselSeconds == 30 && JsonSerializer.Serialize(store.Load().System) == originalSystem);
        // Give all fixture rows layout space for deterministic routed drag checks.
        clientHeight = 1400; Layout();
        CheckBox ModuleSwitch(string id) => Descendants<CheckBox>(list).Single(c => c.Tag is CapsuleModule m && m.Id == id);
        CheckBox SourceSwitch(string id) => Descendants<CheckBox>(list).Single(c => c.Tag is CapsuleModule m && m.Providers.Any(p => p.Id == id));
        string[] Order() => store.Load().Capsules.Select(p => p.Id).ToArray();
        Thumb StartDrag(int index)
        {
            Layout();
            var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(index);
            var handle = Descendants<Thumb>(row).Single(t => Equals(t.Tag, "ReorderHandle"));
            handle.RaiseEvent(new DragStartedEventArgs(handle.ActualWidth / 2, handle.ActualHeight / 2));
            return handle;
        }
        void DragTo(Thumb handle, double y, double? x = null)
        {
            var origin = handle.TranslatePoint(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2), list);
            handle.RaiseEvent(new DragDeltaEventArgs((x ?? origin.X) - origin.X, y - origin.Y));
        }
        void FinishDrag(Thumb handle, bool canceled = false) => handle.RaiseEvent(new DragCompletedEventArgs(0, 0, canceled));
        var originalOrder = Order();
        var honeId = "quota:" + honeWallet.Id;
        var grip = StartDrag(Array.IndexOf(originalOrder, honeId)); DragTo(grip, 3);
        Check("module drag preview does not save an intermediate order", Order().SequenceEqual(originalOrder) && Find<Border>("ModuleDropIndicator").Visibility == Visibility.Visible);
        FinishDrag(grip);
        var honeFirst = new[] { honeId }.Concat(originalOrder.Where(id => id != honeId)).ToArray();
        Check("supplier can move ahead of built-in modules without changing current selection", Order().SequenceEqual(honeFirst) && list.SelectedIndex == 0 && host.Current?.Id == "clock");
        var snapshotBeforeMove = monitor.Snapshots[providers[0].Id];
        grip = StartDrag(0); DragTo(grip, list.ActualHeight - 3); FinishDrag(grip);
        var honeLast = originalOrder.Where(id => id != honeId).Append(honeId).ToArray();
        Check("downward drop corrects insertion index and persists after reload", Order().SequenceEqual(honeLast) && list.SelectedIndex == list.Items.Count - 1);
        Check("runtime module order matches settings and Hone appears once", host.Capsules.Select(c => c.Id).SequenceEqual(CapsuleSelection.Modules(store.Load()).Where(m => m.Enabled).Select(m => m.Id))
            && host.Capsules.Count(c => c.Title == "Hone") == 1 && ReferenceEquals(snapshotBeforeMove, monitor.Snapshots[providers[0].Id]));
        var cycleStart = host.Current!.Id;
        var hostOrder = host.Capsules.Select(c => c.Id).ToArray();
        host.Cycle(-1);
        Check("wheel and carousel advance through the configured module order", host.Current!.Id == hostOrder[(Array.IndexOf(hostOrder, cycleStart) + 1) % hostOrder.Length]);
        host.Cycle(1);
        grip = StartDrag(list.Items.Count - 1); DragTo(grip, 3); FinishDrag(grip, true);
        Check("canceled drag leaves module order intact", Order().SequenceEqual(honeLast));
        grip = StartDrag(list.Items.Count - 1); DragTo(grip, 3); DragTo(grip, -20); FinishDrag(grip);
        Check("release outside module list cancels its drop target", Order().SequenceEqual(honeLast) && Find<Border>("ModuleDropIndicator").Visibility == Visibility.Collapsed);
        Directory.CreateDirectory(store.FilePath + ".tmp");
        grip = StartDrag(0); FinishDrag(grip);
        Check("handle click does not attempt to persist", Order().SequenceEqual(honeLast) && !Find<TextBlock>("StatusText").Text.Contains("已恢复原值"));
        grip = StartDrag(list.Items.Count - 1); DragTo(grip, 3); FinishDrag(grip);
        Check("failed module reorder restores displayed and stored order", Order().SequenceEqual(honeLast) && list.SelectedIndex == list.Items.Count - 1 && Find<TextBlock>("StatusText").Text.Contains("已恢复原值"));
        Directory.Delete(store.FilePath + ".tmp");
        grip = StartDrag(list.Items.Count - 1); DragTo(grip, 3); FinishDrag(grip); Layout();
        Check("one enable switch is shown for every named module", Descendants<CheckBox>(list).Count() == providers.Count - 1 + 2);
        SourceSwitch(providers[0].Id).IsChecked = false; Layout();
        Check("module switch persists without editing connection values", !store.Load().Providers[0].Enabled && store.Load().Providers[0].ManualRemaining == 123 && store.Load().Providers[1].Enabled);
        Directory.CreateDirectory(store.FilePath + ".tmp");
        SourceSwitch(providers[0].Id).IsChecked = true; Layout();
        Check("failed module enable restores its switch and stored state", !store.Load().Providers[0].Enabled && SourceSwitch(providers[0].Id).IsChecked == false);
        Directory.Delete(store.FilePath + ".tmp");
        Click("SourcesNav"); Find<TextBox>("NameInput").Text = "停用后修改名称"; Click("SaveButton");
        Check("account saves preserve module enable and ordering", !store.Load().Providers[0].Enabled && store.Load().Capsules[0].Id == honeId);
        Click("CapsuleSettingsButton"); Layout(); SourceSwitch(providers[0].Id).IsChecked = true; Layout();
        var honeItem = list.Items.Cast<CapsuleModule>().Single(m => m.Name == "Hone");
        var honeContainer = (FrameworkElement)list.ItemContainerGenerator.ContainerFromItem(honeItem);
        Check("Hone remains one row and one switch for wallet and API", Descendants<CheckBox>(honeContainer).Count() == 1
            && ReferenceEquals(SourceSwitch(honeWallet.Id), SourceSwitch(honeApi.Id))
            && !Descendants<TextBlock>(honeContainer).Any(t => t.Text is "账户钱包" or "API key"));
        Check("loading mixed Hone states does not enable the API source", SourceSwitch(honeWallet.Id).IsChecked == true && !store.Load().Providers.Single(p => p.Id == honeApi.Id).Enabled);
        Directory.CreateDirectory(store.FilePath + ".tmp"); SourceSwitch(honeWallet.Id).IsChecked = false; Layout();
        Check("failed supplier save restores the original mixed flags", SourceSwitch(honeWallet.Id).IsChecked == true
            && !store.Load().Providers.Single(p => p.Id == honeApi.Id).Enabled && store.Load().Providers.Single(p => p.Id == honeWallet.Id).Enabled);
        Directory.Delete(store.FilePath + ".tmp"); SourceSwitch(honeWallet.Id).IsChecked = false; Layout();
        Check("disabling Hone disables all members and removes one runtime module", store.Load().Providers.Where(p => p.Id == honeApi.Id || p.Id == honeWallet.Id).All(p => !p.Enabled)
            && host.Capsules.All(c => c.Id != honeId) && list.Items.Cast<CapsuleModule>().Any(m => m.Id == honeId));
        var peer = new System.Windows.Automation.Peers.CheckBoxAutomationPeer(SourceSwitch(honeWallet.Id));
        ((System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)).Toggle(); Layout();
        Check("UIA toggle reenables all Hone sources as one runtime module", store.Load().Providers.Where(p => p.Id == honeApi.Id || p.Id == honeWallet.Id).All(p => p.Enabled) && host.Capsules.Count(c => c.Id == honeId) == 1);
        bool SourceUnchanged(ProviderConfig expected) => JsonSerializer.Serialize(store.Load().Providers.Single(p => p.Id == expected.Id)) == JsonSerializer.Serialize(expected);
        Check("supplier toggles only change enabled flags", SourceUnchanged(honeApi with { Enabled = true }) && SourceUnchanged(honeWallet));
        var beforeLastCheck = app.Settings;
        app.SaveSettings(app.Settings with { Providers = app.Settings.Providers.Select(p => p with { Enabled = false }).ToList(), Capsules = app.Settings.Capsules.Select(p => p with { Enabled = p.Id == "clock" }).ToList() });
        void ReloadModules() => typeof(SettingsWindow).GetMethod("LoadPreferences", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [null]);
        ReloadModules(); Layout(); ModuleSwitch("clock").IsChecked = false; Layout();
        Check("last enabled module cannot be turned off", ModuleSwitch("clock").IsChecked == true && Find<TextBlock>("StatusText").Text.Contains("至少") && host.Current?.Id == "clock");
        app.SaveSettings(beforeLastCheck); ReloadModules();
        clientHeight = 502; Layout();
        var moduleScroll = Descendants<ScrollViewer>(list).First();
        moduleScroll.ScrollToTop(); Layout();
        var reorder = typeof(SettingsWindow).GetField("moduleReorder", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;
        void ScrollAtEdge() => reorder.GetType().GetMethod("ScrollAtEdge", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(reorder, null);
        var orderBeforeScroll = Order();
        grip = StartDrag(0); DragTo(grip, list.ActualHeight - 3); ScrollAtEdge();
        Check("long module list scrolls at drag edge without saving", moduleScroll.VerticalOffset > 0 && Order().SequenceEqual(orderBeforeScroll));
        grip.RaiseEvent(new DragDeltaEventArgs(0, 0));
        var offsetAfterScroll = moduleScroll.VerticalOffset; ScrollAtEdge();
        Check("returning to drag origin stops edge scrolling", moduleScroll.VerticalOffset == offsetAfterScroll && Find<Border>("ModuleDropIndicator").Visibility == Visibility.Collapsed);
        FinishDrag(grip, true); moduleScroll.ScrollToTop();
        Render("modules-final-minimum"); Render("modules-final-200-render", 2);
        var renderSettings = app.Settings;
        app.SaveSettings(CapsuleSelection.Normalize(new AppSettings { Providers = [honeWallet, honeApi,
            ProviderConfig.Create(ProviderType.Codex), ProviderConfig.Create(ProviderType.WgetCloud) with { Name = "WgetCloud" }] }));
        ReloadModules(); clientHeight = 682; Render("modules-standard"); Render("modules-standard-200-render", 2);
        app.SaveSettings(renderSettings); ReloadModules(); clientHeight = 502;
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
        HoneSettingsChecks.Run(app, Check, directory);
        SettingsConnectionChecks.Run(app, history, Check);
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
