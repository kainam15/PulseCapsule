using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using QuotaPeek.Services;
using QuotaPeek.Shell;
using QuotaPeek.Capsules.Clock;
using QuotaPeek.Capsules.Quota;
using QuotaPeek.UI;

namespace QuotaPeek;

public partial class App : Application
{
    private Mutex? mutex;
    public SettingsStore Store { get; private set; } = null!;
    public CredentialStore Credentials { get; private set; } = null!;
    public QuotaMonitor Monitor { get; private set; } = null!;
    public CapsuleHost Host { get; private set; } = null!;
    public AppSettings Settings => Monitor.Settings;
    public bool Demo => Monitor.Demo;
    public string? RenderPath { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            string? Argument(string name) { var i = Array.IndexOf(e.Args, name); return i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : null; }
            var demo = e.Args.Contains("--demo");
            var directory = Argument("--data-dir") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), demo ? "QuotaPeek-Demo" : "QuotaPeek");
            directory = Path.GetFullPath(directory);
            var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory.ToUpperInvariant())))[..12];
            mutex = new Mutex(true, "Local\\QuotaPeek-" + scope, out var first);
            if (!first)
            {
                MessageBox.Show("QuotaPeek 已在运行。请从右下角托盘菜单打开。", "QuotaPeek", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(); return;
            }
            Store = new(directory);
            Credentials = new(scope);
            var settings = Store.Load();
            var history = new HistoryStore(directory);
            Monitor = new(settings, Credentials, history, demo);
            Host = new(settings, () => { try { Store.Save(Settings); } catch (IOException) { } });
            Monitor.LowQuota += (config, snapshot) => Host.Notify(config.Name + " 额度提醒", snapshot.Kind == QuotaKind.RateWindow
                ? "剩余额度低于设置的阈值。" : "当前剩余 " + CardViewModel.Money(snapshot.Remaining, snapshot.Currency));
            if (settings.LastCapsuleId is null && settings.Providers.FirstOrDefault(p => p.Enabled) is { } firstProvider)
                settings.LastCapsuleId = "quota:" + firstProvider.Id;
            _ = ConfigureCapsules();
            RenderPath = Argument("--render");
            var window = new MainWindow(this);
            MainWindow = window;
            window.Show();
            if (e.Args.Contains("--settings")) OpenSettings();
        }
        catch (Exception error)
        {
            MessageBox.Show("QuotaPeek 无法启动。请确认数据目录可写。\n" + error.GetType().Name, "QuotaPeek", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    public void OpenSettings()
    {
        var existing = Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null) { existing.Activate(); return; }
        new SettingsWindow(this) { Owner = MainWindow }.Show();
    }
    public void SaveSettings(AppSettings settings)
    {
        Store.Save(settings);
        Monitor.Apply(settings);
        _ = ConfigureCapsules();
    }
    private Task ConfigureCapsules()
    {
        Settings.Capsules = CapsuleSelection.Normalize(Settings.Capsules);
        List<ICapsule> items = [];
        foreach (var pref in Settings.Capsules.Where(p => p.Enabled))
        {
            switch (pref.Id)
            {
                case "clock": items.Add(new ClockCapsule()); break;
                case "quota": items.AddRange(Settings.Providers.Where(p => p.Enabled).Select(p => new QuotaCapsule(p, Monitor, OpenSettings))); break;
            }
        }
        if (items.Count == 0) items.Add(new ClockCapsule());
        return Host.Configure(Settings, items);
    }
    public static void OpenWebsite(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https")
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Host?.Dispose();
        Monitor?.Dispose();
        mutex?.Dispose();
        base.OnExit(e);
    }
}
