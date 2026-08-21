using System.Diagnostics;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace MaxLight;

/// <summary>
/// Обработчик сообщений из WebView2
/// </summary>
public class MessageHandler
{
    private readonly WebView2Handler _webViewHandler;
    private DateTime _lastNotificationTime = DateTime.MinValue;
    private DateTime _lastReset = DateTime.MinValue;
    private int _unreadCount = 0;

    // События
    public event Action<string, string, string?>? NotificationReceived;
    public event Action? IncomingCallDetected;
    public event Action<string>? AuthTokenCaptured;
    public event Action<int>? UnreadCountChanged;
    public event Action<string>? OpenChatRequested;

    public MessageHandler(WebView2Handler webViewHandler)
    {
        _webViewHandler = webViewHandler ?? throw new ArgumentNullException(nameof(webViewHandler));
    }

    public async Task HandleWebMessageAsync(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string message = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(message)) return;

            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeElement)) return;
            string type = typeElement.GetString() ?? "";

            switch (type)
            {
                case "auth_token_captured":
                    await HandleAuthTokenCaptured(root);
                    break;

                case "device_id_captured":
                    HandleDeviceIdCaptured(root);
                    break;

                case "check_window_state":
                    await HandleCheckWindowState();
                    break;

                case "unread_count":
                    // Игнорируем — счетчик управляется через HandleNotification
                    break;

                case "notification":
                    HandleNotification(root);
                    break;

                case "incoming_call":
                    await HandleIncomingCall();
                    break;

                default:
                    Debug.WriteLine($"📨 Неизвестный тип сообщения: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ошибка обработки сообщения: {ex.Message}");
        }
    }

    private async Task HandleAuthTokenCaptured(JsonElement data)
    {
        try
        {
            string? token = data.TryGetProperty("token", out var t) ? t.GetString() : null;
            long? viewerId = data.TryGetProperty("viewerId", out var v) ? v.GetInt64() : null;
            string? deviceId = data.TryGetProperty("deviceId", out var d) ? d.GetString() : null;

            if (!string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("✅ Токен получен, сохраняем и останавливаем перехватчик");
                ConfigManager.SaveAuthData(token, viewerId, deviceId);
                await _webViewHandler.StopTokenParser();
                _webViewHandler.SetAuthRestored(true);
                AuthTokenCaptured?.Invoke(token);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка сохранения токена: {ex.Message}");
        }
    }

    private void HandleDeviceIdCaptured(JsonElement data)
    {
        if (!_webViewHandler.IsAuthRestored) return;

        string? deviceId = data.TryGetProperty("deviceId", out var d) ? d.GetString() : null;
        if (!string.IsNullOrEmpty(deviceId))
        {
            ConfigManager.UpdateDeviceId(deviceId);
        }
    }

    private async Task HandleCheckWindowState()
    {
        bool isActive = _webViewHandler.IsWindowActive();
        await _webViewHandler.UpdateWindowActiveState(isActive);
        Debug.WriteLine($"📊 Ответ на запрос состояния: {(isActive ? "Активно" : "Неактивно")}");
    }

    private void HandleNotification(JsonElement data)
    {
        string title = data.TryGetProperty("title", out var t) ? t.GetString() ?? "Max Light" : "Max Light";
        string body = data.TryGetProperty("body", out var b) ? b.GetString() ?? "Новое сообщение" : "Новое сообщение";
        
        string? avatar = data.TryGetProperty("avatar", out var a) ? a.GetString() : null;

        var now = DateTime.Now;
        if ((now - _lastNotificationTime).TotalMilliseconds < 2000)
        {
            Debug.WriteLine("⏸️ Антиспам: уведомление отклонено");
            return;
        }
        _lastNotificationTime = now;

        // Увеличиваем счетчик при новом уведомлении
        _unreadCount++;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            UnreadCountChanged?.Invoke(_unreadCount);
            NotificationReceived?.Invoke(title, body, avatar);
        });

        Debug.WriteLine($"📨 Уведомление: {title} - {body[..Math.Min(body.Length, 50)]}");
    }

    private async Task HandleIncomingCall()
    {
        Debug.WriteLine("📞 ВХОДЯЩИЙ ЗВОНОК!");

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var mainWindow = System.Windows.Application.Current.Windows
                .OfType<MainWindow>()
                .FirstOrDefault();

            if (mainWindow != null)
            {
                if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                    mainWindow.WindowState = System.Windows.WindowState.Normal;
                mainWindow.Activate();
                mainWindow.Focus();
            }
        });

        IncomingCallDetected?.Invoke();
    }

    public async Task OpenChatWithUser(string userName)
    {
        if (string.IsNullOrEmpty(userName)) return;

        try
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = System.Windows.Application.Current.Windows
                    .OfType<MainWindow>()
                    .FirstOrDefault();

                if (mainWindow != null)
                {
                    if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                        mainWindow.WindowState = System.Windows.WindowState.Normal;
                    mainWindow.Activate();
                    mainWindow.Focus();
                }
            });

            ResetUnreadCount();

            string escapedName = _webViewHandler.EscapeJsString(userName);
            string script = GetOpenChatScript(escapedName);
            string result = await _webViewHandler.ExecuteScriptAsync(script);

            Debug.WriteLine($"Результат поиска чата '{userName}': {result}");

            OpenChatRequested?.Invoke(userName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка открытия чата: {ex.Message}");
        }
    }

    private string GetOpenChatScript(string escapedName)
    {
        return $@"
(function() {{
    var targetName = '{escapedName}';
    var elements = document.querySelectorAll('.text.svelte-1riu5uh');
    
    for (var i = 0; i < elements.length; i++) {{
        var name = elements[i].innerText.replace(/<!---->/g, '').trim();
        if (name === targetName) {{
            var chatElement = elements[i];
            while (chatElement && !chatElement.classList.contains('dialog')) {{
                chatElement = chatElement.parentElement;
                if (!chatElement) break;
            }}
            if (chatElement) {{
                chatElement.click();
                return 'CLICKED';
            }} else {{
                elements[i].click();
                return 'CLICKED_NAME';
            }}
        }}
    }}
    return 'NOT_FOUND';
}})();";
    }

    // ===== УПРАВЛЕНИЕ СЧЕТЧИКОМ =====

    public void ResetUnreadCount()
    {
        if ((DateTime.Now - _lastReset).TotalSeconds < 2)
        {
            Debug.WriteLine("⏸️ Сброс счетчика пропущен (анти-спам)");
            return;
        }
        _lastReset = DateTime.Now;

        _unreadCount = 0;

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            UnreadCountChanged?.Invoke(0);
        });

        Debug.WriteLine("📊 Счетчик сброшен");
    }

    public int GetUnreadCount() => _unreadCount;

    public class AuthData
    {
        public string? Token { get; set; }
        public long? ViewerId { get; set; }
        public string? DeviceId { get; set; }
        public DateTime? SavedAt { get; set; }
    }
}