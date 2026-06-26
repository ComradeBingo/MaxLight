using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MaxLight
{
    public class PinSetupForm : Form
    {
        private TextBox txtPin;
        private TextBox txtConfirmPin;
        private Button btnOk;
        private Button btnCancel;
        private Label lblTitle;
        private Label lblMessage;
        private Label lblStrength;
        private Panel headerPanel;
        private Panel buttonPanel;
        private ProgressBar strengthBar;

        public string PinCode => txtPin.Text;

        public PinSetupForm()
        {
            InitializeComponent();
            SetupModernStyle();
        }

        private void InitializeComponent()
        {
            this.txtPin = new TextBox();
            this.txtConfirmPin = new TextBox();
            this.btnOk = new Button();
            this.btnCancel = new Button();
            this.lblTitle = new Label();
            this.lblMessage = new Label();
            this.lblStrength = new Label();
            this.strengthBar = new ProgressBar();
            this.headerPanel = new Panel();
            this.buttonPanel = new Panel();
            this.headerPanel.SuspendLayout();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();

            // headerPanel
            this.headerPanel.BackColor = Color.FromArgb(66, 75, 121);
            this.headerPanel.Controls.Add(this.lblTitle);
            this.headerPanel.Dock = DockStyle.Top;
            this.headerPanel.Height = 60;

            // lblTitle
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            this.lblTitle.ForeColor = Color.White;
            this.lblTitle.Location = new Point(20, 15);
            this.lblTitle.Text = "Установка PIN-кода";

            // lblMessage
            this.lblMessage.Font = new Font("Segoe UI", 11);
            this.lblMessage.ForeColor = Color.FromArgb(52, 73, 94);
            this.lblMessage.Location = new Point(30, 80);
            this.lblMessage.Size = new Size(340, 30);
            this.lblMessage.Text = "Придумайте PIN-код (4-8 цифр):";

            // txtPin
            this.txtPin.Font = new Font("Segoe UI", 14);
            this.txtPin.Location = new Point(30, 120);
            this.txtPin.Size = new Size(340, 32);
            this.txtPin.PasswordChar = '●';
            this.txtPin.UseSystemPasswordChar = true;
            this.txtPin.TextAlign = HorizontalAlignment.Center;
            this.txtPin.TextChanged += TxtPin_TextChanged;
            this.txtPin.KeyPress += TxtPin_KeyPress;

            // lblStrength
            this.lblStrength.Font = new Font("Segoe UI", 9);
            this.lblStrength.ForeColor = Color.Gray;
            this.lblStrength.Location = new Point(30, 158);
            this.lblStrength.Size = new Size(100, 20);

            // strengthBar
            this.strengthBar.Location = new Point(130, 158);
            this.strengthBar.Size = new Size(240, 10);
            this.strengthBar.Style = ProgressBarStyle.Continuous;

            // txtConfirmPin
            this.txtConfirmPin.Font = new Font("Segoe UI", 14);
            this.txtConfirmPin.Location = new Point(30, 190);
            this.txtConfirmPin.Size = new Size(340, 32);
            this.txtConfirmPin.PasswordChar = '●';
            this.txtConfirmPin.UseSystemPasswordChar = true;
            this.txtConfirmPin.TextAlign = HorizontalAlignment.Center;
            this.txtConfirmPin.TextChanged += TxtConfirmPin_TextChanged;
            this.txtConfirmPin.KeyPress += TxtPin_KeyPress;

            // buttonPanel
            this.buttonPanel.BackColor = Color.FromArgb(248, 249, 250);
            this.buttonPanel.Controls.Add(this.btnOk);
            this.buttonPanel.Controls.Add(this.btnCancel);
            this.buttonPanel.Dock = DockStyle.Bottom;
            this.buttonPanel.Height = 60;

            // btnOk
            this.btnOk.BackColor = Color.FromArgb(52, 152, 219);
            this.btnOk.Enabled = false;
            this.btnOk.FlatStyle = FlatStyle.Flat;
            this.btnOk.FlatAppearance.BorderSize = 0;
            this.btnOk.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.btnOk.ForeColor = Color.White;
            this.btnOk.Location = new Point(180, 12);
            this.btnOk.Size = new Size(100, 35);
            this.btnOk.Text = "Сохранить";
            this.btnOk.UseVisualStyleBackColor = false;
            this.btnOk.Click += BtnOk_Click;

            // btnCancel
            this.btnCancel.BackColor = Color.FromArgb(149, 165, 166);
            this.btnCancel.FlatStyle = FlatStyle.Flat;
            this.btnCancel.FlatAppearance.BorderSize = 0;
            this.btnCancel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            this.btnCancel.ForeColor = Color.White;
            this.btnCancel.Location = new Point(300, 12);
            this.btnCancel.Size = new Size(90, 35);
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            // Form
            this.ClientSize = new Size(400, 310);
            this.Controls.Add(this.txtPin);
            this.Controls.Add(this.txtConfirmPin);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.lblStrength);
            this.Controls.Add(this.strengthBar);
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

        private void TxtPin_TextChanged(object sender, EventArgs e)
        {
            int strength = GetPinStrength(txtPin.Text);
            strengthBar.Value = strength;

            if (strength < 30)
                lblStrength.Text = "Слабый PIN";
            else if (strength < 70)
                lblStrength.Text = "Средний PIN";
            else
                lblStrength.Text = "Сильный PIN";

            CheckFields();
        }

        private int GetPinStrength(string pin)
        {
            if (string.IsNullOrEmpty(pin)) return 0;

            int score = 0;

            // Длина
            if (pin.Length >= 4) score += 20;
            if (pin.Length >= 6) score += 20;
            if (pin.Length >= 8) score += 20;

            // Разнообразие (не все одинаковые цифры)
            if (!Regex.IsMatch(pin, @"^(\d)\1+$")) score += 20;

            // Не последовательные цифры
            bool isSequential = true;
            for (int i = 1; i < pin.Length; i++)
            {
                if (Math.Abs(pin[i] - pin[i - 1]) != 1)
                {
                    isSequential = false;
                    break;
                }
            }
            if (!isSequential) score += 20;

            return Math.Min(100, score);
        }

        private void TxtConfirmPin_TextChanged(object sender, EventArgs e)
        {
            CheckFields();
        }

        private void CheckFields()
        {
            bool isValid = txtPin.Text.Length >= 4 &&
                          txtPin.Text.Length <= 8 &&
                          txtPin.Text == txtConfirmPin.Text;

            btnOk.Enabled = isValid;

            if (txtPin.Text.Length > 0 && txtConfirmPin.Text.Length > 0 && txtPin.Text != txtConfirmPin.Text)
            {
                lblMessage.Text = "⚠️ PIN-коды не совпадают!";
                lblMessage.ForeColor = Color.FromArgb(231, 76, 60);
            }
            else if (txtPin.Text.Length > 0 && (txtPin.Text.Length < 4 || txtPin.Text.Length > 8))
            {
                lblMessage.Text = "⚠️ PIN должен быть от 4 до 8 цифр!";
                lblMessage.ForeColor = Color.FromArgb(231, 76, 60);
            }
            else
            {
                lblMessage.Text = "Придумайте PIN-код (4-8 цифр):";
                lblMessage.ForeColor = Color.FromArgb(52, 73, 94);
            }
        }

        private void TxtPin_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Только цифры
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (txtPin.Text != txtConfirmPin.Text)
            {
                MessageBox.Show("PIN-коды не совпадают!", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}