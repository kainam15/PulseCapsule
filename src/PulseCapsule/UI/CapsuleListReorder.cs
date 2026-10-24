using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PulseCapsule.UI;

// The Thumb owns mouse capture. Only a completed, in-list gesture changes settings.
internal sealed class CapsuleListReorder
{
    private readonly ListBox list;
    private readonly Border indicator;
    private readonly Action<object, int> move;
    private Thumb? handle;
    private object? item;
    private Point origin;
    private int targetIndex = -1;
    private readonly DispatcherTimer edgeScroll = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private Point lastPoint;
    private double scrollStep;

    public CapsuleListReorder(ListBox list, Border indicator, Action<object, int> move)
    {
        this.list = list; this.indicator = indicator; this.move = move;
        edgeScroll.Tick += (_, _) => ScrollAtEdge();
        list.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Started));
        list.AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(Delta));
        list.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Completed));
        list.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || handle is null) return;
            handle.CancelDrag(); Reset(); e.Handled = true;
        };
        list.IsVisibleChanged += (_, e) => { if (!(bool)e.NewValue) { handle?.CancelDrag(); Reset(); } };
    }

    private void Started(object sender, DragStartedEventArgs e)
    {
        if (e.OriginalSource is not Thumb { Tag: "ReorderHandle" } thumb || !list.Items.Contains(thumb.DataContext)) return;
        Reset();
        handle = thumb; item = thumb.DataContext;
        origin = new Point(e.HorizontalOffset, e.VerticalOffset);
        list.SelectedItem = item;
        e.Handled = true;
    }

    private void Delta(object sender, DragDeltaEventArgs e)
    {
        if (handle is null || !ReferenceEquals(e.OriginalSource, handle)) return;
        targetIndex = -1; indicator.Visibility = Visibility.Collapsed;
        edgeScroll.Stop(); scrollStep = 0;
        // Thumb reports displacement from the original point in DIPs, not incremental movement.
        if (Math.Abs(e.HorizontalChange) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(e.VerticalChange) < SystemParameters.MinimumVerticalDragDistance) return;
        var point = handle.TranslatePoint(new Point(origin.X + e.HorizontalChange, origin.Y + e.VerticalChange), list);
        lastPoint = point;
        if (point.X < 0 || point.X > list.ActualWidth || point.Y < 0 || point.Y > list.ActualHeight) return;
        UpdateTarget(point);
        scrollStep = point.Y < 28 ? -16 : point.Y > list.ActualHeight - 28 ? 16 : 0;
        if (scrollStep != 0) edgeScroll.Start();
        e.Handled = true;
    }

    private void UpdateTarget(Point point)
    {
        targetIndex = -1; indicator.Visibility = Visibility.Collapsed;
        var sourceIndex = list.Items.IndexOf(item);
        if (sourceIndex < 0) return;
        var insertion = list.Items.Count;
        double lineY = 0;
        // Midpoints also give stable insertion positions in the gaps between rows.
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem row) return;
            var top = row.TranslatePoint(new Point(), list).Y;
            lineY = top + row.ActualHeight;
            if (point.Y < top + row.ActualHeight / 2)
            {
                insertion = i; lineY = top; break;
            }
        }
        targetIndex = insertion > sourceIndex ? insertion - 1 : insertion;
        if (targetIndex == sourceIndex) return;
        indicator.Margin = new Thickness(2, Math.Clamp(lineY - 1, 0, Math.Max(0, list.ActualHeight - 2)), 2, 0);
        indicator.Visibility = Visibility.Visible;
    }

    private void ScrollAtEdge()
    {
        if (handle is null || scrollStep == 0) return;
        var scroll = FindScroll(list);
        if (scroll is null) return;
        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + scrollStep);
        list.UpdateLayout();
        UpdateTarget(lastPoint);
    }

    private static ScrollViewer? FindScroll(DependencyObject element)
    {
        if (element is ScrollViewer scroll) return scroll;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            if (FindScroll(VisualTreeHelper.GetChild(element, i)) is { } found) return found;
        return null;
    }

    private void Completed(object sender, DragCompletedEventArgs e)
    {
        if (handle is null || !ReferenceEquals(e.OriginalSource, handle)) return;
        var moved = item;
        var destination = targetIndex;
        Reset();
        if (!e.Canceled && moved is not null && destination >= 0 && list.Items.Contains(moved)) move(moved, destination);
        e.Handled = true;
    }

    private void Reset()
    {
        handle = null; item = null; targetIndex = -1;
        edgeScroll.Stop(); scrollStep = 0;
        indicator.Visibility = Visibility.Collapsed;
    }
}
