using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MaxLight
{
    public class PinInputForm : Form
    {
        private TextBox txtPin;
        private Button btnOk;
        private Button btnCancel;
        private Label lblTitle;
        private Label lblMessage;
        private Label lblError;
        private Panel headerPanel;
        private Panel buttonPanel;
        private int _maxAttempts = 3;
        private int _attempts = 0;

        public string PinCode => txtPin.Text;
        public bool IsMaxAttemptsReached => _attempts >= _maxAttempts;

        public PinInputForm(string message)
        {
            InitializeComponent();
            lblMessage.Text = message;
            SetupModernStyle();
        }

        private void InitializeComponent()
        {
            this.txtPin = new TextBox();
            this.btnOk = new Button();
            this.btnCancel = new Button();
            this.lblTitle = new Label();
            this.lblMessage = new Label();
            this.lblError = new Label();
            this.headerPanel = new Panel();
            this.buttonPanel = new Panel();
            this.headerPanel.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(52, 73, 94);
            this.headerPanel.Controls.Add(this.lblTitle);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 60;

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.Location = new Point(20, 15);
            this.lblTitle.Text = "🔒 Max Light";

            // lblMessage
            this.lblMessage.Font = new Font("Segoe UI", 11);
            this.lblMessage.ForeColor = Color.FromArgb(52, 73, 94);
            this.lblMessage.Location = new Point(30, 80);
            this.lblMessage.Size = new Size(340, 30);
            this.lblMessage.Text = "Введите PIN-код для доступа";

            // lblError (сообщение об ошибке)
            this.lblError.Font = new Font("Segoe UI", 10);
            this.lblError.ForeColor = Color.FromArgb(231, 76, 60);
            this.lblError.Location = new Point(30, 195);
            this.lblError.Size = new Size(340, 40);
            this.lblError.Text = "";
            this.lblError.Visible = false;

            // txtPin
            this.txtPin.Font = new Font("Segoe UI", 14);
            this.txtPin.Location = new Point(30, 120);
            this.txtPin.Size = new Size(340, 32);
            this.txtPin.PasswordChar = '●';
            this.txtPin.UseSystemPasswordChar = true;
            this.txtPin.TextAlign = HorizontalAlignment.Center;
            this.txtPin.KeyPress += TxtPin_KeyPress;

            // buttonPanel
            this.buttonPanel.BackColor = Color.FromArgb(248, 249, 250);
            this.buttonPanel.Controls.Add(this.btnOk);
            this.buttonPanel.Controls.Add(this.btnCancel);
            this.buttonPanel.Dock = DockStyle.Bottom;
            this.buttonPanel.Height = 60;

            // btnOk
            this.btnOk.BackColor = Color.FromArgb(46, 204, 113);
            this.btnOk.FlatStyle = FlatStyle.Flat;
            this.btnOk.FlatAppearance.BorderSize = 0;
            this.btnOk.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.btnOk.ForeColor = Color.White;
            this.btnOk.Location = new Point(180, 12);
            this.btnOk.Size = new Size(90, 35);
            this.btnOk.Text = "Войти";
            this.btnOk.UseVisualStyleBackColor = false;
            this.btnOk.Click += BtnOk_Click;

            // btnCancel
            this.btnCancel.BackColor = Color.FromArgb(231, 76, 60);
            this.btnCancel.FlatStyle = FlatStyle.Flat;
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnCancel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.btnCancel.ForeColor = Color.White;
            this.btnCancel.Location = new Point(280, 12);
            this.btnCancel.Size = new Size(90, 35);
            this.btnCancel.Text = "Выход";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            // Form
            this.ClientSize = new Size(400, 280);
            this.Controls.Add(this.txtPin);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.lblError);
            this.Controls.Add(this.headerPanel);
            this.Controls.Add(this.buttonPanel);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            this.buttonPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupModernStyle()
        {
            // Закругление углов
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

            // Анимация появления
            this.Opacity = 0;
            Timer fadeTimer = new Timer { Interval = 10 };
            fadeTimer.Tick += (s, e) =>
            {
                if (this.Opacity < 0.98)
                    this.Opacity += 0.1;
                else
                    fadeTimer.Stop();
            };
            fadeTimer.Start();
        }

        private void TxtPin_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Только цифры
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }

            // Enter = подтвердить
            if (e.KeyChar == (char)Keys.Enter && txtPin.Text.Length >= 4)
            {
                BtnOk_Click(sender, e);
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (txtPin.Text.Length < 4)
            {
                ShowError("PIN-код должен содержать минимум 4 символа!");
                return;
            }

            // Вместо проверки здесь, мы просто возвращаем OK
            // Проверка будет в вызывающем коде (Form1)
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public void ShowError(string message)
        {
            _attempts++;
            lblError.Text = $"❌ {message}";
            lblError.Visible = true;

            // Анимация встряхивания формы
            ShakeForm();

            // Очищаем поле ввода
            txtPin.Clear();
            txtPin.Focus();

            // Если осталась последняя попытка, показываем предупреждение
            if (_attempts == _maxAttempts - 1)
            {
                lblError.Text += $"\n⚠️ Осталась 1 попытка!";
            }

            // Таймер для скрытия сообщения через 3 секунды
            Timer hideErrorTimer = new Timer { Interval = 3000 };
            hideErrorTimer.Tick += (s, ev) =>
            {
                if (!lblError.Text.Contains("попытка"))
                {
                    lblError.Visible = false;
                }
                hideErrorTimer.Stop();
            };
            hideErrorTimer.Start();
        }

        private void ShakeForm()
        {
            Point originalLocation = this.Location;
            Timer shakeTimer = new Timer { Interval = 30 };
            int shakeCount = 0;

            shakeTimer.Tick += (s, e) =>
            {
                if (shakeCount < 5)
                {
                    int offset = (shakeCount % 2 == 0) ? -5 : 5;
                    this.Location = new Point(originalLocation.X + offset, originalLocation.Y);
                    shakeCount++;
                }
                else
                {
                    this.Location = originalLocation;
                    shakeTimer.Stop();
                    shakeTimer.Dispose();
                }
            };
            shakeTimer.Start();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            txtPin.Focus();
        }

        public void ResetAttempts()
        {
            _attempts = 0;
            lblError.Visible = false;
            lblError.Text = "";
            txtPin.Clear();
        }
    }
}