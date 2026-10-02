using System.Runtime.InteropServices;

namespace QuotaPeek.Capsules.SystemInfo;

public sealed class HardwareMonitor : IDisposable
{
    private readonly object gate = new();
    public AsusAcpiAdapter Asus { get; }
    private readonly CpuTemperatureReader temperatures;
    private readonly FanMonitor fans;
    private ulong previousIdle, previousTotal;
    private long lastSampleTick;
    private SystemSnapshot? latest;
    private bool disposed;
    public HardwareMonitor()
    {
        Asus = new(HardwareIdentity.Detect());
        temperatures = new(Asus); fans = new(Asus);
    }
    public void Reprobe() { lock (gate) { Asus.Reconnect(); fans.Reprobe(); previousIdle = previousTotal = 0; latest = null; } }
    public Task<SystemSnapshot> Sample(SystemPreferences preferences) => Task.Run(() =>
    {
        lock (gate)
        {
            if (disposed) throw new ObjectDisposedException(nameof(HardwareMonitor));
            // Manual refresh/action clicks cannot turn the one-second sampler into a high-rate ACPI loop.
            if (latest is not null && System.Diagnostics.Stopwatch.GetElapsedTime(lastSampleTick) < TimeSpan.FromSeconds(1)) return latest;
            var temperature = preferences.Temperature ? temperatures.Read() : SensorValue.Unavailable(SensorState.Disabled);
            double? load = null;
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                var total = kernel + user;
                if (previousTotal != 0 && total > previousTotal && idle >= previousIdle)
                    load = Math.Clamp(100.0 * (1 - (double)(idle - previousIdle) / (total - previousTotal)), 0, 100);
                previousIdle = idle; previousTotal = total;
            }
            latest = new SystemSnapshot(DateTimeOffset.UtcNow, temperature, load, preferences.FanRpm ? fans.Read() : []);
            lastSampleTick = System.Diagnostics.Stopwatch.GetTimestamp();
            return latest;
        }
    });
    public void Dispose() { lock (gate) { disposed = true; Asus.Dispose(); } }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
}
