using System.Management;

namespace PulseCapsule.Capsules.SystemInfo;

public sealed class CpuTemperatureReader(AsusAcpiAdapter asus)
{
    private DateTimeOffset fallbackRetry;
    private string? fallbackScope, sensorId;
    public SensorValue Read()
    {
        if (asus.Connected)
        {
            try { if (asus.Read(AsusAcpiAdapter.CpuTemperature) is { } value && SensorValue.Temperature(value, "ASUS ACPI").Value is not null) return SensorValue.Temperature(value, "ASUS ACPI"); }
            catch { }
        }
        return ReadFallback();
    }
    private SensorValue ReadFallback()
    {
        // Reuse an already-running sensor provider; never load a kernel driver just to get a temperature.
        if (fallbackScope is null && DateTimeOffset.UtcNow < fallbackRetry) return SensorValue.Unavailable(SensorState.Unsupported);
        foreach (var scope in fallbackScope is null ? new[] { @"root\LibreHardwareMonitor", @"root\OpenHardwareMonitor" } : [fallbackScope])
        {
            try
            {
                using var query = new ManagementObjectSearcher(scope, "SELECT Identifier, Name, Value, Parent FROM Sensor WHERE SensorType='Temperature'");
                query.Options.Timeout = TimeSpan.FromSeconds(2);
                using var results = query.Get();
                var candidates = new List<(string Id, string Name, double Value)>();
                foreach (ManagementObject item in results)
                {
                    using (item)
                    {
                        var id = item["Identifier"]?.ToString() ?? "";
                        var parent = item["Parent"]?.ToString() ?? "";
                        if (!parent.Contains("cpu", StringComparison.OrdinalIgnoreCase) || (sensorId is not null && id != sensorId)) continue;
                        candidates.Add((id, item["Name"]?.ToString() ?? "", Convert.ToDouble(item["Value"])));
                    }
                }
                var selected = candidates.OrderBy(c => c.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || c.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ? 0 : 1).FirstOrDefault();
                if (selected.Id is null) continue;
                fallbackScope = scope; sensorId = selected.Id;
                return SensorValue.Temperature(selected.Value, scope.Split('\\')[^1]);
            }
            catch { }
        }
        fallbackScope = sensorId = null; fallbackRetry = DateTimeOffset.UtcNow.AddSeconds(30);
        return SensorValue.Unavailable(SensorState.Unsupported);
    }
}
