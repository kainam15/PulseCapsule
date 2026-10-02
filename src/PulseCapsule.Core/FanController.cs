using System.Text.Json;

namespace PulseCapsule.Core;

public enum CoolingState { ReadOnly, Auto, Applying, Cooling, Restoring, RecoveryRequired }
public sealed record FanPolicy(string Model, int Mode, byte[] CpuCurve, bool AutomaticVerified);

// Success includes observable firmware state, not just an accepted DEVS command.
public interface IFanControlBackend
{
    string ModelIdentity { get; }
    bool VerifiedModel { get; }
    bool CanVerifyState { get; }
    string UnavailableReason { get; }
    FanPolicy? CaptureAutomaticPolicy();
    bool ApplyCpuCurve(byte[] curve);
    bool IsCpuCurveActive(byte[] curve);
    bool RestoreAutomatic(FanPolicy policy);
    bool IsAutomatic(FanPolicy policy);
}

public static class FanCurveRules
{
    public static bool IsValid(byte[] curve) => curve.Length == 16 && curve.Take(8).All(t => t <= 125)
        && curve.Skip(8).All(v => v <= 100) && Enumerable.Range(1, 7).All(i => curve[i] > curve[i - 1] && curve[i + 8] >= curve[i + 7]);
    public static byte[] BalancedCooling(byte[] original)
    {
        if (!IsValid(original)) throw new ArgumentException("Invalid CPU curve.");
        var result = original.ToArray();
        for (var i = 0; i < 8; i++)
        {
            var floor = Math.Clamp(20 + (original[i] - 30), 20, 90);
            result[i + 8] = (byte)Math.Max(original[i + 8], Math.Min(90, Math.Max(floor, original[i + 8] + 10)));
        }
        return result;
    }
}

public sealed class FanController : IDisposable
{
    private readonly IFanControlBackend backend;
    private readonly string journalPath;
    private readonly object gate = new();
    private FileStream? ownership;
    private FanPolicy? original;
    private byte[]? applied;
    private bool enabled, suspended, disposed;
    public CoolingState State { get; private set; } = CoolingState.ReadOnly;
    public string Message { get; private set; } = "";
    public bool CanToggle => !disposed && ownership is not null && backend.VerifiedModel && backend.CanVerifyState
        && (State == CoolingState.Cooling || State == CoolingState.RecoveryRequired && original is not null || enabled && !suspended && State == CoolingState.Auto);
    public FanController(IFanControlBackend backend, string journalPath, bool enabled)
    {
        this.backend = backend; this.journalPath = journalPath; this.enabled = enabled;
        lock (gate) Initialize();
    }
    private void Initialize()
    {
        if (!backend.VerifiedModel || !backend.CanVerifyState)
        {
            State = File.Exists(journalPath) ? CoolingState.RecoveryRequired : CoolingState.ReadOnly;
            Message = backend.UnavailableReason;
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
            ownership = new FileStream(journalPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (File.Exists(journalPath))
            {
                original = JsonSerializer.Deserialize<FanPolicy>(File.ReadAllText(journalPath));
                if (original is null || !ValidPolicy(original)) { original = null; throw new IOException("Invalid recovery journal."); }
                RestoreCore();
            }
            else
            {
                var current = backend.CaptureAutomaticPolicy();
                if (current is null || !ValidPolicy(current) || !backend.IsAutomatic(current)) { State = CoolingState.ReadOnly; Message = "无法确认当前自动模式"; }
                else { State = CoolingState.Auto; Message = "BIOS 自动控制"; }
            }
        }
        catch { State = File.Exists(journalPath) ? CoolingState.RecoveryRequired : CoolingState.ReadOnly; Message = "控制权或恢复记录不可用，保持只读"; }
    }
    private bool ValidPolicy(FanPolicy policy) => policy.Model == backend.ModelIdentity && policy.AutomaticVerified && policy.Mode is >= 0 and <= 2 && FanCurveRules.IsValid(policy.CpuCurve);
    public bool EnableCooling()
    {
        lock (gate)
        {
            if (!CanToggle || State != CoolingState.Auto || ownership is null || !backend.VerifiedModel || !backend.CanVerifyState) return false;
            State = CoolingState.Applying;
            var writeAttempted = false;
            try
            {
                original = backend.CaptureAutomaticPolicy();
                if (original is null || !ValidPolicy(original)) { State = CoolingState.ReadOnly; Message = "无法确认并保存当前自动模式"; return false; }
                applied = FanCurveRules.BalancedCooling(original.CpuCurve);
                // Persist before the first write; survive termination anywhere after this point.
                var data = JsonSerializer.SerializeToUtf8Bytes(original);
                var temporary = journalPath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(data); stream.Flush(true); }
                File.Move(temporary, journalPath, true);
                writeAttempted = true;
                if (!backend.ApplyCpuCurve(applied) || !backend.IsCpuCurveActive(applied)) throw new IOException("CPU curve not verified.");
                State = CoolingState.Cooling; Message = "Balanced Cooling · BIOS 曲线已确认";
                return true;
            }
            catch
            {
                if (writeAttempted) RestoreCore();
                else { original = null; applied = null; State = CoolingState.Auto; }
                if (State == CoolingState.Auto) Message = writeAttempted ? "写入未确认，已恢复自动控制" : "无法保存恢复记录，未更改风扇";
                return false;
            }
        }
    }
    public bool Restore()
    {
        lock (gate) return RestoreCore();
    }
    private bool RestoreCore()
    {
        if (original is null) return State is CoolingState.Auto or CoolingState.ReadOnly;
        State = CoolingState.Restoring;
        try
        {
            if (ownership is null || !backend.VerifiedModel || !backend.CanVerifyState || !ValidPolicy(original)
                || !backend.RestoreAutomatic(original) || !backend.IsAutomatic(original)) throw new IOException("AUTO not verified.");
            File.Delete(journalPath);
            original = null; applied = null;
            State = CoolingState.Auto; Message = "已确认恢复 BIOS 自动控制";
            return true;
        }
        catch { State = CoolingState.RecoveryRequired; Message = "尚未确认恢复自动控制，请在 Armoury Crate 中恢复自动模式"; return false; }
    }
    public void Observe()
    {
        lock (gate)
        {
            if (State != CoolingState.Cooling || applied is null) return;
            try
            {
                if (backend.IsCpuCurveActive(applied)) return;
                RestoreCore();
                if (State == CoolingState.Auto) Message = "检测到风扇模式变化，已结束 Cooling 并恢复自动";
            }
            catch { RestoreCore(); }
        }
    }
    public void Configure(bool value) { lock (gate) { enabled = value; if (!enabled) RestoreCore(); } }
    public void Pause() { lock (gate) { suspended = true; RestoreCore(); } }
    public void Resume() { lock (gate) { suspended = false; if (State == CoolingState.RecoveryRequired && ownership is not null) RestoreCore(); } }
    public void Dispose() { lock (gate) { if (disposed) return; suspended = true; RestoreCore(); disposed = true; ownership?.Dispose(); ownership = null; } }
}
