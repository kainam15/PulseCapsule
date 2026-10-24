namespace PulseCapsule.Core;

// Shared presentation identity for quota cards and source settings; never changes stored IDs.
public static class ProviderGrouping
{
    public static (ProviderType Family, string Site) Key(ProviderConfig config)
    {
        var family = config.Type == ProviderType.HoneWallet ? ProviderType.Hone : config.Type;
        if (family == ProviderType.Codex) return (family, "local");
        if (family is ProviderType.Manual or ProviderType.WgetCloud) return (family, config.Id);
        // Match the actual fetcher's /v1 normalization; keep tenant paths and ports distinct.
        var address = family == ProviderType.Custom ? config.Http.Url : HttpQuotaProvider.Endpoint(config.BaseUrl, "");
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
            return (family, config.Id);
        return (family, uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped)
            + uri.AbsolutePath.TrimEnd('/') + uri.Query);
    }

    public static string Name(ProviderConfig config) => config.Type switch
    {
        ProviderType.Hone or ProviderType.HoneWallet => "Hone",
        ProviderType.Relay or ProviderType.Custom => new Uri(config.Type == ProviderType.Custom ? config.Http.Url : config.BaseUrl).Host,
        _ => ProviderConfig.Create(config.Type).Name
    };

    public static string ServiceName(ProviderConfig config, IReadOnlyList<ProviderConfig> configs)
    {
        var name = config.Name;
        if (configs.Count(c => c.Type == config.Type) == 1)
            name = config.Type switch
            {
                ProviderType.HoneWallet when name == "Hone 钱包" => "账户钱包",
                ProviderType.Hone when name == "Hone API" => "API key",
                _ => name
            };
        var duplicates = configs.Where(c => c.Name == config.Name).ToList();
        return duplicates.Count > 1 ? name + " " + (duplicates.FindIndex(c => c.Id == config.Id) + 1) : name;
    }
}
