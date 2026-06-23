using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== ВСЕ МЕТОДЫ УВЕДОМЛЕНИЙ ==========

        private void ShowNotificationOnCorrectMonitor(string title, string body, string avatar)
        {
            try
            {
                // Определяем, на каком мониторе показывать уведомление
                Screen targetScreen = GetTargetScreen();

                // Сохраняем текущую позицию, если нужно
                var notification = new CustomNotification(title, body, avatar, async (userName) =>
                {
                    await OpenChatWithUser(userName);
                });

                // Позиционируем уведомление на нужном мониторе
                PositionNotificationOnScreen(notification, targetScreen);

                notification.Show();
                System.Diagnostics.Debug.WriteLine($"📨 Уведомление показано на мониторе: {targetScreen.DeviceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка показа уведомления: {ex.Message}");
                // Fallback - показываем на основном мониторе
                try
                {
                    var notification = new CustomNotification(title, body, avatar, async (userName) =>
                    {
                        await OpenChatWithUser(userName);
                    });
                    notification.Show();
                }
                catch { }
            }
        }

        private Screen GetTargetScreen()
        {
            // Если окно видимо и не свернуто - используем его монитор
            if (this.Visible && this.WindowState != FormWindowState.Minimized)
            {
                try
                {
                    return Screen.FromControl(this);
                }
                catch
                {
                    // Если не удалось определить - используем последний известный
                    if (_currentScreen != null)
                    {
                        return _currentScreen;
                    }
                }
            }

            // Если окно скрыто или свернуто - используем последний известный монитор
            if (_currentScreen != null)
            {
                return _currentScreen;
            }

            // Fallback - основной монитор
            return Screen.PrimaryScreen;
        }

        private void PositionNotificationOnScreen(CustomNotification notification, Screen targetScreen)
        {
            // Используем рабочий экран монитора (без панели задач)
            var workingArea = targetScreen.WorkingArea;

            // Позиционируем в правом нижнем углу
            int x = workingArea.Right - notification.Width - 10;
            int y = workingArea.Bottom - notification.Height - 10;

            notification.Location = new Point(x, y);
        }

        private async Task ActivateWindowForCall()
        {
            try
            {
                if (!this.Visible || this.WindowState == FormWindowState.Minimized)
                {
                    this.Show();
                    this.WindowState = FormWindowState.Normal;
                    this.ShowInTaskbar = true;
                }

                this.TopMost = true;
                this.Activate();
                this.BringToFront();

                await Task.Delay(2000);
                this.TopMost = false;

                webView?.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка активации окна: {ex.Message}");
            }
        }

        private void LoadNotificationsOnTopSetting()
        {
            try
            {
                CustomNotification.AlwaysOnTop = ConfigManager.GetNotificationsOnTop();
            }
            catch
            {
                CustomNotification.AlwaysOnTop = true;
            }
        }

        private void ToggleNotificationsOnTop(bool isOnTop)
        {
            try
            {
                ConfigManager.SaveNotificationsOnTop(isOnTop);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настройки: {ex.Message}");
            }

            CustomNotification.AlwaysOnTop = isOnTop;
        }
    }
}