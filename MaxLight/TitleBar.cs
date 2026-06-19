using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace MaxLight
{
    public class TitleBar : Panel
    {
        private Form parentForm;
        private Label lblTitle;
        private Button btnSettings;
        private Button btnMinimize;
        private Button btnMaximize;
        private Button btnClose;

        private bool isDragging = false;
        private Point mouseOffset;

        public event EventHandler SettingsClick;

        public TitleBar(Form parent)
        {
            parentForm = parent;
            InitializeTitleBar();
        }

        private void InitializeTitleBar()
        {
            this.BackColor = Color.FromArgb(52, 73, 94);
            this.Height = 60; // Можете менять эту высоту
            this.Dock = DockStyle.Top;
            this.Cursor = Cursors.SizeAll;

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string shortVersion = string.Join(".", version.Split('.').Take(3));

            // Заголовок - центрируем по вертикали
            lblTitle = new Label
            {
                Text = $"Max Light v{shortVersion}",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 0) // Y будет установлен ниже
            };
            // Центрируем заголовок
            lblTitle.Top = (this.Height - lblTitle.Height) / 2;

            // Кнопка закрыть - центрируем по вертикали
            btnClose = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text = "✕",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Size = new Size(45, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => parentForm.Close();

            // Кнопка развернуть - центрируем по вертикали
            btnMaximize = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text = "□",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Size = new Size(45, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnMaximize.Click += BtnMaximize_Click;

            // Кнопка свернуть - центрируем по вертикали
            btnMinimize = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text = "─",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Size = new Size(45, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnMinimize.Click += (s, e) => parentForm.WindowState = FormWindowState.Minimized;

            // Кнопка настроек - центрируем по вертикали
            btnSettings = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Text = "⚙",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Size = new Size(45, 35),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnSettings.Click += (s, e) => SettingsClick?.Invoke(s, e);

            // Ховер-эффекты
            btnSettings.MouseEnter += (s, e) => btnSettings.BackColor = Color.FromArgb(64, 85, 106);
            btnSettings.MouseLeave += (s, e) => btnSettings.BackColor = Color.Transparent;
            btnMinimize.MouseEnter += (s, e) => btnMinimize.BackColor = Color.FromArgb(64, 85, 106);
            btnMinimize.MouseLeave += (s, e) => btnMinimize.BackColor = Color.Transparent;
            btnMaximize.MouseEnter += (s, e) => btnMaximize.BackColor = Color.FromArgb(64, 85, 106);
            btnMaximize.MouseLeave += (s, e) => btnMaximize.BackColor = Color.Transparent;
            btnClose.MouseEnter += (s, e) => btnClose.BackColor = Color.FromArgb(231, 76, 60);
            btnClose.MouseLeave += (s, e) => btnClose.BackColor = Color.Transparent;

            this.Controls.Add(lblTitle);
            this.Controls.Add(btnSettings);
            this.Controls.Add(btnMinimize);
            this.Controls.Add(btnMaximize);
            this.Controls.Add(btnClose);

            this.MouseDown += TitleBar_MouseDown;
            this.MouseMove += TitleBar_MouseMove;
            this.MouseUp += TitleBar_MouseUp;

            parentForm.Resize += (s, e) => UpdateButtonsPosition();

            // Подписываемся на изменение размера TitleBar для перецентрирования
            this.Resize += (s, e) => CenterControls();
        }

        private void CenterControls()
        {
            // Центрируем заголовок
            if (lblTitle != null)
            {
                lblTitle.Top = (this.Height - lblTitle.Height) / 2;
            }

            // Центрируем кнопки (их позиции обновляются в UpdateButtonsPosition)
            UpdateButtonsPosition();
        }

        private void UpdateButtonsPosition()
        {
            if (btnClose == null) return;

            int buttonTop = (this.Height - btnClose.Height) / 2;

            btnClose.Location = new Point(this.Width - 50, buttonTop);
            btnMaximize.Location = new Point(this.Width - 95, buttonTop);
            btnMinimize.Location = new Point(this.Width - 140, buttonTop);
            btnSettings.Location = new Point(this.Width - 185, buttonTop);
            btnMaximize.Text = parentForm.WindowState == FormWindowState.Maximized ? "❐" : "□";
        }

        private void BtnMaximize_Click(object sender, EventArgs e)
        {
            if (parentForm.WindowState == FormWindowState.Normal)
                parentForm.WindowState = FormWindowState.Maximized;
            else
                parentForm.WindowState = FormWindowState.Normal;
            btnMaximize.Text = parentForm.WindowState == FormWindowState.Maximized ? "❐" : "□";
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && parentForm.WindowState == FormWindowState.Normal)
            {
                isDragging = true;
                mouseOffset = new Point(-e.X, -e.Y);
            }
        }

        private void TitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging && parentForm.WindowState == FormWindowState.Normal)
            {
                Point mousePos = Control.MousePosition;
                mousePos.Offset(mouseOffset.X, mouseOffset.Y);
                parentForm.Location = mousePos;
            }
        }

        private void TitleBar_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }
    }
}