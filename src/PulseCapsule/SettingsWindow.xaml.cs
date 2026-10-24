using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PulseCapsule.Services;
using PulseCapsule.UI;

namespace PulseCapsule;

public partial class SettingsWindow : Window
{
    private readonly App app;
    private readonly CapsuleListReorder moduleReorder;
    private AppSettings working;
    private ProviderConfig? selected;
    private bool loadingProvider;
    private bool changingProvider;
    private bool formDirty;
    private bool loadingPreferences = true;
    private string page = "sources";
    private bool changingPage;
    private readonly HashSet<Control> invalidFields = [];
    private readonly HashSet<string> drafts = [];
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? connectionTest;
    private static readonly (ProviderType Type, string Name)[] Types = [(ProviderType.HoneWallet, "Hone"), (ProviderType.Codex, "Codex"),
        (ProviderType.DeepSeek, "DeepSeek"), (ProviderType.Kimi, "Kimi"), (ProviderType.SiliconFlow, "硅基流动"),
        (ProviderType.OpenRouter, "OpenRouter"), (ProviderType.WgetCloud, "WgetCloud 流量"),
        (ProviderType.Relay, "New API 中转站"), (ProviderType.Custom, "自定义 HTTP"), (ProviderType.Manual, "手动录入")];
    public SettingsWindow(App app)
    {
        InitializeComponent();
        this.app = app;
        working = Clone(app.Monitor.Settings);
        moduleReorder = new CapsuleListReorder(ModuleList, ModuleDropIndicator, (item, index) => ReorderModule(((CapsuleModule)item).Id, index));
        AddType.ItemsSource = Types.Select(t => t.Name); AddType.SelectedIndex = 0;
        LoadPreferences();
        ReloadList(working.Providers.FirstOrDefault()?.Id);
        Editor.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, e) => { if (e.OriginalSource is Control field) FieldFeedback.SetError(field, ""); MarkFormDirty(); }), true);
        Editor.AddHandler(PasswordBox.PasswordChangedEvent, new RoutedEventHandler((_, _) => MarkFormDirty()), true);
        Editor.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, e) => { if (e.OriginalSource is CheckBox) MarkFormDirty(); }), true);
        Editor.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, e) => { if (e.OriginalSource is CheckBox) MarkFormDirty(); }), true);
        Editor.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => MarkFormDirty()), true);
        Closing += (_, e) => { if (!ResolvePendingChanges()) e.Cancel = true; };
        Closed += (_, _) => { InvalidateConnectionTest(); lifetime.Cancel(); lifetime.Dispose(); };
    }
    private static AppSettings Clone(AppSettings settings) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, SettingsStore.Json), SettingsStore.Json)!;
    private void ReloadList(string? id)
    {
        changingProvider = true;
        try
        {
            var items = SourceItems();
            ProviderList.ItemsSource = items;
            var item = items.FirstOrDefault(p => p.Providers.Any(s => s.Id == id)) ?? items.FirstOrDefault();
            var provider = item?.Providers[0];
            ProviderList.SelectedItem = item;
            Editor.IsEnabled = provider is not null;
            Actions.IsEnabled = Editor.IsEnabled;
            LoadProvider(provider);
        }
        finally { changingProvider = false; }
    }
    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (changingProvider || ProviderList.SelectedItem is not SourceItem next || next.Providers.Any(p => p.Id == selected?.Id)) return;
        if (!ResolvePendingChanges())
        {
            changingProvider = true;
            try { ProviderList.SelectedItem = ProviderList.Items.Cast<SourceItem>().FirstOrDefault(p => p.Providers.Any(s => s.Id == selected?.Id)); }
            finally { changingProvider = false; }
            return;
        }
        ReloadList(next.Providers[0].Id);
    }
    private void LoadProvider(ProviderConfig? p)
    {
        InvalidateConnectionTest();
        selected = p;
        ConnectionFeedback.Text = "";
        ClearValidation();
        if (p is null) { TypeTitle.Text = "请选择数据源"; Editor.Visibility = Visibility.Collapsed; ClearFormDirty(); return; }
        Editor.Visibility = Visibility.Visible;
        loadingProvider = true;
        try
        {
        KeyInput.Clear();
        HoneConnections.Children.Clear();
        HoneFields.Visibility = IsHone ? Visibility.Visible : Visibility.Collapsed;
        StandardFields.Visibility = IsHone ? Visibility.Collapsed : Visibility.Visible;
        if (IsHone) { LoadHone(); EditorScroll.ScrollToTop(); return; }
        TypeTitle.Text = Types.First(t => t.Type == p.Type).Name;
        NameInput.Text = p.Name;
        BaseUrlInput.Text = p.Type == ProviderType.Custom ? p.Http.Url : p.BaseUrl;
        EnvironmentInput.Text = p.SecretEnvironment; CodexPathInput.Text = p.CodexExecutable;
        IntervalInput.Text = p.RefreshMinutes.ToString(); ThresholdInput.Text = p.LowThreshold.ToString(CultureInfo.InvariantCulture);
        ThresholdLabel.Text = p.Type switch { ProviderType.Codex => "剩余百分比提醒 / %", ProviderType.WgetCloud => "剩余流量提醒 / GB", _ => "剩余金额提醒" };
        CurrencyInput.SelectedIndex = p.Currency == "CNY" ? 1 : 0;
        CurrencyInput.IsEnabled = p.Type is not (ProviderType.Hone or ProviderType.HoneWallet or ProviderType.Kimi or ProviderType.OpenRouter);
        UsageMultiplierInput.Text = p.UsageMultiplier.ToString(CultureInfo.InvariantCulture);
        ManualRemainingInput.Text = p.ManualRemaining?.ToString(CultureInfo.InvariantCulture) ?? "";
        ManualUsedInput.Text = p.ManualUsed?.ToString(CultureInfo.InvariantCulture) ?? "";
        BudgetInput.Text = p.Budget?.ToString(CultureInfo.InvariantCulture) ?? "";
        KindInput.SelectedIndex = (int)p.Http.Kind;
        RemainingPathInput.Text = p.Http.RemainingPath; UsedPathInput.Text = p.Http.UsedPath; TotalPathInput.Text = p.Http.TotalPath;
        MultiplierInput.Text = p.Http.Multiplier.ToString(CultureInfo.InvariantCulture);
        HeadersInput.Text = JsonSerializer.Serialize(p.Http.Headers, SettingsStore.Json);
        WgetCloudEndpointInput.Text = p.Type == ProviderType.WgetCloud ? p.Http.Url : "";
        WgetCloudRemainingInput.Text = p.Type == ProviderType.WgetCloud ? p.Http.RemainingPath : "";
        WgetCloudUsedInput.Text = p.Type == ProviderType.WgetCloud ? p.Http.UsedPath : "";
        WgetCloudTotalInput.Text = p.Type == ProviderType.WgetCloud ? p.Http.TotalPath : "";
        WgetCloudPostCheck.IsChecked = p.WgetCloudUsePost;
        WgetCloudBytesCheck.IsChecked = p.WgetCloudValuesAreBytes;
        ApiFields.Visibility = p.NeedsSecret ? Visibility.Visible : Visibility.Collapsed;
        KeyLabel.Text = p.Type switch { ProviderType.HoneWallet => "账户访问令牌", ProviderType.WgetCloud => "用户中心会话 Cookie（可不填）", _ => "API key" };
        WgetCloudFields.Visibility = p.Type == ProviderType.WgetCloud ? Visibility.Visible : Visibility.Collapsed;
        var browserLinked = p.Type == ProviderType.WgetCloud && p.WgetCloudUseBrowserSession;
        KeyLabel.Visibility = KeyInput.Visibility = KeyHint.Visibility = browserLinked ? Visibility.Collapsed : Visibility.Visible;
        EnvironmentLabel.Visibility = EnvironmentInput.Visibility = browserLinked ? Visibility.Collapsed : Visibility.Visible;
        ApiFields.Visibility = p.NeedsSecret ? Visibility.Visible : Visibility.Collapsed;
        EndpointFields.Visibility = p.Type == ProviderType.WgetCloud ? Visibility.Collapsed : Visibility.Visible;
        DeleteKeyButton.Visibility = p.NeedsSecret && !browserLinked ? Visibility.Visible : Visibility.Collapsed;
        CodexFields.Visibility = p.Type == ProviderType.Codex ? Visibility.Visible : Visibility.Collapsed;
        CurrencyFields.Visibility = p.Type is ProviderType.Codex or ProviderType.WgetCloud ? Visibility.Collapsed : Visibility.Visible;
        RelayFields.Visibility = p.Type is ProviderType.Hone or ProviderType.Relay ? Visibility.Visible : Visibility.Collapsed;
        ManualFields.Visibility = p.Type == ProviderType.Manual ? Visibility.Visible : Visibility.Collapsed;
        CustomFields.Visibility = p.Type == ProviderType.Custom ? Visibility.Visible : Visibility.Collapsed;
        BudgetFields.Visibility = p.Type is ProviderType.Manual or ProviderType.Custom ? Visibility.Visible : Visibility.Collapsed;
        try { KeyHint.Text = app.Credentials.Read(p.Id) is null ? p.Type switch
            {
                ProviderType.HoneWallet => "尚未连接钱包；填写账户访问令牌或使用环境变量。",
                ProviderType.WgetCloud => "可以点击「登录并连接 WgetCloud」，复用内置浏览器会话，无需手动复制 Cookie。",
                _ => "尚未保存密钥；可填写 key 或使用环境变量。"
            } : p.Type == ProviderType.WgetCloud ? "已有本地凭据。如果尚未授权，请点击一键登录按钮；旧订阅 URL 不会被发送。" : "密钥已保存在 Windows 凭据管理器；留空保留。"; }
        catch { KeyHint.Text = "Windows 凭据管理器暂不可用。"; }
        EditorScroll.ScrollToTop();
        }
        finally { loadingProvider = false; ClearFormDirty(); }
    }
    private void MarkFormDirty()
    {
        if (loadingProvider || selected is null) return;
        InvalidateConnectionTest();
        formDirty = true;
        UnsavedMarker.Visibility = Visibility.Visible;
        DiscardButton.IsEnabled = true;
        ConnectionFeedback.Text = "表单内容已修改，先前的连接测试结果可能已失效。";
    }
    private void ClearFormDirty()
    {
        formDirty = false;
        UnsavedMarker.Visibility = Visibility.Collapsed;
        DiscardButton.IsEnabled = false;
    }
    private bool ResolvePendingChanges()
    {
        if (!formDirty) return true;
        var answer = MessageBox.Show(this, "当前数据源有未保存的修改。\n\n是：保存并继续\n否：放弃修改并继续\n取消：留在当前页面", "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (answer == MessageBoxResult.Cancel) return false;
        if (answer == MessageBoxResult.Yes) return SaveCurrent();
        if (selected is not null && drafts.Remove(selected.Id)) working.Providers.Remove(selected);
        ClearFormDirty();
        return true;
    }
    private ProviderConfig ReadForm()
    {
        ClearValidation();
        if (selected is null) throw new ProviderException("请先选择一个数据源。");
        var config = selected with { Http = selected.Http with { } };
        config.Name = NameInput.Text.Trim();
        if (config.Name.Length == 0) throw Invalid(NameInput, "请填写显示名称。");
        config.Enabled = app.Settings.Providers.FirstOrDefault(p => p.Id == config.Id)?.Enabled ?? config.Enabled;
        if (!int.TryParse(IntervalInput.Text, out var interval) || interval < 1 || interval > 1440) throw Invalid(IntervalInput, "刷新间隔须在 1–1440 分钟之间。");
        config.RefreshMinutes = interval;
        if (config.Type == ProviderType.WgetCloud && interval < 60)
            throw Invalid(IntervalInput, "WgetCloud 至少间隔 60 分钟查询一次，避免频繁触发订阅限制。");
        config.LowThreshold = Number(ThresholdInput, "阈值", false)!.Value;
        if (config.LowThreshold < 0 || config.Type == ProviderType.Codex && config.LowThreshold > 100) throw Invalid(ThresholdInput, "阈值无效；Codex 应为 0–100%。");
        config.Currency = CurrencyInput.SelectedIndex == 1 ? "CNY" : "USD";
        config.SecretEnvironment = EnvironmentInput.Text.Trim();
        config.CodexExecutable = CodexPathInput.Text.Trim();
        if (config.Type == ProviderType.Codex) config.Currency = "";
        if (config.Type == ProviderType.WgetCloud) config.Currency = "GB";
        if (config.Type == ProviderType.WgetCloud)
        {
            config.Http.Url = WgetCloudEndpointInput.Text.Trim();
            config.Http.RemainingPath = WgetCloudRemainingInput.Text.Trim();
            config.Http.UsedPath = WgetCloudUsedInput.Text.Trim();
            config.Http.TotalPath = WgetCloudTotalInput.Text.Trim();
            config.WgetCloudUsePost = WgetCloudPostCheck.IsChecked == true;
            config.WgetCloudValuesAreBytes = WgetCloudBytesCheck.IsChecked == true;
            if (config.Http.Url.Length > 0)
            {
                var uri = ValidateField(WgetCloudEndpointInput, () => WgetCloudQuotaProvider.ValidateDashboardUrl(config.Http.Url, config.WgetCloudUsePost));
                if (uri.AbsolutePath.TrimEnd('/').Equals("/user", StringComparison.OrdinalIgnoreCase))
                    config.WgetCloudUsePost = false;
            }
            using var empty = JsonDocument.Parse("{}");
            foreach (var field in new[] { WgetCloudRemainingInput, WgetCloudUsedInput, WgetCloudTotalInput })
                if (field.Text.Trim().Length > 0) ValidateField(field, () => JsonFields.Get(empty.RootElement, field.Text.Trim()));
            if (KeyInput.Password.Length > 0 && !WgetCloudQuotaProvider.IsSessionCookie(KeyInput.Password.Trim()))
                throw Invalid(KeyInput, "请填写有效的会话 Cookie（name=value），不能使用订阅链接或密码。");
        }
        if (config.NeedsSecret && config.Type != ProviderType.WgetCloud)
        {
            var url = BaseUrlInput.Text.Trim();
            ValidateField(BaseUrlInput, () => HttpQuotaProvider.ValidateUrl(url));
            if (url.Contains("{key}", StringComparison.OrdinalIgnoreCase) || url.Contains("key=", StringComparison.OrdinalIgnoreCase) || url.Contains("token=", StringComparison.OrdinalIgnoreCase))
                throw Invalid(BaseUrlInput, "密钥请放入请求头模板，不要填在 URL 中。");
            if (config.Type == ProviderType.Custom) config.Http.Url = url; else config.BaseUrl = url;
        }
        if (config.Type is ProviderType.Hone or ProviderType.Relay)
        {
            config.UsageMultiplier = Number(UsageMultiplierInput, "换算倍数", false)!.Value;
            if (config.UsageMultiplier <= 0) throw Invalid(UsageMultiplierInput, "换算倍数必须大于 0。");
        }
        if (config.Type is ProviderType.Manual or ProviderType.Custom)
        {
            config.Budget = Number(BudgetInput, "预算", true);
            if (config.Budget < 0) throw Invalid(BudgetInput, "预算不能为负数。");
        }
        if (config.Type == ProviderType.Manual)
        {
            config.ManualRemaining = Number(ManualRemainingInput, "余额", true);
            config.ManualUsed = Number(ManualUsedInput, "已用", true);
            if (config.ManualUsed < 0) throw Invalid(ManualUsedInput, "已用金额不能为负数。");
            if (config.ManualRemaining is null && config.ManualUsed is null) throw Invalid(ManualRemainingInput, "至少录入一个金额。");
            config.ManualUpdatedAt = DateTimeOffset.UtcNow;
        }
        if (config.Type == ProviderType.Custom)
        {
            config.Http.Kind = (QuotaKind)Math.Clamp(KindInput.SelectedIndex, 0, 2);
            config.Http.RemainingPath = RemainingPathInput.Text.Trim(); config.Http.UsedPath = UsedPathInput.Text.Trim(); config.Http.TotalPath = TotalPathInput.Text.Trim();
            config.Http.Multiplier = Number(MultiplierInput, "换算倍数", false)!.Value;
            if (config.Http.Multiplier <= 0) throw Invalid(MultiplierInput, "换算倍数必须大于 0。");
            using var empty = JsonDocument.Parse("{}");
            foreach (var field in new[] { RemainingPathInput, UsedPathInput, TotalPathInput })
                if (field.Text.Trim().Length > 0) ValidateField(field, () => JsonFields.Get(empty.RootElement, field.Text.Trim()));
            if (new[] { config.Http.RemainingPath, config.Http.UsedPath, config.Http.TotalPath }.All(string.IsNullOrWhiteSpace)) throw Invalid(RemainingPathInput, "请填写至少一个字段路径。");
            try { config.Http.Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(HeadersInput.Text) ?? throw new JsonException(); }
            catch (JsonException) { throw Invalid(HeadersInput, "请求头必须是 JSON 字符串字典。"); }
            foreach (var pair in config.Http.Headers)
                if (pair.Value is null || pair.Value.Contains("sk-") || (new[] { "authorization", "key", "token", "secret" }.Any(word => pair.Key.Contains(word, StringComparison.OrdinalIgnoreCase)) && !pair.Value.Contains("{key}")))
                    throw Invalid(HeadersInput, "请求头中请用 {key} 引用密钥，不要写入实际 key。");
        }
        var endpointChanged = config.BaseUrl != selected.BaseUrl || config.Http.Url != selected.Http.Url;
        if (endpointChanged && config.NeedsSecret && KeyInput.Password.Length == 0 && app.Credentials.Read(selected.Id) is not null && config.Type != ProviderType.WgetCloud)
            throw Invalid(KeyInput, "接口地址已改变，请重新输入该地址对应的 key。");
        return config;
    }
    private decimal? Number(TextBox field, string label, bool optional)
    {
        var value = field.Text;
        if (optional && string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)) return number;
        throw Invalid(field, label + "应填写数字，小数点使用 .。");
    }
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!ResolvePendingChanges()) return;
        var config = ProviderConfig.Create(Types[Math.Max(0, AddType.SelectedIndex)].Type);
        drafts.Add(config.Id);
        working.Providers.Add(config); ReloadList(config.Id); MarkFormDirty(); StatusText.Text = "填写信息后点击保存账户。";
    }
    private async void WgetCloudLogin_Click(object sender, RoutedEventArgs e)
    {
        InvalidateConnectionTest();
        WgetCloudLoginButton.IsEnabled = false;
        try
        {
            var config = ReadForm();
            if (config.Type != ProviderType.WgetCloud)
                throw new ProviderException("请先选择 WgetCloud 数据源。");
            var uri = WgetCloudQuotaProvider.ValidateDashboardUrl(config.Http.Url, false);
            if (uri.AbsolutePath.TrimEnd('/') != "/user" || config.WgetCloudUsePost)
                throw new ProviderException("一键登录仅支持用户中心 /user HTML 模式，请取消 POST。");
            config.SecretEnvironment = "";
            if (app.Monitor.Demo) throw new ProviderException("演示模式不连接真实账户。");

            var login = new WgetCloudLoginWindow(config) { Owner = this };
            if (login.ShowDialog() != true || login.ApprovedSnapshot is not { HasValue: true } verified)
            {
                StatusText.Text = "尚未连接；原有账户设置保持不变。";
                return;
            }

            // Persist only mode + dashboard URL, never a copied raw browser Cookie.
            config.WgetCloudUseBrowserSession = true;
            config.WgetCloudUsePost = false;
            config.SecretEnvironment = "";
            var old = selected!;
            var changed = config.Http.Url != old.Http.Url || config.BaseUrl != old.BaseUrl;
            if (changed) config.Id = Guid.NewGuid().ToString("N");
            var index = working.Providers.IndexOf(old);
            working.Providers[index] = config;
            try { Commit(config.Id); }
            catch { working.Providers[index] = old; throw; }
            // Old WgetCloud credentials are not required by browser-profile mode.
            app.Credentials.Delete(old.Id);
            drafts.Remove(old.Id);
            KeyInput.Clear();
            ReloadList(config.Id);
            StatusText.Text = "连接成功 · 剩余流量 " + CardViewModel.Money(verified.Remaining, "GB") + "。以后使用内置浏览器会话自动刷新。";
            ConnectionFeedback.Text = "WgetCloud 已在内置浏览器完成连接并保存。";
            await app.Monitor.RefreshAsync(true);
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { ShowError(error); }
        finally { WgetCloudLoginButton.IsEnabled = true; }
    }

    private void InvalidateConnectionTest()
    {
        if (connectionTest is not { } previous) return;
        // Relinquish ownership before cancellation: providers can complete during
        // cancellation or ignore it. Their continuations must not touch this form.
        connectionTest = null;
        TestButton.IsEnabled = true;
        ConnectionFeedback.Text = "";
        StatusText.Text = "连接测试已取消；请测试当前账户配置。";
        previous.Cancel();
    }
    private async void Test_Click(object sender, RoutedEventArgs e) => await TestConnectionAsync(FetchTestSnapshotAsync);
    private static async Task<QuotaSnapshot> FetchTestSnapshotAsync(ProviderConfig config, string? key, CancellationToken token)
    {
        using var http = new HttpQuotaProvider();
        using var wgetCloud = new WgetCloudQuotaProvider();
        using var browserQuota = new WgetCloudBrowserQuotaProvider();
        IQuotaProvider provider = config.Type switch
        {
            ProviderType.Codex => new CodexQuotaProvider(),
            ProviderType.WgetCloud when config.WgetCloudUseBrowserSession => browserQuota,
            ProviderType.WgetCloud => wgetCloud,
            _ => http
        };
        return await provider.FetchAsync(config, key, token);
    }
    private async Task TestConnectionAsync(Func<ProviderConfig, string?, CancellationToken, Task<QuotaSnapshot>> fetch)
    {
        if (connectionTest is not null || lifetime.IsCancellationRequested || selected is null || page != "sources") return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        connectionTest = request;
        bool IsCurrent() => ReferenceEquals(connectionTest, request) && !request.IsCancellationRequested;
        TestButton.IsEnabled = false;
        ConnectionFeedback.Text = "正在测试当前表单的连接…";
        try
        {
            if (IsHone) { await TestHoneConnectionsAsync(fetch, request.Token, IsCurrent); return; }
            var config = ReadForm();
            var key = KeyInput.Password.Length > 0 ? KeyInput.Password.Trim() : app.Credentials.Read(config.Id);
            key ??= string.IsNullOrEmpty(config.SecretEnvironment) ? null : Environment.GetEnvironmentVariable(config.SecretEnvironment);
            if (app.Monitor.Demo)
            {
                StatusText.Text = ConnectionFeedback.Text = "演示模式不访问网络；请在正式实例中测试。";
                return;
            }
            StatusText.Text = "正在读取额度…";
            var snapshot = await fetch(config, key, request.Token);
            if (!IsCurrent()) return;
            var card = new CardViewModel(config, snapshot, []);
            StatusText.Text = snapshot.Status == SnapshotStatus.Setup ? snapshot.Message : "连接成功 · " + card.PrimaryLabel + " " + card.PrimaryValue + "。点击保存后开始自动刷新。";
            ConnectionFeedback.Text = snapshot.Status == SnapshotStatus.Setup ? (snapshot.Message ?? "尚未完成配置。") : "连接测试成功 · 当前表单仍需点击「保存账户」。";
        }
        catch (OperationCanceledException)
        {
            if (IsCurrent()) StatusText.Text = ConnectionFeedback.Text = "连接测试已取消。";
        }
        catch (Exception error)
        {
            if (IsCurrent()) { ShowError(error); ConnectionFeedback.Text = StatusText.Text; }
        }
        finally
        {
            if (ReferenceEquals(connectionTest, request))
            {
                connectionTest = null;
                TestButton.IsEnabled = true;
            }
        }
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveCurrent();
    private bool SaveCurrent()
    {
        InvalidateConnectionTest();
        try
        {
            if (IsHone) return SaveHone();
            var config = ReadForm(); var old = selected!;
            var newKey = config.Type == ProviderType.WgetCloud && config.WgetCloudUseBrowserSession
                ? "" : KeyInput.Password.Trim();
            // Start a fresh history stream when account identity or units change.
            var identityChanged = newKey.Length > 0 || config.BaseUrl != old.BaseUrl || config.Http.Url != old.Http.Url || config.Currency != old.Currency
                || config.CodexExecutable != old.CodexExecutable || config.UsageMultiplier != old.UsageMultiplier || config.SecretEnvironment != old.SecretEnvironment
                || JsonSerializer.Serialize(config.Http) != JsonSerializer.Serialize(old.Http);
            if (identityChanged) config.Id = Guid.NewGuid().ToString("N");
            var retainedKey = config.Type == ProviderType.WgetCloud && config.WgetCloudUseBrowserSession
                ? null : newKey.Length > 0 ? newKey : identityChanged ? app.Credentials.Read(old.Id) : null;
            if (config.NeedsSecret && retainedKey is { Length: > 0 }) app.Credentials.Write(config.Id, retainedKey);
            var index = working.Providers.IndexOf(old); working.Providers[index] = config;
            try { Commit(config.Id); }
            catch { working.Providers[index] = old; if (identityChanged && retainedKey is { Length: > 0 }) app.Credentials.Delete(config.Id); throw; }
            if (identityChanged) app.Credentials.Delete(old.Id);
            drafts.Remove(old.Id);
            KeyInput.Clear(); ReloadList(config.Id); StatusText.Text = "账户已保存，正在同步。";
            ConnectionFeedback.Text = "配置已保存 · 这不代表当前连接已经通过测试。";
            return true;
        }
        catch (Exception error) { ShowError(error); return false; }
    }
    private void Commit(string? savingId = null)
    {
        // Merge account edits into the latest globals and window position, not a stale dialog copy.
        var providers = working.Providers.Where(p => !drafts.Contains(p.Id) || p.Id == savingId).ToList();
        app.SaveSettings(app.Settings with { Providers = providers });
    }
    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        var id = selected?.Id;
        if (id is not null && drafts.Remove(id)) working.Providers.RemoveAll(p => p.Id == id);
        ReloadList(id);
        StatusText.Text = "已放弃当前账户的未保存修改。";
    }
    public bool SelectProvider(string id)
    {
        if (!ResolvePendingChanges()) return false;
        working = Clone(app.Settings);
        ReloadList(id);
        ShowPage("sources");
        return true;
    }
    private void Page_Click(object sender, RoutedEventArgs e)
    {
        if (loadingPreferences || changingPage) return;
        var next = (string)((FrameworkElement)sender).Tag;
        if (next == page) return;
        if (!ResolvePendingChanges()) { ShowPage(page); return; }
        working = Clone(app.Settings);
        ReloadList(selected?.Id);
        LoadPreferences();
        ShowPage(next);
    }
    private void ShowPage(string next)
    {
        changingPage = true;
        try
        {
            page = next;
            SourcesPage.Visibility = next == "sources" ? Visibility.Visible : Visibility.Collapsed;
            CapsulesPage.Visibility = next == "capsules" ? Visibility.Visible : Visibility.Collapsed;
            DesktopPage.Visibility = next == "desktop" ? Visibility.Visible : Visibility.Collapsed;
            SourcesNav.IsChecked = next == "sources";
            CapsuleSettingsButton.IsChecked = next == "capsules";
            DesktopNav.IsChecked = next == "desktop";
        }
        finally { changingPage = false; }
    }
    private void LoadPreferences(string? selectedId = null)
    {
        loadingPreferences = true;
        try
        {
            var settings = app.Settings;
            NotifyCheck.IsChecked = settings.Notifications;
            FullscreenCheck.IsChecked = settings.AutoHideFullscreen;
            TaskbarDockCheck.IsChecked = settings.TaskbarDocked;
            StartupCheck.IsChecked = settings.StartWithWindows;
            RotateCheck.IsChecked = settings.AutoRotate;
            RotateSeconds.ItemsSource = new[] { 5, 15, 30, 60, 300, 900, 3600, settings.CarouselSeconds }.Distinct().Order().ToList();
            RotateSeconds.SelectedItem = settings.CarouselSeconds;
            var items = CapsuleSelection.Modules(settings);
            ModuleList.ItemsSource = items;
            ModuleList.SelectedItem = items.FirstOrDefault(p => p.Id == selectedId);
        }
        finally { loadingPreferences = false; }
    }
    private void SavePreferences(AppSettings settings, string? selectedId = null)
    {
        try
        {
            app.SavePreferences(settings);
            StatusText.Text = "已保存 · 全局偏好已生效。";
        }
        catch (Exception error) { ShowError(error); StatusText.Text += " 更改未保存，已恢复原值。"; }
        finally { LoadPreferences(selectedId); }
    }
    private void GlobalPreference_Click(object sender, RoutedEventArgs e)
    {
        if (loadingPreferences) return;
        SavePreferences(app.Settings with {
            Notifications = NotifyCheck.IsChecked == true, AutoHideFullscreen = FullscreenCheck.IsChecked == true,
            TaskbarDocked = TaskbarDockCheck.IsChecked == true, StartWithWindows = StartupCheck.IsChecked == true,
            AutoRotate = RotateCheck.IsChecked == true
        }, (ModuleList.SelectedItem as CapsuleModule)?.Id);
    }
    private void Rotation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!loadingPreferences && RotateSeconds.SelectedItem is int seconds)
            SavePreferences(app.Settings with { CarouselSeconds = seconds });
    }
    private void ReorderModule(string id, int targetIndex)
    {
        var items = app.Settings.Capsules.Select(p => p with { }).ToList();
        var index = items.FindIndex(p => p.Id == id);
        if (index < 0 || targetIndex < 0 || targetIndex >= items.Count || index == targetIndex) return;
        var item = items[index]; items.RemoveAt(index); items.Insert(targetIndex, item);
        SavePreferences(app.Settings with { Capsules = items }, item.Id);
    }
    private void ModuleEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (loadingPreferences || sender is not CheckBox { Tag: CapsuleModule module } check) return;
        var enabled = check.IsChecked == true;
        // Binding can raise Checked after LoadPreferences finishes. A mixed group must
        // not silently enable its disabled members merely because it renders as enabled.
        if (module.Enabled == enabled) return;
        var ids = module.Providers.Select(p => p.Id).ToHashSet();
        try
        {
            var next = app.Settings with {
                Providers = app.Settings.Providers.Select(p => ids.Contains(p.Id) ? p with { Enabled = enabled } : p).ToList(),
                Capsules = app.Settings.Capsules.Select(p => p.Id == module.Id ? p with { Enabled = enabled } : p).ToList()
            };
            if (!next.Capsules.Any(p => p.Id is "clock" or "system" && p.Enabled) && !next.Providers.Any(p => p.Enabled))
                throw new ProviderException("至少启用一个模块；本次更改未保存。");
            if (ids.Count > 0) app.SaveSettings(next); else app.SavePreferences(next);
            StatusText.Text = $"已保存 · {module.Name} 已{(enabled ? "启用" : "停用")}。";
        }
        catch (Exception error) { ShowError(error); StatusText.Text += " 更改未保存，已恢复原值。"; }
        finally { LoadPreferences(module.Id); }
    }
    private ProviderException Invalid(Control field, string message)
    {
        invalidFields.Add(field);
        return FieldFeedback.Invalid(field, message);
    }
    private T ValidateField<T>(Control field, Func<T> read)
    {
        try { return read(); }
        catch (ProviderException error) { throw Invalid(field, error.Message); }
    }
    private void ClearValidation()
    {
        foreach (var field in invalidFields) FieldFeedback.SetError(field, "");
        invalidFields.Clear();
    }
    private void DeleteKey_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null) return;
        var answer = MessageBox.Show(this,
            $"确定清除「{selected.Name}」保存在 Windows 凭据管理器中的密钥？\n\n不会删除环境变量引用；如果环境变量仍有值，账户可能继续认证。此操作不能撤销。",
            "清除保存的密钥", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            app.Credentials.Delete(selected.Id);
            InvalidateConnectionTest();
            KeyInput.Clear();
            KeyHint.Text = "本机保存的密钥已清除；配置的环境变量仍可能提供密钥。";
            StatusText.Text = "已清除保存的密钥；未更改环境变量配置。";
        }
        catch (Exception error) { ShowError(error); }
    }
    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null) return;
        if (IsHone) { RemoveHone(); return; }
        var old = selected;
        var answer = MessageBox.Show(this,
            $"确定移除数据源「{old.Name}」？\n\n此操作会移除账户配置及保存的凭据，未保存的表单修改也将丢失。历史记录将保留至正常过期。此操作不能撤销。",
            "移除数据源", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            var index = working.Providers.IndexOf(old);
            if (index < 0) return;
            working.Providers.RemoveAt(index);
            try { Commit(); }
            catch { working.Providers.Insert(index, old); throw; }
            drafts.Remove(old.Id);
            ReloadList(null);
            try
            {
                app.Credentials.Delete(old.Id);
                StatusText.Text = "数据源已移除，历史记录保留至正常过期。";
            }
            catch (Exception)
            {
                StatusText.Text = "数据源已移除，但清理本机凭据失败；请检查 Windows 凭据管理器。";
            }
        }
        catch (Exception error) { ShowError(error); }
    }
    private void ShowError(Exception error) => StatusText.Text = error is ProviderException ? error.Message : "操作失败（" + error.GetType().Name + "），请检查输入、网络或目录权限。";
}
