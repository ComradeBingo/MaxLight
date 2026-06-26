using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== DllImport ДЛЯ FLASH WINDOW ==========
        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        [DllImport("user32.dll")]
        private static extern int FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_CAPTION = 1;
        private const uint FLASHW_TRAY = 2;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMER = 4;
        private const uint FLASHW_TIMERNOFG = 12;

        // ========== ВСЕ МЕТОДЫ БЕЗОПАСНОСТИ ==========

        // ===== АВТОРИЗАЦИЯ =====

        private void SaveAuthDataToRegistry(string token, long? viewerId, string deviceId)
        {
            try
            {
                ConfigManager.SaveAuth(token, viewerId, deviceId);

                _currentAuthData = new AuthData
                {
                    token = token,
                    viewerId = viewerId,
                    deviceId = deviceId,
                    savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void UpdateDeviceIdInRegistry(string deviceId)
        {
            try
            {
                if (_currentAuthData == null || string.IsNullOrEmpty(_currentAuthData.token))
                {
                    return;
                }

                ConfigManager.UpdateDeviceId(deviceId);

                _currentAuthData.deviceId = deviceId;
                _currentAuthData.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления deviceId: {ex.Message}");
            }
        }

        private async Task ClearSavedAuthData()
        {
            try
            {
                ConfigManager.ClearAuth();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка очистки: {ex.Message}");
            }

            _currentAuthData = null;
            _authRestored = false;
        }

        // ===== PIN-КОД =====

        private string GetSavedPin()
        {
            try
            {
                return ConfigManager.GetPin();
            }
            catch { return null; }
        }

        private void SavePin(string pin)
        {
            ConfigManager.SavePin(pin);
        }

        private void DeletePin()
        {
            ConfigManager.DeletePin();
        }

        private bool CheckPinOnStartup()
        {
            string pin = GetSavedPin();
            if (string.IsNullOrEmpty(pin))
            {
                bool wasProgramRunBefore = false;
                var authData = ConfigManager.GetAuth();
                wasProgramRunBefore = authData != null;

                if (!wasProgramRunBefore)
                {
                    if (MessageBox.Show("Установить PIN-код?", "Безопасность",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        ShowPinSetup();
                        return CheckPinOnStartup();
                    }
                }
                return true;
            }
            return VerifyPinCode(pin);
        }

        private bool VerifyPinCode(string correctPin)
        {
            using (var form = new PinInputForm("Введите PIN-код"))
            {
                for (int i = 0; i < 3; i++)
                {
                    if (form.ShowDialog() == DialogResult.OK && form.PinCode == correctPin)
                        return true;

                    if (i < 2) form.ShowError($"Неверный PIN. Осталось попыток: {2 - i}");
                }
                MessageBox.Show("Превышено число попыток.", "Доступ запрещён", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void ShowPinSetup()
        {
            using (var form = new PinSetupForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    SavePin(form.PinCode);
                    MessageBox.Show("PIN сохранён", "Безопасность", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ChangePinCode()
        {
            string old = GetSavedPin();
            if (!string.IsNullOrEmpty(old))
            {
                using (var f = new PinInputForm("Введите текущий PIN"))
                    if (f.ShowDialog() != DialogResult.OK || (f != null && f.PinCode != old))
                        return;
            }
            ShowPinSetup();
        }

        private void RemovePinCode()
        {
            string old = GetSavedPin();
            if (!string.IsNullOrEmpty(old))
            {
                using (var f = new PinInputForm("Введите PIN для удаления"))
                    if (f.ShowDialog() != DialogResult.OK || f.PinCode != old) return;
            }
            DeletePin();
            MessageBox.Show("PIN удалён", "Безопасность", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowPinSettings()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Установить/Изменить PIN", null, (s, e) => ChangePinCode());
            m.Items.Add("Удалить PIN", null, (s, e) => RemovePinCode());
            m.Show(Cursor.Position);
        }

        private async Task Logout()
        {
            if (MessageBox.Show("Выйти из аккаунта? Данные авторизации будут удалены, программа закроется.",
                "Выход из аккаунта", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            await ClearSavedAuthData();

            DeletePin();

            if (webView?.CoreWebView2 != null)
            {
                string script = @"
            localStorage.removeItem('__oneme_auth');
            localStorage.removeItem('__oneme_device_id');
        ";
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }

            exitRequested = true;
            Application.Exit();
        }

        // ===== ПОДСВЕТКА ИКОНКИ В ПАНЕЛИ ЗАДАЧ =====

        private void StartFlashIcon()
        {
            if (this.IsDisposed) return;

            try
            {
                FLASHWINFO fi = new FLASHWINFO();
                fi.cbSize = (uint)Marshal.SizeOf(fi);
                fi.hwnd = this.Handle;
                fi.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                fi.uCount = 0;
                fi.dwTimeout = 0;

                FlashWindowEx(ref fi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка мигания: {ex.Message}");
            }
        }

        private void StopFlashIcon()
        {
            if (this.IsDisposed) return;

            try
            {
                FLASHWINFO fi = new FLASHWINFO();
                fi.cbSize = (uint)Marshal.SizeOf(fi);
                fi.hwnd = this.Handle;
                fi.dwFlags = FLASHW_STOP;

                FlashWindowEx(ref fi);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка остановки мигания: {ex.Message}");
            }
        }

        private void StartAttentionTimer()
        {
            lock (this)
            {
                bool isWindowVisible = IsWindowVisibleToUser();

                if (isWindowVisible)
                {
                    return;
                }

                CancelAttentionTimer();

                isAttentionRequired = true;

                notificationFlashTimer = new Timer();
                notificationFlashTimer.Interval = ATTENTION_TIMEOUT_MS;
                notificationFlashTimer.Tick += (s, e) =>
                {
                    bool stillInactive = !IsWindowVisibleToUser();
                    if (stillInactive && isAttentionRequired)
                    {
                        StartFlashIcon();
                    }
                    CancelAttentionTimer();
                };
                notificationFlashTimer.Start();
            }
        }

        private void CancelAttentionTimer()
        {
            lock (this)
            {
                isAttentionRequired = false;

                if (notificationFlashTimer != null)
                {
                    notificationFlashTimer.Stop();
                    notificationFlashTimer.Dispose();
                    notificationFlashTimer = null;
                }
            }
        }

        private void ResetAttention()
        {
            StopFlashIcon();
            CancelAttentionTimer();
        }
    }
}