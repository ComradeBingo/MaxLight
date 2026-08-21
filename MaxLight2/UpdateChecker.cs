using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace MaxLight;

public class UpdateChecker
{
    private readonly bool _isPortable;
    private bool _hasUpdate = false;
    private string _updateVersion = "";
    private string _updateReleaseNotes = "";
    private CancellationTokenSource? _updateCheckerCts;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private UpdateManager? _updateManager;

    public event Action<string, string>? UpdateAvailable;
    public bool HasUpdate => _hasUpdate;
    public string UpdateVersion => _updateVersion;
    public string UpdateReleaseNotes => _updateReleaseNotes;

    public UpdateChecker(bool isPortable = false)
    {
        _isPortable = isPortable;

        // Инициализируем UpdateManager один раз
        try
        {
            var source = new GithubSource("https://github.com/ComradeBingo/MaxLight", null, false);
            _updateManager = new UpdateManager(source);
            Debug.WriteLine("✅ UpdateManager инициализирован");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ошибка инициализации UpdateManager: {ex.Message}");
        }
    }

    public void StartBackgroundChecker()
    {
        Debug.WriteLine($"🔄 Запущен фоновый проверщик обновлений (интервал: {CheckInterval.TotalHours} ч)");

        _updateCheckerCts = new CancellationTokenSource();
        _ = StartUpdateCheckerAsync(_updateCheckerCts.Token);
    }

    private async Task StartUpdateCheckerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(5000, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await CheckForUpdatesAsync();
                await Task.Delay(CheckInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка в фоновом проверщике: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
            }
        }
    }

    public async Task<bool> ForceCheckUpdatesAsync()
    {
        try
        {
            await CheckForUpdatesAsync();
            return _hasUpdate;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ ForceCheckUpdatesAsync ошибка: {ex.Message}");
            return false;
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            if (_updateManager == null)
            {
                Debug.WriteLine("❌ UpdateManager не инициализирован");
                return;
            }

            Debug.WriteLine($"🔍 Проверка обновлений...");

            var newVersion = await _updateManager.CheckForUpdatesAsync();

            if (newVersion != null)
            {
                string newVersionStr = newVersion.TargetFullRelease.Version.ToString();
                Debug.WriteLine($"📦 Найдена версия {newVersionStr} на GitHub");

                if (!_hasUpdate || IsNewerVersion(newVersionStr, _updateVersion))
                {
                    Debug.WriteLine($"🔄 Обновление версии: {_updateVersion} → {newVersionStr}");

                    _hasUpdate = true;
                    _updateVersion = newVersionStr;
                    _updateReleaseNotes = await GetReleaseNotesFromGitHub(_updateVersion);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateAvailable?.Invoke(_updateVersion, _updateReleaseNotes);
                    });

                    Debug.WriteLine($"✅ Уведомление обновлено до версии {_updateVersion}");
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
        }
    }

    //100500 раз надо все перепроверять, чтобы не просохотатить последующие обновы при переходе на NET10 
    public async Task DownloadAndInstallUpdateAsync()
    {
        try
        {
            if (_updateManager == null)
            {
                MessageBox.Show("Система обновлений не инициализирована.",
                              "Ошибка",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
                return;
            }

            Debug.WriteLine("Проверка наличия обновления...");
            var newVersion = await _updateManager.CheckForUpdatesAsync();

            if (newVersion == null)
            {
                MessageBox.Show("Обновлений не найдено.",
                              "Информация",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            Debug.WriteLine($"📥 Скачивание обновления {newVersion.TargetFullRelease.Version}...");

            // Скачиваем обновление
            await _updateManager.DownloadUpdatesAsync(newVersion);

            Debug.WriteLine("🔄 Применение обновления и перезапуск...");

            // Применяем обновление и перезапускаем приложение
            _updateManager.ApplyUpdatesAndRestart(newVersion);

            // Если мы дошли сюда, значит что-то пошло не так
            Debug.WriteLine("❌ Не удалось применить обновление");
            MessageBox.Show("Не удалось применить обновление. Попробуйте установить вручную.",
                          "Ошибка",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ошибка обновления: {ex.Message}");
            MessageBox.Show($"Не удалось выполнить обновление: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool IsNewerVersion(string newVersion, string currentVersion)
    {
        try
        {
            var newVer = new Version(newVersion);
            var currentVer = new Version(currentVersion);
            return newVer > currentVer;
        }
        catch
        {
            return true;
        }
    }

    //ну вроде тянет патчноут как надо
    private async Task<string> GetReleaseNotesFromGitHub(string version)
    {
        try
        {
            string tag = version.StartsWith("v") ? version : $"v{version}";
            string apiUrl = $"https://api.github.com/repos/ComradeBingo/MaxLight/releases/tags/{tag}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MaxLight-App");
            var response = await client.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("body", out var bodyElement))
                {
                    return bodyElement.GetString() ?? "📝 Описание изменений не найдено.";
                }
                return "📝 Описание изменений не найдено.";
            }
            return $"⚠️ Не удалось загрузить описание изменений (код: {response.StatusCode}).";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки release notes: {ex.Message}");
            return "⚠️ Ошибка загрузки описания изменений.";
        }
    }

    public void Stop()
    {
        _updateCheckerCts?.Cancel();
        _updateCheckerCts?.Dispose();
        _updateCheckerCts = null;
        Debug.WriteLine("⏹️ Проверка обновлений остановлена");
    }
}