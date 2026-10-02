using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using QuotaPeek.Native;

namespace QuotaPeek.UI;

public partial class TaskbarCapsuleView : UserControl
{
    public event Action<string>? ActionRequested;
    public event Action? ToggleRequested;
    public event Action? UndockRequested;
    public event Action? SettingsRequested;
    public event Action? RefreshRequested;
    public event Action? ExitRequested;

    public TaskbarCapsuleView() => InitializeComponent();

    public void Update(ICapsule capsule, Brush status)
    {
        var style = capsule.Appearance;
        var action = capsule.Actions.FirstOrDefault();
        TaskbarActionButton.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        TaskbarActionButton.Content = action?.Title;
        System.Windows.Automation.AutomationProperties.SetName(TaskbarActionButton, action?.Title ?? "胶囊操作");
        TaskbarActionButton.IsEnabled = action?.Enabled == true;
        TaskbarActionButton.Tag = action?.Id;
        StatusDot.Fill = status;
        if (style.Centered)
        {
            StatusDot.Visibility = Visibility.Collapsed;
            BalanceText.Visibility = Visibility.Collapsed;
            ProviderText.Visibility = Visibility.Visible;
            ProviderText.Text = capsule.PrimaryText;
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
            StatusDot.Visibility = style.ShowStatus ? Visibility.Visible : Visibility.Collapsed;
            ProviderText.Visibility = Visibility.Visible;
            BalanceText.Visibility = Visibility.Visible;
            ProviderText.Text = capsule.PrimaryText;
            BalanceText.Text = capsule.SecondaryText;
            Grid.SetColumn(ProviderText, 1);
            Grid.SetColumnSpan(ProviderText, 1);
            ProviderText.HorizontalAlignment = HorizontalAlignment.Stretch;
            ProviderText.TextAlignment = TextAlignment.Left;
            ProviderText.FontSize = 12;
            TaskbarMenuButton.Visibility = Visibility.Visible;
            TaskbarMenuColumn.Width = new GridLength(26);
            TaskbarExpandButton.Padding = new Thickness(6, 0, 6, 0);
        }
        ProviderText.FontSize = style.DockFontSize;
        if (action is not null) { TaskbarMenuButton.Visibility = Visibility.Collapsed; TaskbarMenuColumn.Width = new GridLength(0); }
        ToolTip = capsule.Tooltip + "\n滚轮切换 Capsule · 单击展开 / 收起 · 右键打开菜单";
    }

    private void Action_Click(object sender, RoutedEventArgs e) { if (TaskbarActionButton.Tag is string id) ActionRequested?.Invoke(id); }
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
