using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MaxLight;

public partial class SettingsWindow : Window
{
    private bool _isPortable;
    private bool _isChecking = false;

    public event Action<string>? DownloadPathChanged;
    public event Action? AskEveryTimeToggled;
    public event Action? AutoStartToggled;
    public event Action<bool>? NotificationsOnTopToggled;
    public event Action? PinSettingsClicked;
    public event Action? LogoutClicked;
    public event Action? AboutClicked;
    public event Action? ProxySettingsChanged;
    public event Func<Task<bool>>? CheckUpdatesClicked;

    public SettingsWindow(bool isPortable = false)
    {
        InitializeComponent();

        _isPortable = isPortable || IsPortableMode();

        if (_isPortable)
        {
            chkAutoStart.IsEnabled = false;
            chkAutoStart.IsChecked = false;
            chkAutoStart.Content = "Автозапуск (недоступен в portable режиме)";
        }

        LoadSettings();
    }

    private bool IsPortableMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
            return true;

        string exePath = AppDomain.CurrentDomain.BaseDirectory;

        // Проверяем в папке с программой (на всякий случай)
        if (File.Exists(Path.Combine(exePath, ".portable")))
            return true;

        // Проверяем на директорию выше (для Velopack)
        var parentDir = Directory.GetParent(exePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parentDir != null && File.Exists(Path.Combine(parentDir.FullName, ".portable")))
            return true;

        // ДОПОЛНИТЕЛЬНО: проверяем в %LocalAppData%\MaxLight\ в случае с установленной прогой. Короче, конкретный путь
        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MaxLight");
        if (File.Exists(Path.Combine(appDataPath, ".portable")))
            return true;

        return false;
    }

    private void LoadSettings()
    {
        LoadAutoStartState();
        LoadNotificationsOnTopState();
        LoadProxySettings();
        LoadDownloadPath();
        LoadPinStatus();
    }

    // Устанавливаем путь для загрузок
    private void LoadDownloadPath()
    {
        txtDownloadPath.Text = ConfigManager.GetDownloadPath();
        chkAskEveryTime.IsChecked = ConfigManager.AskEveryTime();
    }

    // Состояние автозапуска
    private void LoadAutoStartState()
    {
        if (!_isPortable)
        {
            // Проверяем, есть ли ключ в реестре
            bool isEnabled = IsAutoStartEnabled();

            // Если ключа нет (первый запуск) - включаем автозапуск по умолчанию
            if (!isEnabled)
            {
                // Включаем автозапуск
                SaveAutoStart(true);
                chkAutoStart.IsChecked = true;
                Debug.WriteLine("✅ Автозапуск включен по умолчанию");
            }
            else
            {
                // Иначе показываем текущее состояние
                chkAutoStart.IsChecked = true;
            }
        }
    }

    // Состояние уведомлений. Перекрывают другие окна или нет?
    private void LoadNotificationsOnTopState()
    {
        chkNotificationsOnTop.IsChecked = ConfigManager.GetNotificationsOnTop();
    }

    // Настройки прокси. Обязательна реинициализация WebView,
    // чтобы юзеру не пришлось руками перезапускать приложение после смены настроек.
    private void LoadProxySettings()
    {
        var proxy = ConfigManager.GetProxySettings();
        if (proxy != null)
        {
            chkProxyEnabled.IsChecked = proxy.Enabled;
            txtProxyServer.Text = proxy.Server ?? "";
            numProxyPort.Text = proxy.Port > 0 ? proxy.Port.ToString() : "8080";
        }
        txtProxyServer.IsEnabled = numProxyPort.IsEnabled = chkProxyEnabled.IsChecked ?? false;
    }

    // Управляем пин-кодом. 
    private void LoadPinStatus()
    {
        bool pinSet = ConfigManager.IsPinSet();
        if (btnPinSettings != null)
        {
            btnPinSettings.Content = pinSet ? "Изменить PIN-код" : "Установить PIN-код";
        }
    }

    private bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            return key?.GetValue("MaxLight") != null;
        }
        catch
        {
            return false;
        }
    }

    public void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }
    // Автозапуск ТОЛЬКО для НЕпортейбл версии. Потому что портейбл папка может "путешествовать" по диску
    public void ChkAutoStart_CheckedChanged(object sender, RoutedEventArgs e)
    {
        // Защита от изменения в portable режиме
        if (_isPortable)
        {
            chkAutoStart.IsChecked = false;
            MessageBox.Show("Автозапуск недоступен в portable режиме.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AutoStartToggled?.Invoke();
        SaveAutoStart(chkAutoStart.IsChecked ?? false);
    }

    // Данные по состоянию автозапуска храним в реестре. Ну а как иначе?
    private void SaveAutoStart(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (enable)
            {
                string exePath = Environment.ProcessPath ??
                    System.Reflection.Assembly.GetExecutingAssembly().Location;
                key?.SetValue("MaxLight", $"\"{exePath}\"");
            }
            else
            {
                key?.DeleteValue("MaxLight", false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка сохранения автозапуска: {ex.Message}");
        }
    }
    // Уведомления перекрывают другие окна или нет(чтобы в катке не лезло в лицо)
    public void ChkNotificationsOnTop_CheckedChanged(object sender, RoutedEventArgs e)
    {
        bool isChecked = chkNotificationsOnTop.IsChecked ?? false;
        NotificationsOnTopToggled?.Invoke(isChecked);
        ConfigManager.SaveNotificationsOnTop(isChecked);
    }


    // Прокси - вкл\выкл
    public void ChkProxyEnabled_CheckedChanged(object sender, RoutedEventArgs e)
    {
        bool enabled = chkProxyEnabled.IsChecked ?? false;
        txtProxyServer.IsEnabled = enabled;
        numProxyPort.IsEnabled = enabled;
    }

    public void ProxySettings_Changed(object sender, EventArgs e)
    {
        if (btnApplyProxy != null)
            btnApplyProxy.IsEnabled = true;
    }

    public void NumProxyPort_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    public void BtnApplyProxy_Click(object sender, RoutedEventArgs e)
    {
        if (btnApplyProxy == null) return;

        if (int.TryParse(numProxyPort.Text, out int port) && port > 0 && port <= 65535)
        {
            ConfigManager.SaveProxySettings(
                chkProxyEnabled.IsChecked ?? false,
                txtProxyServer.Text.Trim(),
                port);
            ProxySettingsChanged?.Invoke();
            btnApplyProxy.IsEnabled = false;
        }
        else
        {
            MessageBox.Show("Некорректный порт (1-65535)", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    //куда скачиваем файлы?
    public void BtnBrowseDownloadPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Выберите папку для загрузок",
            InitialDirectory = txtDownloadPath.Text,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            ConfigManager.SaveDownloadPath(dialog.FolderName);
            txtDownloadPath.Text = dialog.FolderName;
            DownloadPathChanged?.Invoke(dialog.FolderName);
        }
    }

    // Сбрасываем настройку загрузок
    public void BtnResetDownloadPath_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Сбросить путь загрузок на стандартный?",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ConfigManager.SaveDownloadPath(null);
            txtDownloadPath.Text = ConfigManager.GetDownloadPath();
            DownloadPathChanged?.Invoke(txtDownloadPath.Text);
        }
    }

    // Спрашивать каждый раз при скачивании
    public void ChkAskEveryTime_CheckedChanged(object sender, RoutedEventArgs e)
    {
        AskEveryTimeToggled?.Invoke();
        ConfigManager.SaveAskEveryTime(chkAskEveryTime.IsChecked ?? false);
    }

    // Пин-код настройка
    public void BtnPinSettings_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigManager.IsPinSet())
        {
            var dialog = new PinSettingsDialog
            {
                Owner = this
            };
            dialog.ShowDialog();
        }
        else
        {
            var setupWindow = new PinSetupWindow
            {
                Owner = this
            };

            var result = setupWindow.ShowDialog();

            if (result == true && setupWindow.PinSet && !string.IsNullOrEmpty(setupWindow.PinCode))
            {
                ConfigManager.SavePinCode(setupWindow.PinCode);
                Debug.WriteLine("✅ PIN-код установлен из настроек");
            }
            else
            {
                Debug.WriteLine("⏭️ Установка PIN-кода отменена");
            }
        }

        LoadPinStatus();
        PinSettingsClicked?.Invoke();
    }

    // Удалить данные авторизации (токен)
    public void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Вы уверены, что хотите выйти из аккаунта?",
            "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            LogoutClicked?.Invoke();
        }
    }

    // Проверяем обновы на гитхабе
    public async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_isChecking) return;
        _isChecking = true;

        string originalText = "Проверить обновления";
        if (sender is Button button)
        {
            button.Content = "Проверка...";
            button.IsEnabled = false;
        }

        lblUpdateStatus.Visibility = Visibility.Collapsed;

        try
        {
            bool hasUpdate = CheckUpdatesClicked != null && await CheckUpdatesClicked.Invoke();
            lblUpdateStatus.Text = hasUpdate ? "Найдено обновление!" : "Обновлений нет";
            lblUpdateStatus.Foreground = hasUpdate
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219));
        }
        catch (Exception ex)
        {
            lblUpdateStatus.Text = $"Ошибка: {ex.Message}";
            lblUpdateStatus.Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(231, 76, 60));
        }
        finally
        {
            lblUpdateStatus.Visibility = Visibility.Visible;
            if (sender is Button btn)
            {
                btn.Content = originalText;
                btn.IsEnabled = true;
            }
            _isChecking = false;
        }
    }

    public void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        AboutClicked?.Invoke();
    }

    public void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public void SetAutoStartChecked(bool enabled)
    {
        if (!_isPortable) chkAutoStart.IsChecked = enabled;
    }

    public void SetNotificationsOnTopChecked(bool enabled)
    {
        chkNotificationsOnTop.IsChecked = enabled;
    }
}