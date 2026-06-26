using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Windows.Forms;

namespace MaxLight
{
    public class ConfigManager
    {
        private static readonly object _lock = new object();

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
                string configPath = GetConfigPath();

                if (!File.Exists(configPath))
                {
                    return new ConfigData();
                }

                try
                {
                    string json = File.ReadAllText(configPath);
                    return JsonConvert.DeserializeObject<ConfigData>(json) ?? new ConfigData();
                }
                catch
                {
                    return new ConfigData();
                }
            }
        }

        private static void SaveConfig(ConfigData config)
        {
            lock (_lock)
            {
                try
                {
                    string configPath = GetConfigPath();
                    string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(configPath, json);
                    System.Diagnostics.Debug.WriteLine($"💾 Config сохранен: {configPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Ошибка сохранения config.json: {ex.Message}");
                }
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

        
    }
}