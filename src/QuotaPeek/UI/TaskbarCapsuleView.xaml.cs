using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using QuotaPeek.Native;

namespace QuotaPeek.UI;

public partial class TaskbarCapsuleView : UserControl
{
    public event Action? ToggleRequested;
    public event Action? UndockRequested;
    public event Action? SettingsRequested;
    public event Action? RefreshRequested;
    public event Action? ExitRequested;

    public TaskbarCapsuleView() => InitializeComponent();

    public void Update(string name, string value, Brush status, string tooltip, bool clock = false)
    {
        StatusDot.Fill = status;
        if (clock)
        {
            StatusDot.Visibility = Visibility.Collapsed;
            BalanceText.Visibility = Visibility.Collapsed;
            ProviderText.Visibility = Visibility.Visible;
            ProviderText.Text = value;
            Grid.SetColumn(ProviderText, 0);
            Grid.SetColumnSpan(ProviderText, 3);
            ProviderText.HorizontalAlignment = HorizontalAlignment.Center;
            ProviderText.TextAlignment = TextAlignment.Center;
            ProviderText.FontSize = 14;
            TaskbarMenuButton.Visibility = Visibility.Collapsed;
            TaskbarMenuColumn.Width = new GridLength(0);
            TaskbarExpandButton.Padding = new Thickness(0);
        }
        else
        {
            StatusDot.Visibility = Visibility.Visible;
            ProviderText.Visibility = Visibility.Visible;
            BalanceText.Visibility = Visibility.Visible;
            ProviderText.Text = name;
            BalanceText.Text = value;
            Grid.SetColumn(ProviderText, 1);
            Grid.SetColumnSpan(ProviderText, 1);
            ProviderText.HorizontalAlignment = HorizontalAlignment.Stretch;
            ProviderText.TextAlignment = TextAlignment.Left;
            ProviderText.FontSize = 12;
            TaskbarMenuButton.Visibility = Visibility.Visible;
            TaskbarMenuColumn.Width = new GridLength(26);
            TaskbarExpandButton.Padding = new Thickness(6, 0, 6, 0);
        }
        ToolTip = tooltip + "\n滚轮切换时间 / 钱包 / 额度 · 单击展开 / 收起 · 右键打开菜单";
    }

    private void Expand_Click(object sender, RoutedEventArgs e) => ToggleRequested?.Invoke();
    private void Undock_Click(object sender, RoutedEventArgs e) => UndockRequested?.Invoke();
    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke();
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();
    private void ContextMenu_Opened(object sender, RoutedEventArgs e) => WindowNative.ActivateMenu((ContextMenu)sender);
    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var menu = ((Border)Content).ContextMenu;
        menu.PlacementTarget = TaskbarMenuButton;
        menu.Placement = PlacementMode.Top;
        menu.IsOpen = true;
    }
}
