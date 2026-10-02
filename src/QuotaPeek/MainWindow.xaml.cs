using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using QuotaPeek.Native;
using QuotaPeek.UI;
using Forms = System.Windows.Forms;

namespace QuotaPeek;

public partial class MainWindow : Window
{
    private readonly App app;
    private readonly Icon trayIcon;
    private readonly Forms.NotifyIcon tray;
    private readonly TaskbarCapsuleHost taskbar = new();
    private readonly OutsideClickMonitor outsideClicks;
    private readonly Forms.ToolStripMenuItem taskbarMenu;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool expanded = true, locked, userHidden, fullscreenHidden, sessionLocked, sleeping, exiting;
    private (int X, int Y)? dragFrom;
    private (double X, double Y) dragWindow;
    private bool dragMoved, dragExpands;
    private string unlock = "托盘菜单";
    private bool rendered, positioned, loaded, dockMode;
    private int capsuleWheelDelta;

    public MainWindow(App app)
    {
        InitializeComponent();
        this.app = app;
        outsideClicks = new(Dispatcher, DismissExpandedPanel);
        IsVisibleChanged += (_, _) => UpdateOutsideClickMonitor();
        trayIcon = LoadTrayIcon();
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "QuotaPeek", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏", null, (_, _) => Dispatcher.Invoke(ToggleVisible));
        menu.Items.Add("立即刷新", null, (_, _) => Dispatcher.InvokeAsync(async () => await app.Host.Refresh(true)));
        menu.Items.Add("设置", null, (_, _) => Dispatcher.Invoke(app.OpenSettings));
        menu.Items.Add("锁定 / 解锁穿透", null, (_, _) => Dispatcher.Invoke(ToggleLock));
        taskbarMenu = new Forms.ToolStripMenuItem("嵌入左下角任务栏", null, (_, _) => Dispatcher.Invoke(() => SetTaskbarDocked(!dockMode)));
        menu.Items.Add(taskbarMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出 QuotaPeek", null, (_, _) => Dispatcher.Invoke(Exit));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { userHidden = false; SetExpanded(true); UpdateVisibility(); });
        taskbar.View.ToggleRequested += () => SetExpanded(!expanded);
        taskbar.View.PreviewMouseWheel += (_, e) => CycleCapsule(e);
        taskbar.View.MouseLeave += Capsule_MouseLeave;
        taskbar.View.UndockRequested += () => SetTaskbarDocked(false);
        taskbar.View.SettingsRequested += app.OpenSettings;
        taskbar.View.RefreshRequested += async () => await app.Host.Refresh(true);
        taskbar.View.ExitRequested += Exit;
        taskbar.PlacementChanged += UpdateVisibility;
        app.Host.Changed += Render;
        app.Host.Notification += Notify;
        taskbar.View.ActionRequested += async id => await app.Host.RunAction(id);
        SourceInitialized += (_, _) =>
        {
            WindowNative.Configure(this, false);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(Hook);
            unlock = WindowNative.RegisterUnlock(this);
            LockButton.ToolTip = "锁定穿透 · " + unlock + " 解锁";
            System.Windows.Automation.AutomationProperties.SetHelpText(LockButton, unlock);
        };
        Loaded += async (_, _) =>
        {
            if (loaded) return;
            loaded = true;
            CardsScroll.MaxHeight = Math.Max(160, Math.Min(560, SystemParameters.WorkArea.Height - 150));
            SetExpanded(!app.Settings.TaskbarDocked && app.Settings.StartExpanded, false);
            Render(); UpdateLayout();
            timer.Start();
            await app.Host.Refresh();
            CaptureRequested();
        };
        ContentRendered += (_, _) =>
        {
            if (positioned) return;
            // Let WPF commit the initial capsule size before native positioning;
            // otherwise WM_WINDOWPOSCHANGED can restore the old expanded width.
            // Show() can overwrite a Width change made by the first Loaded event.
            SetExpanded(expanded, false);
            UpdateLayout();
            positioned = true;
            WindowNative.Position(this, app.Settings.LeftPixels, app.Settings.TopPixels);
            ApplyDockMode();
            if (!dockMode) SavePosition();
            UpdateOutsideClickMonitor();
        };
        timer.Tick += (_, _) =>
        {
            if (!sessionLocked && !sleeping)
            {
                CheckFullscreen();
                Render();
            }
        };
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Closing += OnClosing;
        if (app.Demo) Subtitle.Text = "演示模式 · 非真实账户数据";
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e) => WindowNative.ActivateMenu((ContextMenu)sender);

    private void Render()
    {
        if (exiting) return;
        if (positioned) ApplyDockMode();
        var previous = positioned && !dockMode ? WindowNative.PixelPosition(this) : ((double X, double Y)?)null;
        CapsuleContent.Content = app.Host.Current?.ExpandedContent;
        RenderCapsule();
        var current = app.Host.Current;
        var tooltip = current?.Tooltip ?? "QuotaPeek";
        tray.Text = tooltip.Length > 120 ? tooltip[..120] : tooltip.Length == 0 ? "QuotaPeek" : tooltip;
        Subtitle.Text = current?.Title ?? "Capsules";
        FooterText.Text = locked ? "已锁定 · " + unlock + " 解锁" : current?.Footer ?? "在设置中启用 Capsule";
        RefreshButton.IsEnabled = current?.Status != CapsuleStatus.Busy;
        if (previous is { } position) { UpdateLayout(); WindowNative.Position(this, position.X, position.Y); }
        if (dockMode) UpdateVisibility();
    }
    private void DismissExpandedPanel()
    {
        if (!expanded || !IsVisible || locked || exiting || dragFrom is not null) return;
        // Hiding an owner also hides its settings window. Keep that editing session visible.
        if (OwnedWindows.Cast<Window>().Any(window => window.IsVisible)) return;
        SetExpanded(false);
    }
    private void UpdateOutsideClickMonitor()
    {
        try { outsideClicks.SetEnabled(positioned && expanded && IsVisible && !locked && !exiting); }
        catch (Win32Exception) { FooterText.Text = "暂时无法监听外部点击，请从托盘隐藏面板"; }
    }

    private void RenderCapsule()
    {
        var current = app.Host.Current;
        if (current is null) return;
        var style = current.Appearance;
        var color = (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(current.StatusColor)!;
        CapsuleText.Text = current.PrimaryText + (string.IsNullOrEmpty(current.SecondaryText) ? "" : "  " + current.SecondaryText);
        CapsuleText.FontSize = style.FontSize;
        CapsuleDot.Fill = color;
        CapsuleDot.Visibility = style.ShowStatus ? Visibility.Visible : Visibility.Collapsed;
        var action = current.Actions.FirstOrDefault();
        CapsuleGrip.Visibility = !style.Centered && action is null ? Visibility.Visible : Visibility.Collapsed;
        CapsuleActionButton.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        CapsuleActionButton.Content = action?.Title;
        CapsuleActionButton.IsEnabled = action?.Enabled == true;
        CapsuleActionButton.Tag = action?.Id;
        Grid.SetColumn(ExpandButton, style.Centered ? 0 : 1);
        Grid.SetColumnSpan(ExpandButton, style.Centered && action is null ? 3 : 1);
        ExpandButton.HorizontalContentAlignment = style.Centered ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        CapsuleText.TextAlignment = style.Centered ? TextAlignment.Center : TextAlignment.Left;
        CapsuleText.HorizontalAlignment = style.Centered ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        Capsule.ToolTip = current.Tooltip + "\n滚轮切换 Capsule · 单击展开 · " + (dockMode ? "右键切换显示方式" : "按住拖动");
        taskbar.View.Update(current, color);
    }

    private async void CapsuleAction_Click(object sender, RoutedEventArgs e)
    {
        if (CapsuleActionButton.Tag is string id) await app.Host.RunAction(id);
    }

    private void Capsule_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!expanded) CycleCapsule(e);
    }

    private void Capsule_MouseLeave(object sender, MouseEventArgs e) => capsuleWheelDelta = 0;

    private void CycleCapsule(MouseWheelEventArgs e)
    {
        var itemCount = app.Host.Capsules.Count;
        if (locked || dragFrom is not null || itemCount < 2 || e.Delta == 0) return;
        e.Handled = true;
        // Precision wheels can send fractions of a notch. Never jump for each tiny delta.
        if (Math.Sign(capsuleWheelDelta) != Math.Sign(e.Delta)) capsuleWheelDelta = 0;
        capsuleWheelDelta += e.Delta;
        var steps = capsuleWheelDelta / Mouse.MouseWheelDeltaForOneLine;
        capsuleWheelDelta %= Mouse.MouseWheelDeltaForOneLine;
        if (steps == 0) return;
        app.Host.Cycle(steps);
    }

    private void Notify(string title, string message) => tray.ShowBalloonTip(6000, title, message, Forms.ToolTipIcon.Warning);
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        if (message == 0x11 || message == 0x16 && wparam != IntPtr.Zero || (message == 0x218 && wparam.ToInt32() == 4)) app.Host.Pause();
        if (message == 0x16 && wparam == IntPtr.Zero) _ = app.Host.Resume();
        if (message == 0x21) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        if (message == WindowNative.WmHotkey && wparam.ToInt32() == WindowNative.HotkeyId)
        {
            locked = false; WindowNative.Configure(this, false); taskbar.SetLocked(false); userHidden = false; SetExpanded(true); UpdateVisibility(); Render(); handled = true;
        }
        if (message == 0x2E0 && positioned) Dispatcher.BeginInvoke(() => { if (dockMode) UpdateVisibility(); else { var p = WindowNative.PixelPosition(this); WindowNative.Position(this, p.X, p.Y); } });
        return IntPtr.Zero;
    }
    private void SetExpanded(bool value, bool remember = true)
    {
        if (value && app.Host.Current?.CanExpand == false) return;
        if (locked && value) return;
        FinishDrag();
        expanded = value;
        capsuleWheelDelta = 0;
        void Resize() { Width = value ? 370 : 252; Expanded.Visibility = value ? Visibility.Visible : Visibility.Collapsed; Capsule.Visibility = value ? Visibility.Collapsed : Visibility.Visible; }
        if (positioned && !dockMode) WindowNative.ResizeAnchored(this, Resize); else { Resize(); UpdateLayout(); }
        if (remember && !dockMode)
        {
            app.Settings.StartExpanded = value;
            SavePosition();
        }
        if (dockMode) UpdateVisibility();
        UpdateOutsideClickMonitor();
    }
    private void ToggleLock()
    {
        FinishDrag();
        locked = !locked;
        if (locked) SetExpanded(false, false);
        WindowNative.Configure(this, locked);
        taskbar.SetLocked(locked);
        Render();
    }
    private void ToggleVisible() { userHidden = !userHidden; CheckFullscreen(); UpdateVisibility(); }
    private void CheckFullscreen()
    {
        var hide = app.Settings.AutoHideFullscreen && WindowNative.FullscreenApp();
        if (hide != fullscreenHidden) { fullscreenHidden = hide; UpdateVisibility(); }
    }

    private void SetTaskbarDocked(bool value)
    {
        app.Settings.TaskbarDocked = value;
        ApplyDockMode();
        try { app.Store.Save(app.Settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { FooterText.Text = "显示方式未保存：数据目录不可写"; }
    }

    private void ApplyDockMode()
    {
        var value = app.Settings.TaskbarDocked;
        taskbarMenu.Checked = TaskbarDockMenu.IsChecked = value;
        if (dockMode == value) return;
        FinishDrag();
        if (value) SavePosition();
        dockMode = value;
        RenderCapsule();
        taskbar.SetEnabled(value);
        SetExpanded(!value && app.Settings.StartExpanded, false);
        if (!value) WindowNative.Position(this, app.Settings.LeftPixels, app.Settings.TopPixels);
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (exiting || !positioned) return;
        var hidden = userHidden || fullscreenHidden || dockMode && taskbar.TaskbarHidden;
        taskbar.SetVisible(!hidden);
        if (hidden || dockMode && taskbar.IsAttached && !expanded) { Hide(); return; }
        if (!IsVisible) Show();
        if (!dockMode) return;
        UpdateLayout();
        var area = Forms.Screen.PrimaryScreen!.WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var anchor = taskbar.Bounds;
        var x = anchor?.Left ?? area.Left + (int)(8 * dpi.DpiScaleX);
        var y = (anchor?.Top ?? area.Bottom) - ActualHeight * dpi.DpiScaleY - 4 * dpi.DpiScaleY;
        WindowNative.Position(this, x, y);
        if (taskbar.Warning is { } warning) FooterText.Text = warning;
    }
    private void SessionSwitch(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(async () =>
    {
        if (e.Reason == SessionSwitchReason.SessionLock) sessionLocked = true;
        if (e.Reason == SessionSwitchReason.SessionUnlock) sessionLocked = false;
        if (sessionLocked || sleeping) app.Host.Pause(); else await app.Host.Resume();
    });
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(async () =>
    {
        if (e.Mode == PowerModes.Suspend) sleeping = true;
        if (e.Mode == PowerModes.Resume) sleeping = false;
        if (sessionLocked || sleeping) app.Host.Pause(); else await app.Host.Resume();
    });
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (!positioned) return;
        if (dockMode) { _ = taskbar.RefreshAsync(); UpdateVisibility(); return; }
        var p = WindowNative.PixelPosition(this); WindowNative.Position(this, p.X, p.Y);
    });
    private void SavePosition()
    {
        if (!positioned || dockMode) return;
        var p = WindowNative.PixelPosition(this);
        app.Settings.LeftPixels = p.X; app.Settings.TopPixels = p.Y;
        try { app.Store.Save(app.Settings); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { FooterText.Text = "窗口位置未保存：数据目录不可写"; }
    }
    private void DragStart(object sender, MouseButtonEventArgs e)
    {
        if (locked || dockMode || dragFrom is not null) return;
        var control = FindDragControl(e.OriginalSource as DependencyObject);
        if (control is not null && control != ExpandButton) return;
        dragFrom = WindowNative.Cursor(); dragWindow = WindowNative.PixelPosition(this);
        dragMoved = false; dragExpands = !expanded;
        // Capture on the stable outer surface before ButtonBase consumes the press.
        if (!Outer.CaptureMouse()) { FinishDrag(); return; }
        e.Handled = true;
    }
    private static DependencyObject? FindDragControl(DependencyObject? d)
    {
        while (d is not null)
        {
            if (d is ButtonBase or ScrollBar or Thumb) return d;
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
    private void DragMove(object sender, MouseEventArgs e)
    {
        if (dragFrom is not { } from) return;
        // WPF can deliver a queued move after the physical release, before MouseUp.
        // Keep the pending click until MouseUp (or LostMouseCapture) completes it.
        if (e.LeftButton != MouseButtonState.Pressed) return;
        e.Handled = true;
        var cursor = WindowNative.Cursor();
        var dx = cursor.X - from.X; var dy = cursor.Y - from.Y;
        var dpi = VisualTreeHelper.GetDpi(this);
        if (!dragMoved && Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance * dpi.DpiScaleX
            && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance * dpi.DpiScaleY) return;
        dragMoved = true;
        WindowNative.Position(this, dragWindow.X + dx, dragWindow.Y + dy);
    }
    private void DragEnd(object sender, MouseButtonEventArgs e)
    {
        if (dragFrom is null) return;
        var expandOnClick = dragExpands && !dragMoved && Outer.InputHitTest(e.GetPosition(Outer)) is not null;
        e.Handled = true;
        FinishDrag();
        if (expandOnClick) SetExpanded(true);
    }
    private void DragLostCapture(object sender, MouseEventArgs e)
    {
        if (dragFrom is not null && !Outer.IsMouseCaptured) FinishDrag();
    }
    private void FinishDrag()
    {
        if (dragFrom is null) return;
        var moved = dragMoved;
        dragFrom = null; dragMoved = false; dragExpands = false;
        if (Outer.IsMouseCaptured) Outer.ReleaseMouseCapture();
        if (moved) SavePosition();
    }
    private void Expand_Click(object sender, RoutedEventArgs e) => SetExpanded(true);
    private void Lock_Click(object sender, RoutedEventArgs e) => ToggleLock();
    private void Settings_Click(object sender, RoutedEventArgs e) => app.OpenSettings();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await app.Host.Refresh(true);
    private void Exit_Click(object sender, RoutedEventArgs e) => Exit();
    private void TaskbarDock_Click(object sender, RoutedEventArgs e) => SetTaskbarDocked(TaskbarDockMenu.IsChecked);
    private void Exit() { exiting = true; app.Shutdown(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        outsideClicks.Dispose();
        FinishDrag();
        SavePosition(); timer.Stop(); taskbar.Dispose(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose();
        WindowNative.Unregister(this);
        SystemEvents.SessionSwitch -= SessionSwitch; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        app.Host.Changed -= Render; app.Host.Notification -= Notify;
        if (!exiting) { exiting = true; app.Shutdown(); }
    }
    private static Icon LoadTrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/QuotaPeek.ico")).Stream;
        return new Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
    private void CaptureRequested()
    {
        if (rendered || app.RenderPath is not { } path) return;
        rendered = true;
        Dispatcher.InvokeAsync(() =>
        {
            UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(this);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream = File.Create(path); encoder.Save(stream);
        }, DispatcherPriority.ContextIdle);
    }
}
