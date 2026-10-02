using System.ComponentModel;

namespace QuotaPeek.Capsules.SystemInfo;

public sealed class FanMonitor(AsusAcpiAdapter asus)
{
    private readonly HashSet<uint> unsupported = [];
    private readonly HashSet<uint> supported = [];
    public void Reprobe() { unsupported.Clear(); supported.Clear(); }
    public IReadOnlyList<FanReading> Read() => [Read("CPU Fan", AsusAcpiAdapter.CpuFan), Read("GPU Fan", AsusAcpiAdapter.GpuFan), Read("System Fan", AsusAcpiAdapter.MidFan)];
    private FanReading Read(string name, uint endpoint)
    {
        if (!asus.Connected || unsupported.Contains(endpoint)) return new(name, SensorValue.Unavailable(SensorState.Unsupported));
        try
        {
            if (asus.Read(endpoint) is { } value)
            {
                supported.Add(endpoint);
                return new(name, SensorValue.Fan(value, "ASUS ACPI"));
            }
            if (!supported.Contains(endpoint)) { unsupported.Add(endpoint); return new(name, SensorValue.Unavailable(SensorState.Unsupported)); }
        }
        catch (Win32Exception error) when (error.NativeErrorCode is 1 or 50 && !supported.Contains(endpoint))
        {
            unsupported.Add(endpoint); return new(name, SensorValue.Unavailable(SensorState.Unsupported));
        }
        catch { }
        return new(name, SensorValue.Unavailable());
    }
}
