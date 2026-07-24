using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Windows.Forms;

namespace MaxLight
{
    public class ConfigManager
    {
        private static readonly object _lock = new object();

        // Межпроцессная блокировка: при обновлении Velopack старый и новый процессы
        // могут обращаться к config.json одновременно
        private static readonly Mutex _crossProcessLock = new Mutex(false, "MaxLight_ConfigFileMutex");

        private static bool AcquireFileLock()
        {
            try
            {
                return _crossProcessLock.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException)
            {
                // Прежний процесс был убит, не освободив мьютекс — блокировка теперь наша
                return true;
            }
        }

        // ========== ПРАВИЛЬНЫЙ ПУТЬ К CONFIG.JSON ==========
        private static string GetConfigPath()
        {
            // Получаем родительскую папку (на уровень выше current)
            string appDataFolder = Path.GetFullPath(Path.Combine(Application.StartupPath, ".."));

            // Создаем папку, если её нет
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }

            string configPath = Path.Combine(appDataFolder, "config.json");

            System.Diagnostics.Debug.WriteLine($"📁 Путь к config.json: {configPath}");
            return configPath;
        }

        public class ConfigData
        {
            [JsonProperty("Auth")]
            public AuthData Auth { get; set; }

            [JsonProperty("Pin")]
            public string Pin { get; set; }

            [JsonProperty("WindowState")]
            public WindowStateData WindowState { get; set; }

            [JsonProperty("NotificationsOnTop")]
            public bool NotificationsOnTop { get; set; } = true;

            [JsonProperty("Proxy")]
            public ProxySettings Proxy { get; set; }

            [JsonProperty("DownloadPath")]
            public string DownloadPath { get; set; }

            [JsonProperty("AskEveryTime")]
            public bool AskEveryTime { get; set; } = false;
        }

        public class AuthData
        {
            [JsonProperty("Token")]
            public string Token { get; set; }

            [JsonProperty("ViewerId")]
            public long? ViewerId { get; set; }

            [JsonProperty("DeviceId")]
            public string DeviceId { get; set; }

            [JsonProperty("SavedAt")]
            public string SavedAt { get; set; }
        }

        public class WindowStateData
        {
            [JsonProperty("Width")]
            public int Width { get; set; }

            [JsonProperty("Height")]
            public int Height { get; set; }

            [JsonProperty("Left")]
            public int Left { get; set; }

            [JsonProperty("Top")]
            public int Top { get; set; }

            [JsonProperty("State")]
            public int State { get; set; }
        }

        public class ProxySettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled { get; set; }

            [JsonProperty("Server")]
            public string Server { get; set; }

            [JsonProperty("Port")]
            public int Port { get; set; }
        }

        private static ConfigData LoadConfig()
        {
            lock (_lock)
            {
                bool locked = AcquireFileLock();
                try
                {
                    return LoadConfigCore();
                }
                finally
                {
                    if (locked) _crossProcessLock.ReleaseMutex();
                }
            }
        }

        private static ConfigData LoadConfigCore()
        {
            string configPath = GetConfigPath();
            string backupPath = configPath + ".bak";

            if (!File.Exists(configPath))
            {
                // Свежая установка — либо сбой между шагами File.Replace: пробуем бэкап
                return TryReadFile(backupPath) ?? new ConfigData();
            }

            var config = TryReadFile(configPath);
            if (config != null) return config;

            // Файл битый: сохраняем копию для диагностики, пробуем восстановиться из бэкапа
            try
            {
                File.Copy(configPath, configPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true);
            }
            catch { }

            config = TryReadFile(backupPath);
            if (config != null)
            {
                System.Diagnostics.Debug.WriteLine("♻️ config.json битый, восстановлен из .bak");
                try { SaveConfigCore(config); } catch { }
                return config;
            }

            System.Diagnostics.Debug.WriteLine("⚠️ config.json битый, бэкапа нет — создан новый");
            return new ConfigData();
        }

        private static ConfigData TryReadFile(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<ConfigData>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void SaveConfig(ConfigData config)
        {
            lock (_lock)
            {
                bool locked = AcquireFileLock();
                try
                {
                    SaveConfigCore(config);
                    System.Diagnostics.Debug.WriteLine($"💾 Config сохранен: {GetConfigPath()}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка сохранения config.json: {ex.Message}");
                }
                finally
                {
                    if (locked) _crossProcessLock.ReleaseMutex();
                }
            }
        }

        private static void SaveConfigCore(ConfigData config)
        {
            string configPath = GetConfigPath();
            string tempPath = configPath + ".tmp";
            string backupPath = configPath + ".bak";
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);

            // Пишем во временный файл и сбрасываем на диск ДО подмены основного:
            // прерванная запись портит только .tmp, а config.json остаётся целым
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(true);
            }

            if (File.Exists(configPath))
            {
                // Атомарная подмена: читатель видит либо старый, либо новый файл целиком
                File.Replace(tempPath, configPath, backupPath, true);
            }
            else
            {
                File.Move(tempPath, configPath);
            }
        }

        // ========== РАБОТА С АВТОРИЗАЦИЕЙ ==========
        public static void SaveAuth(string token, long? viewerId, string deviceId)
        {
            var config = LoadConfig();
            config.Auth = new AuthData
            {
                Token = EncryptData(token),
                ViewerId = viewerId,
                DeviceId = EncryptData(deviceId ?? ""),
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            SaveConfig(config);
        }

        public static AuthData GetAuth()
        {
            var config = LoadConfig();
            if (config?.Auth == null) return null;

            try
            {
                return new AuthData
                {
                    Token = DecryptData(config.Auth.Token),
                    ViewerId = config.Auth.ViewerId,
                    DeviceId = DecryptData(config.Auth.DeviceId ?? ""),
                    SavedAt = config.Auth.SavedAt
                };
            }
            catch
            {
                return null;
            }
        }

        public static void UpdateDeviceId(string deviceId)
        {
            var config = LoadConfig();
            if (config?.Auth == null) return;

            config.Auth.DeviceId = EncryptData(deviceId ?? "");
            config.Auth.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            SaveConfig(config);
        }

        public static void ClearAuth()
        {
            var config = LoadConfig();
            if (config != null)
            {
                config.Auth = null;
                SaveConfig(config);
            }
        }

        // ========== РАБОТА С PIN ==========
        public static void SavePin(string pin)
        {
            var config = LoadConfig();
            config.Pin = EncryptData(pin);
            SaveConfig(config);
        }

        public static string GetPin()
        {
            var config = LoadConfig();
            if (string.IsNullOrEmpty(config?.Pin)) return null;

            try
            {
                return DecryptData(config.Pin);
            }
            catch
            {
                return null;
            }
        }

        public static void DeletePin()
        {
            var config = LoadConfig();
            if (config != null)
            {
                config.Pin = null;
                SaveConfig(config);
            }
        }

        // ========== РАБОТА С СОСТОЯНИЕМ ОКНА ==========
        public static void SaveWindowState(int width, int height, int left, int top, int state)
        {
            var config = LoadConfig();
            config.WindowState = new WindowStateData
            {
                Width = width,
                Height = height,
                Left = left,
                Top = top,
                State = state
            };
            SaveConfig(config);
        }

        public static WindowStateData GetWindowState()
        {
            var config = LoadConfig();
            return config?.WindowState;
        }

        // ========== РАБОТА С НАСТРОЙКАМИ УВЕДОМЛЕНИЙ ==========
        public static void SaveNotificationsOnTop(bool isOnTop)
        {
            var config = LoadConfig();
            config.NotificationsOnTop = isOnTop;
            SaveConfig(config);
        }

        public static bool GetNotificationsOnTop()
        {
            var config = LoadConfig();
            return config?.NotificationsOnTop ?? true;
        }

        // ========== РАБОТА С ПРОКСИ ==========
        public static void SaveProxySettings(bool enabled, string server, int port)
        {
            var config = LoadConfig();
            config.Proxy = new ProxySettings
            {
                Enabled = enabled,
                Server = server?.Trim() ?? "",
                Port = port > 0 ? port : 0
            };
            SaveConfig(config);
        }

        public static ProxySettings GetProxySettings()
        {
            var config = LoadConfig();
            var proxy = config?.Proxy;

            if (proxy != null && proxy.Enabled)
            {
                if (string.IsNullOrEmpty(proxy.Server) || proxy.Port <= 0)
                {
                    proxy.Enabled = false;
                    System.Diagnostics.Debug.WriteLine("⚠️ Прокси отключен: некорректные параметры");
                    SaveConfig(config);
                }
            }

            return proxy;
        }

        // ========== РАБОТА С ПАПКОЙ ЗАГРУЗОК ==========
        public static void SaveDownloadPath(string path)
        {
            var config = LoadConfig();
            config.DownloadPath = path;
            SaveConfig(config);
        }

        public static string GetDownloadPath()
        {
            var config = LoadConfig();

            // Если путь не задан или папка не существует, возвращаем стандартную папку загрузок
            if (string.IsNullOrEmpty(config?.DownloadPath) || !Directory.Exists(config.DownloadPath))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }

            return config.DownloadPath;
        }

        // ========== ШИФРОВАНИЕ (DPAPI) ==========
        private static string EncryptData(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] encryptedBytes = ProtectedData.Protect(dataBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }

        private static string DecryptData(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return null;
            byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
            byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        // ========== НАСТРОЙКА "СПРАШИВАТЬ КАЖДЫЙ РАЗ" ==========
        public static void SaveAskEveryTime(bool askEveryTime)
        {
            var config = LoadConfig();
            config.AskEveryTime = askEveryTime;
            SaveConfig(config);
        }

        public static bool AskEveryTime()
        {
            var config = LoadConfig();
            return config?.AskEveryTime ?? false;
        }
    }
}