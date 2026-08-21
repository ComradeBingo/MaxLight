using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MaxLight;

public class WebView2Handler
{
    private WebView2 _webView;
    private readonly Window _parentWindow;
    private readonly MessageHandler _messageHandler;
    private PageModifier? _pageModifier;
    private bool _isLoadingCompleted = false;
    private System.Timers.Timer? _loadingTimer;
    private bool _tokenParserActive = false;
    private bool _authRestored = false;
    private string? _tempUserDataFolder;

    private readonly string[] _trackingKeywords = new[]
    {
        "analytics", "tracker", "metric", "collect", "telemetry",
        "google-analytics", "googletagmanager", "facebook.com/tr",
        "doubleclick", "yandex.ru/metrica", "vk.com/rtrg"
    };

    public event Action<string, string, string?>? NotificationReceived { add => _messageHandler.NotificationReceived += value; remove => _messageHandler.NotificationReceived -= value; }
    public event Action? IncomingCallDetected { add => _messageHandler.IncomingCallDetected += value; remove => _messageHandler.IncomingCallDetected -= value; }
    public event Action<string>? AuthTokenCaptured { add => _messageHandler.AuthTokenCaptured += value; remove => _messageHandler.AuthTokenCaptured -= value; }
    public event Action<int>? UnreadCountChanged { add => _messageHandler.UnreadCountChanged += value; remove => _messageHandler.UnreadCountChanged -= value; }
    public event Action<string>? OpenChatRequested { add => _messageHandler.OpenChatRequested += value; remove => _messageHandler.OpenChatRequested -= value; }
    public event Action? ConnectionError;
    public event Action? ConnectionRestored;
    public bool IsAuthRestored => _authRestored;

    public WebView2Handler(WebView2 webView, Window parentWindow)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
        _parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
        _messageHandler = new MessageHandler(this);
    }

    public async Task InitializeAsync()
    {
        string userDataFolder = GetWebViewUserDataFolder();
        _tempUserDataFolder = userDataFolder;
        SafeCleanupOldSessions();
        try
        {
            var options = new CoreWebView2EnvironmentOptions { AdditionalBrowserArguments = "--incognito" };
            var proxyConfig = ConfigManager.GetProxySettings();
            if (proxyConfig?.Enabled == true && !string.IsNullOrEmpty(proxyConfig.Server) && proxyConfig.Port > 0)
                options.AdditionalBrowserArguments += $" --proxy-server={proxyConfig.Server}:{proxyConfig.Port}";
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.IsScriptEnabled = true;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            // Добавляем обработчики
            _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            _webView.CoreWebView2.DownloadStarting += OnDownloadStarting;

            bool hasAuth = await CheckAndRestoreAuth();
            _authRestored = hasAuth;
            if (!hasAuth) { await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetTokenInterceptorScript()); _tokenParserActive = true; }

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _webView.CoreWebView2.DOMContentLoaded += OnDOMContentLoaded;

            // Настраиваем папку загрузок
            await UpdateDownloadFolderPath();

            _webView.CoreWebView2.Navigate("https://web.max.ru");
        }
        catch (Exception ex) { Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        try
        {
            Debug.WriteLine($"[WebView2Handler] Запрос нового окна: {e.Uri}");

            var uri = new Uri(e.Uri);
            bool isOwnDomain =
                uri.Host.EndsWith("max.ru", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("oneme.ru", StringComparison.OrdinalIgnoreCase);

            e.Handled = true;

            if (isOwnDomain)
            {
                // Свой домен - навигируем в том же WebView2
                // Для файлов сработает DownloadStarting
                // Для внутренних страниц - просто перейдет по ссылке
                Debug.WriteLine($"[WebView2Handler] Навигация внутри WebView2: {e.Uri}");
                _webView.CoreWebView2?.Navigate(e.Uri);
            }
            else
            {
                // Внешний домен - открываем в браузере по умолчанию
                Debug.WriteLine($"[WebView2Handler] Открытие во внешнем браузере: {e.Uri}");
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebView2Handler] Ошибка открытия ссылки: {ex.Message}");
        }
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        try
        {
            var download = e.DownloadOperation;
            Debug.WriteLine($"[WebView2Handler] Начало загрузки: {download.ResultFilePath}");

            // Проверяем настройку "Спрашивать каждый раз"
            if (ConfigManager.AskEveryTime())
            {
                // Показываем диалог выбора файла
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Сохранить файл как",
                    FileName = Path.GetFileName(download.ResultFilePath),
                    InitialDirectory = ConfigManager.GetDownloadPath()
                };

                // Определяем фильтр по расширению
                string ext = Path.GetExtension(dialog.FileName).ToLower();
                if (!string.IsNullOrEmpty(ext))
                {
                    dialog.Filter = $"{ext.Substring(1).ToUpper()} файлы (*{ext})|*{ext}|Все файлы (*.*)|*.*";
                }
                else
                {
                    dialog.Filter = "Все файлы (*.*)|*.*";
                }

                bool? result = dialog.ShowDialog();

                if (result == true && !string.IsNullOrEmpty(dialog.FileName))
                {
                    e.ResultFilePath = dialog.FileName;
                    Debug.WriteLine($"[WebView2Handler] Файл сохранен: {dialog.FileName}");
                }
                else
                {
                    // Пользователь отменил диалог - отменяем загрузку
                    e.Cancel = true;
                    Debug.WriteLine("[WebView2Handler] Загрузка отменена пользователем");
                    return;
                }
            }
            else
            {
                // Используем папку по умолчанию из настроек
                string downloadPath = ConfigManager.GetDownloadPath();
                if (!Directory.Exists(downloadPath))
                    Directory.CreateDirectory(downloadPath);

                string fileName = Path.GetFileName(download.ResultFilePath);
                string fullPath = Path.Combine(downloadPath, fileName);

                // Если файл существует, добавляем номер
                if (File.Exists(fullPath))
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    int counter = 1;
                    string newPath;
                    do
                    {
                        newPath = Path.Combine(downloadPath, $"{nameWithoutExt} ({counter}){ext}");
                        counter++;
                    } while (File.Exists(newPath));
                    fullPath = newPath;
                }

                e.ResultFilePath = fullPath;
                Debug.WriteLine($"[WebView2Handler] Файл сохранен в папку по умолчанию: {fullPath}");
            }

            // Подписываемся на события загрузки
            download.StateChanged += OnDownloadStateChanged;
            download.BytesReceivedChanged += OnDownloadProgressChanged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WebView2Handler] Ошибка настройки загрузки: {ex.Message}");
        }
    }

    private void OnDownloadStateChanged(object? sender, object e)
    {
        if (sender is CoreWebView2DownloadOperation download)
        {
            switch (download.State)
            {
                case CoreWebView2DownloadState.InProgress:
                    Debug.WriteLine($"[WebView2Handler] Загрузка в процессе");
                    break;
                case CoreWebView2DownloadState.Completed:
                    Debug.WriteLine($"[WebView2Handler] Загрузка завершена: {download.ResultFilePath}");
                    // Можно показать уведомление о завершении
                    break;
                case CoreWebView2DownloadState.Interrupted:
                    Debug.WriteLine($"[WebView2Handler] Загрузка прервана: {download.InterruptReason}");
                    break;
            }
        }
    }

    private void OnDownloadProgressChanged(object? sender, object e)
    {
        if (sender is CoreWebView2DownloadOperation download)
        {
            // Исправляем ошибку CS0266 - TotalBytesToReceive может быть null
            if (download.TotalBytesToReceive.HasValue && download.TotalBytesToReceive.Value > 0)
            {
                double progress = (double)download.BytesReceived / download.TotalBytesToReceive.Value * 100;
                Debug.WriteLine($"[WebView2Handler] Прогресс: {progress:F1}%");
            }
            else
            {
                // Если неизвестен общий размер, показываем только байты
                Debug.WriteLine($"[WebView2Handler] Загружено: {download.BytesReceived} байт");
            }
        }
    }

    private void SafeCleanupOldSessions()
    {
        try
        {
            string sessionsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebView2Sessions");
            if (!Directory.Exists(sessionsFolder)) return;
            foreach (var dir in Directory.GetDirectories(sessionsFolder))
            { if (dir == _tempUserDataFolder) continue; try { Directory.Delete(dir, true); } catch { } }
        }
        catch { }
    }

    private string GetWebViewUserDataFolder()
    {
        string exePath = AppDomain.CurrentDomain.BaseDirectory;
        string dataFolder = Path.Combine(exePath, "WebView2Sessions", Guid.NewGuid().ToString());
        if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);
        return dataFolder;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e) => await _messageHandler.HandleWebMessageAsync(e);

    public async Task StopTokenParser()
    {
        if (!_tokenParserActive) return;
        try { await _webView.CoreWebView2.ExecuteScriptAsync("if(window._maxLightTokenInterceptorOriginalSetItem){localStorage.setItem=window._maxLightTokenInterceptorOriginalSetItem;delete window._maxLightTokenInterceptorOriginalSetItem;}"); _tokenParserActive = false; }
        catch (Exception ex) { Debug.WriteLine($"Ошибка: {ex.Message}"); }
    }

    public void SetAuthRestored(bool restored) => _authRestored = restored;

    public bool IsWindowActive() => _parentWindow.IsActive && _parentWindow.Visibility == Visibility.Visible && _parentWindow.WindowState != WindowState.Minimized && _parentWindow.IsFocused;

    public async Task UpdateWindowActiveState(bool isActive)
    {
        if (_webView?.CoreWebView2 != null)
            try { await _webView.CoreWebView2.ExecuteScriptAsync($"if(window.updateWindowActiveState)window.updateWindowActiveState({isActive.ToString().ToLower()});"); }
            catch (Exception ex) { Debug.WriteLine($"Ошибка: {ex.Message}"); }
    }

    public async Task<string> ExecuteScriptAsync(string script) => _webView?.CoreWebView2 != null ? await _webView.CoreWebView2.ExecuteScriptAsync(script) : string.Empty;

    public async Task OpenChatWithUser(string userName) => await _messageHandler.OpenChatWithUser(userName);
    public void ResetUnreadCount() => _messageHandler.ResetUnreadCount();
    public int GetUnreadCount() => _messageHandler.GetUnreadCount();

    public string EscapeJsString(string str) => string.IsNullOrEmpty(str) ? "" : str.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

    public async Task ReloadWebView() { if (_webView?.CoreWebView2 != null) _webView.CoreWebView2.Reload(); else await InitializeAsync(); }

    public async Task UpdateDownloadFolderPath()
    {
        try
        {
            if (_webView?.CoreWebView2?.Profile != null)
            {
                string p = ConfigManager.GetDownloadPath();
                if (!Directory.Exists(p)) Directory.CreateDirectory(p);
                _webView.CoreWebView2.Profile.DefaultDownloadFolderPath = p;
                Debug.WriteLine($"[WebView2Handler] Папка загрузок установлена: {p}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ошибка обновления папки загрузок: {ex.Message}");
        }
    }

    private async Task<bool> CheckAndRestoreAuth()
    {
        try
        {
            var a = ConfigManager.GetAuthData(); if (a == null || string.IsNullOrEmpty(a.Token)) return false;
            string t = EscapeJsString(a.Token), d = EscapeJsString(a.DeviceId ?? "");
            await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"localStorage.setItem('__oneme_auth','{EscapeJsString($"{{\"token\":\"{t}\",\"viewerId\":{a.ViewerId ?? 0}}}")}');localStorage.setItem('__oneme_device_id','{d}');");
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"Ошибка: {ex.Message}"); return false; }
    }

    public void HideWebView() { if (_webView?.CoreWebView2 != null) _webView.Visibility = Visibility.Collapsed; }
    public void ShowWebView() { if (_webView?.CoreWebView2 != null) _webView.Visibility = Visibility.Visible; }

    public async Task ReinitializeWebView()
    {
        try
        {
            if (_webView.CoreWebView2 != null)
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                _webView.CoreWebView2.DOMContentLoaded -= OnDOMContentLoaded;
                _webView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
                _webView.CoreWebView2.DownloadStarting -= OnDownloadStarting;
                _webView.CoreWebView2.Stop();
            }
            _pageModifier?.Dispose(); _pageModifier = null;
            _isLoadingCompleted = false; _tokenParserActive = false; _authRestored = false;
            _loadingTimer?.Stop(); _loadingTimer?.Dispose(); _loadingTimer = null;
            await Task.Delay(500);
            await InitializeAsync();
        }
        catch (Exception ex) { Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
    }

    private async void OnDOMContentLoaded(object? sender, CoreWebView2DOMContentLoadedEventArgs e) => await InjectMessageInterceptor();

    private async Task InjectMessageInterceptor()
    {
        try
        {
            var a = Assembly.GetExecutingAssembly();
            var r = a.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith("messageInterceptor.js"));
            if (r == null) return;
            using var s = a.GetManifestResourceStream(r);
            if (s != null) { using var rd = new StreamReader(s); await _webView.CoreWebView2.ExecuteScriptAsync(await rd.ReadToEndAsync()); }
        }
        catch (Exception ex) { Debug.WriteLine($"❌ Ошибка: {ex.Message}"); }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _isLoadingCompleted = true;
            if (_pageModifier != null) await _pageModifier.ApplyModificationsOnNavigationAsync();
            ConnectionRestored?.Invoke();
        }
        else if (!_isLoadingCompleted) ConnectionError?.Invoke();
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    { if (_trackingKeywords.Any(k => e.Request.Uri.ToLower().Contains(k))) e.Response = _webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 204, "No Content", null); }

    private string GetTokenInterceptorScript() => @"(function(){if(window._maxLightTokenInterceptorOriginalSetItem)return;var o=localStorage.setItem;window._maxLightTokenInterceptorOriginalSetItem=o;localStorage.setItem=function(k,v){o.apply(this,arguments);if(k==='__oneme_auth'){try{var d=JSON.parse(v);window.chrome.webview.postMessage(JSON.stringify({type:'auth_token_captured',token:d.token,viewerId:d.viewerId,deviceId:localStorage.getItem('__oneme_device_id')||''}));}catch(e){}}};})();";

    public void Dispose()
    {
        _loadingTimer?.Stop(); _loadingTimer?.Dispose();
        if (_webView?.CoreWebView2 != null)
        {
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
            _webView.CoreWebView2.DOMContentLoaded -= OnDOMContentLoaded;
            _webView.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
            _webView.CoreWebView2.DownloadStarting -= OnDownloadStarting;
        }
        _pageModifier?.Dispose();
    }
}