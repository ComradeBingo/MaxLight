using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MaxLight
{
    public class AboutForm : Form
    {
        // Шапка
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblVersion;

        // Контент
        private TableLayoutPanel mainTable;

        // Левая колонка
        private Label lblDescription;
        private Label lblSecurityInfo;

        // Правая колонка
        private PictureBox picIcon;
        private LinkLabel lblGitHub;
        private Label lblCopyright;

        // Нижняя панель
        private Panel bottomPanel;
        private Button btnClose;

        private float _elementScale = 1f;

        public AboutForm()
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.AutoScaleDimensions = new SizeF(96F, 96F);
            this.AutoScroll = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;

            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            InitializeForm();
            LoadIcon();
            ApplyRoundedCorners();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            var screenBounds = Screen.PrimaryScreen.Bounds;
            float screenWidth = screenBounds.Width;

            int windowWidth;
            int windowHeight;

            if (screenWidth >= 3840)
            {
                windowWidth = LogicalToDeviceUnits(1800);
                windowHeight = LogicalToDeviceUnits(1100);
                _elementScale = 1.0f;
            }
            else if (screenWidth >= 2560)
            {
                windowWidth = LogicalToDeviceUnits(1400);
                windowHeight = LogicalToDeviceUnits(850);
                _elementScale = 0.85f;
            }
            else //1080 и меньше
            {
                windowWidth = LogicalToDeviceUnits(1000);
                windowHeight = LogicalToDeviceUnits(750);
                _elementScale = 1.5f;
            }

            this.MinimumSize = new Size(windowWidth, windowHeight);
            this.ClientSize = new Size(
                (int)(windowWidth * 1.15f),
                (int)(windowHeight * 1.1f)
            );

            ScaleElementSizes(_elementScale);
            UpdateLayout();
        }

        private void ScaleElementSizes(float scale)
        {
            // ===== ШАПКА - ФИКСИРОВАННАЯ ВЫСОТА =====
            int headerHeight = (int)(55 * Math.Max(scale, 0.7f));
            if (headerHeight < 40) headerHeight = 40;
            headerPanel.Height = headerHeight;

            int headerPadding = (int)(25 * scale);
            if (headerPadding < 15) headerPadding = 15;
            headerPanel.Padding = new Padding(headerPadding, 0, headerPadding, 0);

            // Заголовок
            float headerFontSize = 16f * scale;
            if (headerFontSize < 12f) headerFontSize = 12f;
            if (headerFontSize > 24f) headerFontSize = 24f;
            lblTitle.Font = new Font("Segoe UI", headerFontSize, FontStyle.Bold);
            lblTitle.Location = new Point(headerPadding, (headerHeight - lblTitle.Height) / 2);

            // Версия
            float versionFontSize = 11f * scale;
            if (versionFontSize < 9f) versionFontSize = 9f;
            if (versionFontSize > 18f) versionFontSize = 18f;
            lblVersion.Font = new Font("Segoe UI", versionFontSize);
            lblVersion.Location = new Point(
                headerPadding + lblTitle.Width + (int)(10 * scale),
                (headerHeight - lblVersion.Height) / 2
            );

            // ===== КНОПКА ЗАКРЫТИЯ =====
            float closeFontSize = 12f * scale;
            if (closeFontSize < 9f) closeFontSize = 9f;
            if (closeFontSize > 18f) closeFontSize = 18f;
            btnClose.Font = new Font("Segoe UI", closeFontSize, FontStyle.Bold);

            int closePadding = (int)(50 * scale);
            if (closePadding < 30) closePadding = 30;
            btnClose.Padding = new Padding(closePadding, (int)(14 * scale), closePadding, (int)(14 * scale));

            // ===== ОСНОВНОЙ КОНТЕНТ =====
            int mainPadding = (int)(35 * scale);
            if (mainPadding < 20) mainPadding = 20;
            mainTable.Padding = new Padding(
                mainPadding,
                (int)(55 * scale),
                mainPadding,
                (int)(25 * scale)
            );

            // ===== ЛЕВАЯ КОЛОНКА =====
            float descFontSize = 12f * scale;
            if (descFontSize < 9f) descFontSize = 9f;
            if (descFontSize > 18f) descFontSize = 18f;
            lblDescription.Font = new Font("Segoe UI", descFontSize, FontStyle.Bold);

            float securityFontSize = 10f * scale;
            if (securityFontSize < 8f) securityFontSize = 8f;
            if (securityFontSize > 16f) securityFontSize = 16f;
            lblSecurityInfo.Font = new Font("Segoe UI", securityFontSize);

            // ===== ПРАВАЯ КОЛОНКА =====
            int iconSize = (int)(90 * scale);
            if (iconSize < 60) iconSize = 60;
            if (iconSize > 150) iconSize = 150;
            picIcon.Size = new Size(iconSize, iconSize);

            float gitFontSize = 11f * scale;
            if (gitFontSize < 9f) gitFontSize = 9f;
            if (gitFontSize > 17f) gitFontSize = 17f;
            lblGitHub.Font = new Font("Segoe UI", gitFontSize, FontStyle.Bold);

            int gitPadding = (int)(35 * scale);
            if (gitPadding < 20) gitPadding = 20;
            lblGitHub.Padding = new Padding(gitPadding, (int)(10 * scale), gitPadding, (int)(10 * scale));

            // ===== КОПИРАЙТ =====
            float copyFontSize = 9f * scale;
            if (copyFontSize < 7f) copyFontSize = 7f;
            if (copyFontSize > 14f) copyFontSize = 14f;
            lblCopyright.Font = new Font("Segoe UI", copyFontSize);
        }

        private void InitializeForm()
        {
            this.Text = "О программе";

            // ============ ШАПКА ============
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(66, 75, 121),
                Height = 55,
                Padding = new Padding(25, 0, 25, 0)
            };

            // Перетаскивание окна
            bool isDragging = false;
            Point dragStart = Point.Empty;

            headerPanel.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    dragStart = e.Location;
                }
            };

            headerPanel.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    this.Location = new Point(
                        this.Location.X + e.X - dragStart.X,
                        this.Location.Y + e.Y - dragStart.Y
                    );
                }
            };

            headerPanel.MouseUp += (s, e) => isDragging = false;

            // Заголовок
            lblTitle = new Label
            {
                Text = "Max Light",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(25, 14)
            };

            // Версия
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            lblVersion = new Label
            {
                Text = $"Версия {version}",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(189, 195, 199),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(25 + lblTitle.Width + 10, 18)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblVersion);
            this.Controls.Add(headerPanel);

            // ============ НИЖНЯЯ ПАНЕЛЬ ============
            bottomPanel = new Panel
            {
                BackColor = Color.White,
                Dock = DockStyle.Bottom,
                Height = 75,
                Padding = new Padding(10)
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

            // ============ ОСНОВНОЙ КОНТЕНТ ============
            mainTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(35, 30, 35, 25)
            };

            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            mainTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            this.Controls.Add(mainTable);

            // ============ ЛЕВАЯ КОЛОНКА ============
            var leftPanel = CreateLeftPanel();
            mainTable.Controls.Add(leftPanel, 0, 0);

            // ============ ПРАВАЯ КОЛОНКА ============
            var rightPanel = CreateRightPanel();
            mainTable.Controls.Add(rightPanel, 1, 0);
        }

        private Panel CreateLeftPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 15, 0)
            };

            lblDescription = new Label
            {
                Text = "Лёгкий защищенный клиент MAX Messenger",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = true,
                Location = new Point(0, 5)
            };
            panel.Controls.Add(lblDescription);

            lblSecurityInfo = new Label
            {
                Text =
                    "🔒 PIN-код (DPAPI шифрование)\n" +
                    "🛡️ Блокировка 8+ типов трекеров\n" +
                    "🔐 Зашифрованное хранилище (AES-256)\n" +
                    "📊 Счетчик непрочитанных сообщений\n" +
                    "🚫 Блокировка рекламного баннера\n" +
                    "🔔 Уведомления в трее и панели задач\n" +
                    "⚡ Лёгкие и быстрые дельта-обновления\n" +
                    "🌐 Поддержка работы с прокси\n" +
                    "💼 Обычная и portable версии",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = true,
                Location = new Point(0, 45)
            };
            panel.Controls.Add(lblSecurityInfo);

            lblCopyright = new Label
            {
                Text = "© 2026 Max Light",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                Location = new Point(0, panel.Height - 30)
            };
            panel.Controls.Add(lblCopyright);

            panel.Resize += (s, e) =>
            {
                lblCopyright.Location = new Point(0, panel.Height - 30);
            };

            return panel;
        }

        private Panel CreateRightPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15, 0, 0, 0)
            };

            picIcon = new PictureBox
            {
                Size = new Size(90, 90),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.None
            };
            picIcon.Location = new Point(
                (panel.Width - picIcon.Width) / 2,
                25
            );
            panel.Controls.Add(picIcon);

            lblGitHub = new LinkLabel
            {
                Text = "GitHub",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(66, 75, 121),
                AutoSize = true,
                Padding = new Padding(35, 10, 35, 10),
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.White,
                ActiveLinkColor = Color.White,
                VisitedLinkColor = Color.White,
                Cursor = Cursors.Hand,
                LinkBehavior = LinkBehavior.NeverUnderline,
                Anchor = AnchorStyles.None
            };

            lblGitHub.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/ComradeBingo/MaxLight",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            lblGitHub.MouseEnter += (s, e) =>
                lblGitHub.BackColor = Color.FromArgb(86, 96, 145);
            lblGitHub.MouseLeave += (s, e) =>
                lblGitHub.BackColor = Color.FromArgb(66, 75, 121);

            panel.Controls.Add(lblGitHub);

            panel.Resize += (s, e) =>
            {
                if (picIcon != null)
                {
                    picIcon.Location = new Point(
                        (panel.Width - picIcon.Width) / 2,
                        25
                    );
                }

                if (lblGitHub != null)
                {
                    lblGitHub.Location = new Point(
                        (panel.Width - lblGitHub.Width) / 2,
                        picIcon.Bottom + 25
                    );
                }
            };

            return panel;
        }

        private void UpdateLayout()
        {
            if (mainTable != null)
            {
                mainTable.Invalidate();
                mainTable.Update();
            }
        }

        private void LoadIcon()
        {
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "app.ico");
                Icon icon = null;

                if (File.Exists(iconPath))
                {
                    icon = new Icon(iconPath);
                }
                else
                {
                    icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }

                if (icon != null)
                {
                    using (icon)
                    {
                        picIcon.Image = icon.ToBitmap();
                    }
                }
            }
            catch { }
        }

        private void ApplyRoundedCorners()
        {
            if (this.Width <= 0 || this.Height <= 0) return;

            int radius = 12;
            using (GraphicsPath path = new GraphicsPath())
            {
                Rectangle rect = new Rectangle(0, 0, this.Width, this.Height);
                path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y, radius, radius, 270, 90);
                path.AddArc(rect.X + rect.Width - radius, rect.Y + rect.Height - radius, radius, radius, 0, 90);
                path.AddArc(rect.X, rect.Y + rect.Height - radius, radius, radius, 90, 90);
                path.CloseFigure();
                this.Region = new Region(path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedCorners();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                picIcon?.Image?.Dispose();
                Region?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}