using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Web.WebView2.Wpf;

namespace PulseCapsule;

/// <summary>Session stays in an isolated WebView2 profile, never exported as raw cookies.</summary>
internal static class WgetCloudBrowserSession
{
    public const string VisibleTextScript = "document.body ? document.body.innerText : ''";

    public static string ProfileDirectory(Uri dashboard)
    {
        var hostHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(dashboard.Host)))[..16];
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PulseCapsule", "WgetCloudBrowser", hostHash);
    }

    public static async Task<QuotaSnapshot> ReadPageAsync(
        WebView2 browser, ProviderConfig config, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = WgetCloudQuotaProvider.ValidateDashboardUrl(config.Http.Url, false);
        if (expected.AbsolutePath.TrimEnd('/') != "/user" || config.WgetCloudUsePost)
            throw new ProviderException("浏览器会话仅支持 WgetCloud /user 首页。");
        var core = browser.CoreWebView2 ?? throw new ProviderException("内置浏览器尚未完成初始化。");
        var current = core.Source;
        if (!Uri.TryCreate(current, UriKind.Absolute, out var source)
            || source.Scheme != Uri.UriSchemeHttps
            || !source.Host.Equals(expected.Host, StringComparison.OrdinalIgnoreCase)
            || source.AbsolutePath.TrimEnd('/') != "/user")
            throw new ProviderException("请先返回 WgetCloud 用户中心主页后再连接。");

        // A constant script reads only visible DOM text from the user's authorized session.
        // WebView2 returns a JSON-encoded string; never serialize arbitrary page objects.
        var result = await core.ExecuteScriptAsync(VisibleTextScript);
        cancellationToken.ThrowIfCancellationRequested();
        var visibleText = JsonSerializer.Deserialize<string>(result);
        if (string.IsNullOrWhiteSpace(visibleText))
            throw new ProviderException("网页尚未显示套餐数据，请稍后重试。");
        return WgetCloudQuotaProvider.ParseDashboardHtml(config, visibleText);
    }
}
