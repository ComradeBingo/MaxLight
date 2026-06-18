using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Linq;

namespace MaxLight
{
    public class SettingsForm : Form
    {
        private Panel headerPanel;
        private Label lblTitle;
        private CheckBox chkAutoStart;
        private CheckBox chkNotificationsOnTop;
        private Button btnPinSettings;
        private Button btnLogout;
        private Button btnAbout;
        private Button btnClose;

        // Элементы прокси
        private GroupBox grpProxy;
        private CheckBox chkProxyEnabled;
        private Label lblProxyServer;
        private TextBox txtProxyServer;
        private Label lblProxyPort;
        private NumericUpDown numProxyPort;
        private Button btnApplyProxy;

        // События для связи с Form1
        public event Action AutoStartToggled;
        public event Action<bool> NotificationsOnTopToggled;
        public event Action PinSettingsClicked;
        public event Action LogoutClicked;
        public event Action AboutClicked;
        public event Action ProxySettingsChanged;

        // Флаг portable режима
        private bool _isPortable;

        // Сохраняем исходные настройки прокси при открытии
        private ConfigManager.ProxySettings _originalProxySettings;

        public SettingsForm()
        {
            // Определяем portable режим
            _isPortable = IsPortableMode();

            InitializeForm();
            SetupModernStyle();
            LoadAutoStartState();
            LoadNotificationsOnTopState();
            LoadProxySettings();

            // Если portable - отключаем автозапуск
            if (_isPortable)
            {
                chkAutoStart.Enabled = false;
                chkAutoStart.Checked = false;
                chkAutoStart.Text = "Автоматический запуск\nнедоступен в portable-версии";
                chkAutoStart.ForeColor = Color.Gray;
            }
        }

        private bool IsPortableMode()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase));
        }

        private void InitializeForm()
        {
            this.Text = "Настройки Max Light";
            this.Size = new Size(750, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(750, 480);
            this.MaximumSize = new Size(750, 480);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            // Верхняя панель
            headerPanel = new Panel
            {
                BackColor = Color.FromArgb(52, 73, 94),
                Height = 70,
                Dock = DockStyle.Top
            };

            // Заголовок
            lblTitle = new Label
            {
                Text = "Настройки",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 22),
                AutoSize = true
            };

            // ===== ЛЕВАЯ КОЛОНКА =====
            int leftColumnX = 30;
            int rightColumnX = 390;
            int rowY = 100;
            int rowSpacing = 45;

            // ===== СЕКЦИЯ 1: АВТОЗАПУСК (ЛЕВАЯ КОЛОНКА) =====
            chkAutoStart = new CheckBox
            {
                Text = "Автоматически запускать\nпри входе в Windows",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX, rowY),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            chkAutoStart.CheckedChanged += (s, e) => AutoStartToggled?.Invoke();

            // ===== СЕКЦИЯ 2: УВЕДОМЛЕНИЯ (ЛЕВАЯ КОЛОНКА) =====
            rowY += rowSpacing;
            Label lblNotificationsSection = new Label
            {
                Text = "🔔 Уведомления",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX, rowY),
                AutoSize = true
            };

            rowY += 28;
            chkNotificationsOnTop = new CheckBox
            {
                Text = "Показывать уведомления\nповерх всех окон",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX + 20, rowY),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            chkNotificationsOnTop.CheckedChanged += (s, e) => NotificationsOnTopToggled?.Invoke(chkNotificationsOnTop.Checked);

            // ===== СЕКЦИЯ 3: БЕЗОПАСНОСТЬ И АККАУНТ (ЛЕВАЯ КОЛОНКА) =====
            rowY += rowSpacing + 10;
            Label lblSecuritySection = new Label
            {
                Text = "🔒 Безопасность и аккаунт",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX, rowY),
                AutoSize = true
            };

            rowY += 28;
            btnPinSettings = new Button
            {
                Text = "🔑 Управление PIN-кодом",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Size = new Size(220, 35),
                Location = new Point(leftColumnX + 20, rowY),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnPinSettings.FlatAppearance.BorderSize = 0;
            btnPinSettings.Click += (s, e) => PinSettingsClicked?.Invoke();

            rowY += 45;
            btnLogout = new Button
            {
                Text = "🚪 Выйти из аккаунта",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                Size = new Size(220, 35),
                Location = new Point(leftColumnX + 20, rowY),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) => LogoutClicked?.Invoke();

            // ===== СЕКЦИЯ 4: ПРОКСИ (ПРАВАЯ КОЛОНКА) =====
            int rightRowY = 100;

            Label lblProxySection = new Label
            {
                Text = "🌐 Настройки прокси",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(rightColumnX, rightRowY),
                AutoSize = true
            };

            rightRowY += 30;
            grpProxy = new GroupBox
            {
                Location = new Point(rightColumnX, rightRowY),
                Size = new Size(320, 125),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };

            chkProxyEnabled = new CheckBox
            {
                Text = "Использовать прокси-сервер",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(15, 15),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            chkProxyEnabled.CheckedChanged += (s, e) =>
            {
                bool enabled = chkProxyEnabled.Checked;
                txtProxyServer.Enabled = enabled;
                numProxyPort.Enabled = enabled;
                btnApplyProxy.Enabled = true;
            };

            lblProxyServer = new Label
            {
                Text = "Сервер:",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(15, 48),
                AutoSize = true
            };

            txtProxyServer = new TextBox
            {
                Location = new Point(85, 45),
                Size = new Size(190, 25),
                Font = new Font("Segoe UI", 9),
                Enabled = false
            };
            txtProxyServer.TextChanged += (s, e) => btnApplyProxy.Enabled = true;

            lblProxyPort = new Label
            {
                Text = "Порт:",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(15, 80),
                AutoSize = true
            };

            numProxyPort = new NumericUpDown
            {
                Location = new Point(85, 77),
                Size = new Size(80, 25),
                Font = new Font("Segoe UI", 9),
                Minimum = 1,
                Maximum = 65535,
                Value = 8080,
                Enabled = false
            };
            numProxyPort.ValueChanged += (s, e) => btnApplyProxy.Enabled = true;

            btnApplyProxy = new Button
            {
                Text = "Применить",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Size = new Size(90, 27),
                Location = new Point(190, 75),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnApplyProxy.FlatAppearance.BorderSize = 0;
            btnApplyProxy.Click += (s, e) =>
            {
                ApplyProxySettings();
            };

            grpProxy.Controls.Add(chkProxyEnabled);
            grpProxy.Controls.Add(lblProxyServer);
            grpProxy.Controls.Add(txtProxyServer);
            grpProxy.Controls.Add(lblProxyPort);
            grpProxy.Controls.Add(numProxyPort);
            grpProxy.Controls.Add(btnApplyProxy);

            // ===== КНОПКА "О ПРОГРАММЕ" (ПРАВАЯ КОЛОНКА) =====
            rightRowY += 170;
            btnAbout = new Button
            {
                Text = "ℹ️ О программе",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                Size = new Size(220, 35),
                Location = new Point(rightColumnX + 50, rightRowY),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnAbout.FlatAppearance.BorderSize = 0;
            btnAbout.Click += (s, e) => AboutClicked?.Invoke();

            // Кнопка закрытия (внизу по центру)
            btnClose = new Button
            {
                Text = "Закрыть",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Size = new Size(120, 38),
                Location = new Point((this.ClientSize.Width - 120) / 2, 415),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            // Добавляем элементы
            headerPanel.Controls.Add(lblTitle);
            this.Controls.Add(headerPanel);
            this.Controls.Add(chkAutoStart);
            this.Controls.Add(lblNotificationsSection);
            this.Controls.Add(chkNotificationsOnTop);
            this.Controls.Add(lblSecuritySection);
            this.Controls.Add(btnPinSettings);
            this.Controls.Add(btnLogout);
            this.Controls.Add(lblProxySection);
            this.Controls.Add(grpProxy);
            this.Controls.Add(btnAbout);
            this.Controls.Add(btnClose);
        }

        private void SetupModernStyle()
        {
            this.Paint += (s, e) =>
            {
                GraphicsPath path = new GraphicsPath();
                int radius = 15;
                Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);

                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                this.Region = new Region(path);
            };
        }

        private bool IsAutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
            {
                return key?.GetValue("MaxLight") != null;
            }
        }

        private void LoadAutoStartState()
        {
            if (_isPortable)
            {
                chkAutoStart.Checked = false;
                return;
            }

            chkAutoStart.Checked = IsAutoStartEnabled();
        }

        private void LoadNotificationsOnTopState()
        {
            try
            {
                bool isOnTop = ConfigManager.GetNotificationsOnTop();
                chkNotificationsOnTop.Checked = isOnTop;
            }
            catch
            {
                chkNotificationsOnTop.Checked = true;
            }
        }

        private void LoadProxySettings()
        {
            try
            {
                var proxy = ConfigManager.GetProxySettings();
                _originalProxySettings = proxy != null ? new ConfigManager.ProxySettings
                {
                    Enabled = proxy.Enabled,
                    Server = proxy.Server,
                    Port = proxy.Port
                } : null;

                if (proxy != null)
                {
                    chkProxyEnabled.Checked = proxy.Enabled;
                    txtProxyServer.Text = proxy.Server ?? "";
                    numProxyPort.Value = proxy.Port > 0 ? proxy.Port : 8080;
                }
                else
                {
                    chkProxyEnabled.Checked = false;
                    txtProxyServer.Text = "";
                    numProxyPort.Value = 8080;
                }

                // Обновляем состояние полей
                txtProxyServer.Enabled = chkProxyEnabled.Checked;
                numProxyPort.Enabled = chkProxyEnabled.Checked;
                btnApplyProxy.Enabled = false;
            }
            catch
            {
                chkProxyEnabled.Checked = false;
                txtProxyServer.Text = "";
                numProxyPort.Value = 8080;
                txtProxyServer.Enabled = false;
                numProxyPort.Enabled = false;
                btnApplyProxy.Enabled = false;
                _originalProxySettings = null;
            }
        }

        private bool HasProxySettingsChanged()
        {
            bool currentEnabled = chkProxyEnabled.Checked;
            string currentServer = txtProxyServer.Text.Trim();
            int currentPort = (int)numProxyPort.Value;

            if (_originalProxySettings == null)
            {
                return currentEnabled || !string.IsNullOrEmpty(currentServer) || currentPort != 8080;
            }

            return _originalProxySettings.Enabled != currentEnabled ||
                   _originalProxySettings.Server != currentServer ||
                   _originalProxySettings.Port != currentPort;
        }

        private void ApplyProxySettings()
        {
            try
            {
                // Проверяем, изменились ли настройки
                if (!HasProxySettingsChanged())
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ Настройки прокси не изменились");
                    this.Close();
                    return;
                }

                bool enabled = chkProxyEnabled.Checked;
                string server = txtProxyServer.Text.Trim();
                int port = (int)numProxyPort.Value;

                // ВАЛИДАЦИЯ: если прокси включен, но сервер пустой или порт 0 - отключаем
                if (enabled && (string.IsNullOrEmpty(server) || port <= 0))
                {
                    var result = MessageBox.Show(
                        "Для использования прокси необходимо указать сервер и порт.\n" +
                        "Отключить прокси?",
                        "Неверные настройки прокси",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Yes)
                    {
                        enabled = false;
                        chkProxyEnabled.Checked = false;
                        txtProxyServer.Enabled = false;
                        numProxyPort.Enabled = false;
                    }
                    else
                    {
                        return; // Не сохраняем, остаемся в настройках
                    }
                }

                // Сохраняем настройки
                ConfigManager.SaveProxySettings(enabled, server, port);

                System.Diagnostics.Debug.WriteLine($"🌐 Настройки прокси сохранены: {enabled}");

                // Если настройки изменились и прокси включен - перезапускаем
                ProxySettingsChanged?.Invoke();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек прокси: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void SetAutoStartChecked(bool enabled)
        {
            if (_isPortable) return;
            chkAutoStart.Checked = enabled;
        }

        public void SetNotificationsOnTopChecked(bool enabled)
        {
            chkNotificationsOnTop.Checked = enabled;
        }

        public void SetProxySettings(bool enabled, string server, int port)
        {
            chkProxyEnabled.Checked = enabled;
            txtProxyServer.Text = server;
            numProxyPort.Value = port;
            btnApplyProxy.Enabled = false;
        }
    }
}