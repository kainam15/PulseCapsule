using System.Windows;
using System.Windows.Controls;
using PulseCapsule.UI;

namespace PulseCapsule.Capsules.Quota;

public partial class QuotaExpandedView : UserControl
{
    private readonly Action openSettings;
    public QuotaExpandedView(Action openSettings) { InitializeComponent(); this.openSettings = openSettings; }
    public void Update(List<CardViewModel> cards)
    {
        Cards.ItemsSource = ProviderCardViewModel.Group(cards);
        EmptyText.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => openSettings();
}
