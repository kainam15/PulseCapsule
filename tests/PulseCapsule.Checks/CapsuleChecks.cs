using System.IO;
using System.Text.Json;
using PulseCapsule.Core;
using PulseCapsule.Capsules.SystemInfo;
using PulseCapsule.Shell;
using PulseCapsule.Services;

static class CapsuleChecks
{
    public static void Run(Action<string, bool> check)
    {
        check("wheel wraps arbitrary capsule counts", CapsuleSelection.Cycle(0, 1, 7) == 6 && CapsuleSelection.Cycle(6, -2, 7) == 1);
        check("empty capsule list is safe", CapsuleSelection.Cycle(0, 1, 0) == -1);
        var migrationRoot = Path.Combine(Path.GetTempPath(), "PulseCapsule-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(migrationRoot);
        check("new installations use the new product data directory", DataIdentity.DefaultDirectory(migrationRoot, false) == Path.Combine(migrationRoot, "PulseCapsule"));
        var oldDirectory = Path.Combine(migrationRoot, DataIdentity.LegacyName); Directory.CreateDirectory(oldDirectory);
        var oldStore = new SettingsStore(oldDirectory); var oldSettings = new AppSettings(); oldStore.Save(oldSettings);
        check("upgrade keeps the original data directory and provider IDs", DataIdentity.DefaultDirectory(migrationRoot, false) == oldDirectory && new SettingsStore(DataIdentity.DefaultDirectory(migrationRoot, false)).Load().Providers[0].Id == oldSettings.Providers[0].Id);
        check("credential targets and mutex names retain legacy identity", DataIdentity.CredentialTarget("scope", "id") == "QuotaPeek/scope/id" && DataIdentity.MutexName("scope") == @"Local\QuotaPeek-scope");
        var normalized = CapsuleSelection.Normalize([new() { Id = "system", Enabled = false }, new() { Id = "system" }, new() { Id = "unknown" }]);
        check("preferences deduplicate and keep declared order", normalized.Select(p => p.Id).SequenceEqual(["system", "clock", "quota"]) && !normalized[0].Enabled);
        check("all disabled settings retain a usable capsule", CapsuleSelection.Normalize([new() { Id = "clock", Enabled = false }, new() { Id = "quota", Enabled = false }, new() { Id = "system", Enabled = false }]).Any(p => p.Enabled));
        check("temperature excludes unreasonable and nonfinite readings", new[] { -1d, 126, double.NaN, double.PositiveInfinity }.All(v => SensorValue.Temperature(v, "test").Value is null));
        check("temperature accepts endpoints 0 and 125", SensorValue.Temperature(0, "test").Value == 0 && SensorValue.Temperature(125, "test").Value == 125);
        check("stopped is a valid zero fan distinct from failure", SensorValue.Fan(0, "test").State == SensorState.Stopped && SensorValue.Fan(-1, "test").Value is null);
        check("ASUS fan units convert to RPM", SensorValue.Fan(42, "test").Value == 4200 && SensorValue.Fan(121, "test").State == SensorState.ReadFailed);
        check("only complete FA401KM model token is allowed", new HardwareIdentity("ASUSTeK COMPUTER INC.", "TX Air FA401KM_FA401KM", "test").IsAllowedModel
            && !new HardwareIdentity("ASUS", "FA401KM2", "test").IsAllowedModel && !new HardwareIdentity("Other", "FA401KM", "test").IsAllowedModel);
        var now = DateTimeOffset.UtcNow;
        SystemSnapshot Sample(int second, double temp, double load) => new(now.AddSeconds(second), SensorValue.Temperature(temp, "test"), load, [new("CPU", SensorValue.Fan(42, "test"))]);
        var history = new ThermalHistory();
        for (var i = 0; i < 10; i++) history.Add(Sample(i, 72, 30));
        history.BeginCooling(now.AddSeconds(10));
        for (var i = 10; i < 20; i++) history.Add(Sample(i, 64, 32));
        var summary = history.Summary(now.AddSeconds(19));
        check("cooling delta compares ten-second means", summary.Baseline == 72 && summary.CurrentAverage == 64 && summary.Delta == 8 && summary.ComparableLoad);
        for (var i = 20; i < 30; i++) history.Add(Sample(i, 55, 3));
        check("load drop suppresses causal temperature delta", history.Summary(now.AddSeconds(29)).Delta is null && !history.Summary(now.AddSeconds(29)).ComparableLoad);
        var shortHistory = new ThermalHistory(); shortHistory.Add(Sample(0, 73, 30)); shortHistory.BeginCooling(now.AddSeconds(1));
        check("single point never becomes a baseline", shortHistory.Summary(now.AddSeconds(1)).Baseline is null);
        for (var i = 30; i < 200; i++) history.Add(Sample(i, 64, 30));
        check("all telemetry shares bounded 120-second history", history.Samples.Count <= 121 && history.Samples[0].Time >= now.AddSeconds(79));
        var curve = FakeFan.Policy.CpuCurve;
        var cooling = FanCurveRules.BalancedCooling(curve);
        check("cooling curve is valid and never reduces existing speeds", FanCurveRules.IsValid(cooling) && Enumerable.Range(8, 8).All(i => cooling[i] >= curve[i]));
        check("balanced cooling is not a fixed maximum", cooling.Skip(8).All(x => x <= 90) && cooling.Skip(8).Distinct().Count() > 1);
        check("invalid fan curves are rejected", !FanCurveRules.IsValid(new byte[16]) && !FanCurveRules.IsValid([30,40,50,60,70,80,90,100,10,20,30,40,50,60,70,101]));
        var dir = Path.Combine(Path.GetTempPath(), "PulseCapsule-fan-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string Journal(string name) => Path.Combine(dir, name, "recovery.json");
        var backend = new FakeFan(); var journal = Journal("success");
        using (var fan = new FanController(backend, journal, true))
        {
            backend.BeforeWrite = () => check("recovery journal is durable before a write", File.Exists(journal));
            check("verified apply enters COOL", fan.EnableCooling() && fan.State == CoolingState.Cooling);
            check("restore clears journal only after AUTO verification", fan.Restore() && fan.State == CoolingState.Auto && !File.Exists(journal));
            backend.BeforeWrite = null;
            for (var i = 0; i < 20; i++) { if (!fan.EnableCooling() || !fan.Restore()) throw new Exception("toggle cycle failed"); }
            check("20 AUTO COOL cycles recover each time", fan.State == CoolingState.Auto && !File.Exists(journal));
            fan.EnableCooling(); fan.Pause();
            check("suspend restores AUTO and blocks new cooling", fan.State == CoolingState.Auto && !fan.EnableCooling());
            fan.Resume(); check("resume permits a new explicit activation", fan.EnableCooling());
            fan.Configure(false); check("disable restores and blocks activation", fan.State == CoolingState.Auto && !fan.EnableCooling());
            fan.Configure(true); fan.EnableCooling();
        }
        check("dispose restores active cooling", !backend.Active && !File.Exists(journal));
        using (var fan = new FanController(new FakeFan { VerifyApply = false }, Journal("unconfirmed"), true))
            check("accepted but unverified write never displays COOL", !fan.EnableCooling() && fan.State == CoolingState.Auto);
        backend = new FakeFan { AcceptApply = false };
        using (var fan = new FanController(backend, Journal("rejected"), true))
            check("failed write invokes rollback", !fan.EnableCooling() && backend.Restores == 1 && fan.State == CoolingState.Auto);
        backend = new FakeFan { ThrowApply = true };
        using (var fan = new FanController(backend, Journal("throw"), true))
            check("driver exception invokes rollback", !fan.EnableCooling() && backend.Restores == 1);
        backend = new FakeFan { VerifyRestore = false }; journal = Journal("restore-fails");
        using (var fan = new FanController(backend, journal, true))
        {
            fan.EnableCooling();
            check("unverified restore stays recovery-required and retains journal", !fan.Restore() && fan.State == CoolingState.RecoveryRequired && File.Exists(journal));
        }
        backend = new FakeFan();
        using (var fan = new FanController(backend, journal, true))
            check("next startup recovers abandoned cooling", fan.State == CoolingState.Auto && backend.Restores == 1 && !File.Exists(journal));
        backend = new FakeFan { VerifiedModel = false };
        using (var fan = new FanController(backend, Journal("unknown-model"), true))
            check("unknown model is read-only without hardware writes", !fan.EnableCooling() && backend.Writes == 0 && fan.State == CoolingState.ReadOnly);
        backend = new FakeFan { CanVerifyState = false };
        using (var fan = new FanController(backend, Journal("unknown-state"), true))
            check("unverifiable hardware cannot enter COOL", !fan.EnableCooling() && backend.Writes == 0);
        journal = Journal("single-writer");
        using (var first = new FanController(new FakeFan(), journal, true))
        using (var second = new FanController(new FakeFan(), journal, true))
            check("independent instances cannot both control fans", first.CanToggle && !second.CanToggle && second.State == CoolingState.ReadOnly);
        journal = Journal("corrupt"); Directory.CreateDirectory(Path.GetDirectoryName(journal)!); File.WriteAllText(journal, "{broken");
        backend = new FakeFan();
        using (var fan = new FanController(backend, journal, true))
            check("corrupt journal is retained without guessing a mode", fan.State == CoolingState.RecoveryRequired && backend.Restores == 0 && File.Exists(journal));
        journal = Journal("other-machine"); Directory.CreateDirectory(Path.GetDirectoryName(journal)!); File.WriteAllText(journal, JsonSerializer.Serialize(FakeFan.Policy with { Model = "OTHER" }));
        backend = new FakeFan();
        using (var fan = new FanController(backend, journal, true))
            check("recovery journal must match hardware identity", fan.State == CoolingState.RecoveryRequired && backend.Restores == 0);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new AppSettings { LastCapsuleId = "broken" };
                var saved = 0;
                using var host = new CapsuleHost(settings, () => saved++);
                var good = new FakeCapsule("good"); var broken = new FakeCapsule("broken") { FailInit = true };
                host.Configure(settings, [good, broken]).GetAwaiter().GetResult();
                check("capsule initialization failure is isolated", host.Capsules.Count == 2 && broken.Disposed && host.Current?.Id == "broken");
                host.Cycle(1); check("healthy capsule remains selectable after failure", host.Current == good && saved == 1 && settings.LastCapsuleId == "good");
                host.Pause(); check("host pauses every healthy capsule", good.Paused);
                host.Resume().GetAwaiter().GetResult(); check("host resumes after lock or sleep", !good.Paused);
                host.Configure(settings, [good]).GetAwaiter().GetResult(); check("configuration preserves selected capsule identity", host.Current == good && !good.Disposed);
                var hidden = new FakeCapsule("hidden");
                host.Configure(settings, [good, hidden]).GetAwaiter().GetResult();
                var changes = 0; host.Changed += () => changes++;
                hidden.Change(); check("background capsule sampling does not repaint the shell", changes == 0);
                good.Change(); check("selected capsule changes repaint the shell", changes == 1);
                good.FailRefresh = true; host.Refresh().GetAwaiter().GetResult();
                check("refresh failure does not invalidate host enumeration", good.Disposed && hidden.Refreshes > 0 && host.Current?.Status == CapsuleStatus.Unavailable);
                hidden.FailResume = true; host.Resume().GetAwaiter().GetResult();
                check("resume failure does not invalidate host enumeration", hidden.Disposed && host.Capsules.All(c => c.Status == CapsuleStatus.Unavailable));
            }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw failure;
    }
    private sealed class FakeFan : IFanControlBackend
    {
        public static FanPolicy Policy => new("FA401KM", 0, [30,40,50,60,70,80,90,100,10,15,20,30,45,60,75,90], true);
        public string ModelIdentity => "FA401KM";
        public bool VerifiedModel { get; set; } = true;
        public bool CanVerifyState { get; set; } = true;
        public string UnavailableReason => "fixture unavailable";
        public bool AcceptApply = true, VerifyApply = true, VerifyRestore = true, ThrowApply, Active;
        public int Writes, Restores;
        public Action? BeforeWrite;
        public FanPolicy CaptureAutomaticPolicy() => Policy;
        public bool ApplyCpuCurve(byte[] curve) { BeforeWrite?.Invoke(); Writes++; Active = true; if (ThrowApply) throw new IOException(); return AcceptApply; }
        public bool IsCpuCurveActive(byte[] curve) => Active && VerifyApply;
        public bool RestoreAutomatic(FanPolicy policy) { Restores++; Active = false; return true; }
        public bool IsAutomatic(FanPolicy policy) => !Active && (Restores == 0 || VerifyRestore);
    }
    private sealed class FakeCapsule(string id) : ICapsule
    {
        public bool FailInit, FailRefresh, FailResume, Disposed, Paused;
        public int Refreshes;
        public string Id => id;
        public string Title => id;
        public string PrimaryText => id;
        public string SecondaryText => "";
        public CapsuleStatus Status => CapsuleStatus.Ready;
        public string Tooltip => id;
        public bool CanExpand => true;
        public object? ExpandedContent => null;
        public IReadOnlyList<CapsuleAction> Actions => [];
        public CapsuleAppearance Appearance => new();
        public string StatusColor => "#A6EDCF";
        public string Footer => "";
        public event Action? Changed;
        public void Change() => Changed?.Invoke();
        public Task Initialize() { if (FailInit) throw new Exception("fixture"); return Task.CompletedTask; }
        public Task Refresh(bool force = false) { Refreshes++; if (FailRefresh) throw new Exception("fixture"); return Task.CompletedTask; }
        public void Pause() => Paused = true;
        public Task Resume() { if (FailResume) throw new Exception("fixture"); Paused = false; return Task.CompletedTask; }
        public void Dispose() => Disposed = true;
    }
}
