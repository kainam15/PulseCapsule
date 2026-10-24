using System.Windows;
using System.Windows.Controls;
using PulseCapsule.UI;
using PulseCapsule.Native;
using System.Windows.Controls.Primitives;

namespace PulseCapsule.Capsules.Quota;

public partial class QuotaExpandedView : UserControl
{
    private readonly Action<string> openSettings, disable;
    private readonly Func<string, Task> refresh;
    private readonly Func<string, bool> readDetails;
    private readonly Action<string, bool> saveDetails;
    private bool updating;
    public QuotaExpandedView(Action<string> openSettings, Func<string, Task> refresh, Action<string> disable,
        Func<string, bool> readDetails, Action<string, bool> saveDetails)
    {
        InitializeComponent(); this.openSettings = openSettings; this.refresh = refresh;
        this.disable = disable; this.readDetails = readDetails; this.saveDetails = saveDetails;
    }
    public void Update(List<CardViewModel> cards)
    {
        var groups = ProviderCardViewModel.Group(cards);
        foreach (var service in groups.SelectMany(g => g.Services)) service.DetailsExpanded = readDetails(service.Card.Config.Id);
        updating = true;
        try { Cards.ItemsSource = groups; }
        finally { updating = false; }
        EmptyText.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ServiceSectionViewModel service) openSettings(service.Card.Config.Id);
    }
    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ServiceSectionViewModel service) return;
        try { await refresh(service.Card.Config.Id); ActionStatus.Text = "已请求刷新此项；如仍在限流等待期，将按原计划重试。"; }
        catch { ActionStatus.Text = "刷新失败，请稍后重试。"; }
    }
    private void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ServiceSectionViewModel service) return;
        try { disable(service.Card.Config.Id); }
        catch { ActionStatus.Text = "停用未保存，请检查数据目录权限。"; }
    }
    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var menu = button.ContextMenu;
        menu.DataContext = button.DataContext;
        menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true;
    }
    private void Menu_Opened(object sender, RoutedEventArgs e) => WindowNative.ActivateMenu((ContextMenu)sender);
    private void Details_Changed(object sender, RoutedEventArgs e)
    {
        if (updating || sender is not Expander expander || expander.DataContext is not ServiceSectionViewModel service) return;
        var id = service.Card.Config.Id;
        if (readDetails(id) == expander.IsExpanded) return;
        try { saveDetails(id, expander.IsExpanded); }
        catch
        {
            updating = true;
            try { expander.SetCurrentValue(Expander.IsExpandedProperty, readDetails(id)); }
            finally { updating = false; }
            ActionStatus.Text = "详情显示偏好未保存，已恢复原值。";
        }
    }
}
