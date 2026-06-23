using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO; 

namespace MaxLight
{
    public class TitleBar : Panel
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private Form parentForm;
             
        private Label lblTitle;
        private Button btnSettings;
        private Button btnMinimize;
        private Button btnMaximize;
        private Button btnClose;

        public event EventHandler SettingsClick;

        // Цветовая схема 
        private static readonly Color BackgroundColor = Color.FromArgb(66, 75, 121); // Основной цвет BG тайтл-бара
        private static readonly Color HoverColor = Color.FromArgb(60, 60, 60);
        private static readonly Color TextColor = Color.FromArgb(200, 200, 200);
        private static readonly Color CloseHoverColor = Color.FromArgb(232, 17, 35);
        private static readonly Color ClosePressedColor = Color.FromArgb(180, 10, 20);

        public TitleBar(Form parent)
        {
            parentForm = parent;
            InitializeTitleBar();
        }

        private void InitializeTitleBar()
        {
            this.BackColor = BackgroundColor;
            this.Height = 48;
            this.Dock = DockStyle.Top;
            this.Cursor = Cursors.Default;

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string shortVersion = string.Join(".", version.Split('.').Take(3));



            // ========== ЗАГОЛОВОК ==========
            lblTitle = new Label
            {
                Text = $"Max Light   v{shortVersion}",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(246, 244, 249),
                AutoSize = true,
                Location = new Point(12, 0)
            };
            lblTitle.Top = (this.Height - lblTitle.Height) / 2;

            // ========== КНОПКИ ==========
            btnSettings = CreateIconButton("\uE713", "Настройки");
            btnSettings.Click += (s, e) => SettingsClick?.Invoke(s, e);

            btnMinimize = CreateIconButton("\uE921", "Свернуть");
            btnMinimize.Click += (s, e) => parentForm.WindowState = FormWindowState.Minimized;

            btnMaximize = CreateIconButton("\uE922", "Развернуть");
            btnMaximize.Click += BtnMaximize_Click;

            btnClose = CreateIconButton("\uE711", "Закрыть");
            btnClose.Click += (s, e) => parentForm.Close();

            // Особые эффекты для кнопки закрытия
            btnClose.MouseEnter += (s, e) =>
            {
                btnClose.BackColor = CloseHoverColor;
                btnClose.ForeColor = Color.White;
            };
            btnClose.MouseLeave += (s, e) =>
            {
                btnClose.BackColor = Color.Transparent;
                btnClose.ForeColor = TextColor;
            };
            btnClose.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btnClose.BackColor = ClosePressedColor;
            };
            btnClose.MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    btnClose.BackColor = CloseHoverColor;
            };

            // Добавляем все контролы
            
            this.Controls.Add(lblTitle);
            this.Controls.Add(btnSettings);
            this.Controls.Add(btnMinimize);
            this.Controls.Add(btnMaximize);
            this.Controls.Add(btnClose);

            this.MouseDown += TitleBar_MouseDown;
            this.DoubleClick += (s, e) => ToggleMaximize();

            parentForm.Resize += (s, e) => UpdateButtonsPosition();
            
        }

        private Button CreateIconButton(string iconChar, string tooltip)
        {
            var btn = new Button
            {
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderSize = 0 },
                BackColor = Color.Transparent,
                ForeColor = TextColor,
                Text = iconChar,
                Font = new Font("Segoe MDL2 Assets", 10, FontStyle.Regular),
                Size = new Size(46, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tooltipObj = new ToolTip();
                tooltipObj.SetToolTip(btn, tooltip);
            }

            btn.MouseEnter += (s, e) =>
            {
                if (btn != btnClose)
                {
                    btn.BackColor = HoverColor;
                    btn.ForeColor = Color.White;
                }
            };
            btn.MouseLeave += (s, e) =>
            {
                if (btn != btnClose)
                {
                    btn.BackColor = Color.Transparent;
                    btn.ForeColor = TextColor;
                }
            };

            return btn;
        }

        

        private void UpdateButtonsPosition()
        {
            if (btnClose == null) return;

            int buttonTop = (this.Height - btnClose.Height) / 2;
            int rightMargin = 10; //отступ справа для кнопок "свернуть, закрыть и т.д."

            btnClose.Location = new Point(this.Width - btnClose.Width - rightMargin, buttonTop);
            btnMaximize.Location = new Point(btnClose.Left - btnMaximize.Width, buttonTop);
            btnMinimize.Location = new Point(btnMaximize.Left - btnMinimize.Width, buttonTop);
            btnSettings.Location = new Point(btnMinimize.Left - btnSettings.Width - 4, buttonTop);

            btnMaximize.Text = parentForm.WindowState == FormWindowState.Maximized ? "\uE923" : "\uE922";
            btnMaximize.Font = new Font("Segoe MDL2 Assets", 10, FontStyle.Regular);
        }

        private void ToggleMaximize()
        {
            if (parentForm.WindowState == FormWindowState.Normal)
                parentForm.WindowState = FormWindowState.Maximized;
            else
                parentForm.WindowState = FormWindowState.Normal;
            UpdateButtonsPosition();
        }

        private void BtnMaximize_Click(object sender, EventArgs e)
        {
            ToggleMaximize();
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(parentForm.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
    }
}