using System.Collections.Concurrent;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PulseCapsule;
using PulseCapsule.Core;
using PulseCapsule.Services;

// Exercise the production form/async handler on its STA thread. Only the network
// boundary is controlled; delayed responses deliberately ignore cancellation.
static class SettingsConnectionChecks
{
    private sealed class PendingResponse
    {
        private readonly TaskCompletionSource<QuotaSnapshot> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProviderConfig? Config { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task Completion { get; set; } = Task.CompletedTask;
        public Task<QuotaSnapshot> Fetch(ProviderConfig config, string? key, CancellationToken token)
        {
            Config = config;
            Token = token;
            return response.Task;
        }
        public void Succeed(decimal remaining = 17) => response.SetResult(new()
            { ProviderId = Config!.Id, Remaining = remaining, Currency = "USD" });
        public void Fail(Exception error) => response.SetException(error);
        public Task Wait() => Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class StaContext : SynchronizationContext
    {
        public readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> Callbacks = new();
        public override void Post(SendOrPostCallback callback, object? state) => Callbacks.Add((callback, state));
    }

    public static void Run(App app, HistoryStore history, Action<string, bool> check)
    {
        // Pump only async test callbacks, not Dispatcher.Run: a WPF dispatcher
        // pump would also invoke App.OnStartup and open the real application.
        var previous = SynchronizationContext.Current;
        var context = new StaContext();
        using var callbacks = context.Callbacks;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var task = RunAsync(app, history, check);
            while (!task.IsCompleted)
            {
                if (!callbacks.TryTake(out var work, TimeSpan.FromSeconds(10))) throw new TimeoutException("Connection UI check stalled.");
                work.Callback(work.State);
            }
            task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static async Task RunAsync(App app, HistoryStore history, Action<string, bool> check)
    {
        var originalMonitor = app.Monitor;
        using var monitor = new QuotaMonitor(app.Settings, app.Credentials, history, false);
        typeof(App).GetProperty("Monitor")!.SetValue(app, monitor);
        var window = new SettingsWindow(app);
        T Find<T>(string name) where T : class => (T)window.FindName(name);
        void Click(string name)
        {
            if (Find<ButtonBase>(name) is RadioButton tab) tab.IsChecked = true;
            else Find<ButtonBase>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        PendingResponse Begin()
        {
            var pending = new PendingResponse();
            Func<ProviderConfig, string?, CancellationToken, Task<QuotaSnapshot>> fetch = pending.Fetch;
            pending.Completion = (Task)typeof(SettingsWindow).GetMethod("TestConnectionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [fetch])!;
            return pending;
        }
        (string Status, string Feedback, bool Enabled) State() =>
            (Find<TextBlock>("StatusText").Text, Find<TextBlock>("ConnectionFeedback").Text, Find<Button>("TestButton").IsEnabled);
        var list = Find<ListBox>("ProviderList");
        var closed = false;
        try
        {
            foreach (var outcome in new[] { "success", "failure", "cancellation" })
            {
                list.SelectedIndex = 0;
                var pending = Begin();
                list.SelectedIndex = 1;
                var expected = State();
                if (outcome == "success") pending.Succeed();
                else pending.Fail(outcome == "failure" ? new ProviderException("旧账户认证失败") : new OperationCanceledException());
                await pending.Wait();
                check($"late connection {outcome} cannot overwrite another source", State() == expected);
                check($"source switch cancels obsolete {outcome} request and releases button", pending.Token.IsCancellationRequested && State().Enabled);
            }

            // Return to the same provider ID: checking only that ID is insufficient.
            list.SelectedIndex = 0;
            var obsolete = Begin();
            list.SelectedIndex = 1;
            list.SelectedIndex = 0;
            var latest = Begin();
            var waiting = State();
            obsolete.Succeed();
            await obsolete.Wait();
            check("A to B to A keeps the newer test busy when the old one succeeds", State() == waiting && !State().Enabled);
            latest.Succeed(42);
            await latest.Wait();
            check("current connection success is visible and does not claim saved", State().Enabled && State().Status.Contains("42.00") && State().Feedback.Contains("仍需"));

            obsolete = Begin();
            list.SelectedIndex = 1;
            latest = Begin();
            waiting = State();
            obsolete.Fail(new ProviderException("旧请求失败"));
            await obsolete.Wait();
            check("obsolete failure and finally cannot finish a newer request", State() == waiting && !State().Enabled);
            latest.Fail(new ProviderException("当前账户认证失败"));
            await latest.Wait();
            check("current connection failure is visible and releases button", State() == ("当前账户认证失败", "当前账户认证失败", true));

            var cancelled = Begin();
            cancelled.Fail(new OperationCanceledException());
            await cancelled.Wait();
            check("current cancellation clears the loading state", State() == ("连接测试已取消。", "连接测试已取消。", true));

            Find<TextBox>("NameInput").Text += " first edit";
            var dirty = Begin();
            Find<TextBox>("NameInput").Text += " second edit";
            var changed = State();
            dirty.Succeed();
            await dirty.Wait();
            check("editing an already dirty form invalidates its pending result", dirty.Token.IsCancellationRequested && State() == changed && State().Feedback.Contains("已修改"));
            Click("DiscardButton");

            foreach (var field in new[] { "key", "name" })
            {
                var pending = Begin();
                if (field == "key") Find<PasswordBox>("KeyInput").Password = "test-only-not-a-real-key";
                else Find<TextBox>("NameInput").Text += " changed";
                changed = State();
                pending.Succeed();
                await pending.Wait();
                check($"changing {field} cancels the test and rejects its result", pending.Token.IsCancellationRequested && State() == changed && State().Enabled);
                Click("DiscardButton");
            }

            var managing = Begin();
            Click("CapsuleSettingsButton");
            var managementState = State();
            managing.Succeed();
            await managing.Wait();
            check("opening capsule management cancels the source test and rejects its result", managing.Token.IsCancellationRequested && State() == managementState);
            Click("SourcesNav");

            Find<TextBox>("NameInput").Text += " discard";
            var discarded = Begin();
            Click("DiscardButton");
            var discardedState = State();
            discarded.Succeed();
            await discarded.Wait();
            check("discarded form cannot receive the old test result", discarded.Token.IsCancellationRequested && State() == discardedState);

            Find<TextBox>("NameInput").Text += " saved";
            var saved = Begin();
            Click("SaveButton");
            var savedState = State();
            saved.Succeed();
            await saved.Wait();
            check("saving preserves its feedback against a pending connection test", saved.Token.IsCancellationRequested && State() == savedState && State().Feedback.Contains("配置已保存"));

            var leaving = Begin();
            Click("DesktopNav");
            var desktopState = State();
            leaving.Succeed();
            await leaving.Wait();
            check("leaving account settings rejects the old result", leaving.Token.IsCancellationRequested && State() == desktopState);
            Click("SourcesNav");

            Find<TextBox>("IntervalInput").Text = "invalid";
            var invalid = Begin();
            await invalid.Wait();
            check("invalid form never starts a provider request and releases button", invalid.Config is null && State().Enabled && State().Feedback.Contains("刷新间隔"));
            Click("DiscardButton");

            typeof(App).GetProperty("Monitor")!.SetValue(app, originalMonitor);
            var demo = Begin();
            await demo.Wait();
            check("demo connection test does not fetch or leave a loading message", demo.Config is null && State().Enabled && State().Status == State().Feedback && State().Feedback.Contains("演示模式"));
            typeof(App).GetProperty("Monitor")!.SetValue(app, monitor);

            var hone = app.Settings.Providers.First(p => p.Type == ProviderType.Hone);
            window.SelectProvider(hone.Id);
            var honeEditors = Find<StackPanel>("HoneConnections").Children.Cast<PulseCapsule.UI.HoneConnectionEditor>().ToArray();
            ((PasswordBox)honeEditors[0].FindName("SecretInput")).Password = "fixture-wallet";
            ((PasswordBox)honeEditors[1].FindName("SecretInput")).Password = "fixture-api";
            var calls = new List<(ProviderType Type, string? Key)>();
            Func<ProviderConfig, string?, CancellationToken, Task<QuotaSnapshot>> both = (config, key, token) => {
                calls.Add((config.Type, key));
                return config.Type == ProviderType.HoneWallet
                    ? Task.FromException<QuotaSnapshot>(new ProviderException("钱包令牌无效"))
                    : Task.FromResult(new QuotaSnapshot { ProviderId = config.Id, Used = 5, Currency = "USD" });
            };
            await (Task)typeof(SettingsWindow).GetMethod("TestConnectionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [both])!;
            check("Hone test uses each credential and reports wallet failure separately from API success", calls.SequenceEqual(new[] { (ProviderType.HoneWallet, (string?)"fixture-wallet"), (ProviderType.Hone, (string?)"fixture-api") })
                && State().Feedback.Contains("账户钱包：钱包令牌无效") && State().Feedback.Contains("API key：连接成功") && State().Enabled);
            Click("DiscardButton");
            var groupPending = Begin();
            Find<TextBox>("HoneBaseUrl").Text = "https://changed.example.test";
            var groupEdited = State();
            groupPending.Succeed(); await groupPending.Wait();
            check("editing Hone site cancels the whole test and prevents stale results", groupPending.Token.IsCancellationRequested && State() == groupEdited);
            Click("DiscardButton");
            groupPending = Begin();
            Click("AddHoneApi"); groupEdited = State();
            groupPending.Fail(new ProviderException("旧钱包结果")); await groupPending.Wait();
            check("adding a Hone connection invalidates a pending group test", groupPending.Token.IsCancellationRequested && State() == groupEdited);
            Click("DiscardButton");
            groupPending = Begin();
            window.SelectProvider(app.Settings.Providers.First(p => p.Type == ProviderType.Manual).Id);
            var groupLeft = State();
            groupPending.Succeed(); await groupPending.Wait();
            check("leaving Hone ignores its pending result without starting another member", State() == groupLeft && groupPending.Config!.Type == ProviderType.HoneWallet);

            var closing = Begin();
            window.Close(); closed = true;
            var closedState = State();
            closing.Fail(new ProviderException("关闭后的请求失败"));
            await closing.Wait();
            check("closed settings cancel requests and ignore late errors", closing.Token.IsCancellationRequested && State() == closedState);
        }
        finally
        {
            // A failed assertion must not open an interactive dirty-form dialog.
            if (!closed)
            {
                typeof(SettingsWindow).GetField("formDirty", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, false);
                window.Close();
            }
            typeof(App).GetProperty("Monitor")!.SetValue(app, originalMonitor);
        }
    }
}
