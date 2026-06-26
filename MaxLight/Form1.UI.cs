using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== DllImport ДЛЯ RESIZE ==========
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_TOP = 12, HT_TOPLEFT = 13, HT_TOPRIGHT = 14;
        private const int HT_BOTTOM = 15, HT_BOTTOMLEFT = 16, HT_BOTTOMRIGHT = 17;
        private const int HT_LEFT = 10, HT_RIGHT = 11;

        // ========== ВСЕ UI МЕТОДЫ ==========

        private void CreateErrorPanel()
        {
            errorPanel = new Panel
            {
                BackColor = Color.FromArgb(248, 249, 250),
                Visible = false,
                Dock = DockStyle.None
            };

            Panel centerPanel = new Panel
            {
                Size = new Size(400, 200),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label iconLabel = new Label
            {
                Text = "⚠️",
                Font = new Font("Segoe UI", 48, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 53, 69),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(400, 80),
                Location = new Point(0, 20)
            };

            Label messageLabel = new Label
            {
                Text = "Не могу подключиться",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 58, 64),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(400, 40),
                Location = new Point(0, 100)
            };

            Label subMessageLabel = new Label
            {
                Text = "Проверьте соединение с интернетом",
                Font = new Font("Segoe UI", 11, FontStyle.Regular),
                ForeColor = Color.FromArgb(108, 117, 125),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(400, 30),
                Location = new Point(0, 140)
            };

            Button retryButton = new Button
            {
                Text = "Повторить",
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            retryButton.FlatAppearance.BorderSize = 0;
            retryButton.Location = new Point((400 - retryButton.Width) / 2, 180);
            retryButton.Click += async (s, e) => await RetryLoad();

            centerPanel.Controls.Add(iconLabel);
            centerPanel.Controls.Add(messageLabel);
            centerPanel.Controls.Add(subMessageLabel);
            centerPanel.Controls.Add(retryButton);

            centerPanel.Location = new Point(
                (this.ClientSize.Width - centerPanel.Width) / 2,
                (this.ClientSize.Height - titleBar.Height - centerPanel.Height) / 2);

            errorPanel.Controls.Add(centerPanel);
            errorPanel.Resize += (s, e) =>
            {
                centerPanel.Location = new Point(
                    (errorPanel.Width - centerPanel.Width) / 2,
                    (errorPanel.Height - centerPanel.Height) / 2);
            };

            Controls.Add(errorPanel);
        }

        private void ShowConnectionError()
        {
            if (errorPanel != null && !isLoadingCompleted)
            {
                errorPanel.Visible = true;
                errorPanel.BringToFront();
                UpdateErrorPanelPosition();
            }
        }

        private void HideConnectionError()
        {
            if (errorPanel != null)
            {
                errorPanel.Visible = false;
                if (webView != null)
                {
                    webView.BringToFront();
                }
            }
        }

        private async Task RetryLoad()
        {
            HideConnectionError();
            isLoadingCompleted = false;
            await ReloadWebView();
        }

        private void UpdateWebViewPosition()
        {
            if (webView != null && titleBar != null)
            {
                int borderSize = 2;
                int titleBarHeight = titleBar.Height;

                webView.Location = new Point(borderSize, titleBarHeight);
                webView.Size = new Size(
                    this.ClientSize.Width - borderSize * 2,
                    this.ClientSize.Height - titleBarHeight - borderSize
                );
            }
        }

        private void UpdateErrorPanelPosition()
        {
            if (errorPanel != null && titleBar != null)
            {
                int borderSize = 8;
                int titleBarHeight = titleBar.Height;

                errorPanel.Location = new Point(borderSize, titleBarHeight);
                errorPanel.Size = new Size(
                    this.ClientSize.Width - borderSize * 2,
                    this.ClientSize.Height - titleBarHeight - borderSize
                );
            }
        }

        // ========== RESIZE BORDERS ==========

        private void CreateResizeBorders()
        {
            int borderSize = 2; // Значение ширины рамки, которое создается ДО прогрузки стилей

            var topBorder = new Panel { Height = borderSize, Dock = DockStyle.Top, Cursor = Cursors.SizeNS };
            topBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_TOP, IntPtr.Zero); };
            Controls.Add(topBorder);
            topBorder.BringToFront();

            var bottomBorder = new Panel { Height = borderSize, Dock = DockStyle.Bottom, Cursor = Cursors.SizeNS };
            bottomBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_BOTTOM, IntPtr.Zero); };
            Controls.Add(bottomBorder);
            bottomBorder.BringToFront();

            var leftBorder = new Panel { Width = borderSize, Dock = DockStyle.Left, Cursor = Cursors.SizeWE };
            leftBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_LEFT, IntPtr.Zero); };
            Controls.Add(leftBorder);
            leftBorder.BringToFront();

            var rightBorder = new Panel { Width = borderSize, Dock = DockStyle.Right, Cursor = Cursors.SizeWE };
            rightBorder.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_RIGHT, IntPtr.Zero); };
            Controls.Add(rightBorder);
            rightBorder.BringToFront();

            AddCorner(0, 0, Cursors.SizeNWSE, HT_TOPLEFT);
            AddCorner(Width - 16, 0, Cursors.SizeNESW, HT_TOPRIGHT);
            AddCorner(0, Height - 16, Cursors.SizeNESW, HT_BOTTOMLEFT);
            AddCorner(Width - 16, Height - 16, Cursors.SizeNWSE, HT_BOTTOMRIGHT);

            Resize += (s, e) =>
            {
                UpdateCornerPosition(HT_TOPRIGHT, Width - 16, 0);
                UpdateCornerPosition(HT_BOTTOMLEFT, 0, Height - 16);
                UpdateCornerPosition(HT_BOTTOMRIGHT, Width - 16, Height - 16);
            };
        }

        private void AddCorner(int x, int y, Cursor cursor, int hit)
        {
            var p = new Panel { Size = new Size(16, 16), Location = new Point(x, y), Cursor = cursor, BackColor = Color.Transparent };
            p.MouseDown += (s, e) => { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)hit, IntPtr.Zero); };
            Controls.Add(p);
            p.BringToFront();
        }

        private void UpdateCornerPosition(int hit, int x, int y)
        {
            foreach (Control c in Controls)
                if (c is Panel && (c.Location.X == (hit == HT_TOPRIGHT ? Width - 16 : c.Location.X)))
                    c.Location = new Point(x, y);
        }
    }
}