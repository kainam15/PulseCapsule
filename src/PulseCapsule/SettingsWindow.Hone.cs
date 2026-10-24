using System.Windows;
using System.Windows.Controls;
using PulseCapsule.UI;

namespace PulseCapsule;

public partial class SettingsWindow
{
    private sealed record SourceItem(string Name, IReadOnlyList<ProviderConfig> Providers)
    {
        public string Detail { get; set; } = "";
    }
    private IReadOnlyList<ProviderConfig> honeOriginals = [];
    private string honeOriginalAddress = "";
    private bool IsHone => selected?.Type is ProviderType.Hone or ProviderType.HoneWallet;
    private IEnumerable<HoneConnectionEditor> HoneEditors => HoneConnections.Children.Cast<HoneConnectionEditor>();

    private List<SourceItem> SourceItems()
    {
        var items = working.Providers
        .GroupBy(p => p.Type is ProviderType.Hone or ProviderType.HoneWallet && !drafts.Contains(p.Id)
            ? ProviderGrouping.Key(p) : (p.Type, p.Id))
        .Select(g => {
            var members = g.OrderBy(p => p.Type == ProviderType.HoneWallet ? 0 : 1).ToList();
            return new SourceItem(members[0].Type is ProviderType.Hone or ProviderType.HoneWallet ? "Hone" : members[0].Name, members);
        }).ToList();
        var hone = items.Where(p => p.Providers[0].Type is ProviderType.Hone or ProviderType.HoneWallet).ToList();
        if (hone.Count > 1)
            foreach (var item in hone)
                item.Detail = Uri.TryCreate(item.Providers[0].BaseUrl, UriKind.Absolute, out var uri) ? uri.Authority + uri.AbsolutePath.TrimEnd('/') : "新连接";
        return items;
    }

    private void LoadHone()
    {
        honeOriginals = ((SourceItem)ProviderList.SelectedItem).Providers;
        TypeTitle.Text = "Hone";
        HoneBaseUrl.Text = honeOriginalAddress = selected!.BaseUrl;
        foreach (var provider in honeOriginals)
            AddHoneEditor(provider, ProviderGrouping.ServiceName(provider, honeOriginals));
        UpdateHoneButtons();
    }

    private void AddHoneEditor(ProviderConfig provider, string name)
    {
        string hint;
        try { hint = app.Credentials.Read(provider.Id) is null ? "填写密钥，或在连接选项中设置环境变量。" : "密钥已保存在本机；留空保留。"; }
        catch { hint = "Windows 凭据管理器暂不可用。"; }
        var editor = new HoneConnectionEditor(provider, name, hint);
        editor.ClearRequested += ClearHoneCredential;
        editor.RemoveRequested += RemoveHoneConnection;
        HoneConnections.Children.Add(editor);
    }

    private void UpdateHoneButtons() => AddHoneWallet.Visibility = HoneEditors.Any(p => p.Original.Type == ProviderType.HoneWallet) ? Visibility.Collapsed : Visibility.Visible;

    private void AddHoneConnection_Click(object sender, RoutedEventArgs e)
    {
        var type = Enum.Parse<ProviderType>((string)((Button)sender).Tag);
        // Draft connections belong to this form only until the entire account saves.
        var provider = ProviderConfig.Create(type) with { BaseUrl = honeOriginalAddress, Enabled = honeOriginals.Any(p => p.Enabled) };
        AddHoneEditor(provider, type == ProviderType.HoneWallet ? "账户钱包" : "API key");
        UpdateHoneButtons(); MarkFormDirty();
    }

    private string ReadHoneAddress()
    {
        var url = HoneBaseUrl.Text.Trim();
        ValidateField(HoneBaseUrl, () => HttpQuotaProvider.ValidateUrl(url));
        if (url.Contains("{key}", StringComparison.OrdinalIgnoreCase) || url.Contains("key=", StringComparison.OrdinalIgnoreCase) || url.Contains("token=", StringComparison.OrdinalIgnoreCase))
            throw Invalid(HoneBaseUrl, "密钥请填入对应的凭据框，不要填在 URL 中。");
        return url;
    }

    private List<HoneConnectionEdit> ReadHoneForms()
    {
        ClearValidation();
        var address = ReadHoneAddress();
        var edits = new List<HoneConnectionEdit>();
        foreach (var editor in HoneEditors)
        {
            // Merely opening/saving a group must preserve each original URL and identity.
            var edit = editor.Read(address == honeOriginalAddress ? editor.Original.BaseUrl : address);
            edit.Config.Enabled = app.Settings.Providers.FirstOrDefault(p => p.Id == edit.Original.Id)?.Enabled ?? edit.Config.Enabled;
            if (edit.Config.BaseUrl != edit.Original.BaseUrl && edit.NewSecret.Length == 0 && app.Credentials.Read(edit.Original.Id) is not null)
                throw editor.RequireNewSecret();
            edits.Add(edit);
        }
        return edits;
    }

    private bool SaveHone()
    {
        var edits = ReadHoneForms();
        var oldIds = honeOriginals.Select(p => p.Id).ToHashSet();
        var stagedIds = new List<string>();
        try
        {
            foreach (var edit in edits)
            {
                var old = edit.Original; var config = edit.Config;
                var identityChanged = edit.NewSecret.Length > 0 || config.BaseUrl != old.BaseUrl
                    || config.SecretEnvironment != old.SecretEnvironment || config.UsageMultiplier != old.UsageMultiplier;
                if (!identityChanged) continue;
                config.Id = Guid.NewGuid().ToString("N");
                var secret = edit.NewSecret.Length > 0 ? edit.NewSecret : app.Credentials.Read(old.Id);
                if (secret is not { Length: > 0 }) continue;
                stagedIds.Add(config.Id);
                app.Credentials.Write(config.Id, secret);
            }
            var replacements = edits.ToDictionary(p => p.Original.Id, p => p.Config);
            var providers = app.Settings.Providers.Where(p => !oldIds.Contains(p.Id) || replacements.ContainsKey(p.Id))
                .Select(p => replacements.GetValueOrDefault(p.Id, p)).ToList();
            providers.AddRange(edits.Where(e => !app.Settings.Providers.Any(p => p.Id == e.Original.Id)).Select(e => e.Config));
            // Persist the entire group once; failed config/credential writes cannot partially save it.
            app.SaveSettings(app.Settings with { Providers = providers });
        }
        catch
        {
            foreach (var id in stagedIds) { try { app.Credentials.Delete(id); } catch { /* Unreferenced staging credential; original is intact. */ } }
            throw;
        }
        var retainedIds = edits.Select(p => p.Config.Id).ToHashSet();
        var cleanupFailed = false;
        foreach (var id in oldIds.Where(id => !retainedIds.Contains(id)))
            try { app.Credentials.Delete(id); } catch { cleanupFailed = true; }
        drafts.ExceptWith(oldIds);
        working = Clone(app.Settings);
        ReloadList(edits[0].Config.Id);
        StatusText.Text = cleanupFailed ? "Hone 已保存，但清理旧凭据失败；请检查 Windows 凭据管理器。" : "Hone 已保存，正在同步。";
        ConnectionFeedback.Text = "配置已保存 · 这不代表当前连接已经通过测试。";
        return true;
    }

    private async Task TestHoneConnectionsAsync(Func<ProviderConfig, string?, CancellationToken, Task<QuotaSnapshot>> fetch, CancellationToken token, Func<bool> isCurrent)
    {
        var edits = ReadHoneForms();
        if (app.Monitor.Demo) { StatusText.Text = ConnectionFeedback.Text = "演示模式不访问网络；请在正式实例中测试。"; return; }
        var results = new List<string>();
        foreach (var edit in edits)
        {
            if (!isCurrent()) return;
            var label = ProviderGrouping.ServiceName(edit.Config, edits.Select(p => p.Config).ToList());
            try
            {
                var key = edit.NewSecret.Length > 0 ? edit.NewSecret : app.Credentials.Read(edit.Original.Id);
                key ??= string.IsNullOrEmpty(edit.Config.SecretEnvironment) ? null : Environment.GetEnvironmentVariable(edit.Config.SecretEnvironment);
                var snapshot = await fetch(edit.Config, key, token);
                if (!isCurrent()) return;
                results.Add(label + "：" + (snapshot.Status == SnapshotStatus.Ok ? "连接成功" : snapshot.Message ?? "尚未完成配置"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                if (!isCurrent()) return;
                results.Add(label + "：" + (error is ProviderException ? error.Message : "连接失败（" + error.GetType().Name + "）"));
            }
        }
        if (!isCurrent()) return;
        ConnectionFeedback.Text = string.Join("\n", results) + "\n当前表单仍需点击「保存账户」。";
        StatusText.Text = "Hone 连接测试已完成。";
    }

    private void HoneWebsite_Click(object sender, RoutedEventArgs e)
    {
        try { App.OpenWebsite(HttpQuotaProvider.Endpoint(ReadHoneAddress(), "/security")); }
        catch (Exception error) { ShowError(error); }
    }

    private void ClearHoneCredential(HoneConnectionEditor editor)
    {
        if (MessageBox.Show(this, $"确定清除「{editor.ServiceName}」保存在本机的密钥？\n\n不会清除其他连接的密钥或环境变量；环境变量仍可能用于认证。此操作不能撤销。",
            "清除保存的密钥", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            app.Credentials.Delete(editor.Original.Id); InvalidateConnectionTest(); editor.CredentialCleared();
            ConnectionFeedback.Text = "凭据已更改，请重新测试连接。";
            StatusText.Text = "已清除该连接保存的密钥；未更改环境变量配置。";
        }
        catch (Exception error) { ShowError(error); }
    }

    private void RemoveHoneConnection(HoneConnectionEditor editor)
    {
        if (HoneConnections.Children.Count == 1) { StatusText.Text = "最后一项连接请使用下方「移除数据源」。"; return; }
        if (MessageBox.Show(this, $"移除「{editor.ServiceName}」？\n\n点击保存账户后将移除这项连接及其本机凭据，其他连接不受影响，历史记录保留至正常过期。",
            "移除连接", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        HoneConnections.Children.Remove(editor); UpdateHoneButtons(); MarkFormDirty();
    }

    private void RemoveHone()
    {
        if (MessageBox.Show(this, $"确定移除 Hone 的全部 {honeOriginals.Count} 项连接？\n\n将移除本站点的钱包、API key 配置和本机凭据，未保存的修改也将丢失。历史记录保留至正常过期。此操作不能撤销。",
            "移除 Hone", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var ids = honeOriginals.Select(p => p.Id).ToHashSet();
            app.SaveSettings(app.Settings with { Providers = app.Settings.Providers.Where(p => !ids.Contains(p.Id)).ToList() });
            drafts.ExceptWith(ids); working = Clone(app.Settings); ReloadList(null);
            var cleanupFailed = false;
            foreach (var id in ids) try { app.Credentials.Delete(id); } catch { cleanupFailed = true; }
            StatusText.Text = cleanupFailed ? "Hone 已移除，但清理本机凭据失败；请检查 Windows 凭据管理器。" : "Hone 已移除，历史记录保留至正常过期。";
        }
        catch (Exception error) { ShowError(error); }
    }
}
