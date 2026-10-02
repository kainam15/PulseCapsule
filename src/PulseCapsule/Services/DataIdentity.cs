namespace PulseCapsule.Services;

// Persisted identities are intentionally independent of product branding.
public static class DataIdentity
{
    public const string LegacyName = "QuotaPeek";
    public const string ProductName = "PulseCapsule";
    public static string DefaultDirectory(string appData, bool demo)
    {
        if (demo) return Path.Combine(appData, ProductName + "-Demo");
        var legacy = Path.Combine(appData, LegacyName);
        return Directory.Exists(legacy) ? legacy : Path.Combine(appData, ProductName);
    }
    public static string CredentialTarget(string scope, string id) => $"{LegacyName}/{scope}/{id}";
    public static string MutexName(string scope) => $"Local\\{LegacyName}-{scope}";
    public static void UpgradeStartupRegistration(bool enabled)
    {
        if (!enabled) return;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key?.GetValue(LegacyName) is not string) return;
            key.SetValue(ProductName, "\"" + Environment.ProcessPath + "\"");
            key.DeleteValue(LegacyName, false);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or IOException) { }
    }
}
