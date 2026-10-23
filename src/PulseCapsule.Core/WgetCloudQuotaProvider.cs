using System.Globalization;
using System.Net;
using System.Net.Http.Headers;

namespace PulseCapsule.Core;

/// <summary>
/// Reads quota metadata only. Never downloads a subscription body, fetches nodes,
/// logs the URL, or attempts to open WgetCloud's five-minute subscription switch.
/// </summary>
public sealed class WgetCloudQuotaProvider : IQuotaProvider, IDisposable
{
    private const decimal BytesPerGiB = 1_073_741_824m;
    private readonly HttpClient client;

    public WgetCloudQuotaProvider(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = TimeSpan.FromSeconds(25) };
    }

    public static Uri ValidateSubscriptionUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0 || !uri.IsDefaultPort
            || !(uri.Host.Equals("wgetcloud.online", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".wgetcloud.online", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("wgetcloud.org", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".wgetcloud.org", StringComparison.OrdinalIgnoreCase)))
            throw new ProviderException("请使用 WgetCloud 官方提供的 HTTPS 订阅地址。");
        return uri;
    }

    public async Task<QuotaSnapshot> FetchAsync(ProviderConfig config, string? secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return new QuotaSnapshot { ProviderId = config.Id, Kind = QuotaKind.Limit, Currency = "GB",
                Status = SnapshotStatus.Setup, Scope = "套餐流量",
                Message = "请填写 WgetCloud 订阅地址；先在官网手动开启 5 分钟订阅窗口。" };
        var uri = ValidateSubscriptionUrl(secret.Trim());
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            // This widely used header is normally provided to Clash-family clients.
            request.Headers.UserAgent.ParseAdd("clash-PulseCapsule/0.2");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var retry = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow;
                throw new ProviderException(response.StatusCode switch
                {
                    HttpStatusCode.Forbidden => "WgetCloud 拒绝访问订阅（403）。请在官网手动开启订阅开关后再刷新。",
                    HttpStatusCode.Unauthorized => "订阅地址无效或已失效，请在 WgetCloud 官网重新获取。",
                    HttpStatusCode.TooManyRequests => "WgetCloud 已限制请求频率，稍后再试。",
                    >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "订阅地址发生重定向；为了保护订阅凭据，已停止请求。",
                    _ => $"WgetCloud 暂时无法读取订阅信息（HTTP {(int)response.StatusCode}）。"
                }, retry);
            }
            if (!response.Headers.TryGetValues("subscription-userinfo", out var values))
                throw new ProviderException("订阅响应没有返回 subscription-userinfo 流量字段；无法获取真实配额，请勿用节点数量推算。");
            return ParseSubscriptionUserInfo(config, values);
        }
        catch (HttpRequestException) { throw new ProviderException("无法连接 WgetCloud，请检查网络和订阅开关。"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ProviderException("WgetCloud 请求超时，请稍后重试。"); }
    }

    public static QuotaSnapshot ParseSubscriptionUserInfo(ProviderConfig config, IEnumerable<string> headers)
    {
        var fields = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
            foreach (var segment in header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var equals = segment.IndexOf('=');
                if (equals <= 0 || equals == segment.Length - 1
                    || !ulong.TryParse(segment[(equals + 1)..].Trim(), NumberStyles.None,
                        CultureInfo.InvariantCulture, out var value)
                    || !fields.TryAdd(segment[..equals].Trim(), value))
                    throw new ProviderException("WgetCloud 流量响应头格式异常。");
            }
        if (!fields.TryGetValue("upload", out var upload) || !fields.TryGetValue("download", out var download)
            || !fields.TryGetValue("total", out var total) || total == 0)
            throw new ProviderException("WgetCloud 未提供完整的上传、下载和总流量信息。");

        var usedBytes = (decimal)upload + download;
        var remainingBytes = Math.Max(0m, (decimal)total - usedBytes);
        DateTimeOffset? expires = null;
        if (fields.TryGetValue("expire", out var expire) && expire > 0)
        {
            if (expire > (ulong)DateTimeOffset.MaxValue.ToUnixTimeSeconds())
                throw new ProviderException("WgetCloud 订阅到期时间超出支持范围。");
            expires = DateTimeOffset.FromUnixTimeSeconds((long)expire);
        }
        var note = expires is null ? "订阅未提供到期时间。"
            : expires <= DateTimeOffset.UtcNow ? "订阅显示已到期，请到官网确认套餐状态。"
            : "套餐到期：" + expires.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (usedBytes > total) note = "流量使用量已超过套餐总量。 " + note;
        return new QuotaSnapshot
        {
            ProviderId = config.Id, Kind = QuotaKind.Limit, Currency = "GB",
            Remaining = remainingBytes / BytesPerGiB, Used = usedBytes / BytesPerGiB,
            Total = total / BytesPerGiB, Scope = "套餐流量", UsageLabel = "已用流量",
            FetchedAt = DateTimeOffset.UtcNow, ExpiresAt = expires, Message = note
        };
    }

    public void Dispose() => client.Dispose();
}
