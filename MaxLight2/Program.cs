using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Velopack;

namespace MaxLight;

public class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            
            ConfigManager.EnsureConfigExists();

            
            bool isPortable = args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)) ||
                              File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".portable"));

            // Проверяем родительскую папку (для Velopack)
            if (!isPortable)
            {
                var parentDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (parentDir != null && File.Exists(Path.Combine(parentDir.FullName, ".portable")))
                    isPortable = true;
            }

            bool isVelopackUpdate = args.Any(a =>
                a.Contains("--veloapp", StringComparison.OrdinalIgnoreCase) ||
                a.Contains("--squirrel", StringComparison.OrdinalIgnoreCase) ||
                a.Contains("--apply", StringComparison.OrdinalIgnoreCase));

            

            //  МЬЮТЕКС (только не portable и не обновление) 
            if (!isPortable && !isVelopackUpdate)
            {
                bool isNewInstance;
                _mutex = new Mutex(true, "MaxLight_Unique_Instance_2024", out isNewInstance);

                if (!isNewInstance)
                {
                    
                    ActivateExistingWindow();
                    return;
                }

               
            }
            else if (isVelopackUpdate)
            {
              
            }
            else
            {
               
                _mutex = null;
            }

            //  ПРОВЕРКА .NET RUNTIME 
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
                return;
            }

            //  ИНИЦИАЛИЗАЦИЯ VELOPACK
            VelopackApp.Build().Run();

            // ЗАПУСК WPF ПРИЛОЖЕНИЯ
            

            var app = new App();
            var mainWindow = new MainWindow();
            app.Run(mainWindow);

            
        }
        catch (Exception ex)
        {
            

            MessageBox.Show($"Критическая ошибка при запуске:\n{ex.Message}",
                "MaxLight — Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    //  АКТИВАЦИЯ СУЩЕСТВУЮЩЕГО ОКНА
    private static void ActivateExistingWindow()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName("MaxLight");

           

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    

                    const int SW_RESTORE = 9;
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    FlashWindow(process.MainWindowHandle, true);

                    // Пытаемся восстановить окно через WPF
                    try
                    {
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                            if (mainWindow != null)
                            {
                                if (mainWindow.Visibility == Visibility.Hidden)
                                {
                                    mainWindow.RestoreFromTray();
                                }
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
                       
                    }

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            
        }
    }

    //  ПРОВЕРКА .NET RUNTIME 
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
            return false;
        }
    }

   

    // ========== WINAPI ==========
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);
}