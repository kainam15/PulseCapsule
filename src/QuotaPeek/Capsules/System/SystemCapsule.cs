namespace QuotaPeek.Capsules.SystemInfo;

public sealed class SystemCapsule : CapsuleBase
{
    private readonly HardwareMonitor? hardware;
    private readonly FanController fan;
    private readonly SystemExpandedView view;
    private readonly ThermalHistory history = new();
    private SystemPreferences preferences;
    private readonly bool demo;
    private bool toggling;
    private DateTimeOffset lastObserved;
    public override string Id => "system";
    public override string Title => "System";
    public override CapsuleAppearance Appearance => new(false, false, 13, 12);
    public override string Footer => demo ? "演示数据 · 不读写真实硬件" : "1 Hz · 最近 2 分钟 · 本机监控";
    public override IReadOnlyList<CapsuleAction> Actions => [new("cooling", fan.State == CoolingState.Cooling ? "🌀 COOL" : fan.State == CoolingState.RecoveryRequired ? "⚠ 恢复" : "🌀 AUTO", Toggle, !toggling && fan.CanToggle)];
    public SystemCapsule(SystemPreferences preferences, bool demo, string dataDirectory) : base(TimeSpan.FromSeconds(1))
    {
        this.preferences = preferences; this.demo = demo;
        IFanControlBackend backend;
        if (demo) backend = new DemoFanBackend();
        else { hardware = new(); backend = new AsusFanBackend(hardware.Asus); }
        // Hardware recovery state is independent of account/data-dir and survives all normal launches.
        var journal = demo ? Path.Combine(dataDirectory, "demo-cooling.json") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PulseCapsule", "fan-recovery.json");
        fan = new(backend, journal, preferences.FanControl);
        view = new(Toggle); ExpandedContent = view;
        PrimaryText = "CPU —";
    }
    public void Configure(SystemPreferences value) { preferences = value; fan.Configure(value.FanControl); }
    protected override async Task RefreshCore(bool force)
    {
        var now = DateTimeOffset.UtcNow;
        var sample = demo ? new SystemSnapshot(now, SensorValue.Temperature(64 + Math.Sin(now.ToUnixTimeMilliseconds() / 8000d) * 2, "Demo"), 27,
            [new("CPU Fan", SensorValue.Fan(42, "Demo")), new("GPU Fan", SensorValue.Fan(31, "Demo"))]) : await hardware!.Sample(preferences);
        if (Paused) return;
        history.Add(sample);
        if (now - lastObserved >= TimeSpan.FromSeconds(5)) { await Task.Run(fan.Observe); lastObserved = now; }
        if (fan.State != CoolingState.Cooling) history.EndCooling();
        var summary = history.Summary(now);
        PrimaryText = sample.CpuTemperature.Value is { } temperature ? $"CPU {temperature:F0}°C" : preferences.Temperature ? "CPU unavailable" : "CPU —";
        SecondaryText = fan.State == CoolingState.Cooling && summary.Delta is { } delta ? $"{(delta >= 0 ? "↓" : "↑")}{Math.Abs(delta):F0}°C" : "";
        Status = sample.CpuTemperature.Value is not null ? CapsuleStatus.Ready : CapsuleStatus.Unavailable;
        Tooltip = (demo ? "演示数据\n" : "") + PrimaryText + $"\nCPU Load {sample.CpuLoad:F0}%\n"
            + string.Join("\n", sample.Fans.Where(f => f.Speed.State != SensorState.Unsupported).Select(SystemExpandedView.FormatFan)) + "\n" + fan.Message;
        var identity = hardware?.Asus.Identity;
        view.Update(sample, history, fan, demo ? "Demo · 不访问 ACPI" : $"{identity?.Manufacturer}\n{identity?.Model}\n{identity?.Driver}\n温度来源：{sample.CpuTemperature.Source}\n{fan.Message}");
    }
    private async Task Toggle()
    {
        if (toggling || Paused || !fan.CanToggle) return;
        toggling = true; Publish();
        try
        {
            if (fan.State == CoolingState.Auto)
            {
                history.BeginCooling(DateTimeOffset.UtcNow);
                if (!await Task.Run(fan.EnableCooling)) history.EndCooling();
            }
            else { await Task.Run(fan.Restore); history.EndCooling(); }
        }
        finally { toggling = false; await Refresh(); Publish(); }
    }
    public void EmergencyRestore() => fan.Pause();
    public override void Pause() { fan.Pause(); base.Pause(); history.EndCooling(); }
    public override async Task Resume() { hardware?.Reprobe(); fan.Resume(); await base.Resume(); }
    public override void Dispose() { fan.Dispose(); base.Dispose(); hardware?.Dispose(); }
    private sealed class DemoFanBackend : IFanControlBackend
    {
        public string ModelIdentity => "Demo";
        private bool active;
        public bool VerifiedModel => true;
        public bool CanVerifyState => true;
        public string UnavailableReason => "Demo";
        public FanPolicy CaptureAutomaticPolicy() => new("Demo", 0, [30,40,50,60,70,80,90,100,10,15,20,30,45,60,75,90], true);
        public bool ApplyCpuCurve(byte[] curve) { active = true; return true; }
        public bool IsCpuCurveActive(byte[] curve) => active;
        public bool RestoreAutomatic(FanPolicy policy) { active = false; return true; }
        public bool IsAutomatic(FanPolicy policy) => !active;
    }
}
