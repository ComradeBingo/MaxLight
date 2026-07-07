using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== ВСЕ МЕТОДЫ ОБРАБОТКИ СООБЩЕНИЙ ==========

        private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string message = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(message)) return;

                var data = JsonConvert.DeserializeObject<dynamic>(message);

                if (data?.type == "auth_token_captured")
                {
                    string token = data.token;
                    long? viewerId = data.viewerId;
                    string deviceId = data.deviceId;

                    SaveAuthDataToRegistry(token, viewerId, deviceId);
                    await StopTokenParser();

                    CustomNotification.Show("Max Light", "✅ Авторизация сохранена", null, null);

                    _authRestored = true;
                }

                if (data?.type == "device_id_captured" && _authRestored)
                {
                    string deviceId = data.deviceId;
                    UpdateDeviceIdInRegistry(deviceId);
                }

                // Обработка запроса состояния из JS
                if (data?.type == "check_window_state")
                {
                    bool isActive = this.Visible &&
                                    this.WindowState != FormWindowState.Minimized &&
                                    this.ContainsFocus;

                    await UpdateWebViewWindowState(isActive);
                    System.Diagnostics.Debug.WriteLine($"📊 Ответ на запрос состояния: {(isActive ? "Активно" : "Неактивно")}");
                }

                // ===== ОБРАБОТЧИК ДЛЯ СЧЕТЧИКА НЕПРОЧИТАННЫХ =====
                if (data?.type == "unread_count")
                {
                    int count = data?.count ?? 0;

                    // Обновляем счетчик в UI
                    this.BeginInvoke(new Action(() =>
                    {
                        UpdateUnreadCount(count);
                    }));

                    System.Diagnostics.Debug.WriteLine($"🔔 Обновлен счетчик непрочитанных: {count}");
                }

                if (data?.type == "notification")
                {
                    string title = data.title ?? "Max Light";
                    string body = data.body ?? "Новое сообщение";
                    string avatar = data.avatar ?? null;

                    var now = DateTime.Now;
                    if ((now - lastNotificationTime).TotalMilliseconds < 2000)
                    {
                        return;
                    }
                    lastNotificationTime = now;

                    // Увеличиваем счетчик при новом уведомлении
                    IncrementUnreadCount();

                    StartAttentionTimer();

                    // ========== ПОКАЗ УВЕДОМЛЕНИЯ НА ПРАВИЛЬНОМ МОНИТОРЕ ==========
                    ShowNotificationOnCorrectMonitor(title, body, avatar);
                }

                if (data?.type == "incoming_call")
                {
                    await ActivateWindowForCall();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private async Task OpenChatWithUser(string userName)
        {
            if (webView?.CoreWebView2 == null) return;

            try
            {
                if (!this.Visible || this.WindowState == FormWindowState.Minimized)
                {
                    RestoreFromTray();
                }

                this.Activate();
                webView.Focus();

                // Сбрасываем счетчик при открытии чата
                ResetUnreadCount();

                string escapedName = EscapeJsString(userName);

                string script = @"(function() {
            var targetName = '" + escapedName + @"';
            var elements = document.querySelectorAll('.text.svelte-1riu5uh');
            
            for (var i = 0; i < elements.length; i++) {
                var name = elements[i].innerText.replace(/<!---->/g, '').trim();
                if (name === targetName) {
                    var chatElement = elements[i];
                    while (chatElement && !chatElement.classList.contains('dialog')) {
                        chatElement = chatElement.parentElement;
                        if (!chatElement) break;
                    }
                    if (chatElement) {
                        chatElement.click();
                        return 'CLICKED';
                    } else {
                        elements[i].click();
                        return 'CLICKED_NAME';
                    }
                }
            }
            return 'NOT_FOUND';
        })();";

                string result = await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private string EscapeJsString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("'", "\\'")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r");
        }
    }
}