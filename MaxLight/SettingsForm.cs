using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace MaxLight
{
    public class SettingsForm : Form
    {
        // Шапка
        private Panel headerPanel;
        private Label lblTitle;

        // Левая колонка - Общие
        private GroupBox grpGeneral;
        private CheckBox chkAutoStart;
        private CheckBox chkNotificationsOnTop;

        // Левая колонка - Безопасность
        private GroupBox grpSecurity;
        private Button btnPinSettings;
        private Button btnLogout;
        private Button btnCheckUpdates;
        private Button btnAbout;
        private Label lblUpdateStatus;

        // Правая колонка - Прокси
        private GroupBox grpProxy;
        private CheckBox chkProxyEnabled;
        private TextBox txtProxyServer;
        private NumericUpDown numProxyPort;
        private Button btnApplyProxy;

        // Правая колонка - Загрузки
        private GroupBox grpDownloads;
        private TextBox txtDownloadPath;
        private Button btnBrowseDownloadPath;
        private Button btnResetDownloadPath;
        private CheckBox chkAskEveryTime;

        // Нижняя панель
        private Panel bottomPanel;
        private Button btnClose;

        // Контейнер с прокруткой
        private Panel scrollContainer;
        private TableLayoutPanel mainTable;

        public event Action<string> DownloadPathChanged;
        public event Action AskEveryTimeToggled;
        public event Action AutoStartToggled;
        public event Action<bool> NotificationsOnTopToggled;
        public event Action PinSettingsClicked;
        public event Action LogoutClicked;
        public event Action AboutClicked;
        public event Action ProxySettingsChanged;
        public event Func<Task<bool>> CheckUpdatesClicked;

        private bool _isPortable;
        private bool _isChecking = false;

        public SettingsForm(bool isPortable = false)
        {
            _isPortable = isPortable;

            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScroll = true;

            

            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(245, 246, 250);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            InitializeForm();
            LoadSettings();

            if (_isPortable)
            {
                chkAutoStart.Enabled = false;
                chkAutoStart.Checked = false;
                chkAutoStart.Text = "Автозапуск (недоступен)";
            }
        }

        private float _currentScale = 1f;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // Получаем размер экрана
            var screenBounds = Screen.PrimaryScreen.Bounds;
            float screenWidth = screenBounds.Width;

            // Определяем коэффициент масштабирования для элементов
            float elementScale = 1f;
            int windowWidth;
            int windowHeight;

            if (screenWidth >= 3840) // 4K
            {
                windowWidth = LogicalToDeviceUnits(1800);
                windowHeight = LogicalToDeviceUnits(1100);
                elementScale = 1.0f;
            }
            else if (screenWidth >= 2560) // 1440p
            {
                windowWidth = LogicalToDeviceUnits(1400);
                windowHeight = LogicalToDeviceUnits(850);
                elementScale = 0.85f;
            }
            else // 1080p и меньше
            {
                windowWidth = LogicalToDeviceUnits(1000);  
                windowHeight = LogicalToDeviceUnits(750);  
                elementScale = 0.7f;  // Уменьшаем элементы на 20%
            }

            this.MinimumSize = new Size(windowWidth, windowHeight);
            this.ClientSize = new Size(
                (int)(windowWidth * 1.15f),
                (int)(windowHeight * 1.1f)
            );

            // Применяем масштабирование к элементам
            if (elementScale < 1f)
            {
                ScaleElementSizes(elementScale);
            }
        }

        private void ScaleElementSizes(float scale)
        {
            // ❌ НЕ ТРОГАЕМ ШРИФТЫ - оставляем как есть

            // 1. Уменьшаем MinimumSize для CheckBox
            int checkBoxMinWidth = (int)(550 * scale);
            if (checkBoxMinWidth < 380) checkBoxMinWidth = 380;

            chkAutoStart.MinimumSize = new Size(checkBoxMinWidth, 30);
            chkNotificationsOnTop.MinimumSize = new Size(checkBoxMinWidth, 30);
            chkProxyEnabled.MinimumSize = new Size((int)(400 * scale), 30);
            chkAskEveryTime.MinimumSize = new Size((int)(400 * scale), 30);

            // 2. Уменьшаем отступы в GroupBox
            int pad = (int)(15 * scale);
            if (pad < 8) pad = 8;

            grpGeneral.Padding = new Padding(pad, (int)(35 * scale), pad, (int)(15 * scale));
            grpSecurity.Padding = new Padding(pad, (int)(35 * scale), pad, (int)(15 * scale));
            grpProxy.Padding = new Padding(pad, (int)(35 * scale), pad, (int)(15 * scale));
            grpDownloads.Padding = new Padding(pad, (int)(35 * scale), pad, (int)(15 * scale));

            // 3. Уменьшаем отступы в FlowLayoutPanel
            int flowPad = (int)(10 * scale);
            if (flowPad < 5) flowPad = 5;

            foreach (Control ctrl in grpGeneral.Controls)
            {
                if (ctrl is FlowLayoutPanel flow)
                {
                    flow.Padding = new Padding(flowPad, flowPad, flowPad, flowPad);
                }
            }

            // 4. Уменьшаем кнопки (Padding)
            int btnPad = (int)(15 * scale);
            if (btnPad < 8) btnPad = 8;

            btnPinSettings.Padding = new Padding(btnPad, (int)(10 * scale), btnPad, (int)(10 * scale));
            btnLogout.Padding = new Padding(btnPad, (int)(10 * scale), btnPad, (int)(10 * scale));
            btnCheckUpdates.Padding = new Padding(btnPad, (int)(10 * scale), btnPad, (int)(10 * scale));
            btnAbout.Padding = new Padding(btnPad, (int)(10 * scale), btnPad, (int)(10 * scale));

            // 5. Уменьшаем кнопку "ЗАКРЫТЬ"
            int closePad = (int)(40 * scale);
            if (closePad < 25) closePad = 25;
            btnClose.Padding = new Padding(closePad, (int)(12 * scale), closePad, (int)(12 * scale));

            // 6. Уменьшаем кнопки в секции загрузок
            int btnSmallPad = (int)(20 * scale);
            if (btnSmallPad < 12) btnSmallPad = 12;

            btnBrowseDownloadPath.Padding = new Padding(btnSmallPad, (int)(10 * scale), btnSmallPad, (int)(10 * scale));
            btnResetDownloadPath.Padding = new Padding(btnSmallPad, (int)(10 * scale), btnSmallPad, (int)(10 * scale));
            btnApplyProxy.Padding = new Padding(btnSmallPad, (int)(10 * scale), btnSmallPad, (int)(10 * scale));

            // 7. Уменьшаем отступы между элементами
            chkAutoStart.Margin = new Padding(0, 0, 0, (int)(15 * scale));
            chkNotificationsOnTop.Margin = new Padding(0, (int)(10 * scale), 0, 0);
            chkProxyEnabled.Margin = new Padding(0, 0, 0, (int)(10 * scale));

            // 8. Уменьшаем высоту шапки
            int headerHeight = (int)(55 * Math.Max(1, scale));
            headerPanel.Height = headerHeight;
            lblTitle.Location = new Point((int)(25 * scale), (headerHeight - lblTitle.Height) / 2);
        }

        private void InitializeForm()
        {
            // === ШАПКА ===
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(66, 75, 121),
                AutoSize = true,  
                AutoSizeMode = AutoSizeMode.GrowAndShrink,  
                Padding = new Padding(30, 16, 30, 16)
            };

            bool drag = false;
            Point ds = Point.Empty;
            headerPanel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { drag = true; ds = e.Location; } };
            headerPanel.MouseMove += (s, e) => { if (drag) this.Location = new Point(this.Location.X + e.X - ds.X, this.Location.Y + e.Y - ds.Y); };
            headerPanel.MouseUp += (s, e) => drag = false;

            lblTitle = new Label
            {
                Text = "Настройки Max Light",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,  
                Dock = DockStyle.Fill,  
                TextAlign = ContentAlignment.MiddleLeft
            };
            headerPanel.Controls.Add(lblTitle);
            this.Controls.Add(headerPanel);

            // === НИЖНЯЯ ПАНЕЛЬ ===
            bottomPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Bottom,
                BackColor = Color.White
            };

            btnClose = new Button
            {
                Text = "ЗАКРЫТЬ",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(50, 14, 50, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => Close();
            bottomPanel.Controls.Add(btnClose);

            bottomPanel.Resize += (s, e) =>
            {
                btnClose.Location = new Point(
                    (bottomPanel.Width - btnClose.Width) / 2,
                    (bottomPanel.Height - btnClose.Height) / 2
                );
            };
            this.Controls.Add(bottomPanel);

            // === КОНТЕЙНЕР С ПРОКРУТКОЙ ===
            scrollContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(245, 246, 250)
            };
            this.Controls.Add(scrollContainer);

            // === ГЛАВНАЯ ТАБЛИЦА ===
            mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(30, 40, 30, 30)
            };
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            scrollContainer.Controls.Add(mainTable);

            // Левая колонка
            var leftColumn = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 20, 0)
            };
            leftColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            CreateGeneralSection(leftColumn);
            CreateSecuritySection(leftColumn);
            mainTable.Controls.Add(leftColumn, 0, 0);

            // Правая колонка
            var rightColumn = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(20, 0, 0, 0)
            };
            rightColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            rightColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            CreateProxySection(rightColumn);
            CreateDownloadsSection(rightColumn);
            mainTable.Controls.Add(rightColumn, 1, 0);
        }

        private void CreateGeneralSection(TableLayoutPanel parent)
        {
            grpGeneral = new GroupBox
            {
                Text = "ОБЩИЕ НАСТРОЙКИ",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 60, 0, 25),
                Padding = new Padding(25, 45, 25, 30)
            };

            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(15, 10, 15, 10)
            };

            chkAutoStart = new CheckBox
            {
                Text = "Автоматически запускать при входе в Windows",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 25),
                Padding = new Padding(10, 8, 10, 8),
                UseVisualStyleBackColor = true,
                MinimumSize = new Size(550, 35)
            };
            chkAutoStart.CheckedChanged += (s, e) => AutoStartToggled?.Invoke();
            flowLayout.Controls.Add(chkAutoStart);

            chkNotificationsOnTop = new CheckBox
            {
                Text = "Показывать уведомления поверх всех окон",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 20, 0, 0),
                Padding = new Padding(10, 8, 10, 8),
                UseVisualStyleBackColor = true,
                MinimumSize = new Size(550, 35)
            };
            chkNotificationsOnTop.CheckedChanged += (s, e) => NotificationsOnTopToggled?.Invoke(chkNotificationsOnTop.Checked);
            flowLayout.Controls.Add(chkNotificationsOnTop);

            grpGeneral.Controls.Add(flowLayout);
            parent.Controls.Add(grpGeneral, 0, 0);
        }

        private void CreateSecuritySection(TableLayoutPanel parent)
        {
            grpSecurity = new GroupBox
            {
                Text = "БЕЗОПАСНОСТЬ И АККАУНТ",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 25, 0, 0),
                Padding = new Padding(25, 45, 25, 30)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };

            for (int i = 0; i < 6; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            btnPinSettings = CreateButton("🔒 Управление PIN-кодом", Color.FromArgb(66, 75, 121));
            btnPinSettings.Click += (s, e) => PinSettingsClicked?.Invoke();
            layout.Controls.Add(btnPinSettings, 0, 0);

            btnLogout = CreateButton("🚪 Выйти из аккаунта", Color.FromArgb(231, 76, 60));
            btnLogout.Click += (s, e) => LogoutClicked?.Invoke();
            layout.Controls.Add(btnLogout, 0, 1);

            layout.Controls.Add(new Label { AutoSize = false, Height = 15 }, 0, 2);

            btnCheckUpdates = CreateButton("🔄 Проверка обновлений", Color.FromArgb(86, 86, 157));
            btnCheckUpdates.Click += async (s, e) => await OnCheckUpdatesClicked();
            layout.Controls.Add(btnCheckUpdates, 0, 3);

            lblUpdateStatus = new Label
            {
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Margin = new Padding(10, 10, 0, 10),
                Visible = false,
                Padding = new Padding(8)
            };
            layout.Controls.Add(lblUpdateStatus, 0, 4);

            btnAbout = CreateButton("ℹ️ О программе", Color.FromArgb(66, 75, 121));
            btnAbout.Click += (s, e) => AboutClicked?.Invoke();
            btnAbout.Margin = new Padding(0, 10, 0, 0);
            layout.Controls.Add(btnAbout, 0, 5);

            grpSecurity.Controls.Add(layout);
            parent.Controls.Add(grpSecurity, 0, 1);
        }

        private void CreateProxySection(TableLayoutPanel parent)
        {
            grpProxy = new GroupBox
            {
                Text = "НАСТРОЙКИ ПРОКСИ",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 60, 0, 25),
                Padding = new Padding(25, 45, 25, 30)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(15, 10, 15, 10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            chkProxyEnabled = new CheckBox
            {
                Text = "Использовать прокси-сервер",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 20),
                Padding = new Padding(10, 8, 10, 8),
                UseVisualStyleBackColor = true,
                MinimumSize = new Size(400, 35)
            };
            chkProxyEnabled.CheckedChanged += (s, e) =>
            {
                txtProxyServer.Enabled = chkProxyEnabled.Checked;
                numProxyPort.Enabled = chkProxyEnabled.Checked;
            };
            layout.Controls.Add(chkProxyEnabled, 0, 0);

            // Строка Сервер
            var serverRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 10)
            };
            serverRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  
            serverRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var lblServer = new Label
            {
                Text = "Сервер:",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Margin = new Padding(0, 10, 20, 0),
                Padding = new Padding(8),
                 MinimumSize = new Size(80, 30)
            };
            serverRow.Controls.Add(lblServer, 0, 0);

            txtProxyServer = new TextBox
            {
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Enabled = false,
                Dock = DockStyle.Fill,
                Height = 40,
                Margin = new Padding(0),
                MinimumSize = new Size(250, 35)
            };
            txtProxyServer.TextChanged += (s, e) => btnApplyProxy.Enabled = true;
            serverRow.Controls.Add(txtProxyServer, 1, 0);
            layout.Controls.Add(serverRow, 0, 1);

            // Строка Порт + Применить
            var portRow = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 10, 0, 0)
            };
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 150F));
            portRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var lblPort = new Label
            {
                Text = "Порт:",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Margin = new Padding(0, 10, 20, 0),
                Padding = new Padding(8),
                MinimumSize = new Size(80, 30)
            };
            portRow.Controls.Add(lblPort, 0, 0);

            numProxyPort = new NumericUpDown
            {
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Minimum = 1,
                Maximum = 65535,
                Value = 8080,
                Enabled = false,
                Dock = DockStyle.Fill,
                Height = 40,
                MinimumSize = new Size(100, 35)
            };
            numProxyPort.ValueChanged += (s, e) => btnApplyProxy.Enabled = true;
            portRow.Controls.Add(numProxyPort, 1, 0);

            portRow.Controls.Add(new Label { AutoSize = false }, 2, 0);

            btnApplyProxy = new Button
            {
                Text = "Применить",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(35, 12, 35, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Enabled = false,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Right,
                Margin = new Padding(5, 0, 0, 0),
                MinimumSize = new Size(120, 40)
            };
            btnApplyProxy.FlatAppearance.BorderSize = 0;
            btnApplyProxy.Click += (s, e) => ApplyProxySettings();
            portRow.Controls.Add(btnApplyProxy, 3, 0);
            layout.Controls.Add(portRow, 0, 2);

            layout.Controls.Add(new Label { AutoSize = false, Height = 8 }, 0, 3);

            grpProxy.Controls.Add(layout);
            parent.Controls.Add(grpProxy, 0, 0);
        }

        private void CreateDownloadsSection(TableLayoutPanel parent)
        {
            grpDownloads = new GroupBox
            {
                Text = "ПАПКА ЗАГРУЗОК",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 25, 0, 0),
                Padding = new Padding(25, 45, 25, 30)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(15, 10, 15, 10)
            };
            for (int i = 0; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblPath = new Label
            {
                Text = "Путь сохранения:",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(8)
            };
            layout.Controls.Add(lblPath, 0, 0);

            txtDownloadPath = new TextBox
            {
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Height = 40,
                Margin = new Padding(0, 0, 0, 20),
                ForeColor = Color.FromArgb(44, 62, 80),
                BackColor = Color.FromArgb(245, 246, 250),
                MinimumSize = new Size(250, 35)
            };
            layout.Controls.Add(txtDownloadPath, 0, 1);

            var btnRow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 20)
            };

            btnBrowseDownloadPath = new Button
            {
                Text = "Обзор...",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(35, 12, 35, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 20, 0),
                MinimumSize = new Size(120, 40)
            };
            btnBrowseDownloadPath.FlatAppearance.BorderSize = 0;
            btnBrowseDownloadPath.Click += BrowseDownloadPath_Click;
            btnRow.Controls.Add(btnBrowseDownloadPath);

            btnResetDownloadPath = new Button
            {
                Text = "Сброс",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(35, 12, 35, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                MinimumSize = new Size(120, 40)
            };
            btnResetDownloadPath.FlatAppearance.BorderSize = 0;
            btnResetDownloadPath.Click += ResetDownloadPath_Click;
            btnRow.Controls.Add(btnResetDownloadPath);
            layout.Controls.Add(btnRow, 0, 2);

            chkAskEveryTime = new CheckBox
            {
                Text = "Спрашивать каждый раз при скачивании",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                ForeColor = Color.FromArgb(44, 62, 80),
                AutoSize = true,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 10, 0, 0),
                Padding = new Padding(10, 8, 10, 8),
                UseVisualStyleBackColor = true,
                MinimumSize = new Size(450, 35)
            };
            chkAskEveryTime.CheckedChanged += (s, e) =>
            {
                AskEveryTimeToggled?.Invoke();
                ConfigManager.SaveAskEveryTime(chkAskEveryTime.Checked);
            };
            layout.Controls.Add(chkAskEveryTime, 0, 3);

            grpDownloads.Controls.Add(layout);
            parent.Controls.Add(grpDownloads, 0, 1);
        }

        private Button CreateButton(string text, Color color)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(25, 14, 25, 14),
                FlatStyle = FlatStyle.Flat,
                BackColor = color,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 12),
                TextAlign = ContentAlignment.MiddleLeft,
                MinimumSize = new Size(250, 45)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private async Task OnCheckUpdatesClicked()
        {
            if (_isChecking) return;
            _isChecking = true;
            string orig = btnCheckUpdates.Text;
            btnCheckUpdates.Text = "⏳ Проверка...";
            btnCheckUpdates.Enabled = false;
            lblUpdateStatus.Visible = false;
            try
            {
                bool has = CheckUpdatesClicked != null && await CheckUpdatesClicked.Invoke();
                lblUpdateStatus.Text = has ? "✅ Найдено обновление!" : "✅ Обновлений нет";
                lblUpdateStatus.ForeColor = has ? Color.FromArgb(46, 204, 113) : Color.FromArgb(52, 152, 219);
            }
            catch (Exception ex)
            {
                lblUpdateStatus.Text = $"❌ {ex.Message}";
                lblUpdateStatus.ForeColor = Color.FromArgb(231, 76, 60);
            }
            finally
            {
                lblUpdateStatus.Visible = true;
                btnCheckUpdates.Text = orig;
                btnCheckUpdates.Enabled = true;
                _isChecking = false;
            }
        }

        private void LoadSettings()
        {
            LoadAutoStartState();
            LoadNotificationsOnTopState();
            LoadProxySettings();
            LoadDownloadPath();
        }

        private void LoadDownloadPath()
        {
            txtDownloadPath.Text = ConfigManager.GetDownloadPath();
            chkAskEveryTime.Checked = ConfigManager.AskEveryTime();
        }

        private void BrowseDownloadPath_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ConfigManager.SaveDownloadPath(dlg.SelectedPath);
                    txtDownloadPath.Text = dlg.SelectedPath;
                    DownloadPathChanged?.Invoke(dlg.SelectedPath);
                }
            }
        }

        private void ResetDownloadPath_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Сбросить путь?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                ConfigManager.SaveDownloadPath(null);
                txtDownloadPath.Text = ConfigManager.GetDownloadPath();
                DownloadPathChanged?.Invoke(txtDownloadPath.Text);
            }
        }

        private bool IsAutoStartEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                return k?.GetValue("MaxLight") != null;
        }

        private void LoadAutoStartState()
        {
            if (!_isPortable) chkAutoStart.Checked = IsAutoStartEnabled();
        }

        private void LoadNotificationsOnTopState()
        {
            chkNotificationsOnTop.Checked = ConfigManager.GetNotificationsOnTop();
        }

        private void LoadProxySettings()
        {
            var proxy = ConfigManager.GetProxySettings();
            if (proxy != null)
            {
                chkProxyEnabled.Checked = proxy.Enabled;
                txtProxyServer.Text = proxy.Server ?? "";
                numProxyPort.Value = proxy.Port > 0 ? proxy.Port : 8080;
            }
            txtProxyServer.Enabled = numProxyPort.Enabled = chkProxyEnabled.Checked;
        }

        private void ApplyProxySettings()
        {
            ConfigManager.SaveProxySettings(chkProxyEnabled.Checked, txtProxyServer.Text.Trim(), (int)numProxyPort.Value);
            ProxySettingsChanged?.Invoke();
            Close();
        }

        public void SetAutoStartChecked(bool en) { if (!_isPortable) chkAutoStart.Checked = en; }
        public void SetNotificationsOnTopChecked(bool en) { chkNotificationsOnTop.Checked = en; }
    }
}