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
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblVersion;
        private Label lblDescription;
        private Label lblSecurityInfo;
        private LinkLabel lblGitHub;
        private Button btnOk;
        private PictureBox picIcon;

        public AboutForm()
        {
            InitializeForm();
            SetupModernStyle();
        }

        private void InitializeForm()
        {
            this.Text = "О программе";
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
                Text = "Max Light",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(20, 22),
                AutoSize = true
            };

            // Версия
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            lblVersion = new Label
            {
                Text = $"Версия {version}",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(189, 195, 199),
                Location = new Point(150, 32),
                AutoSize = true
            };

            // ===== ОСНОВНОЙ КОНТЕНТ =====
            int leftColumnX = 40;
            int rightColumnX = 390;
            int rowY = 100;

            // Описание (левая колонка)
            lblDescription = new Label
            {
                Text = "Лёгкий защищенный клиент MAX Messenger",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(leftColumnX, rowY),
                AutoSize = true
            };

            rowY += 35;

            // Информация о безопасности (левая колонка)
            lblSecurityInfo = new Label
            {
                Text = 
                       "• PIN-код (DPAPI шифрование)\n" +
                       "• Блокировка 8+ типов трекеров\n" +
                       "• Зашифрованное хранилище (AES-256)\n" +
                       "• Автоматическая очистка кеша при выходе\n" +
                       "• Блокировка рекламного баннера\n" +
                       "• Уведомления в трее и панели задач\n" +
                       "• Лёгкие и быстрые дельта-обновления\n" +
                       "• Поддержка работы с прокси\n" +
                       "• Разные режимы работы для обычной и portable версий\n" ,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(80, 80, 80),
                Location = new Point(leftColumnX, rowY),
                Size = new Size(350, 270)
            };

            // ===== ИКОНКА (ПРАВАЯ КОЛОНКА) =====
            picIcon = new PictureBox
            {
                Size = new Size(64, 64), // Размер иконки 64x64
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(rightColumnX + 105, 115), // Центрируем
                BackColor = Color.Transparent
            };

            // Загружаем иконку из app.ico
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "app.ico");
                if (File.Exists(iconPath))
                {
                    using (var icon = new Icon(iconPath))
                    {
                        // Конвертируем Icon в Bitmap для PictureBox
                        picIcon.Image = icon.ToBitmap();
                    }
                }
                else
                {
                    // Если app.ico не найден, используем стандартную иконку приложения
                    picIcon.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap();
                }
            }
            catch
            {
                // Если ошибка, используем стандартную иконку приложения
                try
                {
                    picIcon.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath)?.ToBitmap();
                }
                catch { }
            }

            // ===== ПРАВАЯ КОЛОНКА (нижняя часть) =====
            int rightRowY = 210;

            Label lblInfoText = new Label
            {
                Text = "© 2026 Max Light",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(rightColumnX + 90, rightRowY),
                AutoSize = true
            };

            rightRowY += 45;

            // Кнопка GitHub
            lblGitHub = new LinkLabel
            {
                Text = "GitHub",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(52, 73, 94),
                Location = new Point(rightColumnX + 50, rightRowY),
                Size = new Size(180, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.White,
                ActiveLinkColor = Color.LightGray,
                VisitedLinkColor = Color.White,
                Cursor = Cursors.Hand
            };
            lblGitHub.LinkBehavior = LinkBehavior.NeverUnderline;
            lblGitHub.LinkClicked += (s, e) =>
            {
                Process.Start(new ProcessStartInfo("https://github.com/ComradeBingo/MaxLight") { UseShellExecute = true });
            };

            // ===== КНОПКА ЗАКРЫТИЯ =====
            btnOk = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Text = "Закрыть",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(120, 38),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => this.Close();
            btnOk.Location = new Point((this.ClientSize.Width - btnOk.Width) / 2, 415);

            // Добавляем элементы
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblVersion);
            this.Controls.Add(headerPanel);
            this.Controls.Add(lblDescription);
            this.Controls.Add(lblSecurityInfo);
            this.Controls.Add(picIcon);
            this.Controls.Add(lblInfoText);
            this.Controls.Add(lblGitHub);
            this.Controls.Add(btnOk);
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
    }
}