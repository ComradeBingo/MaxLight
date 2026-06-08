using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MaxLight
{
    public class SettingsForm : Form
    {
        private Panel headerPanel;
        private Label lblTitle;
        private CheckBox chkAutoStart;
        private Button btnPinSettings;
        private Button btnLogout;
        private Button btnAbout;
        private Button btnClose;

        // События для связи с Form1
        public event Action AutoStartToggled;
        public event Action PinSettingsClicked;
        public event Action LogoutClicked;
        public event Action AboutClicked;

        public SettingsForm()
        {
            InitializeForm();
            SetupModernStyle();
            LoadAutoStartState();
        }

        private void InitializeForm()
        {
            this.Text = "Настройки Max Light";
            this.Size = new Size(480, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(480, 480);
            this.MaximumSize = new Size(480, 480);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            // Верхняя панель
            headerPanel = new Panel
            {
                BackColor = Color.FromArgb(52, 73, 94),
                Height = 80,
                Dock = DockStyle.Top
            };

            // Заголовок
            lblTitle = new Label
            {
                Text = "Настройки",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 28),
                AutoSize = true
            };

            // Автозапуск (по центру)
            chkAutoStart = new CheckBox
            {
                Text = "Автоматически запускать при входе в Windows",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point((this.ClientSize.Width - 300) / 2, 110),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            chkAutoStart.CheckedChanged += (s, e) => AutoStartToggled?.Invoke();

            // Разделитель (по центру)
            Label separator1 = new Label
            {
                Text = "________________________________________",
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(40, 145),
                Width = 400,
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Заголовок секции "Безопасность" (по центру)
            Label lblSecuritySection = new Label
            {
                Text = "🔒 Безопасность",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point((this.ClientSize.Width - 120) / 2, 170),
                AutoSize = true
            };

            // PIN-код (по центру)
            btnPinSettings = new Button
            {
                Text = "🔑 Управление PIN-кодом",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 73, 94),
                ForeColor = Color.White,
                Size = new Size(220, 38),
                Location = new Point((this.ClientSize.Width - 220) / 2, 200),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnPinSettings.FlatAppearance.BorderSize = 0;
            btnPinSettings.Click += (s, e) => PinSettingsClicked?.Invoke();

            // Заголовок секции "Аккаунт" (по центру)
            Label lblAccountSection = new Label
            {
                Text = "👤 Аккаунт",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point((this.ClientSize.Width - 100) / 2, 260),
                AutoSize = true
            };

            // Выход из аккаунта (по центру)
            btnLogout = new Button
            {
                Text = "🚪 Выйти из аккаунта",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(231, 76, 60),
                ForeColor = Color.White,
                Size = new Size(220, 38),
                Location = new Point((this.ClientSize.Width - 220) / 2, 290),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) => LogoutClicked?.Invoke();

            // О программе (по центру)
            btnAbout = new Button
            {
                Text = "ℹ️ О программе",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                Size = new Size(220, 38),
                Location = new Point((this.ClientSize.Width - 220) / 2, 345),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnAbout.FlatAppearance.BorderSize = 0;
            btnAbout.Click += (s, e) => AboutClicked?.Invoke();

            // Кнопка закрытия (по центру внизу)
            btnClose = new Button
            {
                Text = "Закрыть",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Size = new Size(120, 38),
                Location = new Point((this.ClientSize.Width - 120) / 2, 420),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            // Добавляем элементы
            headerPanel.Controls.Add(lblTitle);
            this.Controls.Add(headerPanel);
            this.Controls.Add(chkAutoStart);
            this.Controls.Add(separator1);
            this.Controls.Add(lblSecuritySection);
            this.Controls.Add(btnPinSettings);
            this.Controls.Add(lblAccountSection);
            this.Controls.Add(btnLogout);
            this.Controls.Add(btnAbout);
            this.Controls.Add(btnClose);

            // Центрирование элементов при изменении размера
            this.Resize += (s, e) =>
            {
                chkAutoStart.Location = new Point((this.ClientSize.Width - chkAutoStart.Width) / 2, 110);
                separator1.Location = new Point(40, 145);
                lblSecuritySection.Location = new Point((this.ClientSize.Width - lblSecuritySection.Width) / 2, 170);
                btnPinSettings.Location = new Point((this.ClientSize.Width - btnPinSettings.Width) / 2, 200);
                lblAccountSection.Location = new Point((this.ClientSize.Width - lblAccountSection.Width) / 2, 260);
                btnLogout.Location = new Point((this.ClientSize.Width - btnLogout.Width) / 2, 290);
                btnAbout.Location = new Point((this.ClientSize.Width - btnAbout.Width) / 2, 345);
                btnClose.Location = new Point((this.ClientSize.Width - btnClose.Width) / 2, 420);
            };
        }

        private void SetupModernStyle()
        {
            // Только скругление углов без обводки
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
            chkAutoStart.Checked = IsAutoStartEnabled();
        }

        public void SetAutoStartChecked(bool enabled)
        {
            chkAutoStart.Checked = enabled;
        }
    }
}