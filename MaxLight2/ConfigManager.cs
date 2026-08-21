using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaxLight;

public static class ConfigManager
{
    private static readonly string ConfigPath;
    private static ConfigData? _config;
    private static readonly object _lock = new object();
    private static readonly bool _isPortable;

    static ConfigManager()
    {
        _isPortable = CheckPortableMode();
        ConfigPath = GetConfigPath();

        Debug.WriteLine($"📁 Режим: {(_isPortable ? "Portable" : "Обычный")}");
        Debug.WriteLine($"📁 Конфигурация: {ConfigPath}");

        LoadConfig();
    }

    public static bool IsPortable => _isPortable;

    private static string GetConfigPath()
    {
        if (_isPortable)
        {
            // Portable: конфиг в папке с программой
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        }

        // КАК В СТАРОЙ ВЕРСИИ - на уровень выше current
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string appDataFolder = Path.GetFullPath(Path.Combine(baseDirectory, ".."));

        if (!Directory.Exists(appDataFolder))
        {
            try
            {
                Directory.CreateDirectory(appDataFolder);
                Debug.WriteLine($"📁 Создана папка: {appDataFolder}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка создания папки: {ex.Message}");
            }
        }

        return Path.Combine(appDataFolder, "config.json");
    }

    private static bool CheckPortableMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Any(a => a.Equals("--portable", StringComparison.OrdinalIgnoreCase)))
            return true;

        string exePath = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(exePath, ".portable")))
            return true;

        return false;
    }

    // ==========================================
    // АВТОЗАПУСК (только в обычном режиме) Вроде как дублирование логики на всякий случай.
    // Потому что бывает проёб с .portable флагом (не в той папке создается)
    // ==========================================

    public static bool IsAutoStartEnabled()
    {
        if (_isPortable) return false;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            return key?.GetValue("MaxLight") != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetAutoStart(bool enabled)
    {
        if (_isPortable) return;

        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (enabled)
            {
                string exePath = Environment.ProcessPath ??
                    System.Reflection.Assembly.GetExecutingAssembly().Location;
                key?.SetValue("MaxLight", $"\"{exePath}\"");
            }
            else
            {
                key?.DeleteValue("MaxLight", false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка настройки автозапуска: {ex.Message}");
        }
    }

    // ==========================================
    // ЗАГРУЗКА/СОХРАНЕНИЕ КОНФИГА
    // ==========================================

    private static void LoadConfig()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _config = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();

                    // Расшифровываем токен
                    if (_config?.Auth != null && !string.IsNullOrEmpty(_config.Auth.EncryptedToken))
                    {
                        try
                        {
                            _config.Auth.Token = DecryptString(_config.Auth.EncryptedToken);
                            Debug.WriteLine($"✅ Токен расшифрован: ...{_config.Auth.Token[^4..]}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ Ошибка расшифровки: {ex.Message}");
                            _config.Auth.Token = null;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("⚠️ EncryptedToken пустой или отсутствует");
                    }
                }
                else
                {
                    Debug.WriteLine("ℹ️ Файл конфига не найден, создаем новый");
                    _config = new ConfigData();
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка загрузки config: {ex.Message}");
                _config = new ConfigData();
            }
        }
    }

    //это костыль, который был сделан при попытке перейти на обнову (Net10). На там засада в другом оказалась...
    public static void SaveConfig()
    {
        lock (_lock)
        {
            try
            {
                // КЛОНИРУЕМ конфиг для сохранения
                var configToSave = new ConfigData
                {
                    Auth = _config?.Auth != null ? new AuthData
                    {
                        Token = null,  // Не сохраняем открытый токен
                        EncryptedToken = !string.IsNullOrEmpty(_config.Auth.Token)
                            ? EncryptString(_config.Auth.Token)
                            : _config.Auth.EncryptedToken,
                        TokenPreview = !string.IsNullOrEmpty(_config.Auth.Token)
                            ? "..." + _config.Auth.Token[^Math.Min(4, _config.Auth.Token.Length)..]
                            : _config.Auth.TokenPreview,
                        ViewerId = _config.Auth.ViewerId,
                        DeviceId = _config.Auth.DeviceId,
                        SavedAt = _config.Auth.SavedAt
                    } : null,
                    PinHash = _config?.PinHash,
                    Proxy = _config?.Proxy,
                    DownloadPath = _config?.DownloadPath,
                    AskEveryTime = _config?.AskEveryTime ?? false,
                    NotificationsOnTop = _config?.NotificationsOnTop ?? true,
                    WindowState = _config?.WindowState
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(configToSave, options);

                Debug.WriteLine($"Сохраняем JSON ({json.Length} байт)");

                // Убеждаемся, что папка существует
                string? directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Debug.WriteLine($"📁 Создана папка: {directory}");
                }

                File.WriteAllText(ConfigPath, json);
                Debug.WriteLine($"✅ Config записан в файл: {ConfigPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка сохранения config: {ex.Message}");
                Debug.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }

    // ==========================================
    // DPAPI ШИФРОВАНИЕ
    // ==========================================

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MaxLight_Secure_Store_2024");

    private static string EncryptString(string plainText)
    {
        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            return plainText;
        }
    }

    private static string DecryptString(string encryptedText)
    {
        try
        {
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
            byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return encryptedText;
        }
    }

    // ==========================================
    // АВТОРИЗАЦИЯ (вот здесь, кстати, есть отличие по ключам сохранения авторизации в конфиге, по сравнению с net framework)
    // ==========================================

    public static void SaveAuthData(string token, long? viewerId, string? deviceId)
    {
        if (_config == null) LoadConfig();

        Debug.WriteLine($"=== SaveAuthData ===");
        Debug.WriteLine($"Token получен: {(token != null ? token[..Math.Min(10, token.Length)] + "..." : "NULL")}");
        Debug.WriteLine($"ViewerId: {viewerId}");
        Debug.WriteLine($"DeviceId: {deviceId}");

        if (string.IsNullOrEmpty(token))
        {
            Debug.WriteLine("❌ Токен пустой! Не сохраняем.");
            return;
        }

        _config!.Auth = new AuthData
        {
            Token = token,
            EncryptedToken = null,
            ViewerId = viewerId,
            DeviceId = deviceId,
            SavedAt = DateTime.Now
        };

        Debug.WriteLine($"_config.Auth.Token: {(_config.Auth.Token != null ? _config.Auth.Token[..Math.Min(10, _config.Auth.Token.Length)] + "..." : "NULL")}");

        SaveConfig();

        Debug.WriteLine($"✅ Config сохранен в: {ConfigPath}");
        Debug.WriteLine($"✅ Файл существует: {File.Exists(ConfigPath)}");

        if (File.Exists(ConfigPath))
        {
            string savedJson = File.ReadAllText(ConfigPath);
            Debug.WriteLine($"Содержимое файла: {savedJson[..Math.Min(200, savedJson.Length)]}...");
        }
    }

    public static AuthData? GetAuthData()
    {
        if (_config == null) LoadConfig();
        return _config?.Auth;
    }

    public static void UpdateDeviceId(string deviceId)
    {
        if (_config?.Auth != null)
        {
            _config.Auth.DeviceId = deviceId;
            SaveConfig();
        }
    }

    //Удаляем данные авторизации
    public static void ClearAuthData()
    {
        if (_config != null)
        {
            _config.Auth = null;
            _config.PinHash=null; // на всякий случай пин-код тоже сносим
            SaveConfig();
        }
    }

    // ==========================================
    // PIN-КОД
    // ==========================================

    public static void SavePinCode(string pin)
    {
        if (_config == null) LoadConfig();
        string hash = ComputeHash(pin);
        _config!.PinHash = EncryptString(hash);
        SaveConfig();
    }

    public static bool VerifyPinCode(string pin)
    {
        if (_config == null) LoadConfig();
        if (string.IsNullOrEmpty(_config?.PinHash)) return true;

        try
        {
            string savedHash = DecryptString(_config.PinHash);
            string inputHash = ComputeHash(pin);
            return savedHash == inputHash;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPinSet()
    {
        if (_config == null) LoadConfig();
        return !string.IsNullOrEmpty(_config?.PinHash);
    }

    public static void RemovePinCode()
    {
        if (_config != null)
        {
            _config.PinHash = null;
            SaveConfig();
        }
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }

    // ==========================================
    // ПРОКСИ (работает, перезапускает WEbView2 после применения настроек)
    // ==========================================

    public static void SaveProxySettings(bool enabled, string server, int port)
    {
        if (_config == null) LoadConfig();
        _config!.Proxy = new ProxySettings { Enabled = enabled, Server = server, Port = port };
        SaveConfig();
    }

    public static ProxySettings? GetProxySettings()
    {
        if (_config == null) LoadConfig();
        return _config?.Proxy;
    }

    // ==========================================
    // ПАПКА ЗАГРУЗОК
    // ==========================================

    public static void SaveDownloadPath(string? path)
    {
        if (_config == null) LoadConfig();
        _config!.DownloadPath = path;
        SaveConfig();
    }

    //Если папки нет, или портейбл режим, то делаем папку Downloads в корне с прогой
    public static string GetDownloadPath()
    {
        if (_config == null) LoadConfig();

        if (!string.IsNullOrEmpty(_config?.DownloadPath) && Directory.Exists(_config.DownloadPath))
            return _config.DownloadPath;

        // Путь по умолчанию
        if (_isPortable)
        {
            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            if (!Directory.Exists(portablePath)) Directory.CreateDirectory(portablePath);
            return portablePath;
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    public static void SaveAskEveryTime(bool ask)
    {
        if (_config == null) LoadConfig();
        _config!.AskEveryTime = ask;
        SaveConfig();
    }

    public static bool AskEveryTime()
    {
        if (_config == null) LoadConfig();
        return _config?.AskEveryTime ?? false;
    }

    // ==========================================
    // УВЕДОМЛЕНИЯ
    // ==========================================

    public static void SaveNotificationsOnTop(bool onTop)
    {
        if (_config == null) LoadConfig();
        _config!.NotificationsOnTop = onTop;
        SaveConfig();
    }

    public static bool GetNotificationsOnTop()
    {
        if (_config == null) LoadConfig();
        return _config?.NotificationsOnTop ?? true;
    }

    // ==========================================
    // СОСТОЯНИЕ ОКНА
    // ==========================================

    public static void SaveWindowState(int left, int top, int width, int height, bool maximized)
    {
        if (_config == null) LoadConfig();
        _config!.WindowState = new WindowStateData
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Maximized = maximized
        };
        SaveConfig();
    }

    public static WindowStateData? GetWindowState()
    {
        if (_config == null) LoadConfig();
        return _config?.WindowState;
    }

    // ==========================================
    // ВОССТАНОВЛЕНИЕ CONFIG ПРИ ЗАПУСКЕ
    // ==========================================

    /// <summary>
    /// Проверяет наличие конфига и создает новый при необходимости
    /// </summary>
    public static void EnsureConfigExists()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Debug.WriteLine($"ℹ️ Конфиг не найден, создаем новый");
                _config = new ConfigData();
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Ошибка проверки конфига: {ex.Message}");
        }
    }

    // ==========================================
    // МОДЕЛИ
    // ==========================================

    public class ConfigData
    {
        public AuthData? Auth { get; set; }
        public string? PinHash { get; set; }
        public ProxySettings? Proxy { get; set; }
        public string? DownloadPath { get; set; }
        public bool AskEveryTime { get; set; }
        public bool NotificationsOnTop { get; set; } = true;
        public WindowStateData? WindowState { get; set; }
    }

    public class AuthData
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public string? Token { get; set; }
        public string? EncryptedToken { get; set; }
        public string? TokenPreview { get; set; }
        public long? ViewerId { get; set; }
        public string? DeviceId { get; set; }
        public DateTime? SavedAt { get; set; }
    }

    public class ProxySettings
    {
        public bool Enabled { get; set; }
        public string? Server { get; set; }
        public int Port { get; set; }
    }

    public class WindowStateData
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool Maximized { get; set; }
    }
}