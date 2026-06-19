using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Velopack;
using Velopack.Sources;

namespace MaxLight
{
    internal static class Program
    {
        private static Mutex _appMutex;

        [STAThread]
        static void Main()
        {
            try
            {
                bool isPortable = IsPortableMode();

                if (!isPortable)
                {
                    string mutexName = "MaxLight_AppMutex";
                    bool createdNew;
                    _appMutex = new Mutex(true, mutexName, out createdNew);

                    if (!createdNew)
                    {
                        ActivateExistingInstance();
                        return;
                    }
                }

                VelopackApp.Build().Run();

                _ = CheckForUpdatesAsync(isPortable);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form1 = new Form1();
                Application.Run(form1);

                _appMutex?.ReleaseMutex();
                _appMutex?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                var hWnd = FindWindow(null, "Max Light");
                if (hWnd != IntPtr.Zero)
                {
                    ShowWindow(hWnd, 9);
                    SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка активации: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string className, string windowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private static bool IsPortableMode()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
                return true;

            string exeName = Path.GetFileName(Application.ExecutablePath);
            if (exeName.Contains(".portable.exe"))
                return true;

            string portableFile = Path.Combine(Application.StartupPath, ".portable");
            if (File.Exists(portableFile))
                return true;

            string parentFolder = Path.GetFullPath(Path.Combine(Application.StartupPath, ".."));
            string parentPortableFile = Path.Combine(parentFolder, ".portable");
            if (File.Exists(parentPortableFile))
                return true;

            return false;
        }

        private static async Task CheckForUpdatesAsync(bool isPortable)
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/ComradeBingo/MaxLight", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    string versionText = newVersion.TargetFullRelease.Version.ToString();
                    string portableHint = isPortable ? "\n(будет сохранена portable-версия)" : "";
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
                        return !string.IsNullOrEmpty(releaseData?.Body) ? releaseData.Body : "📝 Описание изменений не найдено.";
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
    }
}