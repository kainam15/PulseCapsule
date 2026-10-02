namespace PulseCapsule.Core;

public enum SensorState { Available, Stopped, Unsupported, ReadFailed, Disabled }
public sealed record SensorValue(double? Value, SensorState State, string Source = "")
{
    public static SensorValue Unavailable(SensorState state = SensorState.ReadFailed, string source = "") => new(null, state, source);
    public static SensorValue Temperature(double value, string source) => double.IsFinite(value) && value is >= 0 and <= 125
        ? new(value, SensorState.Available, source) : Unavailable(SensorState.ReadFailed, source);
    public static SensorValue Fan(int raw, string source) => raw is >= 0 and <= 120
        ? new(raw * 100, raw == 0 ? SensorState.Stopped : SensorState.Available, source) : Unavailable(SensorState.ReadFailed, source);
}
public sealed record FanReading(string Name, SensorValue Speed);
public sealed record SystemSnapshot(DateTimeOffset Time, SensorValue CpuTemperature, double? CpuLoad, IReadOnlyList<FanReading> Fans);
public sealed record CoolingSummary(double? Baseline, double? CurrentAverage, double? Delta, double? BaselineLoad,
    double? CurrentLoad, bool ComparableLoad, double? Minimum, TimeSpan Duration);

public sealed class ThermalHistory
{
    private readonly List<SystemSnapshot> samples = [];
    private DateTimeOffset? started;
    private double? baseline, baselineLoad;
    public IReadOnlyList<SystemSnapshot> Samples => samples;
    public void Add(SystemSnapshot sample)
    {
        if (samples.Count > 0 && sample.Time <= samples[^1].Time) return;
        samples.Add(sample);
        samples.RemoveAll(s => sample.Time - s.Time > TimeSpan.FromSeconds(120));
    }
    public void BeginCooling(DateTimeOffset now)
    {
        var before = samples.Where(s => s.Time <= now && s.Time > now.AddSeconds(-10)).ToList();
        // Require a real ten-second baseline with paired load readings.
        var valid = before.Where(s => s.CpuTemperature.Value.HasValue && s.CpuLoad.HasValue).ToList();
        var enough = valid.Count >= 8 && valid[^1].Time - valid[0].Time >= TimeSpan.FromSeconds(7);
        baseline = enough ? valid.Average(s => s.CpuTemperature.Value!.Value) : null;
        baselineLoad = enough ? valid.Average(s => s.CpuLoad!.Value) : null;
        started = now;
    }
    public void EndCooling() { started = null; baseline = baselineLoad = null; }
    public CoolingSummary Summary(DateTimeOffset now)
    {
        var recent = samples.Where(s => s.Time > now.AddSeconds(-10) && (started is null || s.Time >= started))
            .Where(s => s.CpuTemperature.Value.HasValue && s.CpuLoad.HasValue).ToList();
        var enough = recent.Count >= 8 && recent[^1].Time - recent[0].Time >= TimeSpan.FromSeconds(7);
        double? mean = enough ? recent.Average(s => s.CpuTemperature.Value!.Value) : null;
        double? load = enough ? recent.Average(s => s.CpuLoad!.Value) : null;
        var comparable = baselineLoad.HasValue && load.HasValue && Math.Abs(load.Value - baselineLoad.Value) <= 10;
        var temperatures = samples.Where(s => started is null || s.Time >= started).Select(s => s.CpuTemperature.Value).Where(t => t.HasValue).ToList();
        return new(baseline, mean, comparable ? baseline - mean : null, baselineLoad, load, comparable,
            temperatures.Count == 0 ? null : temperatures.Min(), started is null ? TimeSpan.Zero : now - started.Value);
    }
}
