using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Linq;
using System.Threading.Tasks;

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
        private Button btnCheckUpdates;  // ← НОВО
        private Label lblUpdateStatus;   // ← НОВО: для отображения статуса
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
        public event Func<Task<bool>> CheckUpdatesClicked;  // ← ИЗМЕНЕНО: теперь возвращает Task<bool>

        private bool _isPortable;
        private ConfigManager.ProxySettings _originalProxySettings;
        private bool _isChecking = false;

        public SettingsForm(bool isPortable = false)
        {
            _isPortable = isPortable;

            InitializeForm();

            LoadAutoStartState();
            LoadNotificationsOnTopState();
            LoadProxySettings();

            if (_isPortable)
            {
                chkAutoStart.Enabled = false;
                chkAutoStart.Checked = false;
                chkAutoStart.Text = "Автоматический запуск\nнедоступен в portable-версии";
                chkAutoStart.ForeColor = Color.Gray;
            }
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
                BackColor = Color.FromArgb(66, 75, 121),
                Height = 48,
                Dock = DockStyle.Top
            };

            lblTitle = new Label
            {
                Text = "Настройки",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(12, 12),
                AutoSize = true
            };

            int leftColumnX = 30;
            int rightColumnX = 390;
            int rowY = 100;
            int rowSpacing = 35;

            // ===== Общие настройки =====
            rowY += 5;
            CreateSectionHeader("\uE713", "ОБЩИЕ", new Point(leftColumnX + 60, rowY));

            // ===== АВТОЗАПУСК =====
            chkAutoStart = new CheckBox
            {
                Text = "Автоматически запускать\nпри входе в Windows",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX + 30, rowY += rowSpacing),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            chkAutoStart.CheckedChanged += (s, e) => AutoStartToggled?.Invoke();

            rowY += 28;
            chkNotificationsOnTop = new CheckBox
            {
                Text = "Показывать уведомления\nповерх всех окон",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX + 30, rowY += rowSpacing),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            chkNotificationsOnTop.CheckedChanged += (s, e) => NotificationsOnTopToggled?.Invoke(chkNotificationsOnTop.Checked);

            // ===== БЕЗОПАСНОСТЬ =====
            rowY += rowSpacing + 35;
            CreateSectionHeader("\uE72E", "Безопасность и аккаунт", new Point(leftColumnX + 28, rowY));

            rowY += 28;
            btnPinSettings = CreateStyledButton(
                "\uE72E",
                "Управление PIN-кодом",
                Color.FromArgb(66, 75, 121),
                new Size(220, 35),
                new Point(leftColumnX + 20, rowY)
            );
            btnPinSettings.Click += (s, e) => PinSettingsClicked?.Invoke();

            rowY += 45;
            btnLogout = CreateStyledButton(
                "\uE711",
                "Выйти из аккаунта",
                Color.FromArgb(231, 76, 60),
                new Size(220, 35),
                new Point(leftColumnX + 20, rowY)
            );
            btnLogout.Click += (s, e) => LogoutClicked?.Invoke();

            // ===== ПРОКСИ =====
            int rightRowY = 100;
            CreateSectionHeader("\uE774", "Настройки прокси", new Point(rightColumnX + 48, rightRowY));

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
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Size = new Size(90, 27),
                Location = new Point(185, 75),
                Cursor = Cursors.Hand,
                Enabled = false,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnApplyProxy.Click += (s, e) => ApplyProxySettings();

            // Эффекты при наведении
            btnApplyProxy.MouseEnter += (s, e) =>
            {
                btnApplyProxy.BackColor = ControlPaint.Light(Color.FromArgb(46, 204, 113), 0.3f);
            };
            btnApplyProxy.MouseLeave += (s, e) =>
            {
                btnApplyProxy.BackColor = Color.FromArgb(46, 204, 113);
            };

            btnApplyProxy.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btnApplyProxy.BackColor = ControlPaint.Dark(Color.FromArgb(46, 204, 113), 0.2f);
            };
            btnApplyProxy.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btnApplyProxy.BackColor = Color.FromArgb(46, 204, 113);
            };

            grpProxy.Controls.Add(chkProxyEnabled);
            grpProxy.Controls.Add(lblProxyServer);
            grpProxy.Controls.Add(txtProxyServer);
            grpProxy.Controls.Add(lblProxyPort);
            grpProxy.Controls.Add(numProxyPort);
            grpProxy.Controls.Add(btnApplyProxy);

            // ===== О ПРОГРАММЕ и ПРОВЕРКА ОБНОВЛЕНИЙ =====
            rightRowY += 170;

            //  КНОПКА "ПОИСК ОБНОВЛЕНИЙ" 
            btnCheckUpdates = CreateStyledButton(
                "\uE896",  // Иконка обновления
                "Проверка обновлений",
                Color.FromArgb(86, 86, 157), 
                new Size(220, 35),
                new Point(rightColumnX + 50, rightRowY)
            );
            btnCheckUpdates.Click += async (s, e) => await OnCheckUpdatesClicked();

            // СТАТУС ОБНОВЛЕНИЯ (показывается вместо кнопки) 
            lblUpdateStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(rightColumnX + 55, rightRowY + 8),
                Size = new Size(210, 35),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false,
                BackColor = Color.Transparent
            };

            // КНОПКА "О ПРОГРАММЕ" (под кнопкой проверки обновлений) 
            rightRowY += 45;
            btnAbout = CreateStyledButton(
                "\uE946",
                "О ПРОГРАММЕ",
                Color.FromArgb(66, 75, 121),
                new Size(220, 35),
                new Point(rightColumnX + 50, rightRowY)
            );
            btnAbout.Click += (s, e) => AboutClicked?.Invoke();

            // ===== КНОПКА ЗАКРЫТИЯ =====
            btnClose = new Button
            {
                Text = "ЗАКРЫТЬ",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Size = new Size(120, 38),
                Location = new Point((this.ClientSize.Width - 120) / 2, 415),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnClose.Click += (s, e) => this.Close();

            // Добавляем эффекты при наведении
            btnClose.MouseEnter += (s, e) =>
            {
                btnClose.BackColor = ControlPaint.Light(Color.FromArgb(52, 152, 219), 0.3f);
            };
            btnClose.MouseLeave += (s, e) =>
            {
                btnClose.BackColor = Color.FromArgb(52, 152, 219);
            };

            btnClose.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btnClose.BackColor = ControlPaint.Dark(Color.FromArgb(52, 152, 219), 0.2f);
            };
            btnClose.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btnClose.BackColor = Color.FromArgb(52, 152, 219);
            };

            headerPanel.Controls.Add(lblTitle);
            this.Controls.Add(headerPanel);
            this.Controls.Add(chkAutoStart);
            this.Controls.Add(chkNotificationsOnTop);
            this.Controls.Add(btnPinSettings);
            this.Controls.Add(btnLogout);
            this.Controls.Add(grpProxy);
            this.Controls.Add(btnCheckUpdates);
            this.Controls.Add(lblUpdateStatus);  // ← НОВО
            this.Controls.Add(btnAbout);
            this.Controls.Add(btnClose);
        }

        // ===== НОВЫЙ МЕТОД: Обработчик клика по кнопке проверки =====
        private async Task OnCheckUpdatesClicked()
        {
            if (_isChecking) return;
            _isChecking = true;

            // Отключаем кнопку и показываем статус "Проверка..."
            btnCheckUpdates.Visible = false;
            lblUpdateStatus.Visible = true;
            lblUpdateStatus.Text = "⏳ Проверка...";
            lblUpdateStatus.ForeColor = Color.FromArgb(52, 152, 219);
            this.Refresh();

            try
            {
                // Вызываем проверку
                bool hasUpdate = false;
                if (CheckUpdatesClicked != null)
                {
                    hasUpdate = await CheckUpdatesClicked.Invoke();
                }

                // Показываем результат
                if (hasUpdate)
                {
                    lblUpdateStatus.Text = "✅ Обновление найдено!";
                    lblUpdateStatus.ForeColor = Color.FromArgb(46, 204, 113);

                    // Через 3 секунды возвращаем кнопку
                    await Task.Delay(3000);
                }
                else
                {
                    lblUpdateStatus.Text = "✅ Обновлений нет";
                    lblUpdateStatus.ForeColor = Color.FromArgb(52, 152, 219);

                    // Через 3 секунды возвращаем кнопку
                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                lblUpdateStatus.Text = $"❌ Ошибка: {ex.Message}";
                lblUpdateStatus.ForeColor = Color.FromArgb(231, 76, 60);

                // Через 5 секунд возвращаем кнопку
                await Task.Delay(5000);
            }
            finally
            {
                // Возвращаем кнопку
                lblUpdateStatus.Visible = false;
                btnCheckUpdates.Visible = true;
                _isChecking = false;
            }
        }

        // ===== МЕТОД ДЛЯ ЗАГОЛОВКОВ СЕКЦИЙ =====
        private void CreateSectionHeader(string iconChar, string text, Point location)
        {
            Label iconLabel = new Label
            {
                Text = iconChar,
                Font = new Font("Segoe MDL2 Assets", 14, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(location.X, location.Y),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label textLabel = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(location.X + 28, location.Y),
                TextAlign = ContentAlignment.MiddleLeft
            };

            this.Controls.Add(iconLabel);
            this.Controls.Add(textLabel);
        }

        // ===== МЕТОД ДЛЯ КНОПОК =====
        private Button CreateStyledButton(string iconChar, string text, Color backColor, Size size, Point location, float fontSize = 10)
        {
            var btn = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = backColor,
                ForeColor = Color.White,
                Size = size,
                Location = location,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                Text = ""
            };

            var iconLabel = new Label
            {
                Text = iconChar,
                Font = new Font("Segoe MDL2 Assets", 14, FontStyle.Regular),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(12, (size.Height - 20) / 2),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };

            var textLabel = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(40, (size.Height - 20) / 2),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };

            btn.Controls.Add(iconLabel);
            btn.Controls.Add(textLabel);

            iconLabel.Click += (s, e) => btn.PerformClick();
            textLabel.Click += (s, e) => btn.PerformClick();

            btn.MouseEnter += (s, e) =>
            {
                btn.BackColor = ControlPaint.Light(backColor, 0.3f);
            };
            btn.MouseLeave += (s, e) =>
            {
                btn.BackColor = backColor;
            };

            btn.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btn.BackColor = ControlPaint.Dark(backColor, 0.2f);
            };
            btn.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btn.BackColor = backColor;
            };

            return btn;
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
                if (!HasProxySettingsChanged())
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ Настройки прокси не изменились");
                    this.Close();
                    return;
                }

                bool enabled = chkProxyEnabled.Checked;
                string server = txtProxyServer.Text.Trim();
                int port = (int)numProxyPort.Value;

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
                        return;
                    }
                }

                ConfigManager.SaveProxySettings(enabled, server, port);

                System.Diagnostics.Debug.WriteLine($"🌐 Настройки прокси сохранены: {enabled}");

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