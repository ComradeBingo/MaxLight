using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Net;

namespace MaxLight
{
    public class CustomNotification : Form
    {
        private Timer autoCloseTimer;
        private static int notificationCount = 0;
        private static object lockObj = new object();
        private static bool _alwaysOnTop = true; // Статическая настройка по умолчанию

        private string _userName;
        private Action<string> _onClick;

        private const int AVATAR_SIZE = 64;
        private const int AVATAR_MARGIN = 12;
        private const int CLOSE_BUTTON_SIZE = 24;
        private const int FORM_PADDING = 12;

        private Button closeButton;

        // Глобальное свойство для настройки TopMost
        public static bool AlwaysOnTop
        {
            get { return _alwaysOnTop; }
            set { _alwaysOnTop = value; }
        }

        // ========== НЕ ПЕРЕХВАТЫВАЕТ ФОКУС ==========
        protected override bool ShowWithoutActivation => true;

        public CustomNotification(string title, string message, string avatarUrl = null, Action<string> onClick = null)
        {
            _userName = title;
            _onClick = onClick;

            InitializeComponents();

            // Форматируем сообщение: ограничиваем 5 строками
            string displayMessage = FormatMessageToMaxLines(message, 5);

            this.Text = title;

            // Устанавливаем размер формы
            int formWidth = 460;
            this.ClientSize = new Size(formWidth, 200);

            // Создаем главную панель
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Аватарка
            var avatar = new PictureBox
            {
                Size = new Size(AVATAR_SIZE, AVATAR_SIZE),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(60, 60, 65),
                Location = new Point(FORM_PADDING, FORM_PADDING),
                Cursor = Cursors.Hand
            };

            if (!string.IsNullOrEmpty(avatarUrl))
            {
                LoadAvatarAsync(avatar, avatarUrl, title);
            }
            else
            {
                CreateInitialsAvatar(avatar, title);
            }

            // Заголовок
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(FORM_PADDING + AVATAR_SIZE + AVATAR_MARGIN, FORM_PADDING + (AVATAR_SIZE / 2) - 10),
                Cursor = Cursors.Hand,
                MaximumSize = new Size(formWidth - (FORM_PADDING + AVATAR_SIZE + AVATAR_MARGIN + CLOSE_BUTTON_SIZE + FORM_PADDING + 10), 0)
            };

            // Сообщение - на всю ширину
            var messageLabel = new Label
            {
                Text = displayMessage,
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(220, 220, 220),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
                AutoSize = false,
                MinimumSize = new Size(formWidth - FORM_PADDING * 2, 0),
                MaximumSize = new Size(formWidth - FORM_PADDING * 2, 0),
                TextAlign = ContentAlignment.TopLeft
            };

            // Рассчитываем позицию сообщения (под аватаркой)
            int messageTop = FORM_PADDING + AVATAR_SIZE + 8;
            int messageLeft = FORM_PADDING;

            // Рассчитываем высоту для 5 строк
            using (Graphics g = messageLabel.CreateGraphics())
            {
                float lineHeight = g.MeasureString("Sample text", messageLabel.Font).Height;
                int calculatedHeight = (int)(lineHeight * 5.5);

                SizeF actualSize = g.MeasureString(displayMessage, messageLabel.Font, formWidth - FORM_PADDING * 2);
                int actualHeight = (int)Math.Min(actualSize.Height, calculatedHeight);

                messageLabel.Height = actualHeight;
                messageLabel.Width = formWidth - FORM_PADDING * 2;
            }

            messageLabel.Location = new Point(messageLeft, messageTop);

            // Кнопка закрытия
            closeButton = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(CLOSE_BUTTON_SIZE, CLOSE_BUTTON_SIZE),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false,
                FlatAppearance = { BorderSize = 0 },
                Location = new Point(this.ClientSize.Width - CLOSE_BUTTON_SIZE - FORM_PADDING, FORM_PADDING)
            };

            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 65);
            closeButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 55);

            closeButton.Click += (s, e) =>
            {
                autoCloseTimer?.Stop();
                autoCloseTimer?.Dispose();
                this.Close();
            };

            // Добавляем все элементы на форму
            mainPanel.Controls.Add(avatar);
            mainPanel.Controls.Add(titleLabel);
            mainPanel.Controls.Add(messageLabel);
            mainPanel.Controls.Add(closeButton);

            this.Controls.Add(mainPanel);

            // ========== ОБРАБОТЧИКИ КЛИКОВ ==========
            EventHandler clickHandler = (s, e) =>
            {
                // Игнорируем клик по кнопке закрытия
                if (s == closeButton || (s is Control && HasParent(s as Control, closeButton)))
                    return;

                OnNotificationClick();
            };

            this.Click += clickHandler;
            avatar.Click += clickHandler;
            titleLabel.Click += clickHandler;
            messageLabel.Click += clickHandler;
            mainPanel.Click += clickHandler;

            // Пересчитываем размер формы
            int formHeight = messageLabel.Top + messageLabel.Height + FORM_PADDING;
            this.ClientSize = new Size(formWidth, formHeight);

            // Обновляем позицию кнопки после изменения размера
            closeButton.Location = new Point(this.ClientSize.Width - CLOSE_BUTTON_SIZE - FORM_PADDING, FORM_PADDING);

            // Обновляем ширину сообщения
            messageLabel.Width = this.ClientSize.Width - FORM_PADDING * 2;
            messageLabel.MaximumSize = new Size(this.ClientSize.Width - FORM_PADDING * 2, 0);

            // Обновляем MaximumSize заголовка
            titleLabel.MaximumSize = new Size(this.ClientSize.Width - (FORM_PADDING + AVATAR_SIZE + AVATAR_MARGIN + CLOSE_BUTTON_SIZE + FORM_PADDING + 10), 0);

            // Авто-закрытие
            autoCloseTimer = new Timer();
            autoCloseTimer.Interval = Math.Max(7000, displayMessage.Length / 15);
            autoCloseTimer.Tick += (s, e) => CloseNotification();
            autoCloseTimer.Start();

            PositionNotification();
        }

        private string FormatMessageToMaxLines(string message, int maxLines)
        {
            if (string.IsNullOrEmpty(message)) return message;

            string[] lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length <= maxLines)
                return message;

            return string.Join(Environment.NewLine, lines, 0, maxLines) + "...";
        }

        private bool HasParent(Control child, Control parent)
        {
            Control current = child.Parent;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        private void OnNotificationClick()
        {
            System.Diagnostics.Debug.WriteLine($"=== НАЖАТО УВЕДОМЛЕНИЕ ===");
            System.Diagnostics.Debug.WriteLine($"_userName = '{_userName}'");
            System.Diagnostics.Debug.WriteLine($"_onClick = {(_onClick != null ? "есть" : "null")}");

            autoCloseTimer?.Stop();
            autoCloseTimer?.Dispose();

            string userName = _userName;
            Action<string> onClick = _onClick;

            this.Close();

            if (!string.IsNullOrEmpty(userName) && onClick != null)
            {
                System.Diagnostics.Debug.WriteLine($"Вызываем onClick для '{userName}'");
                try
                {
                    if (Application.OpenForms.Count > 0)
                    {
                        var mainForm = Application.OpenForms[0];
                        mainForm.BeginInvoke(new Action(() =>
                        {
                            onClick(userName);
                        }));
                    }
                    else
                    {
                        onClick(userName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }

        private void CreateInitialsAvatar(PictureBox avatar, string name)
        {
            try
            {
                var bitmap = new Bitmap(avatar.Width, avatar.Height);
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.FromArgb(80, 80, 85));

                    string initials = GetInitials(name);

                    using (var font = new Font("Segoe UI", 24, FontStyle.Bold))
                    {
                        var textSize = g.MeasureString(initials, font);
                        var x = (avatar.Width - textSize.Width) / 2;
                        var y = (avatar.Height - textSize.Height) / 2;

                        using (var brush = new SolidBrush(Color.White))
                        {
                            g.DrawString(initials, font, brush, x, y);
                        }
                    }
                }
                avatar.Image = bitmap;
            }
            catch { }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            var parts = name.Trim().Split(' ');
            if (parts.Length >= 2)
            {
                return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
            }
            else if (name.Length >= 2)
            {
                return name.Substring(0, 2).ToUpper();
            }
            else
            {
                return name.Substring(0, 1).ToUpper();
            }
        }

        private async void LoadAvatarAsync(PictureBox avatar, string url, string name)
        {
            try
            {
                string fullUrl = url;
                if (url.StartsWith("/"))
                {
                    fullUrl = "https://web.max.ru" + url;
                }

                using (var webClient = new WebClient())
                {
                    webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                    var imageData = await webClient.DownloadDataTaskAsync(fullUrl);

                    using (var ms = new System.IO.MemoryStream(imageData))
                    {
                        var img = Image.FromStream(ms);

                        if (avatar.IsHandleCreated && !avatar.IsDisposed)
                        {
                            avatar.Invoke(new Action(() =>
                            {
                                if (!avatar.IsDisposed)
                                {
                                    avatar.Image = img;
                                    foreach (Control c in avatar.Controls)
                                    {
                                        c.Dispose();
                                    }
                                    avatar.Controls.Clear();
                                }
                            }));
                        }
                    }
                }
            }
            catch
            {
                CreateInitialsAvatar(avatar, name);
            }
        }

        private void InitializeComponents()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.Opacity = 0.95;

            // Используем глобальную настройку для TopMost
            this.TopMost = _alwaysOnTop;

            this.Load += (s, e) => SetRoundedRegion();
            this.Resize += (s, e) => SetRoundedRegion();
        }

        private void SetRoundedRegion()
        {
            using (var path = new GraphicsPath())
            {
                int radius = 12;
                path.AddArc(0, 0, radius, radius, 180, 90);
                path.AddArc(this.Width - radius, 0, radius, radius, 270, 90);
                path.AddArc(this.Width - radius, this.Height - radius, radius, radius, 0, 90);
                path.AddArc(0, this.Height - radius, radius, radius, 90, 90);
                path.CloseFigure();
                this.Region = new Region(path);
            }
        }

        private void PositionNotification()
        {
            lock (lockObj)
            {
                var screen = Screen.PrimaryScreen.WorkingArea;
                int x = screen.Width - this.Width - 10;
                int y = screen.Height - this.Height - 10;

                y -= notificationCount * (this.Height + 8);
                notificationCount++;

                if (y < 10)
                {
                    y = 10;
                }

                this.Location = new Point(x, y);

                this.FormClosed += (s, e) =>
                {
                    lock (lockObj)
                    {
                        notificationCount--;
                    }
                };
            }
        }

        private void CloseNotification()
        {
            autoCloseTimer?.Stop();
            autoCloseTimer?.Dispose();
            this.Close();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            this.Opacity = 0;
            Timer fadeTimer = new Timer();
            fadeTimer.Interval = 20;
            fadeTimer.Tick += (s, args) =>
            {
                if (this.Opacity < 0.95)
                {
                    this.Opacity += 0.1;
                }
                else
                {
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                }
            };
            fadeTimer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            autoCloseTimer?.Dispose();
        }

        public static void Show(string title, string message, string avatarUrl = null, Action<string> onClick = null)
        {
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(message))
                return;

            if (Application.OpenForms.Count > 0)
            {
                var mainForm = Application.OpenForms[0];
                mainForm.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var notification = new CustomNotification(title, message, avatarUrl, onClick);
                        notification.Show();
                    }
                    catch
                    {
                        try
                        {
                            var notification = new CustomNotification(title, message, null, onClick);
                            notification.Show();
                        }
                        catch { }
                    }
                }));
            }
            else
            {
                try
                {
                    var notification = new CustomNotification(title, message, avatarUrl, onClick);
                    notification.Show();
                }
                catch { }
            }
        }
    }
}