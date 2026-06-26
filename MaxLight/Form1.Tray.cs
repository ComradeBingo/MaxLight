using System;
using System.Drawing;
using System.IO;
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

        private bool IsWindowVisibleToUser()
        {
            return this.Visible && this.WindowState != FormWindowState.Minimized;
        }

        private void IncrementUnreadCount()
        {
            _unreadCount++;
            System.Diagnostics.Debug.WriteLine($"📊 Счетчик непрочитанных: {_unreadCount}");

            if (!IsWindowVisibleToUser())
            {
                UpdateTrayIcon(true, _unreadCount);
            }
            else
            {
                if (!this.ContainsFocus)
                {
                    StartAttentionTimer();
                }
            }
        }

        private void ResetUnreadCount()
        {
            if (_unreadCount > 0)
            {
                _unreadCount = 0;
                UpdateTrayIcon(false, 0);
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