using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace MaxLight
{
    public partial class Form1
    {
        // ========== ВСЕ МЕТОДЫ УПРАВЛЕНИЯ ОКНОМ ==========

        private void SetupWindowStateTracking()
        {
            // Основные события
            this.Activated += OnWindowActivated;
            this.Deactivate += OnWindowDeactivated;
            this.Resize += OnWindowResize;
            this.VisibleChanged += OnWindowVisibleChanged;

            // Таймер для дополнительной проверки
            _stateCheckTimer = new Timer();
            _stateCheckTimer.Interval = 1000;
            _stateCheckTimer.Tick += OnStateCheckTimer;
            _stateCheckTimer.Start();

            // Отслеживаем перемещение между мониторами
            this.Move += (s, e) => UpdateCurrentScreen();
            this.Resize += (s, e) => UpdateCurrentScreen();
        }

        private void UpdateCurrentScreen()
        {
            if (this.Visible && this.WindowState != FormWindowState.Minimized)
            {
                var screen = Screen.FromControl(this);
                if (screen != _currentScreen)
                {
                    _currentScreen = screen;
                    System.Diagnostics.Debug.WriteLine($"🖥️ Переключено на монитор: {screen.DeviceName}");
                }
            }
        }

        private void OnWindowActivated(object sender, EventArgs e)
        {
            SetWindowActiveState(true);
            UpdateCurrentScreen();
        }

        private void OnWindowDeactivated(object sender, EventArgs e)
        {
            SetWindowActiveState(false);
        }

        private void OnWindowResize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                SetWindowActiveState(false);
            }
            else if (this.WindowState == FormWindowState.Normal && this.ContainsFocus)
            {
                SetWindowActiveState(true);
                UpdateCurrentScreen();
            }
        }

        private void OnWindowVisibleChanged(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                SetWindowActiveState(false);
            }
        }

        private void OnStateCheckTimer(object sender, EventArgs e)
        {
            bool isActive = this.Visible &&
                            this.WindowState != FormWindowState.Minimized &&
                            this.ContainsFocus;

            if (_isWindowActive != isActive)
            {
                SetWindowActiveState(isActive);
            }
        }

        private async void SetWindowActiveState(bool isActive)
        {
            if (_isWindowActive == isActive) return;

            _isWindowActive = isActive;
            _lastActivityCheck = DateTime.Now;

            System.Diagnostics.Debug.WriteLine($"🔄 Состояние окна: {(isActive ? "АКТИВНО" : "НЕАКТИВНО")}");

            await UpdateWebViewWindowState(isActive);
        }

        private async Task UpdateWebViewWindowState(bool isActive)
        {
            if (webView?.CoreWebView2 == null) return;

            try
            {
                string script = $@"
                    try {{
                        if (typeof updateWindowActiveState === 'function') {{
                            updateWindowActiveState({isActive.ToString().ToLower()});
                        }} else {{
                            console.log('⚠️ Функция updateWindowActiveState не найдена');
                        }}
                    }} catch(e) {{
                        console.log('❌ Ошибка обновления состояния:', e);
                    }}
                ";

                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка передачи состояния: {ex.Message}");
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            UpdateWebViewPosition();
            UpdateErrorPanelPosition();

            if (this.WindowState == FormWindowState.Minimized)
            {
                this.ShowInTaskbar = true;
                _ = UpdateWebViewWindowState(false);
            }
            else if (this.WindowState == FormWindowState.Normal && !this.ContainsFocus)
            {
                _ = UpdateWebViewWindowState(false);
            }
            else if (this.WindowState == FormWindowState.Normal && this.ContainsFocus)
            {
                _ = UpdateWebViewWindowState(true);
            }

            UpdateCurrentScreen();
        }

        private void SaveWindowState()
        {
            try
            {
                if (WindowState == FormWindowState.Minimized) return;

                int width, height, left, top;
                if (WindowState == FormWindowState.Normal)
                {
                    width = this.Width;
                    height = this.Height;
                    left = this.Left;
                    top = this.Top;
                }
                else
                {
                    width = this.RestoreBounds.Width;
                    height = this.RestoreBounds.Height;
                    left = this.RestoreBounds.Left;
                    top = this.RestoreBounds.Top;
                }

                width = Math.Max(width, MinimumSize.Width);
                height = Math.Max(height, MinimumSize.Height);

                ConfigManager.SaveWindowState(width, height, left, top, (int)WindowState);
            }
            catch { }
        }

        private void LoadWindowState()
        {
            try
            {
                var state = ConfigManager.GetWindowState();
                if (state == null)
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                    return;
                }

                bool isValidPosition = false;
                if (state.Left != -1 && state.Top != -1)
                {
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        if (state.Left + 50 > screen.Bounds.Left && state.Left - 50 < screen.Bounds.Right &&
                            state.Top + 50 > screen.Bounds.Top && state.Top - 50 < screen.Bounds.Bottom)
                        {
                            isValidPosition = true;
                            break;
                        }
                    }
                }

                if (state.Width >= MinimumSize.Width && state.Height >= MinimumSize.Height && isValidPosition)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Size = new Size(state.Width, state.Height);
                    this.Location = new Point(state.Left, state.Top);
                }
                else
                {
                    this.StartPosition = FormStartPosition.CenterScreen;
                }

                if (state.State == 1)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
            }
            catch
            {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }

        // ========== ОБРАБОТКА КЛИКА ПО ПАНЕЛИ ЗАДАЧ ==========

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;
            const int SC_RESTORE = 0xF120;

            if (m.Msg == WM_SYSCOMMAND)
            {
                int command = m.WParam.ToInt32() & 0xFFF0;

                if (command == SC_MINIMIZE)
                {
                    System.Diagnostics.Debug.WriteLine("📌 Сворачивание из панели задач");
                    base.WndProc(ref m);
                    _ = UpdateWebViewWindowState(false);
                    return;
                }
                else if (command == SC_RESTORE)
                {
                    System.Diagnostics.Debug.WriteLine("📌 Разворачивание из панели задач");
                    base.WndProc(ref m);
                    if (this.ContainsFocus)
                    {
                        _ = UpdateWebViewWindowState(true);
                    }
                    return;
                }
            }

            base.WndProc(ref m);
        }

        public void ToggleWindowState()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
                this.Activate();
                this.ShowInTaskbar = true;
                _ = UpdateWebViewWindowState(true);
                System.Diagnostics.Debug.WriteLine("📌 Окно развернуто из панели задач");
            }
            else
            {
                this.WindowState = FormWindowState.Minimized;
                _ = UpdateWebViewWindowState(false);
                System.Diagnostics.Debug.WriteLine("📌 Окно свернуто в панель задач");
            }
        }

        private void WaitForActivationSignal(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activateEvent.WaitOne(1000))
                    {
                        this.BeginInvoke(new Action(() => ActivateWindow()));
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка в WaitForActivationSignal: {ex.Message}");
                }
            }
        }

        private void ActivateWindow()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔄 Активация окна...");

                if (!this.Visible)
                {
                    this.Show();
                    this.ShowInTaskbar = true;
                }

                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }

                this.Activate();
                this.BringToFront();
                webView?.Focus();

                _ = UpdateWebViewWindowState(true);

                System.Diagnostics.Debug.WriteLine("✅ Окно активировано");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Ошибка активации окна: {ex.Message}");
            }
        }
    }
}