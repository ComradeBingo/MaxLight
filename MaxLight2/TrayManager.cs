using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Hardcodet.Wpf.TaskbarNotification;

namespace MaxLight;

public class TrayManager : IDisposable
{
    private readonly Window _mainWindow;
    private TaskbarIcon? _trayIcon;
    private ImageSource? _normalIconSource;
    private ImageSource? _unreadIconSource;
    private bool _exitRequested;
    private int _currentUnreadCount = 0;
    private int _lastRenderedCount = -1;

    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action? WindowToggleRequested;

    public TrayManager(Window mainWindow)
    {
        _mainWindow = mainWindow;
        LoadIcons();
        CreateTrayIcon();
    }

    private void LoadIcons()
    {
        string appPath = AppDomain.CurrentDomain.BaseDirectory;


        _normalIconSource = IconFileToImageSource(Path.Combine(appPath, "app.ico"));
        _unreadIconSource = IconFileToImageSource(Path.Combine(appPath, "app_unread.ico")) ?? CreateUnreadIcon();


    }

    private ImageSource? IconFileToImageSource(string path)
    {
        if (!File.Exists(path)) return null;

        try
        {
            using var icon = new System.Drawing.Icon(path);
            return IconToImageSource(icon);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки иконки: {ex.Message}");
            return null;
        }
    }

    private ImageSource IconToImageSource(System.Drawing.Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using var ms = new MemoryStream();
        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private ImageSource CreateUnreadIcon()
    {
        if (_normalIconSource == null)
        {
            var visual = new DrawingVisual();
            using var ctx = visual.RenderOpen();
            ctx.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(231, 76, 60)), null,
                new System.Windows.Point(16, 16), 14, 14);
            var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)_normalIconSource));

            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Seek(0, SeekOrigin.Begin);

            using var normalBitmap = new System.Drawing.Bitmap(ms);
            using var g = System.Drawing.Graphics.FromImage(normalBitmap);

            int dotSize = normalBitmap.Width / 3;
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
            g.FillEllipse(brush, normalBitmap.Width - dotSize, 0, dotSize, dotSize);

            using var resultMs = new MemoryStream();
            normalBitmap.Save(resultMs, System.Drawing.Imaging.ImageFormat.Png);
            resultMs.Seek(0, SeekOrigin.Begin);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = resultMs;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return _normalIconSource;
        }
    }

    private void CreateTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            IconSource = _normalIconSource,
            ToolTipText = "Max Light"
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => WindowToggleRequested?.Invoke();

        var menu = new System.Windows.Controls.ContextMenu();

        var toggleItem = new System.Windows.Controls.MenuItem { Header = "Открыть/Свернуть" };
        toggleItem.Click += (_, _) => WindowToggleRequested?.Invoke();
        menu.Items.Add(toggleItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Настройки" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Закрыть MaxLight" };
        exitItem.Click += (_, _) =>
        {
            _exitRequested = true;
            ExitRequested?.Invoke();
        };
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
    }

    public void UpdateTrayIcon(bool hasUnread)
    {
        if (_trayIcon == null) return;

        _trayIcon.IconSource = hasUnread ? _unreadIconSource : _normalIconSource;
        _trayIcon.ToolTipText = hasUnread ? "Max Light — есть новые сообщения" : "Max Light";
    }

    public void UpdateTaskbarBadge(int count)
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            // Если окно скрыто с панели задач — бейдж не обновляем
            if (!_mainWindow.ShowInTaskbar) return;

            // Если цифра не поменялась — не перерисовываем
            if (_lastRenderedCount == count) return;
            _lastRenderedCount = count;

            if (_mainWindow is MainWindow mainWindow && mainWindow.AppTaskbarInfo != null)
            {
                mainWindow.AppTaskbarInfo.Overlay = count > 0 ? CreateBadgeImage(count) : null;
                mainWindow.AppTaskbarInfo.Description = count > 0 ? $"{count} непрочитанных" : "";
            }
        });
    }

    private ImageSource CreateBadgeImage(int count)
    {
        string text = count > 99 ? "99+" : count.ToString();
        int size = 16;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var brush = new SolidColorBrush(Color.FromRgb(232, 17, 35));
            dc.DrawEllipse(brush, null, new System.Windows.Point(size / 2.0, size / 2.0), size / 2.0, size / 2.0);

            double fontSize = text.Length switch { 1 => 11, 2 => 9, _ => 7 };
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                fontSize,
                System.Windows.Media.Brushes.White,
                1.0);

            double textX = (size - ft.Width) / 2;
            double textY = (size - ft.Height) / 2;
            dc.DrawText(ft, new System.Windows.Point(textX, textY));
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    public void UpdateUnreadCount(int count)
    {
        _currentUnreadCount = count;
        if (_mainWindow.ShowInTaskbar) UpdateTaskbarBadge(count);
        UpdateTrayIcon(count > 0);
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.Title = count > 0 ? $"MaxLight ({count})" : "MaxLight");
        Debug.WriteLine($"📊 Счетчик: {count}");
    }

    public void MinimizeToTray()
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            try
            {
                // Передаём фокус окну перед скрытием, чтобы WebView2 не пытался писать в кэш
                _mainWindow.Focus();
                _mainWindow.Hide();
                _mainWindow.ShowInTaskbar = false;
            }
            catch (System.IO.IOException ex)
            {
                Debug.WriteLine($"⚠️ IOException при сворачивании в трей: {ex.Message}");
            }
        });
    }

    public void RestoreFromTray()
    {
        _mainWindow.Dispatcher.Invoke(() =>
        {
            try
            {
                // Сначала показываем окно
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.ShowInTaskbar = true;

                // Сразу убираем фокус — окно видно, но не в фокусе
                _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var shellWindow = MainWindow.GetShellWindow();
                    if (shellWindow != IntPtr.Zero) MainWindow.SetForegroundWindow(shellWindow);
                }), System.Windows.Threading.DispatcherPriority.Background);

                _lastRenderedCount = -1;
                UpdateTaskbarBadge(_currentUnreadCount);
            }
            catch (System.IO.IOException ex)
            {
                Debug.WriteLine($"⚠️ IOException: {ex.Message}");
            }
        });
    }

    public bool IsExitRequested => _exitRequested;

    public void RequestExit()
    {
        _exitRequested = true;
        ExitRequested?.Invoke();
    }

    // === ПРИНУДИТЕЛЬНОЕ ЗАВЕРШЕНИЕ ПРИ УДАЛЕНИИ ===
    public void Dispose()
    {
        // Убиваем все дочерние WebView2 процессы
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var allProcesses = Process.GetProcesses();

            foreach (var p in allProcesses)
            {
                try
                {
                    // Проверяем, является ли процесс дочерним
                    if (p.Id != currentProcess.Id &&
                        p.StartTime > currentProcess.StartTime &&
                        (p.ProcessName.Contains("WebView2") ||
                         p.ProcessName.Contains("msedge") ||
                         p.ProcessName.Contains("MicrosoftEdge")))
                    {
                        Debug.WriteLine($"🔚 TrayManager: Завершаем процесс: {p.ProcessName} (PID: {p.Id})");
                        try
                        {
                            p.Kill();
                            p.WaitForExit(2000);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"⚠️ Ошибка убийства процесса {p.ProcessName}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ Ошибка обработки процесса: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Ошибка при завершении процессов: {ex.Message}");
        }

        _trayIcon?.Dispose();
    }
}