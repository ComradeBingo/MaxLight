using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace MaxLight
{
    public class AboutForm : Form
    {
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblVersion;
        private Label lblDescription;
        private Label lblSecurityInfo;
        private LinkLabel lblGitHub;
        private Button btnOk;

        public AboutForm()
        {
            InitializeForm();
            SetupModernStyle();
        }

        private void InitializeForm()
        {
            this.Text = "О программе";
            this.Size = new Size(480, 480);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(480, 480);
            this.MaximumSize = new Size(480, 480);

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
                Text = "Max Light",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 20),
                AutoSize = true
            };

            // Версия
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            lblVersion = new Label
            {
                Text = $"Версия {version}",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(189, 195, 199),
                Location = new Point(25, 52),
                AutoSize = true
            };

            // Описание
            lblDescription = new Label
            {
                Text = "Лёгкий защищенный клиент MAX Messenger",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(25, 110),
                AutoSize = true
            };

            // Информация о безопасности
            lblSecurityInfo = new Label
            {
                Text = "🔒 Безопасность:\n\n" +
                       "• PIN-код (DPAPI шифрование)\n" +
                       "• Блокировка 8+ типов трекеров\n" +
                       "• Зашифрованное хранилище (AES-256)\n" +
                       "• Автоматическая очистка кеша при выходе\n" +
                       "• Блокировка рекламного баннера\n" +
                       "• Уведомления в трее\n",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(25, 145),
                Size = new Size(430, 130)
            };


            // Копирайт
            Label lblCopyright = new Label
            {
                Text = "© 2026 Max Light",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(150, 150, 150),
                AutoSize = true
            };
            // Центрируем по горизонтали
            lblCopyright.Location = new Point((this.ClientSize.Width - lblCopyright.Width) / 2, 305);

            // GitHub кнопка
            lblGitHub = new LinkLabel
            {
                Text = "GitHub",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Black,
                Location = new Point(25, 285),
                Size = new Size(120, 38),
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.White,
                ActiveLinkColor = Color.LightGray,
                VisitedLinkColor = Color.White
            };
            // Отключаем подчёркивание
            lblGitHub.LinkBehavior = LinkBehavior.NeverUnderline;

            // Центрируем по горизонтали
            lblGitHub.Location = new Point((this.ClientSize.Width - lblGitHub.Width) / 2, 375);

            lblGitHub.BorderStyle = BorderStyle.FixedSingle;
            lblGitHub.LinkClicked += (s, e) =>
                        lblGitHub.BorderStyle = BorderStyle.FixedSingle;
            lblGitHub.LinkClicked += (s, e) =>
            {
                Process.Start(new ProcessStartInfo("https://github.com/ComradeBingo/MaxLight") { UseShellExecute = true });
            };

            

            // Кнопка OK
            btnOk = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Text = "OK",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(120, 38),  // Такой же размер, как у btnClose
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => this.Close();
            btnOk.Location = new Point((this.ClientSize.Width - btnOk.Width) / 2, 420);  // Y = 420


            // Добавляем элементы
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblVersion);
            this.Controls.Add(headerPanel);
            this.Controls.Add(lblDescription);
            this.Controls.Add(lblSecurityInfo);
            this.Controls.Add(lblGitHub);
            this.Controls.Add(lblCopyright);
            this.Controls.Add(btnOk);
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

            
        }
    }
}