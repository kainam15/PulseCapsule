
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace PulseCapsule;

/// <summary>
/// Dedicated WgetCloud-only browser profile. This never accesses Chrome's credentials
/// or exposes cookie values to the UI. The user explicitly signs in and confirms linkage.
/// </summary>
public sealed class WgetCloudLoginWindow : Window
{
    private readonly Uri dashboard;
    private readonly ProviderConfig config;
    private readonly WebView2 browser = new();
    private readonly TextBlock hint = new()
    {
        Text = "请在页面中登录 WgetCloud（包括两步验证）。完成后点击「连接此账户」。",
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(14, 10, 14, 10),
        Foreground = Brushes.LightGray
    };
    private readonly Button connect = new()
    {
        Content = "连接此账户",
        Padding = new Thickness(18, 10, 18, 10),
        Margin = new Thickness(8),
        IsEnabled = false
    };
    private readonly Button cancel = new() { Content = "取消", Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(8) };
    private readonly CancellationTokenSource lifetime = new();
    public QuotaSnapshot? ApprovedSnapshot { get; private set; }

    public WgetCloudLoginWindow(ProviderConfig config)
    {
        this.config = config;
        dashboard = WgetCloudQuotaProvider.ValidateDashboardUrl(config.Http.Url, false);
        if (dashboard.AbsolutePath.TrimEnd('/') != "/user")
            throw new ProviderException("浏览器登录只支持官网用户中心 /user 页面。");
        Title = "连接 WgetCloud · PulseCapsule";
        Width = 1050;
        Height = 770;
        MinWidth = 700;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(25, 33, 44));
        var layout = new DockPanel();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        toolbar.Children.Add(cancel);
        toolbar.Children.Add(connect);
        DockPanel.SetDock(toolbar, Dock.Bottom);
        layout.Children.Add(toolbar);
        DockPanel.SetDock(hint, Dock.Top);
        layout.Children.Add(hint);
        layout.Children.Add(browser);
        Content = layout;
        cancel.Click += (_, _) => Close();
        connect.Click += Connect_Click;
        Loaded += async (_, _) => await InitializeBrowserAsync();
        Closed += (_, _) => { lifetime.Cancel(); browser.Dispose(); lifetime.Dispose(); };
    }

    public static bool IsAllowedNavigation(Uri trustedDashboard, string? candidate)
    {
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.IsDefaultPort
            || uri.Host != trustedDashboard.Host
            || uri.UserInfo.Length > 0
            || uri.Fragment.Length > 0) return false;
        var path = uri.AbsolutePath.ToLowerInvariant();
        // Never allow navigation to subscription endpoints or account-changing routes.
        if (path.Contains("/link/") || path.Contains("/subscribe") || path.Contains("/reset")
            || path.Contains("/logout") || path.Contains("/delete") || path.Contains("/pay")
            || path.Contains("/shop") || path.Contains("/bought")) return false;
        return path == "/" || path == "/user" || path.StartsWith("/user/")
            || path.StartsWith("/auth/") || path.StartsWith("/login");
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            var profile = WgetCloudBrowserSession.ProfileDirectory(dashboard);
            Directory.CreateDirectory(profile);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            if (lifetime.IsCancellationRequested) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (lifetime.IsCancellationRequested) return;
            browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            browser.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!IsAllowedNavigation(dashboard, e.Uri))
                {
                    e.Cancel = true;
                    hint.Text = "已阻止离开 WgetCloud 登录页面（包括节点订阅和账户操作）。";
                }
            };
            browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            browser.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;
            browser.Source = dashboard;
            connect.IsEnabled = true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            hint.Text = "内置浏览器初始化失败，请检查 WebView2 Runtime 是否已安装。";
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        connect.IsEnabled = false;
        try
        {
            if (browser.CoreWebView2 is null) throw new ProviderException("内置浏览器尚未准备好。");
            // Verify the visible page using its real browser session. Do not export Cookie
            // headers to a separate HTTP stack, which can be blocked by site protections.
            var result = await WgetCloudBrowserSession.ReadPageAsync(browser, config, lifetime.Token);
            if (result.Status != SnapshotStatus.Ok || !result.HasValue)
                throw new ProviderException("用户中心尚未显示有效的套餐流量。");
            ApprovedSnapshot = result;
            DialogResult = true;
        }
        catch (OperationCanceledException) { }
        catch (ProviderException ex) { hint.Text = ex.Message; }
        catch (Exception) { hint.Text = "连接失败，请检查网站是否允许内置浏览器登录。"; }
        finally { connect.IsEnabled = true; }
    }
}
