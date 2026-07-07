using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== ВСЕ МЕТОДЫ СИСТЕМНОГО ТРЕЯ ==========

        private void CreateTrayIcon()
        {
            string iconPath = Path.Combine(Application.StartupPath, "app.ico");
            Icon appIcon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application;

            trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "Max Light",
                Visible = true
            };

            trayIcon.DoubleClick += (s, e) => ToggleWindow();

            var contextMenu = new ContextMenuStrip();

            var toggleItem = new ToolStripMenuItem("Открыть/Свернуть");
            toggleItem.Click += (s, e) => ToggleWindow();
            contextMenu.Items.Add(toggleItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var settingsItem = new ToolStripMenuItem("Настройки");
            settingsItem.Click += (s, e) => ShowSettings();
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Выйти");
            exitItem.Click += (s, e) =>
            {
                exitRequested = true;
                Application.Exit();
            };
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void LoadIcons()
        {
            string normalPath = Path.Combine(Application.StartupPath, "app.ico");
            string unreadPath = Path.Combine(Application.StartupPath, "app_unread.ico");

            _normalIcon = File.Exists(normalPath) ? new Icon(normalPath) : SystemIcons.Application;

            if (File.Exists(unreadPath))
            {
                _unreadIcon = new Icon(unreadPath);
            }
            else
            {
                _unreadIcon = CreateUnreadIconOverlay(_normalIcon);
            }

            if (trayIcon != null)
            {
                trayIcon.Icon = _normalIcon;
            }
        }

        private Icon CreateUnreadIconOverlay(Icon baseIcon)
        {
            var bitmap = baseIcon.ToBitmap();
            using (var g = Graphics.FromImage(bitmap))
            {
                using (var brush = new SolidBrush(Color.Red))
                {
                    int dotSize = bitmap.Width / 3;
                    g.FillEllipse(brush, bitmap.Width - dotSize, 0, dotSize, dotSize);
                }
            }
            return Icon.FromHandle(bitmap.GetHicon());
        }

        // ===== ОБНОВЛЕНИЕ ИКОНКИ В ТРЕЕ =====
        private void UpdateTrayIcon(bool hasUnread, int count = 0)
        {
            if (trayIcon == null) return;

            if (hasUnread && !IsWindowVisibleToUser())
            {
                trayIcon.Icon = _unreadIcon;
                trayIcon.Text = count > 0 ? $"Max Light ({count} непрочитанных)" : "Max Light (есть новые сообщения)";
                System.Diagnostics.Debug.WriteLine($"🔴 Иконка трея изменена: есть {count} непрочитанных");
            }
            else
            {
                trayIcon.Icon = _normalIcon;
                trayIcon.Text = "Max Light";
                System.Diagnostics.Debug.WriteLine($"🟢 Иконка трея восстановлена");
            }
        }

        // ===== БЕЙДЖ В ПАНЕЛИ ЗАДАЧ =====
        private void UpdateTaskbarBadge(int count)
        {
            try
            {
                if (!TaskbarManager.IsPlatformSupported) return;

                if (count <= 0)
                {
                    TaskbarManager.Instance.SetOverlayIcon(this.Handle, null, "");
                    return;
                }

                using (Bitmap bitmap = CreateBadgeBitmap(count))
                {
                    IntPtr hIcon = bitmap.GetHicon();
                    using (Icon icon = Icon.FromHandle(hIcon))
                    {
                        TaskbarManager.Instance.SetOverlayIcon(
                            this.Handle,
                            icon,
                            $"{count} непрочитанных сообщений"
                        );
                    }
                    DestroyIcon(hIcon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка обновления бейджа: {ex.Message}");
            }
        }

        // ===== СОЗДАНИЕ ИКОНКИ ДЛЯ БЕЙДЖА =====
        private Bitmap CreateBadgeBitmap(int count)
        {
            int size = 16;
            Bitmap bitmap = new Bitmap(size, size);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                g.FillEllipse(Brushes.Red, 0, 0, size, size);

                using (Pen pen = new Pen(Color.White, 1))
                {
                    g.DrawEllipse(pen, 0, 0, size - 1, size - 1);
                }

                string text = count > 9 ? "9+" : count.ToString();
                using (Font font = new Font("Arial", 8, FontStyle.Bold))
                {
                    SizeF textSize = g.MeasureString(text, font);
                    float x = (size - textSize.Width) / 2;
                    float y = (size - textSize.Height) / 2 + 1;
                    g.DrawString(text, font, Brushes.White, x, y);
                }
            }

            return bitmap;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        // ===== ГЛАВНЫЙ МЕТОД ОБНОВЛЕНИЯ СЧЕТЧИКА =====
        public void UpdateUnreadCount(int count)
        {
            _unreadCount = count;

            UpdateTaskbarBadge(count);

            if (count > 0 && !IsWindowVisibleToUser())
            {
                UpdateTrayIcon(true, count);
            }
            else
            {
                UpdateTrayIcon(false, 0);
            }

            this.Text = count > 0 ? $"Max Light ({count})" : "Max Light";

            if (count > 0 && !IsWindowVisibleToUser())
            {
                StartAttentionTimer();
            }
            else if (count == 0)
            {
                ResetAttention();
            }

            System.Diagnostics.Debug.WriteLine($"🔔 Обновлен счетчик: {count}");
        }

        private bool IsWindowVisibleToUser()
        {
            return this.Visible && this.WindowState != FormWindowState.Minimized;
        }

        // ===== УВЕЛИЧЕНИЕ СЧЕТЧИКА =====
        private void IncrementUnreadCount()
        {
            int newCount = _unreadCount + 1;
            UpdateUnreadCount(newCount);
            System.Diagnostics.Debug.WriteLine($"📊 Счетчик непрочитанных: {newCount}");
        }

        // ===== СБРОС СЧЕТЧИКА =====
        private void ResetUnreadCount()
        {
            if (_unreadCount > 0)
            {
                UpdateUnreadCount(0);
                System.Diagnostics.Debug.WriteLine($"📊 Счетчик непрочитанных сброшен");
            }
            StopFlashIcon();
            CancelAttentionTimer();
        }

        private void ToggleWindow()
        {
            bool isWindowVisible = IsWindowVisibleToUser();

            if (isWindowVisible)
            {
                MinimizeToTray();
            }
            else
            {
                RestoreFromTray();
            }
        }

        private void MinimizeToTray()
        {
            this.Hide();
            this.ShowInTaskbar = false;
            _ = UpdateWebViewWindowState(false);

            if (_unreadCount > 0)
            {
                UpdateTrayIcon(true, _unreadCount);
            }
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
            webView?.Focus();
            UpdateWebViewPosition();
            ResetAttention();
            ResetUnreadCount();
            UpdateCurrentScreen();
            _ = UpdateWebViewWindowState(true);
        }
    }
}