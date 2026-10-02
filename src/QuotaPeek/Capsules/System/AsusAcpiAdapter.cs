using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace QuotaPeek.Capsules.SystemInfo;

public sealed record HardwareIdentity(string Manufacturer, string Model, string Driver)
{
    public bool IsAsus => Manufacturer.Contains("ASUS", StringComparison.OrdinalIgnoreCase) || Manufacturer.Contains("ASUSTeK", StringComparison.OrdinalIgnoreCase);
    public bool IsAllowedModel => IsAsus && System.Text.RegularExpressions.Regex.IsMatch(Model.ToUpperInvariant(), @"(^|[^A-Z0-9])FA401KM($|[^A-Z0-9])");
    public static HardwareIdentity Detect()
    {
        using var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
        var manufacturer = bios?.GetValue("SystemManufacturer")?.ToString() ?? "Unknown";
        var model = bios?.GetValue("SystemProductName")?.ToString() ?? "Unknown";
        var driver = "未检测到 ASUS System Control Interface";
        using var device = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\ACPI\ASUS2018");
        foreach (var instance in device?.GetSubKeyNames() ?? [])
        {
            using var key = device!.OpenSubKey(instance);
            if (key?.GetValue("Driver") is not string path) continue;
            using var info = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\" + path);
            driver = (info?.GetValue("DriverDesc")?.ToString() ?? "ASUS System Control Interface") + " " + info?.GetValue("DriverVersion");
            break;
        }
        return new(manufacturer, model, driver);
    }
}

// A deliberately narrow adapter. No EC access, driver installation, service changes or arbitrary endpoint writes.
public sealed class AsusAcpiAdapter : IDisposable
{
    private const uint Dsts = 0x53545344, Devs = 0x53564544;
    internal const uint CpuTemperature = 0x00120094, CpuFan = 0x00110013, GpuFan = 0x00110014, MidFan = 0x00110031;
    private const uint CpuCurve = 0x00110024, PerformanceMode = 0x00120075;
    private SafeFileHandle? handle;
    private readonly object gate = new();
    public HardwareIdentity Identity { get; }
    public bool Connected => handle is { IsInvalid: false, IsClosed: false };
    public AsusAcpiAdapter(HardwareIdentity identity)
    {
        Identity = identity;
        Reconnect();
    }
    public void Reconnect()
    {
        lock (gate)
        {
            handle?.Dispose(); handle = null;
            if (Identity.IsAsus) handle = CreateFile(@"\\.\ATKACPI", 0xC0000000, 3, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
        }
    }
    private byte[] Call(uint method, uint endpoint, byte[] parameters)
    {
        lock (gate)
        {
            if (!Connected) throw new Win32Exception(6);
            var input = new byte[12 + parameters.Length];
            BitConverter.GetBytes(method).CopyTo(input, 0);
            BitConverter.GetBytes(4 + parameters.Length).CopyTo(input, 4);
            BitConverter.GetBytes(endpoint).CopyTo(input, 8);
            parameters.CopyTo(input, 12);
            var output = new byte[16];
            if (!DeviceIoControl(handle!, 0x0022240C, input, input.Length, output, output.Length, out var returned, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (returned < 4) throw new IOException("ASUS 响应不完整。");
            return output[..returned];
        }
    }
    internal int? Read(uint endpoint)
    {
        var raw = BitConverter.ToUInt32(Call(Dsts, endpoint, new byte[4]));
        return (raw & 0x10000) == 0 ? null : (int)(raw & 0xFFFF);
    }
    public int? ReadPerformanceCapability() => Read(PerformanceMode);
    public byte[] ReadFactoryCpuCurve(int mode) => Call(Dsts, CpuCurve, BitConverter.GetBytes(mode switch { 1 => 2, 2 => 1, _ => 0 }));
    // DSTS(PerformanceMode) is not a reliable readback of the active Armoury Crate policy.
    // The production adapter therefore cannot truthfully certify restore-to-AUTO yet.
    public bool CanVerifyAutomaticRestore => false;
    public string ControlUnavailableReason => !Identity.IsAllowedModel ? "此型号仅支持监控" : !Connected ? "ASUS 驱动不可用"
        : "当前 ASUS 接口无法确认原风扇模式及恢复结果，风扇控制保持只读";
    internal bool WriteCpuCurve(byte[] curve)
    {
        if (!Identity.IsAllowedModel || !CanVerifyAutomaticRestore) return false;
        if (!FanCurveRules.IsValid(curve)) return false;
        return BitConverter.ToInt32(Call(Devs, CpuCurve, curve)) == 1;
    }
    internal bool RestoreMode(int mode)
    {
        if (!Identity.IsAllowedModel || !CanVerifyAutomaticRestore || mode is < 0 or > 2) return false;
        return BitConverter.ToInt32(Call(Devs, PerformanceMode, BitConverter.GetBytes(mode))) == 1;
    }
    public void Dispose() { lock (gate) { handle?.Dispose(); handle = null; } }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle file, uint code, byte[] input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);
}
