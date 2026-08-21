using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MaxLight;

public partial class NotificationWindow : Window
{
    // СТАТИЧЕСКИЙ счетчик - общий для всех окон
    private static int _notificationCount = 0;
    private static readonly object _lockObj = new object();
    private static bool _alwaysOnTop = true;

    private readonly string _userName;
    private readonly Action<string>? _onClick;
    private DispatcherTimer? _autoCloseTimer;
    private readonly string? _avatarUrl;
    private bool _isClosed = false;

    public static bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set => _alwaysOnTop = value;
    }

    public NotificationWindow(string title, string message, string? avatarUrl = null, Action<string>? onClick = null)
    {
        InitializeComponent();

        // ВАЖНО: окно не должно перехватывать фокус, а то зело бесит
        this.ShowActivated = false;
        this.Focusable = false;

        _userName = title;
        _onClick = onClick;
        _avatarUrl = avatarUrl;

        this.Topmost = _alwaysOnTop;

        txtTitle.Text = title;
        txtMessage.Text = message;

        // Автоматически подгоняем высоту под содержимое (тело сообщения в пуш)
        this.SizeToContent = SizeToContent.Height;

        if (!string.IsNullOrEmpty(avatarUrl))
            _ = LoadAvatarAsync(avatarUrl, title);
        else
            ShowInitials(title);

        // ПОДПИСКА НА CLOSED - ТОЛЬКО ОДИН РАЗ В КОНСТРУКТОРЕ! Как же заебали эти костыли.
        this.Closed += OnWindowClosed;

        PositionNotification();
        SetupAutoClose(message.Length);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        lock (_lockObj)
        {
            if (!_isClosed)
            {
                _notificationCount--;
                _isClosed = true;
                Debug.WriteLine($"📢 Уведомление закрыто. Осталось: {_notificationCount}");
            }
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.Opacity = 0;
        var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        fadeTimer.Tick += (s, args) =>
        {
            if (this.Opacity < 0.95) this.Opacity += 0.1;
            else fadeTimer.Stop();
        };
        fadeTimer.Start();
    }

    // Дёргаем аватарку
    private async Task LoadAvatarAsync(string url, string name)
    {
        try
        {
            string fullUrl = url.StartsWith("/") ? "https://web.max.ru" + url : url; // https...это 443. Когда будем сертификат Минцифры ставить? Шучу!
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0"); // На всякий случай юзер-агент (хотя и так по сути через хромиум лезем)
            var imageData = await client.GetByteArrayAsync(fullUrl);

            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    using var ms = new MemoryStream(imageData);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();

                    imgAvatar.Source = bitmap;
                    imgAvatar.Visibility = Visibility.Visible;
                    borderInitials.Visibility = Visibility.Collapsed;
                }
                catch { ShowInitials(name); }
            });
        }
        catch { await Dispatcher.InvokeAsync(() => ShowInitials(name)); }
    }

    private void ShowInitials(string name)
    {
        txtInitials.Text = GetInitials(name);
        imgAvatar.Visibility = Visibility.Collapsed;
        borderInitials.Visibility = Visibility.Visible;

        var random = new Random(name.GetHashCode());
        borderInitials.Background = new SolidColorBrush(Color.FromRgb(
            (byte)random.Next(60, 120), (byte)random.Next(60, 120), (byte)random.Next(60, 120)));
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        var parts = name.Trim().Split(' ');
        if (parts.Length >= 2) return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
        return name.Length >= 2 ? name[..2].ToUpper() : name[..1].ToUpper();
    }


    // Заебало это окно скакать... вообще "под потолок" прыгало
    private void PositionNotification()
    {
        lock (_lockObj)
        {
            try
            {
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();

                Rect workingArea;

                if (mainWindow != null)
                {
                    try
                    {
                        var screen = ScreenHelper.GetCurrentScreen(mainWindow);
                        workingArea = screen.WorkingArea;
                        Debug.WriteLine($"📢 Экран через ScreenHelper: {workingArea}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ Ошибка ScreenHelper: {ex.Message}");
                        workingArea = SystemParameters.WorkArea;
                    }
                }
                else
                {
                    workingArea = SystemParameters.WorkArea;
                }

                // Убеждаемся, что размеры окна корректны (не NaN и не 0)
                double width = this.Width > 0 ? this.Width : 380;
                double height = this.Height > 0 ? this.Height : 120;

                double x = workingArea.Right - width - 10;
                double y = workingArea.Bottom - height - 10;

                // Смещаем вверх для нескольких уведомлений
                y -= _notificationCount * (height + 8);
                _notificationCount++;

                // Не выходим за верхнюю границу
                if (y < workingArea.Top + 10)
                    y = workingArea.Top + 10;

                this.Left = x;
                this.Top = y;

                Debug.WriteLine($"📢 Уведомление: Left={x}, Top={y}, Count={_notificationCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка позиционирования: {ex.Message}");

                var workArea = SystemParameters.WorkArea;
                this.Left = workArea.Right - this.Width - 10;
                this.Top = workArea.Bottom - this.Height - 10;
            }
        }
    }

    // Если за отведенное время юзер не среагировал на пуш - автоматом закрываем, а то овердохуя накопится
    private void SetupAutoClose(int messageLength)
    {
        int delay = Math.Max(5000, Math.Min(12000, messageLength / 10 * 1000));
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
        _autoCloseTimer.Tick += (s, e) => CloseNotification();
        _autoCloseTimer.Start();
    }

    // Юзер нажал на пуш - закрываем уведомление и...
    private void OnNotificationClick()
    {
        _autoCloseTimer?.Stop();
        string userName = _userName;
        Action<string>? onClick = _onClick;

        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        mainWindow?.ResetUnreadCount();//... и с брасываем счетчик на 0

        this.Close();

        if (!string.IsNullOrEmpty(userName) && onClick != null)
            Application.Current.Dispatcher.BeginInvoke(new Action(() => onClick(userName)));
    }

    private void CloseNotification()
    {
        _autoCloseTimer?.Stop();
        this.Close();
    }

    // Юзер нажал на крестик в пуш - закрываем уведомление и...
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer?.Stop();

        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        mainWindow?.ResetUnreadCount(); //... и с брасываем счетчик на 0

        this.Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.Button) OnNotificationClick();
    }

    private void Avatar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OnNotificationClick();
    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OnNotificationClick();
    private void Message_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => OnNotificationClick();

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer?.Stop();
        base.OnClosed(e);
    }

    public static void Show(string title, string message, string? avatarUrl = null, Action<string>? onClick = null)
    {
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message)) return;
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            try { new NotificationWindow(title, message, avatarUrl, onClick).Show(); }
            catch { }
        }));
    }
}