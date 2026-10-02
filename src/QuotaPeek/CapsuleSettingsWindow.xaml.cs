using System.Collections.ObjectModel;
using System.Windows;

namespace QuotaPeek;

public partial class CapsuleSettingsWindow : Window
{
    private readonly App app;
    private readonly ObservableCollection<CapsulePreference> items;
    public CapsuleSettingsWindow(App app)
    {
        InitializeComponent(); this.app = app;
        items = new(CapsuleSelection.Normalize(app.Settings.Capsules.Select(p => p with { })));
        CapsuleList.ItemsSource = items; CapsuleList.SelectedIndex = 0;
        RotateCheck.IsChecked = app.Settings.AutoRotate; RotateSeconds.Text = app.Settings.CarouselSeconds.ToString();
        TemperatureCheck.IsChecked = app.Settings.System.Temperature;
        FanRpmCheck.IsChecked = app.Settings.System.FanRpm;
        FanControlCheck.IsChecked = app.Settings.System.FanControl;
    }
    private void Move(int delta)
    {
        var index = CapsuleList.SelectedIndex;
        if (index < 0 || index + delta < 0 || index + delta >= items.Count) return;
        items.Move(index, index + delta); CapsuleList.SelectedIndex = index + delta;
    }
    private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void Down_Click(object sender, RoutedEventArgs e) => Move(1);
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!items.Any(p => p.Enabled)) { StatusText.Text = "至少启用一个 Capsule。"; return; }
        if (!int.TryParse(RotateSeconds.Text, out var seconds) || seconds is < 5 or > 3600) { StatusText.Text = "轮播间隔须在 5–3600 秒之间。"; return; }
        try
        {
            app.SaveSettings(app.Settings with { Capsules = items.Select(p => p with { }).ToList(), AutoRotate = RotateCheck.IsChecked == true, CarouselSeconds = seconds,
                System = new() { Temperature = TemperatureCheck.IsChecked == true, FanRpm = FanRpmCheck.IsChecked == true, FanControl = FanControlCheck.IsChecked == true } });
            StatusText.Text = "已保存，关闭窗口即可继续使用。";
        }
        catch { StatusText.Text = "保存失败，请检查数据目录权限。"; }
    }
}
