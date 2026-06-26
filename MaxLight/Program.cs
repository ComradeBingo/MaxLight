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
        private static Form1 _mainForm;
        private static bool _hasUpdate = false;
        private static string _updateVersion = "";
        private static string _updateReleaseNotes = "";
        private static CancellationTokenSource _updateCheckerCts;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12); // Проверка каждые 12 часов

        [STAThread]
        static void Main()
        {
            try
            {
                bool isPortable = IsPortableMode();

                if (!isPortable)
                {
                    bool createdNew;
                    _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "MaxLight_ActivateEvent", out createdNew);

                    if (!createdNew)
                    {
                        _activateEvent.Set();
                        Debug.WriteLine("📢 Сигнал активации отправлен существующему экземпляру");
                        Thread.Sleep(500);
                        return;
                    }
                }

                VelopackApp.Build().Run();

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form1 = new Form1(_activateEvent);
                _mainForm = form1;
                form1.UpdateNotificationClicked += OnUpdateNotificationClicked;

                // ★ ЗАПУСКАЕМ ФОНОВУЮ ПРОВЕРКУ ОБНОВЛЕНИЙ ★
                _updateCheckerCts = new CancellationTokenSource();
                _ = StartUpdateCheckerAsync(isPortable, _updateCheckerCts.Token);

                Application.Run(form1);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ========== ФОНОВЫЙ ПРОВЕРЩИК ОБНОВЛЕНИЙ ==========
        private static async Task StartUpdateCheckerAsync(bool isPortable, CancellationToken cancellationToken)
        {
            Debug.WriteLine($"🔄 Запущен фоновый проверщик обновлений (интервал: {CheckInterval.TotalHours} ч)");

            // Первая проверка через 5 секунд после запуска (чтобы форма успела загрузиться)
            await Task.Delay(5000, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForUpdatesAsync(isPortable);

                    // Ждем указанный интервал перед следующей проверкой
                    await Task.Delay(CheckInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("⏹️ Проверка обновлений остановлена");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Ошибка в фоновом проверщике: {ex.Message}");
                    // В случае ошибки ждем 15 минут и пробуем снова
                    await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
                }
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

            string parentFolder = Path.GetFullPath(Path.Combine(Application.StartupPath, ".."));
            string parentPortableFile = Path.Combine(parentFolder, ".portable");
            if (File.Exists(parentPortableFile))
                return true;

            return false;
        }

        private static void OnUpdateNotificationClicked(object sender, EventArgs e)
        {
            if (!_hasUpdate || string.IsNullOrEmpty(_updateVersion))
                return;

            try
            {
                bool isPortable = IsPortableMode();
                string portableHint = isPortable ? "\n(будет сохранена portable-версия)" : "";

                using (var updateForm = new UpdateDialog(_updateVersion, _updateReleaseNotes, portableHint))
                {
                    var result = updateForm.ShowDialog(_mainForm);

                    if (result == DialogResult.Yes)
                    {
                        _mainForm?.HideUpdateNotification();
                        _ = PerformUpdateAsync(_updateVersion);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии диалога обновления: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static async Task PerformUpdateAsync(string version)
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/ComradeBingo/MaxLight", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    await mgr.DownloadUpdatesAsync(newVersion);
                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления: {ex.Message}");
                MessageBox.Show($"Не удалось выполнить обновление: {ex.Message}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ========== ОСНОВНАЯ ПРОВЕРКА ОБНОВЛЕНИЙ ==========
        private static async Task CheckForUpdatesAsync(bool isPortable)
        {
            try
            {
                Debug.WriteLine($"🔍 Проверка обновлений... (текущая сохраненная версия: {(_hasUpdate ? _updateVersion : "нет")})");

                var mgr = new UpdateManager(new GithubSource("https://github.com/ComradeBingo/MaxLight", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    string newVersionStr = newVersion.TargetFullRelease.Version.ToString();
                    Debug.WriteLine($"📦 Найдена версия {newVersionStr} на GitHub");

                    // ★ Проверяем, это более свежая версия, чем уже сохраненная ★
                    if (!_hasUpdate || IsNewerVersion(newVersionStr, _updateVersion))
                    {
                        Debug.WriteLine($"🔄 Обновление версии: {_updateVersion} → {newVersionStr}");

                        _hasUpdate = true;
                        _updateVersion = newVersionStr;
                        _updateReleaseNotes = await GetReleaseNotesFromGitHub(_updateVersion);

                        // ★ Обновляем уведомление в TitleBar ★
                        _mainForm?.ShowUpdateNotification(_updateVersion);
                        Debug.WriteLine($"✅ Уведомление обновлено до версии {_updateVersion}");
                    }
                    else
                    {
                        Debug.WriteLine($"ℹ️ Версия {newVersionStr} не новее текущей сохраненной ({_updateVersion})");
                    }
                }
                else
                {
                    Debug.WriteLine("ℹ️ Новых обновлений не найдено.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        public static async Task ForceCheckUpdatesAsync()
        {
            try
            {
                bool isPortable = IsPortableMode();
                await CheckForUpdatesAsync(isPortable);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ForceCheckUpdatesAsync ошибка: {ex.Message}");
                throw; // Пробрасываем исключение дальше
            }
        }

        // ========== СРАВНЕНИЕ ВЕРСИЙ ==========
        private static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                var newVer = new Version(newVersion);
                var currentVer = new Version(currentVersion);
                return newVer > currentVer;
            }
            catch
            {
                // Если не удалось распарсить - считаем, что новая версия новее
                return true;
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

            [JsonProperty("published_at")]
            public string PublishedAt { get; set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; set; }
        }
    }
}