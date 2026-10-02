using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuotaPeek.Capsules.SystemInfo;

public partial class SystemExpandedView : UserControl
{
    private readonly Func<Task> toggle;
    public SystemExpandedView(Func<Task> toggle) { InitializeComponent(); this.toggle = toggle; }
    public void Update(SystemSnapshot sample, ThermalHistory history, FanController controller, string diagnostics)
    {
        TemperatureText.Text = sample.CpuTemperature.Value is { } temperature ? $"{temperature:F0}°C" : "unavailable";
        LoadText.Text = sample.CpuLoad is { } load ? $"{load:F0}%" : "—";
        FansText.Text = string.Join("\n", sample.Fans.Where(f => f.Speed.State != SensorState.Unsupported).Select(FormatFan));
        var points = history.Samples.Where(s => s.CpuTemperature.Value.HasValue).ToArray();
        var max = points.Length == 0 ? 100 : points.Max(p => p.CpuTemperature.Value!.Value);
        var min = points.Length == 0 ? 0 : points.Min(p => p.CpuTemperature.Value!.Value);
        TemperatureLine.Points = new PointCollection(points.Select(p => new Point(Math.Clamp(280 * (1 - (sample.Time - p.Time).TotalSeconds / 120), 0, 280), 55 - 50 * (p.CpuTemperature.Value!.Value - min) / Math.Max(5, max - min))));
        ChartLabel.Text = $"最近 2 分钟 · {min:F0}–{max:F0}°C";
        var summary = history.Summary(sample.Time);
        CoolingText.Text = controller.State == CoolingState.Cooling ? "ON" : controller.State == CoolingState.RecoveryRequired ? "需要恢复" : "OFF";
        static string Temp(double? value) => value.HasValue ? $"{value:F1}°C" : "—";
        var change = summary.Delta is { } delta ? $"{(delta >= 0 ? "↓" : "↑")}{Math.Abs(delta):F1}°C" : "—";
        StatisticsText.Text = $"Before     {Temp(summary.Baseline)}\nCurrent    {Temp(summary.CurrentAverage)}\nChange     {change}\nMinimum    {Temp(summary.Minimum)}\nTime          {summary.Duration:hh\\:mm\\:ss}";
        ControlMessage.Text = controller.Message + (controller.State == CoolingState.Cooling && summary.Delta is null
            ? summary.Baseline is null ? "\n开启前有效采样不足 10 秒，暂不计算温差。" : "\n等待有效采样；负载变化超过 10 个百分点时不归因温降。" : "");
        CoolingButton.Content = controller.State is CoolingState.Cooling or CoolingState.RecoveryRequired ? "恢复自动" : "开启 Balanced Cooling";
        CoolingButton.IsEnabled = controller.CanToggle;
        DiagnosticsText.Text = diagnostics;
    }
    public static string FormatFan(FanReading fan) => fan.Name + "    " + (fan.Speed.State switch
    {
        SensorState.Stopped => "0 RPM · stopped",
        SensorState.Available => $"{fan.Speed.Value:F0} RPM",
        SensorState.ReadFailed => "读取失败",
        _ => "不可用"
    });
    private async void Cooling_Click(object sender, RoutedEventArgs e)
    {
        CoolingButton.IsEnabled = false;
        try { await toggle(); }
        catch { ControlMessage.Text = "散热操作失败，请检查当前风扇状态。"; }
    }
}
