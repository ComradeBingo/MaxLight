using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Velopack;

namespace MaxLight;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Проверяем, является ли приложение portable
        bool isPortable = IsPortableMode();

        if (!isPortable)
        {
            // Проверка на единственный экземпляр (только для НЕ portable)
            bool isNewInstance;
            _mutex = new Mutex(true, "MaxLight_Unique_Instance_2024", out isNewInstance);

            if (!isNewInstance)
            {
                Debug.WriteLine("⚠️ Приложение уже запущено! Активируем существующее окно.");

                // Активируем существующее окно
                ActivateExistingWindow();

                // Закрываем новую копию
                Shutdown();
                return;
            }

            Debug.WriteLine("✅ Приложение запущено в единственном экземпляре");
        }
        else
        {
            Debug.WriteLine("📁 Portable режим: несколько копий разрешены");
            // В portable режиме мьютекс НЕ создается!
            _mutex = null;
        }

        ConfigManager.EnsureConfigExists();

        if (!IsDotNetRuntimeInstalled())
        {
            var result = MessageBox.Show(
                "Требуется .NET Runtime 10.0\n\n" +
                "Открыть страницу загрузки?",
                "MaxLight — Ошибка",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://dotnet.microsoft.com/download/dotnet/10.0",
                    UseShellExecute = true
                });
            }

            Shutdown();
            return;
        }

        VelopackApp.Build().Run();
    }

    private bool IsPortableMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
            return true;

        string exePath = AppDomain.CurrentDomain.BaseDirectory;

        // Проверяем в папке с программой (для обратной совместимости)
        if (File.Exists(Path.Combine(exePath, ".portable")))
            return true;

        //  поднимаемся на директорию выше (для Velopack)
        var parentDir = Directory.GetParent(exePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parentDir != null && File.Exists(Path.Combine(parentDir.FullName, ".portable")))
            return true;

        return false;
    }


    // Обработка разворачивания окна при повторной попытке запуска в НЕпортейбл режиме
    private void ActivateExistingWindow()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName("MaxLight");

            Debug.WriteLine($"🔍 Найдено процессов MaxLight: {processes.Length}");

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    Debug.WriteLine($"✅ Активируем окно процесса {process.Id}");

                    const int SW_RESTORE = 9;

                    // Показываем и активируем окно через WinAPI
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    FlashWindow(process.MainWindowHandle, true);

                    // Пытаемся восстановить окно из трея через WPF
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                            if (mainWindow != null)
                            {
                                // Если окно скрыто (в трее) - восстанавливаем
                                if (mainWindow.Visibility == Visibility.Hidden)
                                {
                                    mainWindow.RestoreFromTray();
                                }
                                // Если окно свернуто - разворачиваем
                                else if (mainWindow.WindowState == WindowState.Minimized)
                                {
                                    mainWindow.WindowState = WindowState.Normal;
                                    mainWindow.Show();
                                    mainWindow.Activate();
                                    mainWindow.Focus();
                                }
                                else
                                {
                                    mainWindow.Show();
                                    mainWindow.Activate();
                                    mainWindow.Focus();
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[App] Ошибка восстановления окна: {ex.Message}");
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Ошибка активации: {ex.Message}");
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(System.IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool FlashWindow(System.IntPtr hWnd, bool bInvert);


    //Надо через Velopack собирать билд с флагом "-f net10-x64-runtime"
    //ибо если юзер на старом фреймворке, чтобы не пришлось качать отдельно через открытие браузера.
    //А с флагом типа автоматом дёрнет (в фоне, да не совсем...)
    private static bool IsDotNetRuntimeInstalled()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-runtimes",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("Microsoft.NETCore.App 10.");
        }
        catch
        {
            // dotnet не найден в PATH
            return false;
        }
    }

    
}