using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Velopack;
using Velopack.Sources;

namespace MaxLight
{
    internal static class Program
    {
        private const string PORTABLE_ARG = "--portable";

        [STAThread]
        static void Main()
        {
            try
            {
                // Проверяем аргументы командной строки
                var args = Environment.GetCommandLineArgs();
                bool isPortable = args.Any(a => a.Equals(PORTABLE_ARG, StringComparison.OrdinalIgnoreCase));

                // ===== ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА PORTABLE =====
                // Проверяем имя файла и наличие .portable
                if (!isPortable)
                {
                    string exeName = Path.GetFileName(Application.ExecutablePath);
                    if (exeName.Contains(".portable.exe"))
                        isPortable = true;
                }
                if (!isPortable)
                {
                    string portableFile = Path.Combine(Application.StartupPath, ".portable");
                    if (File.Exists(portableFile))
                        isPortable = true;
                }

                // ===== PORTABLE ВЕРСИЯ: ПРОПУСКАЕМ ПРОВЕРКУ =====
                if (!isPortable)
                {
                    // Проверяем, не запущена ли уже копия (ТОЛЬКО для обычной версии)
                    if (IsAnotherInstanceRunning())
                    {
                        ActivateExistingInstance();
                        return;
                    }

                    // Для обычной версии создаем мьютекс
                    if (!TryCreateMutex())
                    {
                        ActivateExistingInstance();
                        return;
                    }
                }
                else
                {
                    // Portable версия - логируем, что запущена в портативном режиме
                    Debug.WriteLine("📁 Запуск в Portable режиме (несколько экземпляров разрешены)");
                }

                // Инициализация Velopack
                VelopackApp.Build().Run();

                // Проверка обновлений в фоне
                CheckForUpdatesAsync(isPortable);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());

                // Освобождаем мьютекс при выходе (только если он был создан)
                ReleaseMutex();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Single Instance Management

        private static System.Threading.Mutex _mutex;
        private const string MUTEX_NAME = "MaxLight_Instance_Mutex";

        private static bool IsAnotherInstanceRunning()
        {
            try
            {
                var processes = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("MaxLight") &&
                               p.Id != Process.GetCurrentProcess().Id);

                // Проверяем, есть ли обычная версия (не portable)
                var normalProcesses = processes.Where(p => !p.ProcessName.Contains("portable"));
                if (normalProcesses.Any())
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCreateMutex()
        {
            try
            {
                string mutexId = $"{MUTEX_NAME}_{Environment.UserName}";
                bool createdNew;
                _mutex = new System.Threading.Mutex(true, mutexId, out createdNew);
                return createdNew;
            }
            catch
            {
                return false;
            }
        }

        private static void ReleaseMutex()
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

        private static void ActivateExistingInstance()
        {
            try
            {
                var mainProcess = Process.GetProcesses()
                    .Where(p => p.ProcessName.Contains("MaxLight") &&
                               !p.ProcessName.Contains("portable") &&
                               p.Id != Process.GetCurrentProcess().Id)
                    .FirstOrDefault();

                if (mainProcess != null && mainProcess.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(mainProcess.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(mainProcess.MainWindowHandle);
                    FlashWindow(mainProcess.MainWindowHandle, true);
                }
                else
                {
                    var anyProcess = Process.GetProcesses()
                        .Where(p => p.ProcessName.Contains("MaxLight") &&
                                   p.Id != Process.GetCurrentProcess().Id)
                        .FirstOrDefault();

                    if (anyProcess != null && !string.IsNullOrEmpty(anyProcess.MainModule?.FileName))
                    {
                        Process.Start(anyProcess.MainModule.FileName, "--activate");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка активации окна: {ex.Message}");
            }
        }

        #endregion

        #region Update Management

        private static async void CheckForUpdatesAsync(bool isPortable)
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/ComradeBingo/MaxLight", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    string versionText = newVersion.TargetFullRelease.Version.ToString();
                    string portableHint = isPortable ? "\n(будет сохранена portable-версия)" : "";

                    // Загружаем release notes с GitHub
                    string releaseNotes = await GetReleaseNotesFromGitHub(versionText);

                    using (var updateForm = new UpdateDialog(versionText, releaseNotes, portableHint))
                    {
                        var result = updateForm.ShowDialog();

                        if (result == DialogResult.Yes)
                        {
                            await mgr.DownloadUpdatesAsync(newVersion);
                            mgr.ApplyUpdatesAndRestart(newVersion);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
            }
        }

        private static async Task<string> GetReleaseNotesFromGitHub(string version)
        {
            try
            {
                string tag = version.StartsWith("v") ? version : $"v{version}";
                string apiUrl = $"https://api.github.com/repos/ComradeBingo/MaxLight/releases/tags/{tag}";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "MaxLight-App");

                    var response = await client.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var releaseData = JsonConvert.DeserializeObject<GitHubRelease>(json);

                        if (!string.IsNullOrEmpty(releaseData?.Body))
                        {
                            return releaseData.Body;
                        }
                        else
                        {
                            return "📝 Описание изменений не найдено.";
                        }
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return "📝 Релиз с этим тегом не найден на GitHub.";
                    }
                    else
                    {
                        return $"⚠️ Не удалось загрузить описание изменений (код: {response.StatusCode}).";
                    }
                }
            }
            catch (HttpRequestException)
            {
                return "⚠️ Не удалось подключиться к GitHub для загрузки описания изменений.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки release notes: {ex.Message}");
                return "⚠️ Ошибка загрузки описания изменений.";
            }
        }

        #endregion

        #region WinAPI Imports

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hWnd, bool bInvert);

        private const int SW_RESTORE = 9;

        #endregion

        #region GitHub Release Model

        private class GitHubRelease
        {
            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("body")]
            public string Body { get; set; }

            [JsonProperty("tag_name")]
            public string TagName { get; set; }
        }

        #endregion
    }
}