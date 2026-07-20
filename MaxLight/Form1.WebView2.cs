using MaxLight.Security;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== ВСЕ МЕТОДЫ WEBVIEW2 ==========

        private void CreateWebView()
        {
            string userDataFolder = GetWebViewUserDataFolder();
            tempUserDataFolder = userDataFolder;

            webView = new WebView2
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(webView);
            webView.SendToBack();

            // Инициализируем модификатор страницы
            _pageModifier = new PageModifier(webView);

            this.Load += (s, e) => UpdateWebViewPosition();
            this.Resize += (s, e) => UpdateWebViewPosition();

            _ = InitializeWebViewAsync(userDataFolder);
        }

        private string GetWebViewUserDataFolder()
        {
            string rootFolder = Application.StartupPath;

            // Вместо фиксированной папки WebView2Data, создаем уникальную папку под текущую сессию
            string uniqueSessionId = Guid.NewGuid().ToString();
            string dataFolder = Path.Combine(rootFolder, "WebView2Sessions", uniqueSessionId);

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }
            return dataFolder;
        }


        private async Task InitializeWebViewAsync(string userDataFolder)
        {
            try
            {
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--incognito"
                };

                var proxyConfig = ConfigManager.GetProxySettings();
                if (proxyConfig?.Enabled == true &&
                    !string.IsNullOrEmpty(proxyConfig.Server) &&
                    proxyConfig.Port > 0)
                {
                    options.AdditionalBrowserArguments += $" --proxy-server={proxyConfig.Server}:{proxyConfig.Port}";
                }
                else if (proxyConfig?.Enabled == true)
                {
                    ConfigManager.SaveProxySettings(false, "", 0);
                }

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                // ===== УСТАНАВЛИВАЕМ ПАПКУ ЗАГРУЗОК ЧЕРЕЗ PROFILE =====
                try
                {
                    string downloadPath = ConfigManager.GetDownloadPath();

                    // Проверяем, существует ли папка, если нет - создаем
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                        System.Diagnostics.Debug.WriteLine($"📁 Создана папка загрузок: {downloadPath}");
                    }

                    // Устанавливаем папку загрузок через профиль
                    webView.CoreWebView2.Profile.DefaultDownloadFolderPath = downloadPath;

                    System.Diagnostics.Debug.WriteLine($"📁 Папка загрузок установлена: {downloadPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка установки папки загрузок: {ex.Message}");
                    // Если не удалось установить, используем стандартную папку
                }

                bool hasAuth = await CheckAndRestoreAuth();

                if (!hasAuth)
                {
                    // Нет токена - показываем настройки (первый запуск программы)
                    System.Diagnostics.Debug.WriteLine("[MaxLight] ⚠️ Токен не найден, открываем настройки");

                    // Открываем настройки с небольшой задержкой
                    await Task.Delay(1000);
                    this.BeginInvoke(new Action(() =>
                    {
                        ShowSettings();
                    }));

                    // Включаем перехват токена
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetTokenInterceptorScript());
                    _tokenParserActive = true;
                }
                else
                {
                    _tokenParserActive = false;
                    _authRestored = true;
                }

                // Инициализация модификаторов WebView2
                await _pageModifier.InitializeModificationsAsync();

                // Подгрузка XSS защиты
                await XssProtection.InjectProtectionScript(webView.CoreWebView2);

                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;

                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;

                loadingTimer = new Timer();
                loadingTimer.Interval = 5000;
                loadingTimer.Tick += (s, timerE) =>
                {
                    loadingTimer.Stop();
                    if (!isLoadingCompleted)
                    {
                        ShowConnectionError();
                    }
                };
                loadingTimer.Start();

                webView.CoreWebView2.NavigationCompleted += async (sender, args) =>
                {
                    if (args.IsSuccess)
                    {
                        isLoadingCompleted = true;
                        loadingTimer?.Stop();
                        HideConnectionError();

                        string script = GetEmbeddedResource("MaxLight.MessageParser.js");
                        if (!string.IsNullOrEmpty(script))
                        {
                            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
                            await webView.CoreWebView2.ExecuteScriptAsync(script);
                        }

                        // ===== ПРИМЕНЕНИЕ МОДИФИКАЦИЙ ПРИ НАВИГАЦИИ =====
                        await _pageModifier.ApplyModificationsOnNavigationAsync();

                        await UpdateWebViewWindowState(this.ContainsFocus);
                    }
                    else if (!isLoadingCompleted)
                    {
                        var proxyConfig2 = ConfigManager.GetProxySettings();
                        if (proxyConfig2?.Enabled == true &&
                            !string.IsNullOrEmpty(proxyConfig2.Server) &&
                            proxyConfig2.Port > 0)
                        {
                            var result = MessageBox.Show(
                                "Не удалось подключиться через прокси-сервер.\n" +
                                $"Адрес: {proxyConfig2.Server}:{proxyConfig2.Port}\n\n" +
                                "Проверьте настройки или отключите прокси.",
                                "Ошибка подключения",
                                MessageBoxButtons.RetryCancel,
                                MessageBoxIcon.Warning);

                            if (result == DialogResult.Retry)
                            {
                                ShowSettings();
                            }
                            else
                            {
                                ConfigManager.SaveProxySettings(false, "", 0);
                                await ReinitializeWebView();
                            }
                        }
                        else
                        {
                            ShowConnectionError();
                        }
                    }
                };

                webView.CoreWebView2.Navigate("https://web.max.ru");
            }
            catch (Exception ex)
            {
                loadingTimer?.Stop();
                MessageBox.Show($"Ошибка инициализации WebView2: {ex.Message}\n\nУстановите WebView2 Runtime",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowConnectionError();
            }
        }

        // ===== МЕТОД ДЛЯ ОБНОВЛЕНИЯ ПАПКИ ЗАГРУЗОК (если пользователь изменил настройки) =====
        public async Task UpdateDownloadFolderPath()
        {
            try
            {
                if (webView?.CoreWebView2?.Profile != null)
                {
                    string downloadPath = ConfigManager.GetDownloadPath();

                    // Проверяем, существует ли папка
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }

                    // Обновляем папку загрузок
                    webView.CoreWebView2.Profile.DefaultDownloadFolderPath = downloadPath;

                    System.Diagnostics.Debug.WriteLine($"📁 Папка загрузок обновлена: {downloadPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка обновления папки загрузок: {ex.Message}");
            }
        }

        private string GetTokenInterceptorScript()
        {
            return @"
        (function() {
            if (window._maxLightTokenInterceptorInstalled) {
                console.log('[MaxLight] Перехватчик токенов уже установлен');
                return;
            }
            
            var originalSetItem = localStorage.setItem;
            var hasProcessed = false;
            
            localStorage.setItem = function(key, value) {
                originalSetItem.apply(this, arguments);
                
                if (hasProcessed) {
                    return;
                }
                
                if (key === '__oneme_auth') {
                    try {
                        var authData = JSON.parse(value);
                        var deviceId = localStorage.getItem('__oneme_device_id') || '';
                        
                        window.chrome.webview.postMessage(JSON.stringify({
                            type: 'auth_token_captured',
                            token: authData.token,
                            viewerId: authData.viewerId,
                            deviceId: deviceId
                        }));
                        
                        hasProcessed = true;
                        console.log('[MaxLight] ✓ Токен перехвачен и отправлен');
                    } catch(e) {
                        console.log('[MaxLight] Ошибка парсинга токена:', e);
                    }
                }
            };
            
            window._maxLightTokenInterceptorInstalled = true;
            console.log('[MaxLight] 🎯 Перехватчик токенов установлен');
        })();
    ";
        }

        private async Task<bool> CheckAndRestoreAuth()
        {
            try
            {
                var authData = ConfigManager.GetAuth();
                if (authData == null || string.IsNullOrEmpty(authData.Token)) return false;

                _currentAuthData = new AuthData
                {
                    token = authData.Token,
                    viewerId = authData.ViewerId,
                    deviceId = authData.DeviceId,
                    savedAt = authData.SavedAt
                };

                string escapedToken = EscapeJsString(authData.Token);
                string escapedDeviceId = EscapeJsString(authData.DeviceId ?? "");
                string authObjectJson = $"{{\"token\":\"{escapedToken}\",\"viewerId\":{authData.ViewerId ?? 0}}}";
                string escapedAuthObject = EscapeJsString(authObjectJson);

                string injectionScript = $@"
                        localStorage.setItem('__oneme_auth', '{escapedAuthObject}');
                        localStorage.setItem('__oneme_device_id', '{escapedDeviceId}');
                        console.log('Данные авторизации восстановлены из config.json');
                    ";

                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(injectionScript);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка восстановления: {ex.Message}");
                return false;
            }
        }

        private async Task StopTokenParser()
        {
            if (!_tokenParserActive) return;

            try
            {
                string stopScript = @"
                    if (window._maxLightTokenInterceptorInstalled) {
                        if (window._originalSetItem) {
                            localStorage.setItem = window._originalSetItem;
                        }
                        delete window._maxLightTokenInterceptorInstalled;
                        delete window._originalSetItem;
                        console.log('[MaxLight] ✓ Парсер токенов остановлен');
                    }
                    
                    if (window._tokenInterceptorInstalled) {
                        if (window._originalSetItem) {
                            localStorage.setItem = window._originalSetItem;
                        }
                        delete window._tokenInterceptorInstalled;
                        delete window._originalSetItem;
                        console.log('Парсер токенов остановлен (старая версия)');
                    }
                ";
                await webView.CoreWebView2.ExecuteScriptAsync(stopScript);
                _tokenParserActive = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка остановки парсера: {ex.Message}");
            }
        }

        private async Task ReinitializeWebView()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;

                if (webView != null)
                {
                    if (webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                        webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                        webView.CoreWebView2.Stop();
                    }

                    this.Controls.Remove(webView);
                    webView.Dispose();
                    webView = null;
                }

                CreateWebView();

                await Task.Delay(1000);

                this.Cursor = Cursors.Default;

                CustomNotification.Show("Max Light", "✅ Настройки прокси применены", null, null);
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show($"Ошибка применения настроек: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReloadWebView()
        {
            if (webView?.CoreWebView2 != null)
            {
                isLoadingCompleted = false;
                loadingTimer?.Start();
                webView.CoreWebView2.Reload();
            }
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            string uri = e.Request.Uri.ToLower();
            if (_trackingKeywords.Any(kw => uri.Contains(kw)))
            {
                e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 204, "No Content", null);
            }
        }
    }
}