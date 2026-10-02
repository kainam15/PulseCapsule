using System.IO;
using System.Text.Json;
using QuotaPeek.Core;
using QuotaPeek.Capsules.SystemInfo;

static class HardwareProbe
{
    public static async Task Run(string[] args)
    {
        var index = Array.IndexOf(args, "--system-probe");
        var seconds = index + 1 < args.Length && int.TryParse(args[index + 1], out var duration) ? Math.Clamp(duration, 2, 600) : 12;
        using var hardware = new HardwareMonitor();
        var samples = new List<SystemSnapshot>();
        Console.WriteLine(JsonSerializer.Serialize(new { hardware.Asus.Identity, hardware.Asus.Connected, ReadOnly = true, hardware.Asus.CanVerifyAutomaticRestore, hardware.Asus.ControlUnavailableReason }));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        for (var i = 0; i < seconds; i++)
        {
            await timer.WaitForNextTickAsync();
            var sample = await hardware.Sample(new()); samples.Add(sample);
            Console.WriteLine(JsonSerializer.Serialize(sample));
        }
        var temps = samples.Select(s => s.CpuTemperature.Value).Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
        Console.WriteLine(JsonSerializer.Serialize(new { Samples = samples.Count, ValidTemperatures = temps.Length,
            Mean = temps.Length > 0 ? temps.Average() : (double?)null, P95 = temps.Length > 0 ? temps[(int)Math.Ceiling(temps.Length * .95) - 1] : (double?)null,
            Peak = temps.Length > 0 ? temps[^1] : (double?)null, AverageLoad = samples.Average(s => s.CpuLoad), Phase = "read-only baseline; no cooling writes" }));
    }
}
