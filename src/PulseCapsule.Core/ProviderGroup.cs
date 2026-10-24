namespace PulseCapsule.Core;

public sealed class ProviderGroup
{
    public string Name { get; }
    public string Id { get; }
    public bool Enabled { get; }
    public IReadOnlyList<ProviderConfig> Providers { get; }

    private ProviderGroup(IEnumerable<ProviderConfig> providers)
    {
        var ordered = providers.OrderBy(p => p.Type == ProviderType.HoneWallet ? 0 : 1).ToList();
        Name = ordered.Count > 1 || ordered[0].Type is ProviderType.Hone or ProviderType.HoneWallet
            ? ProviderGrouping.Name(ordered[0]) : ordered[0].Name;
        Id = "quota:" + ordered[0].Id;
        // Reflect existing mixed configurations without changing them while loading the page.
        Enabled = ordered.Any(p => p.Enabled);
        Providers = ordered;
    }

    // Include disabled services so every saved source can still be enabled from management.
    public static IReadOnlyList<ProviderGroup> Group(IEnumerable<ProviderConfig> providers)
        => providers.GroupBy(ProviderGrouping.Key).Select(g => new ProviderGroup(g)).ToList();
}
