using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace PulseCapsule.Services;

/// <summary>
/// Makes a low-frequency read-only user-center navigation in the SAME WebView2
/// browser profile where the user signed in. Never copies cookies into HttpClient.
/// </summary>
public sealed class WgetCloudBrowserQuotaProvider : IQuotaProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;

    public async Task<QuotaSnapshot> FetchAsync(
        ProviderConfig config, string? unusedSecret, CancellationToken cancellationToken)
    {
        if (disposed) throw new ObjectDisposedException(nameof(WgetCloudBrowserQuotaProvider));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var dispatcher = Application.Current?.Dispatcher
                ?? throw new ProviderException("无法访问桌面浏览器环境。");
            return await dispatcher.InvokeAsync(
                () => FetchOnUiThreadAsync(config, cancellationToken),
                DispatcherPriority.Normal, cancellationToken).Task.Unwrap();
        }
        finally { gate.Release(); }
    }

    private static async Task<QuotaSnapshot> FetchOnUiThreadAsync(
        ProviderConfig config, CancellationToken cancellationToken)
    {
        var uri = WgetCloudQuotaProvider.ValidateDashboardUrl(config.Http.Url, false);
        if (uri.AbsolutePath.TrimEnd('/') != "/user" || config.WgetCloudUsePost)
            throw new ProviderException("自动浏览器查询只允许 WgetCloud /user 首页。");

        // WebView2 requires a live WPF host with an STA message pump.
        // Keep it off-screen without activating or covering the user's windows.
        var window = new Window
        {
            Width = 480, Height = 320, Left = -12000, Top = -12000,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Title = "PulseCapsule WgetCloud background"
        };
        var browser = new WebView2();
        window.Content = browser;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(40));
        try
        {
            window.Show();
            var profile = WgetCloudBrowserSession.ProfileDirectory(uri);
            Directory.CreateDirectory(profile);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            timeout.Token.ThrowIfCancellationRequested();
            await browser.EnsureCoreWebView2Async(environment);
            timeout.Token.ThrowIfCancellationRequested();
            browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            browser.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target)
                    || target.Scheme != Uri.UriSchemeHttps || target.Host != uri.Host
                    || target.AbsolutePath.TrimEnd('/') != "/user" || target.Query.Length != 0)
                    e.Cancel = true;
            };
            browser.CoreWebView2.NewWindowRequested += (_, e) => e.Handled = true;
            browser.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
                => completion.TrySetResult(e.IsSuccess);
            browser.CoreWebView2.NavigationCompleted += NavigationCompleted;
            try
            {
                browser.CoreWebView2.Navigate(uri.AbsoluteUri);
                if (!await completion.Task.WaitAsync(timeout.Token))
                    throw new ProviderException("用户中心网页加载失败。");
            }
            finally { browser.CoreWebView2.NavigationCompleted -= NavigationCompleted; }

            // The browser is authenticated using its persisted profile, not a transplanted
            // Cookie header. A login page / expired session fails clearly in the parser.
            return await WgetCloudBrowserSession.ReadPageAsync(browser, config, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ProviderException("WgetCloud 用户中心浏览器查询超时，请稍后重试。"); }
        catch (ProviderException) { throw; }
        catch (Exception e) when (e is not OperationCanceledException)
        { throw new ProviderException("无法读取 WgetCloud 内置浏览器会话，请在设置中重新连接。"); }
        finally
        {
            browser.Dispose();
            window.Close();
        }
    }

    public void Dispose() { disposed = true; }
}
