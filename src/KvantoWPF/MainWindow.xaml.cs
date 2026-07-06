using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using KvantoWPF.Models;
using KvantoWPF.ViewModels;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace KvantoWPF;

public partial class MainWindow : Window
{
    private const int WmHotKey = 0x0312;
    private const int ToggleHotKeyId = 1001;
    private const int SkipHotKeyId = 1002;
    private const int RestoreHotKeyId = 1003;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private readonly MainViewModel _viewModel;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayIconResources _trayIcons;
    private CompactWindow? _compactWindow;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.NotificationRequested += OnNotificationRequested;
        _viewModel.CompactViewRequested += (_, _) => ShowCompactWindow();

        DataContext = _viewModel;
        _trayIcons = new TrayIconResources();

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Kvanto"
        };
        _notifyIcon.DoubleClick += (_, _) => RestoreWindow();
        _notifyIcon.ContextMenuStrip = BuildTrayMenu();
        UpdateTrayIcon();

        Closing += OnWindowClosing;
        StateChanged += OnWindowStateChanged;
        Closed += OnWindowClosed;
        SourceInitialized += OnWindowSourceInitialized;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open app", null, (_, _) => RestoreWindow());
        menu.Items.Add("Start / Pause", null, (_, _) => _viewModel.StartPauseCommand.Execute(null));
        menu.Items.Add("Stop", null, (_, _) => _viewModel.StopCommand.Execute(null));
        menu.Items.Add("Open compact view", null, (_, _) => ShowCompactWindow());
        menu.Items.Add("Skip to next", null, (_, _) => _viewModel.SkipCommand.Execute(null));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);

        RegisterHotKey(helper.Handle, ToggleHotKeyId, ModControl | ModAlt, (uint)Keys.S);
        RegisterHotKey(helper.Handle, SkipHotKeyId, ModControl | ModAlt, (uint)Keys.N);
        RegisterHotKey(helper.Handle, RestoreHotKeyId, ModControl | ModAlt, (uint)Keys.K);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotKey)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case ToggleHotKeyId:
                _viewModel.StartPauseCommand.Execute(null);
                handled = true;
                break;
            case SkipHotKeyId:
                _viewModel.SkipCommand.Execute(null);
                handled = true;
                break;
            case RestoreHotKeyId:
                RestoreWindow();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested)
        {
            return;
        }

        e.Cancel = true;
        Hide();
        _notifyIcon.ShowBalloonTip(2000, "Kvanto", "Kvanto is still running in the system tray.", ToolTipIcon.Info);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcons.Dispose();

        var handle = new WindowInteropHelper(this).Handle;
        UnregisterHotKey(handle, ToggleHotKeyId);
        UnregisterHotKey(handle, SkipHotKeyId);
        UnregisterHotKey(handle, RestoreHotKeyId);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void ShowCompactWindow()
    {
        _compactWindow ??= CreateCompactWindow();
        _compactWindow.Topmost = _viewModel.IsCompactPinned;
        _compactWindow.Show();
        _compactWindow.Activate();
    }

    private CompactWindow CreateCompactWindow()
    {
        var compactWindow = new CompactWindow
        {
            DataContext = _viewModel,
            Topmost = _viewModel.IsCompactPinned
        };
        compactWindow.Closing += (_, args) =>
        {
            if (_exitRequested)
            {
                return;
            }

            args.Cancel = true;
            compactWindow.Hide();
        };
        return compactWindow;
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        _compactWindow?.Close();
        Close();
        Application.Current.Shutdown();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentPhase) or nameof(MainViewModel.IsCompactPinned))
        {
            UpdateTrayIcon();
            if (_compactWindow is not null)
            {
                _compactWindow.Topmost = _viewModel.IsCompactPinned;
            }
        }
    }

    private void OnNotificationRequested(object? sender, NotificationRequestEventArgs e)
    {
        _notifyIcon.ShowBalloonTip(2500, e.Title, e.Message, ToolTipIcon.Info);

        if (e.Action == NotificationAction.StartNextPomodoro)
        {
            var startNext = MessageBox.Show(
                $"{e.Message}\n\nStart the next Pomodoro now?",
                e.Title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (startNext == MessageBoxResult.Yes)
            {
                _viewModel.StartNextPomodoroFromNotification();
            }
        }
    }

    private void UpdateTrayIcon()
    {
        _notifyIcon.Icon = _viewModel.CurrentPhase switch
        {
            PomodoroPhase.Work => _trayIcons.WorkIcon,
            PomodoroPhase.ShortBreak or PomodoroPhase.LongBreak => _trayIcons.BreakIcon,
            _ => _trayIcons.IdleIcon
        };
    }

    private static Icon CreateIcon(Color color, out IntPtr iconHandle)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        using var pen = new Pen(Color.White, 2f);
        graphics.FillEllipse(brush, 2, 2, 28, 28);
        graphics.DrawEllipse(pen, 2, 2, 28, 28);
        iconHandle = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(iconHandle);
    }

    private sealed class TrayIconResources : IDisposable
    {
        private readonly IntPtr _idleHandle;
        private readonly IntPtr _workHandle;
        private readonly IntPtr _breakHandle;

        public TrayIconResources()
        {
            IdleIcon = CreateIcon(Color.FromArgb(107, 114, 128), out _idleHandle);
            WorkIcon = CreateIcon(Color.FromArgb(239, 68, 68), out _workHandle);
            BreakIcon = CreateIcon(Color.FromArgb(16, 185, 129), out _breakHandle);
        }

        public Icon IdleIcon { get; }

        public Icon WorkIcon { get; }

        public Icon BreakIcon { get; }

        public void Dispose()
        {
            IdleIcon.Dispose();
            WorkIcon.Dispose();
            BreakIcon.Dispose();

            DestroyIcon(_idleHandle);
            DestroyIcon(_workHandle);
            DestroyIcon(_breakHandle);
        }
    }
}
