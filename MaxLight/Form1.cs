using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace MaxLight
{
    public partial class Form1 : Form
    {
        // ========== ПОЛЯ ==========
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

        private bool _tokenParserActive = false;
        private bool _authRestored = false;

        private Timer notificationFlashTimer;
        private bool isAttentionRequired = false;
        private const int ATTENTION_TIMEOUT_MS = 5000;
        private PageModifier _pageModifier;
        private string _pendingDownloadPath;

        private int _unreadCount = 0;
        private Icon _normalIcon;
        private Icon _unreadIcon;

        private bool _isPortable;
        private EventWaitHandle _activateEvent;
        private CancellationTokenSource _cts;

        private bool _isWindowActive = true;
        private DateTime _lastActivityCheck = DateTime.Now;
        private Timer _stateCheckTimer;

        private Screen _currentScreen;

        private readonly string[] _trackingKeywords = new[]
        {
            "analytics", "apptracer", "perf/", "sdk-api",
            "adsystem", "crashtoken", "crash", "track"
        };

        // ===== DPI МАСШТАБИРОВАНИЕ =====
        private float _currentScale = 1.0f;
        private const int BASE_WIDTH = 1050;
        private const int BASE_HEIGHT = 800;

        // ========== КОНСТРУКТОР ==========
        public Form1() : this(null) { }

        public Form1(EventWaitHandle activateEvent)
        {
            _activateEvent = activateEvent;
            _isPortable = IsPortableMode();

            // ===== ВКЛЮЧАЕМ ПОДДЕРЖКУ DPI =====
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.DpiChanged += Form1_DpiChanged;

            if (!CheckPinOnStartup()) Environment.Exit(0);

            InitializeForm();

            // Применить масштаб для стартового DPI
            float initialScale = this.DeviceDpi / 96f;
            if (Math.Abs(initialScale - 1f) > 0.01f)
                ApplyDpiScale(initialScale);

            LoadWindowState();
            CreateTrayIcon();
            LoadIcons();
            CreateTitleBar();
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

            SetupWindowStateTracking();

            this.Shown += (s, e) =>
            {
                try
                {
                    var state = ConfigManager.GetWindowState();
                    if (state != null && state.Left != -1 && state.Top != -1 &&
                        this.WindowState != FormWindowState.Maximized)
                    {
                        this.Location = new Point(state.Left, state.Top);
                    }
                    UpdateCurrentScreen();
                }
                catch { }
            };

            if (_activateEvent != null)
            {
                _cts = new CancellationTokenSource();
                Task.Run(() => WaitForActivationSignal(_cts.Token));
            }
        }

        // ===== ОБРАБОТЧИК ИЗМЕНЕНИЯ DPI =====
        private void Form1_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            _currentScale = e.DeviceDpiNew / 96f;
            ApplyDpiScale(_currentScale);
        }

        private void ApplyDpiScale(float scale)
        {
            _currentScale = scale;

            // Снять ограничения перед ресайзом
            this.MinimumSize = Size.Empty;
            this.MaximumSize = Size.Empty;

            // Обновляем размер формы
            int newWidth = (int)(BASE_WIDTH * scale);
            int newHeight = (int)(BASE_HEIGHT * scale);
            this.Size = new Size(newWidth, newHeight);

            // Устанавливаем новые ограничения
            var screen = Screen.FromControl(this).Bounds;
            this.MinimumSize = new Size((int)(640 * scale), (int)(480 * scale));
            this.MaximumSize = new Size(screen.Width, screen.Height);

            // Обновляем шрифты
            this.Font = new Font("Segoe UI", 9f * scale, FontStyle.Regular, GraphicsUnit.Point);

            // Обновляем размер WebView если он есть
            if (webView != null)
            {
                UpdateWebViewSize();
            }
        }

        private void UpdateWebViewSize()
        {
            if (webView == null) return;

            int titleBarHeight = titleBar?.Height ?? (int)(40 * _currentScale);
            int borderSize = (int)(2 * _currentScale);

            webView.Location = new Point(borderSize, titleBarHeight + borderSize);
            webView.Size = new Size(
                this.ClientSize.Width - (borderSize * 2),
                this.ClientSize.Height - titleBarHeight - (borderSize * 2)
            );
        }

        // ========== Событие клика по уведомлению ==========
        public event EventHandler UpdateNotificationClicked;

        // ========== Показать уведомление в TitleBar ==========
        public void ShowUpdateNotification(string version)
        {
            if (titleBar != null)
            {
                titleBar.UpdateNotificationClick -= OnTitleBarUpdateClick;
                titleBar.UpdateNotificationClick += OnTitleBarUpdateClick;
                titleBar.ShowUpdateNotification(version);
            }
        }

        // ========== Скрыть уведомление ==========
        public void HideUpdateNotification()
        {
            titleBar?.HideUpdateNotification();
        }

        // ========== Обработчик клика по уведомлению в TitleBar ==========
        private void OnTitleBarUpdateClick(object sender, EventArgs e)
        {
            UpdateNotificationClicked?.Invoke(this, EventArgs.Empty);
        }

        // ========== Проверка, есть ли обновление ==========
        public bool HasUpdate => titleBar?.HasUpdate ?? false;
        public string UpdateVersion => titleBar?.UpdateVersion ?? "";

        // ========== ОСНОВНЫЕ МЕТОДЫ ==========

        private void InitializeForm()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                  ControlStyles.UserPaint |
                  ControlStyles.DoubleBuffer |
                  ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();

            var screen = Screen.PrimaryScreen.Bounds;
            this.Size = new Size(BASE_WIDTH, BASE_HEIGHT);
            this.BackColor = Color.FromArgb(66, 75, 121);
            this.MinimumSize = new Size(640, 480);
            this.MaximumSize = new Size(screen.Width, screen.Height);
            this.ShowInTaskbar = true;

            string iconPath = Path.Combine(Application.StartupPath, "app.ico");
            if (File.Exists(iconPath))
            {
                try { this.Icon = new Icon(iconPath); } catch { }
            }
        }

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
            var settingsForm = new SettingsForm(_isPortable);
            settingsForm.AutoStartToggled += ToggleAutoStart;
            settingsForm.NotificationsOnTopToggled += ToggleNotificationsOnTop;
            settingsForm.PinSettingsClicked += ShowPinSettings;
            settingsForm.LogoutClicked += async () => await Logout();
            settingsForm.AboutClicked += ShowAbout;
            settingsForm.ProxySettingsChanged += () =>
            {
                MessageBox.Show(
                    "Настройки прокси применены. Для полного применения требуется перезапуск программы.",
                    "Перезапуск",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Application.Restart();
                Environment.Exit(0);
            };
            settingsForm.DownloadPathChanged += async (path) =>
            {
                await UpdateDownloadFolderPath();
            };
            settingsForm.AskEveryTimeToggled += () =>
            {
                System.Diagnostics.Debug.WriteLine($"🔔 Настройка 'спрашивать каждый раз' изменена: {ConfigManager.AskEveryTime()}");
            };
            settingsForm.CheckUpdatesClicked += OnCheckUpdatesClickedAsync;

            settingsForm.ShowDialog(this);
        }

        private async Task<bool> OnCheckUpdatesClickedAsync()
        {
            try
            {
                await Program.ForceCheckUpdatesAsync();
                return titleBar.HasUpdate;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
                throw;
            }
        }

        private void ShowAbout()
        {
            new AboutForm().ShowDialog();
        }

        private bool IsPortableMode()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
                return true;

            string portableFile = Path.Combine(Application.StartupPath, ".portable");
            if (File.Exists(portableFile))
                return true;

            string parentFolder = Path.GetFullPath(Path.Combine(Application.StartupPath, ".."));
            string parentPortableFile = Path.Combine(parentFolder, ".portable");
            if (File.Exists(parentPortableFile))
                return true;

            return false;
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
                _cts?.Cancel();
                _cts?.Dispose();
                _activateEvent?.Dispose();

                _stateCheckTimer?.Stop();
                _stateCheckTimer?.Dispose();

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

                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                    webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
                }

                CancelAttentionTimer();
            }
        }

        // ========== АВТОЗАПУСК ==========

        private void ToggleAutoStart()
        {
            if (_isPortable) return;
            bool enabled = !IsAutoStartEnabled();
            SetAutoStart(enabled);
        }

        private bool IsAutoStartEnabled()
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                return key?.GetValue("MaxLight") != null;
            }
        }

        private void SetAutoStart(bool enabled)
        {
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (enabled)
                    key.SetValue("MaxLight", Application.ExecutablePath);
                else
                    key.DeleteValue("MaxLight", false);
            }
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