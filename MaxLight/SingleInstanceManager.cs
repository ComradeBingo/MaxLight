using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MaxLight
{
    public static class SingleInstanceManager
    {
        private static Mutex _mutex;
        private const string MUTEX_NAME = "MaxLight_Instance_Mutex";
        private const string PORTABLE_ARG = "--portable";

        /// <summary>
        /// Проверяет, является ли текущий запуск portable-версией
        /// </summary>
        public static bool IsPortableMode()
        {
            var args = Environment.GetCommandLineArgs();
            return args.Any(a => a.Equals(PORTABLE_ARG, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Проверяет, запущена ли уже другая копия приложения
        /// </summary>
        /// <returns>true - если копия уже запущена, false - если можно запускаться</returns>
        public static bool IsAnotherInstanceRunning()
        {
            try
            {
                // Проверяем, запущен ли процесс с тем же именем (без .portable)
                string currentProcessName = Process.GetCurrentProcess().ProcessName;

                // Если мы в portable режиме, ищем процессы без .portable
                if (IsPortableMode())
                {
                    // Portable версия может запускаться параллельно с обычной
                    // Поэтому проверяем только процессы с .portable
                    var portableProcesses = Process.GetProcesses()
                        .Where(p => p.ProcessName.Contains("MaxLight") &&
                                   p.ProcessName.Contains("portable") &&
                                   p.Id != Process.GetCurrentProcess().Id);

                    if (portableProcesses.Any())
                    {
                        return true;
                    }
                }
                else
                {
                    // Обычная версия - блокируем любые другие копии (и portable, и обычные)
                    var allProcesses = Process.GetProcesses()
                        .Where(p => p.ProcessName.Contains("MaxLight") &&
                                   p.Id != Process.GetCurrentProcess().Id);

                    if (allProcesses.Any())
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                // В случае ошибки лучше разрешить запуск
                return false;
            }
        }

        /// <summary>
        /// Проверяет, можно ли запускать приложение
        /// </summary>
        public static bool CanStartApplication()
        {
            // Если это portable версия - всегда разрешаем запуск
            if (IsPortableMode())
            {
                return true;
            }

            // Для обычной версии - проверяем, нет ли уже запущенной копии
            if (IsAnotherInstanceRunning())
            {
                // Активируем существующее окно
                ActivateExistingInstance();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Активирует существующее окно приложения
        /// </summary>
        private static void ActivateExistingInstance()
        {
            try
            {
                // Ищем процесс с таким же именем (без .portable)
                var currentProcess = Process.GetCurrentProcess();
                var mainProcess = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("MaxLight") &&
                               !p.ProcessName.Contains("portable") &&
                               p.Id != currentProcess.Id)
                    .FirstOrDefault();

                if (mainProcess != null)
                {
                    // Активируем окно через WinAPI
                    IntPtr hWnd = mainProcess.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                        FlashWindow(hWnd, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка активации окна: {ex.Message}");
            }
        }

        /// <summary>
        /// Создает мьютекс для предотвращения множественных запусков
        /// </summary>
        public static bool TryCreateMutex()
        {
            // Для portable версии не используем мьютекс
            if (IsPortableMode())
            {
                return true;
            }

            try
            {
                string mutexId = $"{MUTEX_NAME}_{Environment.UserName}";
                bool createdNew;
                _mutex = new Mutex(true, mutexId, out createdNew);
                return createdNew;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Освобождает мьютекс при закрытии приложения
        /// </summary>
        public static void ReleaseMutex()
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                    _mutex = null;
                }
                catch { }
            }
        }

        #region WinAPI Imports

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        private const int SW_RESTORE = 9;

        #endregion
    }
}