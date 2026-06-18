using MaxLight.Security;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1 : Form
    {
        private WebView2 webView;
        private TitleBar titleBar;
        private NotifyIcon trayIcon;
        private bool exitRequested = false;
        private DateTime lastNotificationTime = DateTime.MinValue;
        private Panel errorPanel;
        private Timer loadingTimer;
        private bool isLoadingCompleted = false;
        private string tempUserDataFolder;
        private AuthData _currentAuthData;

        // Флаг для управления парсером токенов
        private bool _tokenParserActive = false;
        private bool _authRestored = false;

        // Поля для подсветки иконки в панели задач
        private Timer notificationFlashTimer;
        private bool isAttentionRequired = false;
        private const int ATTENTION_TIMEOUT_MS = 5000;

        // ========== ПОЛЯ ДЛЯ ТРЕЯ ==========
        private int _unreadCount = 0;
        private Icon _normalIcon;
        private Icon _unreadIcon;

        private readonly string[] _trackingKeywords = new[]
        {
            "analytics", "apptracer", "perf/", "sdk-api",
            "adsystem", "crashtoken", "crash", "track"
        };

        public Form1()
        {
            // ========== ПРОВЕРКА PIN ==========
            if (!CheckPinOnStartup())
            {
                Environment.Exit(0);
            }

            // ========== ОСТАЛЬНАЯ ИНИЦИАЛИЗАЦИЯ ==========
            InitializeForm();
            LoadWindowState();
            CreateTrayIcon();
            LoadIcons();
            CreateTitleBar();

            // Загружаем настройку для уведомлений
            LoadNotificationsOnTopSetting();

            CreateErrorPanel();
            CreateResizeBorders();
            CreateWebView();

            this.FormBorderStyle = FormBorderStyle.None;
            this.FormClosing += Form1_FormClosing;
            this.Resize += Form1_Resize;

            this.ResizeEnd += (s, e) => SaveWindowState();
            this.Move += (s, e) => SaveWindowState();

            Application.ApplicationExit += (s, e) => SaveWindowState();

            this.Activated += async (s, e) =>
            {
                ResetAttention();
                ResetUnreadCount();
                await UpdateWebViewWindowState(true);
            };

            this.Deactivate += async (s, e) =>
            {
                await UpdateWebViewWindowState(false);
            };

            this.Shown += (s, e) =>
            {
                try
                {
                    var state = ConfigManager.GetWindowState();
                    if (state != null && state.Left != -1 && state.Top != -1 && this.WindowState != FormWindowState.Maximized)
                    {
                        this.Location = new Point(state.Left, state.Top);
                    }
                }
                catch { }
            };

            // Обработка аргументов командной строки для активации
            CheckActivationArgs();
        }

        private void InitializeForm()
        {
            // ========== МИГРАЦИЯ ИЗ РЕЕСТРА ==========
            try
            {
                if (ConfigManager.MigrateFromRegistry())
                {
                    System.Diagnostics.Debug.WriteLine("✅ Миграция из реестра в config.json выполнена");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка миграции: {ex.Message}");
            }

            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.UserPaint |
                  ControlStyles.DoubleBuffer |
                  ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();

            var screen = Screen.PrimaryScreen.Bounds;
            this.Size = new Size(1050, 800);
            this.BackColor = Color.FromArgb(236, 240, 241);
            this.MinimumSize = new Size(640, 480);
            this.MaximumSize = new Size(screen.Width, screen.Height);
            this.ShowInTaskbar = true;

            string iconPath = Path.Combine(Application.StartupPath, "app.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }
        }

        private void CheckActivationArgs()
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.Equals("--activate", StringComparison.OrdinalIgnoreCase))
                {
                    // Активируем окно
                    if (this.WindowState == FormWindowState.Minimized)
                    {
                        this.WindowState = FormWindowState.Normal;
                    }
                    this.Show();
                    this.Activate();
                    this.BringToFront();
                    break;
                }
            }
        }

        private bool IsPortableMode()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase));
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            UpdateWebViewPosition();
            UpdateErrorPanelPosition();

            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = true;
                _ = UpdateWebViewWindowState(false);
            }
            else if (this.WindowState == FormWindowState.Normal && !this.ContainsFocus)
            {
                _ = UpdateWebViewWindowState(false);
            }
            else if (this.WindowState == FormWindowState.Normal && this.ContainsFocus)
            {
                _ = UpdateWebViewWindowState(true);
            }
        }

        private void UpdateWebViewPosition()
        {
            if (webView != null && titleBar != null)
            {
                int borderSize = 5;
                int titleBarHeight = titleBar.Height;

                webView.Location = new Point(borderSize, titleBarHeight);
                webView.Size = new Size(
                    this.ClientSize.Width - borderSize * 2,
                    this.ClientSize.Height - titleBarHeight - borderSize
                );
            }
        }

        private void UpdateErrorPanelPosition()
        {
            if (errorPanel != null && titleBar != null)
            {
                int borderSize = 8;
                int titleBarHeight = titleBar.Height;

                errorPanel.Location = new Point(borderSize, titleBarHeight);
                errorPanel.Size = new Size(
                    this.ClientSize.Width - borderSize * 2,
                    this.ClientSize.Height - titleBarHeight - borderSize
                );
            }
        }

        #region Панель ошибки подключения

        private void CreateErrorPanel()
        {
            errorPanel = new Panel
            {
                BackColor = Color.FromArgb(248, 249, 250),
                Visible = false,
                Dock = DockStyle.None
            };

            Panel centerPanel = new Panel
            {
                Size = new Size(400, 200),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label iconLabel = new Label
            {
                Text = "⚠️",
                Font = new Font("Segoe UI", 48, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 53, 69),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(400, 80),
                Location = new Point(0, 20)
            };

            Label messageLabel = new Label
            {
                Text = "Не могу подключиться",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 58, 64),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(400, 40),
                Location = new Point(0, 100)
            };

            Label subMessageLabel = new Label
            {
                Text = "Проверьте соединение с интернетом",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(400, 30),
                Location = new Point(0, 140)
            };

            Button retryButton = new Button
            {
                Text = "Повторить",
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            retryButton.FlatAppearance.BorderSize = 0;
            retryButton.Location = new Point((400 - retryButton.Width) / 2, 180);
            retryButton.Click += async (s, e) => await RetryLoad();

            centerPanel.Controls.Add(iconLabel);
            centerPanel.Controls.Add(messageLabel);
            centerPanel.Controls.Add(subMessageLabel);
            centerPanel.Controls.Add(retryButton);

            centerPanel.Location = new Point(
                (this.ClientSize.Width - centerPanel.Width) / 2,
                (this.ClientSize.Height - titleBar.Height - centerPanel.Height) / 2);

            errorPanel.Controls.Add(centerPanel);
            errorPanel.Resize += (s, e) =>
            {
                centerPanel.Location = new Point(
                    (errorPanel.Width - centerPanel.Width) / 2,
                    (errorPanel.Height - centerPanel.Height) / 2);
            };

            Controls.Add(errorPanel);
        }

        private void ShowConnectionError()
        {
            if (errorPanel != null && !isLoadingCompleted)
            {
                errorPanel.Visible = true;
                errorPanel.BringToFront();
                UpdateErrorPanelPosition();
            }
        }

        private void HideConnectionError()
        {
            if (errorPanel != null)
            {
                errorPanel.Visible = false;
                if (webView != null)
                {
                    webView.BringToFront();
                }
            }
        }

        private async Task RetryLoad()
        {
            HideConnectionError();
            isLoadingCompleted = false;
            await ReloadWebView();
        }

        #endregion

        #region Системный трей

        private void CreateTrayIcon()
        {
            string iconPath = Path.Combine(Application.StartupPath, "app.ico");
            Icon appIcon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

            trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "Max Light",
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => ToggleWindow();

            var contextMenu = new ContextMenuStrip();

            var toggleItem = new ToolStripMenuItem("Открыть/Свернуть");
            toggleItem.Click += (s, e) => ToggleWindow();
            contextMenu.Items.Add(toggleItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var settingsItem = new ToolStripMenuItem("Настройки");
            settingsItem.Click += (s, e) => ShowSettings();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Выйти");
            exitItem.Click += (s, e) =>
            {
                exitRequested = true;
                Application.Exit();
            };
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void LoadIcons()
        {
            string normalPath = Path.Combine(Application.StartupPath, "app.ico");
            string unreadPath = Path.Combine(Application.StartupPath, "app_unread.ico");

            _normalIcon = File.Exists(normalPath) ? new Icon(normalPath) : SystemIcons.Application;

            if (File.Exists(unreadPath))
            {
                _unreadIcon = new Icon(unreadPath);
            }
            else
            {
                _unreadIcon = CreateUnreadIconOverlay(_normalIcon);
            }

            if (trayIcon != null)
            {
                trayIcon.Icon = _normalIcon;
            }
        }

        private Icon CreateUnreadIconOverlay(Icon baseIcon)
        {
            var bitmap = baseIcon.ToBitmap();
            using (var g = Graphics.FromImage(bitmap))
            {
                using (var brush = new SolidBrush(Color.Red))
                {
                    int dotSize = bitmap.Width / 3;
                    g.FillEllipse(brush, bitmap.Width - dotSize, 0, dotSize, dotSize);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        private void UpdateTrayIcon(bool hasUnread, int count = 0)
        {
            if (trayIcon == null) return;

            if (hasUnread && !IsWindowVisibleToUser())
            {
                trayIcon.Icon = _unreadIcon;
                trayIcon.Text = count > 0 ? $"Max Light ({count} непрочитанных)" : "Max Light (есть новые сообщения)";
                System.Diagnostics.Debug.WriteLine($"🔴 Иконка трея изменена: есть {count} непрочитанных");
            }
            else
            {
                trayIcon.Icon = _normalIcon;
                trayIcon.Text = "Max Light";
                System.Diagnostics.Debug.WriteLine($"🟢 Иконка трея восстановлена");
            }
        }

        private bool IsWindowVisibleToUser()
        {
            return this.Visible && this.WindowState != FormWindowState.Minimized;
        }

        private void IncrementUnreadCount()
        {
            _unreadCount++;
            System.Diagnostics.Debug.WriteLine($"📊 Счетчик непрочитанных: {_unreadCount}");

            if (!IsWindowVisibleToUser())
            {
                UpdateTrayIcon(true, _unreadCount);
            }
            else
            {
                if (!this.ContainsFocus)
                {
                    StartAttentionTimer();
                }
            }
        }

        private void ResetUnreadCount()
        {
            if (_unreadCount > 0)
            {
                _unreadCount = 0;
                UpdateTrayIcon(false, 0);
                System.Diagnostics.Debug.WriteLine($"📊 Счетчик непрочитанных сброшен");
            }
            StopFlashIcon();
            CancelAttentionTimer();
        }

        private void ToggleWindow()
        {
            bool isWindowVisible = IsWindowVisibleToUser();

            if (isWindowVisible)
            {
                MinimizeToTray();
            }
            else
            {
                RestoreFromTray();
            }
        }

        private void MinimizeToTray()
        {
            this.Hide();
            this.ShowInTaskbar = false;
            if (_unreadCount > 0)
            {
                UpdateTrayIcon(true, _unreadCount);
            }
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
            webView?.Focus();
            UpdateWebViewPosition();
            ResetAttention();
            ResetUnreadCount();
        }

        #endregion

        #region Сохранение состояния окна

        private void SaveWindowState()
        {
            try
            {
                if (WindowState == FormWindowState.Minimized) return;

                int width, height, left, top;
                if (WindowState == FormWindowState.Normal)
                {
                    width = this.Width;
                    height = this.Height;
                    left = this.Left;
                    top = this.Top;
                }
                else
                {
                    width = this.RestoreBounds.Width;
                    height = this.RestoreBounds.Height;
                    left = this.RestoreBounds.Left;
                    top = this.RestoreBounds.Top;
                }

                width = Math.Max(width, MinimumSize.Width);
                height = Math.Max(height, MinimumSize.Height);

                ConfigManager.SaveWindowState(width, height, left, top, (int)WindowState);
            }
            catch { }
        }

        private void LoadWindowState()
        {
            try
            {
                var state = ConfigManager.GetWindowState();
                if (state == null)
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                    return;
                }

                bool isValidPosition = false;
                if (state.Left != -1 && state.Top != -1)
                {
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        if (state.Left + 50 > screen.Bounds.Left && state.Left - 50 < screen.Bounds.Right &&
                            state.Top + 50 > screen.Bounds.Top && state.Top - 50 < screen.Bounds.Bottom)
                        {
                            isValidPosition = true;
                            break;
                        }
                    }
                }

                if (state.Width >= MinimumSize.Width && state.Height >= MinimumSize.Height && isValidPosition)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Size = new Size(state.Width, state.Height);
                    this.Location = new Point(state.Left, state.Top);
                }
                else
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                }

                if (state.State == 1)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
            }
            catch
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }

        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!exitRequested)
            {
                e.Cancel = true;
                MinimizeToTray();
            }
            else
            {
                SaveWindowState();

                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Icon?.Dispose();
                    trayIcon.Dispose();
                    trayIcon = null;
                }

                _normalIcon?.Dispose();
                _unreadIcon?.Dispose();

                // ВСЕГДА удаляем папку WebView2
                if (!string.IsNullOrEmpty(tempUserDataFolder) && Directory.Exists(tempUserDataFolder))
                {
                    try
                    {
                        await Task.Run(() => Directory.Delete(tempUserDataFolder, true));
                    }
                    catch { }
                }

                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                    webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                }

                CancelAttentionTimer();
            }
        }

        #endregion

        #region UI Components

        private void CreateTitleBar()
        {
            titleBar = new TitleBar(this);
            titleBar.SettingsClick += (s, e) => ShowSettings();
            titleBar.Dock = DockStyle.Top;
            Controls.Add(titleBar);
            titleBar.BringToFront();
        }

        private void ShowSettings()
        {
            var settingsForm = new SettingsForm();
            settingsForm.AutoStartToggled += ToggleAutoStart;
            settingsForm.NotificationsOnTopToggled += ToggleNotificationsOnTop;
            settingsForm.PinSettingsClicked += ShowPinSettings;
            settingsForm.LogoutClicked += async () => await Logout();
            settingsForm.AboutClicked += ShowAbout;

            // Подписываемся на событие изменения прокси
            settingsForm.ProxySettingsChanged += () =>
            {
                System.Diagnostics.Debug.WriteLine("🔄 Перезапуск приложения для применения прокси...");
                // Показываем сообщение пользователю
                MessageBox.Show(
                    "Настройки прокси применены. Для полного применения требуется перезапуск программы.",
                    "Перезапуск",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Restart();
                Environment.Exit(0);
            };

            settingsForm.ShowDialog(this);
        }

        private void ShowAbout()
        {
            new AboutForm().ShowDialog();
        }

        #endregion

        #region WebView2

        private void CreateWebView()
        {
            string userDataFolder = GetWebViewUserDataFolder();
            tempUserDataFolder = userDataFolder; // Сохраняем для очистки

            webView = new WebView2
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(webView);
            webView.SendToBack();

            this.Load += (s, e) => UpdateWebViewPosition();
            this.Resize += (s, e) => UpdateWebViewPosition();

            _ = InitializeWebViewAsync(userDataFolder);
        }

        private string GetWebViewUserDataFolder()
        {
            
            string dataFolder = Path.Combine(Application.StartupPath, "WebView2Data");

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
                // Создаем опции
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--inprivate"
                };

                // ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА ПРОКСИ
                var proxyConfig = ConfigManager.GetProxySettings();
                if (proxyConfig?.Enabled == true &&
                    !string.IsNullOrEmpty(proxyConfig.Server) &&
                    proxyConfig.Port > 0)
                {
                    options.AdditionalBrowserArguments += $" --proxy-server={proxyConfig.Server}:{proxyConfig.Port}";
                    System.Diagnostics.Debug.WriteLine($"🌐 Прокси настроен: {proxyConfig.Server}:{proxyConfig.Port}");
                }
                else if (proxyConfig?.Enabled == true)
                {
                    // Если прокси включен, но параметры некорректны - отключаем
                    System.Diagnostics.Debug.WriteLine("⚠️ Прокси отключен: некорректные параметры в config.json");
                    ConfigManager.SaveProxySettings(false, "", 0);
                }

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                // ========== 1. ПЕРЕХВАТЧИК ТОКЕНА - САМЫЙ ПЕРВЫЙ! ==========
                bool hasAuth = await CheckAndRestoreAuth();

                if (!hasAuth)
                {
                    System.Diagnostics.Debug.WriteLine("=== Устанавливаем перехватчик ДО загрузки страницы ===");
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GetTokenInterceptorScript());
                    _tokenParserActive = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("=== Токен уже есть, перехватчик не нужен ===");
                    _tokenParserActive = false;
                    _authRestored = true;
                }

                // Блок инфобаара (верхняя строка с рекламой)
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("setInterval(()=>{const e=document.querySelector('.infobar.svelte-1aijhs3');if(e)e.remove()},100);");

                // ========== 2. ЗАЩИТА ОТ XSS ==========
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
                            System.Diagnostics.Debug.WriteLine("✓ Парсер уведомлений подключен");
                        }

                        await UpdateWebViewWindowState(this.ContainsFocus);
                    }
                    else if (!isLoadingCompleted)
                    {
                        // Проверяем, может быть проблема с прокси
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
                                // Отключаем прокси и перезагружаем
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

        private string GetEmbeddedResource(string resourceName)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        var allResources = assembly.GetManifestResourceNames();
                        System.Diagnostics.Debug.WriteLine($"Ресурс '{resourceName}' не найден. Доступные: {string.Join(", ", allResources)}");
                        return null;
                    }

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки ресурса: {ex.Message}");
                return null;
            }
        }

        private async Task UpdateWebViewWindowState(bool isActive)
        {
            if (webView?.CoreWebView2 != null)
            {
                try
                {
                    string script = $"if(typeof updateWindowActiveState === 'function') updateWindowActiveState({isActive.ToString().ToLower()});";
                    await webView.CoreWebView2.ExecuteScriptAsync(script);
                    System.Diagnostics.Debug.WriteLine($"📢 Состояние окна передано в JS: {(isActive ? "Активно" : "Неактивно")}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка передачи состояния в JS: {ex.Message}");
                }
            }
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

                System.Diagnostics.Debug.WriteLine("Токен и deviceId восстановлены из config.json");
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
                System.Diagnostics.Debug.WriteLine("Парсер токенов успешно остановлен");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка остановки парсера: {ex.Message}");
            }
        }

        // ========== ПЕРЕИНИЦИАЛИЗАЦИЯ WEBVIEW2  ==========
        private async Task ReinitializeWebView()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;

                // 1. Останавливаем текущий WebView2
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

                // 2. Пересоздаем с новыми настройками
                CreateWebView();

                // 3. Ждем загрузки
                await Task.Delay(1000);

                this.Cursor = Cursors.Default;

                // Уведомляем пользователя
                CustomNotification.Show("Max Light", "✅ Настройки прокси применены", null, null);
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show($"Ошибка применения настроек: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Управление авторизацией

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

                System.Diagnostics.Debug.WriteLine("Токен и deviceId сохранены в config.json");
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
                    System.Diagnostics.Debug.WriteLine("Нет сохранённого токена для обновления deviceId");
                    return;
                }

                ConfigManager.UpdateDeviceId(deviceId);

                _currentAuthData.deviceId = deviceId;
                _currentAuthData.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                System.Diagnostics.Debug.WriteLine($"DeviceId обновлён в config.json: {deviceId}");
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
                System.Diagnostics.Debug.WriteLine("✅ Данные авторизации удалены из config.json");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка очистки: {ex.Message}");
            }

            _currentAuthData = null;
            _authRestored = false;
        }

        #endregion

        private async Task ReloadWebView()
        {
            if (webView?.CoreWebView2 != null)
            {
                isLoadingCompleted = false;
                loadingTimer?.Start();
                webView.CoreWebView2.Reload();
            }
        }

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

                    System.Diagnostics.Debug.WriteLine($"🔑 Перехвачен токен! Длина: {token?.Length}");
                    System.Diagnostics.Debug.WriteLine($"📱 DeviceId при авторизации: {deviceId}");

                    SaveAuthDataToRegistry(token, viewerId, deviceId);
                    await StopTokenParser();

                    CustomNotification.Show("Max Light", "✅ Авторизация сохранена", null, null);

                    _authRestored = true;
                    System.Diagnostics.Debug.WriteLine("✓ Парсер токенов остановлен, авторизация восстановлена");
                }

                if (data?.type == "device_id_captured" && _authRestored)
                {
                    string deviceId = data.deviceId;
                    System.Diagnostics.Debug.WriteLine($"💾 Перехвачено обновление DeviceId: {deviceId}");
                    UpdateDeviceIdInRegistry(deviceId);
                }

                if (data?.type == "notification")
                {
                    string title = data.title ?? "Max Light";
                    string body = data.body ?? "Новое сообщение";
                    string avatar = data.avatar ?? null;

                    var now = DateTime.Now;
                    if ((now - lastNotificationTime).TotalMilliseconds < 5000)
                    {
                        System.Diagnostics.Debug.WriteLine($"⏸️ Антиспам: уведомление отклонено (менее 5 сек назад)");
                        return;
                    }
                    lastNotificationTime = now;

                    System.Diagnostics.Debug.WriteLine($"🔔 Уведомление: {title}");

                    IncrementUnreadCount();

                    StartAttentionTimer();

                    CustomNotification.Show(title, body, avatar, async (userName) =>
                    {
                        await OpenChatWithUser(userName);
                    });
                }

                if (data?.type == "incoming_call")
                {
                    System.Diagnostics.Debug.WriteLine("📞 ПОЛУЧЕН ВХОДЯЩИЙ ЗВОНОК!");
                    await ActivateWindowForCall();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        #region Активация окна при звонке

        private async Task ActivateWindowForCall()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("📞 Активация окна для входящего звонка...");

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

                System.Diagnostics.Debug.WriteLine("✅ Окно активировано для звонка");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка активации окна: {ex.Message}");
            }
        }

        #endregion

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            string uri = e.Request.Uri.ToLower();
            if (_trackingKeywords.Any(kw => uri.Contains(kw)))
            {
                e.Response = webView.CoreWebView2.Environment.CreateWebResourceResponse(null, 204, "No Content", null);
            }
        }

        #region Открытие чата по клику на уведомление

        private async Task OpenChatWithUser(string userName)
        {
            if (webView?.CoreWebView2 == null) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"Открываем чат с: '{userName}'");

                if (!this.Visible || this.WindowState == FormWindowState.Minimized)
                {
                    RestoreFromTray();
                }

                this.Activate();
                webView.Focus();

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
                System.Diagnostics.Debug.WriteLine($"Результат: {result}");
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

        #endregion

        #region Resize Borders

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_TOP = 12, HT_TOPLEFT = 13, HT_TOPRIGHT = 14;
        private const int HT_BOTTOM = 15, HT_BOTTOMLEFT = 16, HT_BOTTOMRIGHT = 17;
        private const int HT_LEFT = 10, HT_RIGHT = 11;

        private void CreateResizeBorders()
        {
            int borderSize = 5;

            var topBorder = new Panel { Height = borderSize, Dock = DockStyle.Top, Cursor = Cursors.SizeNS };
            topBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_TOP, IntPtr.Zero); };
            Controls.Add(topBorder);
            topBorder.BringToFront();

            var bottomBorder = new Panel { Height = borderSize, Dock = DockStyle.Bottom, Cursor = Cursors.SizeNS };
            bottomBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_BOTTOM, IntPtr.Zero); };
            Controls.Add(bottomBorder);
            bottomBorder.BringToFront();

            var leftBorder = new Panel { Width = borderSize, Dock = DockStyle.Left, Cursor = Cursors.SizeWE };
            leftBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_LEFT, IntPtr.Zero); };
            Controls.Add(leftBorder);
            leftBorder.BringToFront();

            var rightBorder = new Panel { Width = borderSize, Dock = DockStyle.Right, Cursor = Cursors.SizeWE };
            rightBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_RIGHT, IntPtr.Zero); };
            Controls.Add(rightBorder);
            rightBorder.BringToFront();

            AddCorner(0, 0, Cursors.SizeNWSE, HT_TOPLEFT);
            AddCorner(Width - 16, 0, Cursors.SizeNESW, HT_TOPRIGHT);
            AddCorner(0, Height - 16, Cursors.SizeNESW, HT_BOTTOMLEFT);
            AddCorner(Width - 16, Height - 16, Cursors.SizeNWSE, HT_BOTTOMRIGHT);

            Resize += (s, e) =>
            {
                UpdateCornerPosition(HT_TOPRIGHT, Width - 16, 0);
                UpdateCornerPosition(HT_BOTTOMLEFT, 0, Height - 16);
                UpdateCornerPosition(HT_BOTTOMRIGHT, Width - 16, Height - 16);
            };
        }

        private void AddCorner(int x, int y, Cursor cursor, int hit)
        {
            var p = new Panel { Size = new Size(16, 16), Location = new Point(x, y), Cursor = cursor, BackColor = Color.Transparent };
            p.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)hit, IntPtr.Zero); };
            Controls.Add(p);
            p.BringToFront();
        }

        private void UpdateCornerPosition(int hit, int x, int y)
        {
            foreach (Control c in Controls)
                if (c is Panel && (c.Location.X == (hit == HT_TOPRIGHT ? Width - 16 : c.Location.X)))
                    c.Location = new Point(x, y);
        }

        #endregion

        #region Автозапуск

        private void ToggleAutoStart()
        {
            // Если portable - ничего не делаем
            if (IsPortableMode())
            {
                System.Diagnostics.Debug.WriteLine("⏸️ Portable режим: автозапуск недоступен");
                return;
            }

            bool enabled = !IsAutoStartEnabled();
            SetAutoStart(enabled);
        }

        private bool IsAutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                return key?.GetValue("MaxLight") != null;
            }
        }

        private void SetAutoStart(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (enabled)
                    key.SetValue("MaxLight", Application.ExecutablePath);
                else
                    key.DeleteValue("MaxLight", false);
            }
        }

        #endregion

        #region PIN-код

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

        #endregion

        #region Подсветка иконки в панели задач

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
                System.Diagnostics.Debug.WriteLine("🔔 Запущено мигание иконки");
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
                System.Diagnostics.Debug.WriteLine("🔔 Мигание иконки остановлено");
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
                    System.Diagnostics.Debug.WriteLine("⏸️ Окно активно, таймер внимания не запускается");
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

                System.Diagnostics.Debug.WriteLine($"⏰ Таймер внимания запущен на {ATTENTION_TIMEOUT_MS} мс");
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

        #endregion

        #region Выход из аккаунта

        private async Task Logout()
        {
            if (MessageBox.Show("Выйти из аккаунта? Данные авторизации будут удалены, программа закроется.",
                "Выход из аккаунта", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            await ClearSavedAuthData();

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

        #endregion

        #region Настройки уведомлений

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

            System.Diagnostics.Debug.WriteLine($"🔔 Настройка уведомлений: TopMost = {CustomNotification.AlwaysOnTop}");
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
            System.Diagnostics.Debug.WriteLine($"🔔 Настройка уведомлений изменена: TopMost = {isOnTop}");
        }

        #endregion
    }

    internal class AuthData
    {
        [JsonProperty("token")]
        public string token { get; set; }

        [JsonProperty("viewerId")]
        public long? viewerId { get; set; }

        [JsonProperty("deviceId")]
        public string deviceId { get; set; }

        [JsonProperty("savedAt")]
        public string savedAt { get; set; }
    }
}