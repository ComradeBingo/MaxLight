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
        private static EventWaitHandle _activateEvent;

        [STAThread]
        static void Main()
        {
            try
            {
                // Проверяем portable режим
                bool isPortable = IsPortableMode();

                // Проверяем, есть ли уже запущенный экземпляр (только для обычной версии)
                if (!isPortable)
                {
                    bool createdNew;
                    _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "MaxLight_ActivateEvent", out createdNew);

                    if (!createdNew)
                    {
                        // Сигнализируем существующему экземпляру
                        _activateEvent.Set();
                        Debug.WriteLine("📢 Сигнал активации отправлен существующему экземпляру");
                        Thread.Sleep(500);
                        return;
                    }
                }

                // Инициализация Velopack
                VelopackApp.Build().Run();

                // Проверка обновлений в фоне
                CheckForUpdatesAsync(isPortable);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Создаем Form1 и передаем EventWaitHandle
                var form1 = new Form1(_activateEvent);
                Application.Run(form1);

                // Освобождаем ресурсы
                _activateEvent?.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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

            return false;
        }

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