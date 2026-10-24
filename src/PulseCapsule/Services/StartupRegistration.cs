using Microsoft.Win32;

namespace PulseCapsule.Services;

internal static class StartupRegistration
{
    // Keep the exact prior registry values so a failed settings write can be rolled back.
    public static Action Change(bool enabled)
    {
        const string path = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.CreateSubKey(path);
        var names = new[] { DataIdentity.ProductName, DataIdentity.LegacyName }.Distinct().ToArray();
        var previous = names.Select(name => (Name: name, Value: key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
            Kind: key.GetValue(name) is null ? RegistryValueKind.String : key.GetValueKind(name))).ToArray();
        void Restore()
        {
            using var restore = Registry.CurrentUser.CreateSubKey(path);
            foreach (var item in previous)
                if (item.Value is null) restore.DeleteValue(item.Name, false); else restore.SetValue(item.Name, item.Value, item.Kind);
        }
        try
        {
            if (enabled) key.SetValue(DataIdentity.ProductName, "\"" + Environment.ProcessPath + "\"");
            else key.DeleteValue(DataIdentity.ProductName, false);
            key.DeleteValue(DataIdentity.LegacyName, false);
            return Restore;
        }
        catch { Restore(); throw; }
    }
}
