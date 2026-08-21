using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MaxLight;

public partial class MainWindow : Window
{
    private WebView2Handler? _webViewHandler;
    private TrayManager? _trayManager;
    private UpdateChecker? _updateChecker;
    private WindowResizeHelper? _resizeHelper;
    private bool _hasUpdate = false;
    private string _updateVersion = "";
    private bool _exitRequested = false;
    private bool _isPortable;
    private int _unreadCount = 0;
    private bool _isRestoring = false;
    private System.Threading.Timer? _saveTimer;

    private IntPtr _handle;
    private HwndSource? _hwndSource;

    private const int BASE_WIDTH = 1050;
    private const int BASE_HEIGHT = 800;

    [DllImport("user32.dll")] private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);
    [DllImport("user32.dll")] private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint FLASHW_STOP = 0;

    [StructLayout(LayoutKind.Sequential)] private struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO { public POINT ptReserved; public POINT ptMaxSize; public POINT ptMaxPosition; public POINT ptMinTrackSize; public POINT ptMaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }

    public MainWindow()
    {
        _isPortable = IsPortableMode();
        InitializeComponent();
        SetVersion();
        this.MinWidth = 640; this.MinHeight = 480;
        this.SourceInitialized += MainWindow_SourceInitialized;
        this.Loaded += MainWindow_Loaded;
        this.Activated += MainWindow_Activated;
        this.Deactivated += MainWindow_Deactivated;
        this.StateChanged += MainWindow_StateChanged;
        this.LocationChanged += MainWindow_LocationChanged;
        this.SizeChanged += MainWindow_SizeChanged;
        this.Closing += MainWindow_Closing;
        CreateTrayIcon();
    }

    private bool IsPortableMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
            return true;

        string exePath = AppDomain.CurrentDomain.BaseDirectory;

        // Проверяем в папке с программой (для обратной совместимости)
        if (File.Exists(Path.Combine(exePath, ".portable")))
            return true;

        // Проверяем на директорию выше (для Velopack)
        string parentPath = Path.GetDirectoryName(exePath) ?? exePath;
        if (File.Exists(Path.Combine(parentPath, ".portable")))
            return true;

        return false;
    }

    private void SetVersion()
    {
        string v = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        lblTitle.Text = $"MaxLight   v{string.Join(".", v.Split('.').Take(3))}";
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _handle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_handle);
        _hwndSource?.AddHook(WndProc);
        _resizeHelper = new WindowResizeHelper(this);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadWindowState();

        // === ЛОГИКА PIN-КОДА ===
        var authData = ConfigManager.GetAuthData();

        if (authData == null || string.IsNullOrEmpty(authData.Token))
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var setupWindow = new PinSetupWindow
                {
                    Owner = this
                };

                var result = setupWindow.ShowDialog();

                if (result == true && setupWindow.PinSet && !string.IsNullOrEmpty(setupWindow.PinCode))
                {
                    ConfigManager.SavePinCode(setupWindow.PinCode);
                    Debug.WriteLine("✅ PIN-код установлен");
                }
                else
                {
                    Debug.WriteLine("⏭️ Пользователь пропустил установку PIN-кода");
                }
            });
        }
        else
        {
            if (ConfigManager.IsPinSet())
            {
                bool pinVerified = false;

                await Dispatcher.InvokeAsync(() =>
                {
                    this.WindowState = WindowState.Minimized;
                    this.ShowInTaskbar = false;

                    var entryWindow = new PinEntryWindow
                    {
                        Owner = this
                    };

                    entryWindow.ShowDialog();
                    pinVerified = entryWindow.IsPinVerified;

                    this.ShowInTaskbar = true;
                    this.WindowState = WindowState.Normal;
                });

                if (!pinVerified)
                {
                    Debug.WriteLine("❌ PIN не подтвержден - закрытие приложения");
                    _exitRequested = true;
                    Application.Current.Shutdown();
                    return;
                }

                Debug.WriteLine("✅ PIN подтвержден");
            }
        }
        // === КОНЕЦ ЛОГИКИ PIN-КОДА ===

        _webViewHandler = new WebView2Handler(webView, this);
        _webViewHandler.NotificationReceived += OnNotificationReceived;
        _webViewHandler.IncomingCallDetected += OnIncomingCall;
        _webViewHandler.ConnectionError += OnConnectionError;
        _webViewHandler.ConnectionRestored += OnConnectionRestored;
        _webViewHandler.UnreadCountChanged += OnUnreadCountChanged;

        
        await _webViewHandler.InitializeAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
                Debug.WriteLine($"❌ Ошибка инициализации: {t.Exception}");
        }, TaskScheduler.Default);

        _webViewHandler.UpdateWindowActiveState(this.IsActive);
        // === ЗАПУСКАЕМ ПРОВЕРКУ ОБНОВЛЕНИЙ ===
        Debug.WriteLine("[MainWindow] Инициализация UpdateChecker...");
        _updateChecker = new UpdateChecker(_isPortable);  
        _updateChecker.UpdateAvailable += OnUpdateAvailable;
        _updateChecker.StartBackgroundChecker();
        Debug.WriteLine("[MainWindow] UpdateChecker успешно инициализирован");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_ACTIVATE = 0x0006, WM_CLOSE = 0x0010, WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var mi = new MONITORINFO(); mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi);
            var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
            mmi.ptMaxSize.x = mi.rcWork.Right - mi.rcWork.Left; mmi.ptMaxSize.y = mi.rcWork.Bottom - mi.rcWork.Top;
            mmi.ptMaxPosition.x = mi.rcWork.Left; mmi.ptMaxPosition.y = mi.rcWork.Top;
            Marshal.StructureToPtr(mmi, lParam, true); handled = true;
        }
        else if (msg == WM_ACTIVATE)
        {
            bool active = (wParam.ToInt32() & 0xFFFF) != 0;
            _webViewHandler?.UpdateWindowActiveState(active);
        }
        else if (msg == WM_CLOSE) { MinimizeToTray(); handled = true; }
        return IntPtr.Zero;
    }

    private bool _suppressFlashing = false;

    private void MainWindow_Activated(object? sender, EventArgs e)

    {
        _webViewHandler?.UpdateWindowActiveState(true);
        _suppressFlashing = true;
        StopFlashing();
        ResetUnreadCount(); // ← Сбрасываем счетчик

        Task.Run(async () =>
        {
            await Task.Delay(2000);
            await Dispatcher.InvokeAsync(() => _suppressFlashing = false);
        });
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        _webViewHandler?.UpdateWindowActiveState(false);
        _webViewHandler?.ExecuteScriptAsync("document.activeElement?.blur(); if(typeof isWindowActive!=='undefined') isWindowActive=false;");
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        btnMaximize.Content = this.WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        ScheduleSaveWindowState();

        if (this.WindowState == WindowState.Minimized)
        {
            _webViewHandler?.UpdateWindowActiveState(false);
            _webViewHandler?.HideWebView();

            _suppressFlashing = true;
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                await Dispatcher.InvokeAsync(() => _suppressFlashing = false);
            });
        }
        else if (this.WindowState == WindowState.Normal)
        {
            _webViewHandler?.ShowWebView(); // ← Показываем WebView2
            _webViewHandler?.UpdateWindowActiveState(true);
            StopFlashing();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (e.ClickCount == 2) ToggleMaximize(); else DragMove(); }

    private void MainWindow_MouseMove(object sender, MouseEventArgs e) { if (_resizeHelper != null) Cursor = _resizeHelper.GetResizeCursor(e.GetPosition(this)); }
    private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (_resizeHelper != null) _resizeHelper.HandleResize(_handle, e.ButtonState, e.GetPosition(this)); }

    private void OnNotificationReceived(string title, string body, string? avatarUrl)
    { FlashWindow(); NotificationWindow.Show(title, body, avatarUrl, userName => { RestoreFromTray(); _webViewHandler?.OpenChatWithUser(userName).ContinueWith(_ => { }); }); }

    private void OnIncomingCall()
    {
        FlashWindow();
        Dispatcher.Invoke(() => { this.Topmost = true; this.Show(); this.WindowState = WindowState.Normal; this.ShowInTaskbar = true; this.Activate(); this.Focus(); _ = Task.Run(async () => { await Task.Delay(2000); Dispatcher.Invoke(() => this.Topmost = false); }); });
        RestoreFromTray();
        NotificationWindow.Show("MaxLight", "📞 Входящий звонок!");
    }

    private void OnConnectionError() => Dispatcher.Invoke(() => { errorPanel.Visibility = Visibility.Visible; webView.Visibility = Visibility.Collapsed; });
    private void OnConnectionRestored() => Dispatcher.Invoke(() => { errorPanel.Visibility = Visibility.Collapsed; webView.Visibility = Visibility.Visible; });
    private async void BtnRetry_Click(object sender, RoutedEventArgs e) { errorPanel.Visibility = Visibility.Collapsed; webView.Visibility = Visibility.Visible; if (_webViewHandler != null) await _webViewHandler.ReloadWebView(); }

    private void OnUnreadCountChanged(int count) { _unreadCount = count; _trayManager?.UpdateUnreadCount(count); }

    private void CreateTrayIcon()
    {
        _trayManager = new TrayManager(this);
        _trayManager.SettingsRequested += () => Dispatcher.Invoke(ShowSettings);
        _trayManager.ExitRequested += () => { _exitRequested = true; _webViewHandler?.Dispose(); _trayManager?.Dispose(); _updateChecker?.Stop(); Environment.Exit(0); };
        _trayManager.WindowToggleRequested += () => Dispatcher.Invoke(ToggleWindow);
    }

    public void MinimizeToTray()
    {
        _trayManager?.MinimizeToTray();
        _webViewHandler?.UpdateWindowActiveState(false);
        _webViewHandler?.HideWebView(); // ← Скрываем WebView2
    }


    public void RestoreFromTray()
    {
        _trayManager?.RestoreFromTray();
        _webViewHandler?.ShowWebView();
    }
    private void ToggleWindow() { if (IsVisible && WindowState != WindowState.Minimized) MinimizeToTray(); else RestoreFromTray(); }

    private void StopFlashing()
    {
        if (_handle != IntPtr.Zero)
        {
            FlashWindow(_handle, false);
            // Дополнительно — полная остановка через FlashWindowEx
            var fInfo = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = _handle,
                dwFlags = FLASHW_STOP,
                uCount = 0,
                dwTimeout = 0
            };
            FlashWindowEx(ref fInfo);
            Debug.WriteLine("🛑 Мигание остановлено");
        }
    }

    public void ResetUnreadCount()
    {
        Debug.WriteLine("📊 ResetUnreadCount вызван");
        _webViewHandler?.ResetUnreadCount();
        _trayManager?.UpdateUnreadCount(0);
        StopFlashing();
    }

    private void OnUpdateAvailable(string version, string releaseNotes)
    {
        Debug.WriteLine($"📢 OnUpdateAvailable: version={version}, notes={releaseNotes}");
        Dispatcher.Invoke(() => ShowUpdateNotification(version));
    }

    private void UpdateNotification_Click(object sender, MouseButtonEventArgs e)
    {
        if (_updateChecker?.HasUpdate == true)
        {
            Debug.WriteLine($"📢 UpdateNotification_Click: version={_updateChecker.UpdateVersion}, notes length={_updateChecker.UpdateReleaseNotes?.Length ?? 0}");

            var d = new UpdateDialog(
                _updateChecker.UpdateVersion,
                _updateChecker.UpdateReleaseNotes ?? "📝 Описание изменений не найдено.",
                _isPortable ? " (Portable)" : "")
            {
                Owner = this
            };

            if (d.ShowDialog() == true && d.UpdateAccepted)
            {
                HideUpdateNotification();
                _ = _updateChecker.DownloadAndInstallUpdateAsync();
            }
        }
    }
    public void ShowUpdateNotification(string v) { _updateVersion = v; _hasUpdate = true; lblUpdateNotification.Text = $"ОБНОВИТЬ ДО {v}"; updateNotification.Visibility = Visibility.Visible; }
    public void HideUpdateNotification() { _hasUpdate = false; updateNotification.Visibility = Visibility.Collapsed; }

    
    private void ScheduleSaveWindowState() { _saveTimer?.Dispose(); _saveTimer = new System.Threading.Timer(_ => Dispatcher.Invoke(SaveWindowState), null, 1000, Timeout.Infinite); }
    private void MainWindow_LocationChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Normal && IsLoaded) ScheduleSaveWindowState(); }
    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e) { if (WindowState == WindowState.Normal && IsLoaded) ScheduleSaveWindowState(); }

    private void ShowSettings()
    {
        var w = new SettingsWindow(_isPortable) { Owner = this };
        w.AutoStartToggled += () => { if (!_isPortable) ConfigManager.SetAutoStart(!ConfigManager.IsAutoStartEnabled()); };
        w.NotificationsOnTopToggled += (t) => NotificationWindow.AlwaysOnTop = t;
        w.ProxySettingsChanged += () => { w.Close(); SaveWindowState(); _webViewHandler?.Dispose(); _trayManager?.Dispose(); if (Environment.ProcessPath is string p) Process.Start(p); Environment.Exit(0); };
        w.DownloadPathChanged += async (_) => { if (_webViewHandler != null) await _webViewHandler.UpdateDownloadFolderPath(); };
        w.LogoutClicked += async () => { ConfigManager.ClearAuthData(); if (_webViewHandler != null) await _webViewHandler.ReinitializeWebView(); };
        w.CheckUpdatesClicked += async () => _updateChecker != null ? await _updateChecker.ForceCheckUpdatesAsync() : false;
        w.AboutClicked += () => new AboutWindow { Owner = this }.ShowDialog();
        w.ShowDialog();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_Click(object sender, RoutedEventArgs e) => MinimizeToTray();
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void SaveWindowState()
    { if (_isRestoring) return; if (WindowState == WindowState.Normal) ConfigManager.SaveWindowState((int)Left, (int)Top, (int)Width, (int)Height, false); else if (WindowState == WindowState.Maximized) { var b = RestoreBounds; ConfigManager.SaveWindowState((int)b.Left, (int)b.Top, (int)b.Width, (int)b.Height, true); } }

    private void LoadWindowState()
    { var s = ConfigManager.GetWindowState(); if (s == null || !ScreenHelper.IsPositionOnScreen(s.Left, s.Top)) return; _isRestoring = true; if (s.Width > 0) Width = s.Width; if (s.Height > 0) Height = s.Height; Left = s.Left; Top = s.Top; if (s.Maximized) WindowState = WindowState.Maximized; _isRestoring = false; }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_exitRequested)
        {
            e.Cancel = true;
            MinimizeToTray();
        }
        else
        {
            _saveTimer?.Dispose();
            SaveWindowState();
            _updateChecker?.Stop();
            _webViewHandler?.Dispose();
            _trayManager?.Dispose();

            // Принудительно убиваем WebView2 процессы перед завершением
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    if (p.Id != currentProcess.Id &&
                        p.StartTime > currentProcess.StartTime &&
                        (p.ProcessName.Contains("WebView2") ||
                         p.ProcessName.Contains("msedge") ||
                         p.ProcessName.Contains("MicrosoftEdge")))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
            }
            catch { }
        }
    }

    private void FlashWindow()
    {
        if (_suppressFlashing) return; //  Не мигаем 2 сек после активации
        if (_handle != IntPtr.Zero) FlashWindow(_handle, true);
    }

    protected override void OnClosed(EventArgs e) { _saveTimer?.Dispose(); _updateChecker?.Stop(); _webViewHandler?.Dispose(); _trayManager?.Dispose(); base.OnClosed(e); }
}