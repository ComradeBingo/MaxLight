using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MaxLight
{
    public class UpdateDialog : Form
    {
        private Panel headerPanel;
        private Label lblTitle;
        private Label lblVersion;
        private RichTextBox txtReleaseNotes;
        private Button btnUpdate;
        private Button btnSkip;

        public UpdateDialog(string version, string releaseNotes, string portableHint = "")
        {
            InitializeForm(version, releaseNotes, portableHint);
            
        }

        private void InitializeForm(string version, string releaseNotes, string portableHint)
        {
            this.Text = "Обновление";
            this.Size = new Size(550, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.MinimumSize = new Size(550, 500);
            this.MaximumSize = new Size(550, 500);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            // Верхняя панель
            headerPanel = new Panel
            {
                BackColor = Color.FromArgb(66, 75, 121), //цвет верхней панели
                Height = 48,
                Dock = DockStyle.Top
            };

            // Заголовок
            lblTitle = new Label
            {
                Text = "Доступно обновление",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 10),
                AutoSize = true
            };

            // Версия
            lblVersion = new Label
            {
                Text = $"Версия {version}{portableHint}",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(189, 195, 199),
                Location = new Point(150, 22),
                AutoSize = true
            };

            // Заголовок "Что нового"
            Label lblChangesTitle = new Label
            {
                Text = "📝 Что нового:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                Location = new Point(20, 110),
                AutoSize = true
            };

            // RichTextBox для отображения release notes
            txtReleaseNotes = new RichTextBox
            {
                Location = new Point(20, 140),
                Size = new Size(510, 220),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.FromArgb(248, 249, 250),
                ForeColor = Color.FromArgb(52, 73, 94),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = releaseNotes ?? "Описание изменений не найдено."
            };

            // Кнопка "Обновить"
            btnUpdate = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                Text = "✅ Обновить",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(150, 38),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Yes;
                this.Close();
            };

            // Кнопка "Пропустить"
            btnSkip = new Button
            {
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(149, 165, 166),
                ForeColor = Color.White,
                Text = "Пропустить",
                Font = new Font("Segoe UI", 10),
                Size = new Size(120, 38),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnSkip.FlatAppearance.BorderSize = 0;
            btnSkip.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.No;
                this.Close();
            };

            // Размещаем кнопки внизу по центру
            int buttonY = 390;
            int centerX = (this.ClientSize.Width - btnUpdate.Width - btnSkip.Width - 15) / 2;
            btnUpdate.Location = new Point(centerX, buttonY);
            btnSkip.Location = new Point(centerX + btnUpdate.Width + 15, buttonY);

            // Добавляем элементы
            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblVersion);
            this.Controls.Add(headerPanel);
            this.Controls.Add(lblChangesTitle);
            this.Controls.Add(txtReleaseNotes);
            this.Controls.Add(btnUpdate);
            this.Controls.Add(btnSkip);
        }

        
    }
}