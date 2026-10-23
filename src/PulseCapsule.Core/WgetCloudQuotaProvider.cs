using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PulseCapsule.Core;

/// <summary>
/// Queries account-center JSON only. Never reads a /link/ subscription,
/// enables subscription windows, downloads node lists or logs credentials.
/// </summary>
public sealed class WgetCloudQuotaProvider : IQuotaProvider, IDisposable
{
    private const decimal BytesPerGiB = 1_073_741_824m;
    private readonly HttpClient client;
    public WgetCloudQuotaProvider(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PulseCapsule/0.2");
    }

    public static Uri ValidateDashboardUrl(string url, bool post)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0
            || uri.Fragment.Length > 0 || uri.Query.Length > 0
            || !(uri.Host.Equals("wgetcloud.org", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".wgetcloud.org", StringComparison.OrdinalIgnoreCase))
            || !uri.AbsolutePath.StartsWith("/user/", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Length > 160
            || !uri.AbsolutePath["/user/".Length..].All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '/')
            || uri.AbsolutePath.Split('/').Any(part => new[] { "subscribe", "subscription", "reset", "logout",
                "login", "password", "order", "pay", "buy", "delete", "update", "change", "gift", "invite" }
                .Contains(part, StringComparer.OrdinalIgnoreCase))
            || (post && !uri.AbsolutePath.Equals("/user/log_month", StringComparison.OrdinalIgnoreCase)))
            throw new ProviderException("只允许 WgetCloud 用户中心的只读 JSON 地址；禁止订阅、重置、购买和跳转地址。");
        return uri;
    }

    public static bool IsSessionCookie(string? secret)
        => !string.IsNullOrWhiteSpace(secret) && secret.Length <= 4096
            && secret.IndexOfAny(['\r', '\n']) < 0
            && !secret.Contains("://", StringComparison.Ordinal)
            && !secret.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase)
            && secret.Split(';', StringSplitOptions.TrimEntries).All(pair =>
            {
                var separator = pair.IndexOf('=');
                return separator > 0 && separator < pair.Length - 1
                    && pair[..separator].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
                    && !pair[(separator + 1)..].Contains('\0');
            });

    private static QuotaSnapshot Setup(ProviderConfig config, string explanation) => new()
    {
        ProviderId = config.Id, Kind = QuotaKind.Limit, Currency = "GB",
        Status = SnapshotStatus.Setup, Scope = "用户中心流量", Message = explanation
    };

    public async Task<QuotaSnapshot> FetchAsync(ProviderConfig config, string? secret, CancellationToken cancellationToken)
    {
        // Existing installations may still store the old private subscription URL.
        // Never send that value anywhere, even when the provider auto-refreshes.
        if (!IsSessionCookie(secret))
            return Setup(config, "请在设置中填写用户中心会话 Cookie；旧订阅地址不再使用，也不会自动查询订阅。");
        if (string.IsNullOrWhiteSpace(config.Http.Url)
            || new[] { config.Http.RemainingPath, config.Http.UsedPath, config.Http.TotalPath }.Count(p => !string.IsNullOrWhiteSpace(p)) < 2)
            return Setup(config, "请先配置用户中心 JSON 地址及至少两个配额字段路径（剩余 / 已用 / 总量）。");

        var uri = ValidateDashboardUrl(config.Http.Url, config.WgetCloudUsePost);
        try
        {
            using var request = new HttpRequestMessage(config.WgetCloudUsePost ? HttpMethod.Post : HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Cookie", secret);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var retry = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow;
                throw new ProviderException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "用户中心会话已失效或无权访问，请重新登录后更新 Cookie。",
                    HttpStatusCode.TooManyRequests => "用户中心查询受到限流，请延后重试。",
                    >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "用户中心发生重定向；为保护 Cookie 已停止请求。",
                    _ => $"用户中心流量查询失败（HTTP {(int)response.StatusCode}）。"
                }, retry);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + count > 1_048_576)
                    throw new ProviderException("用户中心 JSON 响应超过 1 MiB，已停止读取。");
                buffer.Write(chunk, 0, count);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            return ParseDashboardJson(config, document.RootElement);
        }
        catch (JsonException) { throw new ProviderException("用户中心未返回有效 JSON；请确认接口地址与会话状态。"); }
        catch (HttpRequestException) { throw new ProviderException("无法连接用户中心，请检查网络与 JSON 接口地址。"); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ProviderException("用户中心请求超时，稍后重试。"); }
    }

    public static QuotaSnapshot ParseDashboardJson(ProviderConfig config, JsonElement root)
    {
        JsonFields.RequireSuccess(root);
        decimal? Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var number = JsonFields.Number(root, path, true)!.Value;
            if (number < 0) throw new ProviderException("用户中心返回负数流量，请核对字段映射。");
            return config.WgetCloudValuesAreBytes ? number / BytesPerGiB : number;
        }
        var remaining = Read(config.Http.RemainingPath);
        var used = Read(config.Http.UsedPath);
        var total = Read(config.Http.TotalPath);
        if (new[] { remaining, used, total }.Count(v => v.HasValue) < 2)
            throw new ProviderException("用户中心配额字段不足，不能推算流量。");
        if (total is null) total = remaining + used;
        else if (used is null) used = total - remaining;
        else if (remaining is null) remaining = total - used;
        if (total <= 0 || used < 0 || remaining < 0 ||
            Math.Abs((remaining ?? 0) + (used ?? 0) - total.GetValueOrDefault()) > Math.Max(0.01m, total.GetValueOrDefault() * 0.001m))
            throw new ProviderException("流量字段不一致；请核对 GB/字节单位及当前套餐周期。");

        return new QuotaSnapshot
        {
            ProviderId = config.Id, Kind = QuotaKind.Limit, Currency = "GB",
            Remaining = remaining, Used = used, Total = total, UsageLabel = "已用流量",
            Scope = "用户中心流量", FetchedAt = DateTimeOffset.UtcNow,
            Message = "来自用户中心 JSON；套餐周期与重置日请以官网为准。"
        };
    }

    public void Dispose() => client.Dispose();
}