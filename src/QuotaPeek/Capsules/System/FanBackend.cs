namespace QuotaPeek.Capsules.SystemInfo;

public sealed class AsusFanBackend(AsusAcpiAdapter adapter) : IFanControlBackend
{
    public string ModelIdentity => adapter.Identity.Model;
    public bool VerifiedModel => adapter.Identity.IsAllowedModel;
    public bool CanVerifyState => adapter.CanVerifyAutomaticRestore;
    public string UnavailableReason => adapter.ControlUnavailableReason;
    // ASUS DSTS returns capabilities / factory curves, not proof of current ownership.
    // Keep this unavailable until FA401KM has an independently validated state reader.
    public FanPolicy? CaptureAutomaticPolicy() => null;
    public bool ApplyCpuCurve(byte[] curve) => adapter.WriteCpuCurve(curve);
    public bool IsCpuCurveActive(byte[] curve) => false;
    public bool RestoreAutomatic(FanPolicy policy) => adapter.RestoreMode(policy.Mode);
    public bool IsAutomatic(FanPolicy policy) => false;
}
