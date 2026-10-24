using System.Windows;
using System.Windows.Controls;

namespace PulseCapsule.UI;

// Attached feedback keeps validation beside its input, including inside a scrolled form.
public static class FieldFeedback
{
    public static readonly DependencyProperty ErrorProperty = DependencyProperty.RegisterAttached(
        "Error", typeof(string), typeof(FieldFeedback), new PropertyMetadata(""));
    public static string GetError(DependencyObject element) => (string)element.GetValue(ErrorProperty);
    public static void SetError(DependencyObject element, string value) => element.SetValue(ErrorProperty, value);
    public static ProviderException Invalid(Control field, string message)
    {
        SetError(field, message);
        for (DependencyObject? parent = field; parent is not null; parent = LogicalTreeHelper.GetParent(parent))
            if (parent is Expander expander) expander.IsExpanded = true;
        field.BringIntoView();
        field.Focus();
        if (field is TextBox text) text.SelectAll();
        return new ProviderException(message);
    }
}
