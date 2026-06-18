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
        private static readonly string ConfigPath = Path.Combine(Application.StartupPath, "config.json");
        private static readonly object _lock = new object();

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
                if (!File.Exists(ConfigPath))
                {
                    return new ConfigData();
                }

                try
                {
                    string json = File.ReadAllText(ConfigPath);
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
                    string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(ConfigPath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка сохранения config.json: {ex.Message}");
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

            // Если прокси включен, но сервер пустой или порт 0 - отключаем прокси
            if (enabled && (string.IsNullOrEmpty(server) || port <= 0))
            {
                enabled = false;
                System.Diagnostics.Debug.WriteLine("⚠️ Прокси отключен: некорректные параметры");
            }

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

            // Валидация при загрузке
            if (proxy != null && proxy.Enabled)
            {
                if (string.IsNullOrEmpty(proxy.Server) || proxy.Port <= 0)
                {
                    proxy.Enabled = false;
                    System.Diagnostics.Debug.WriteLine("⚠️ Прокси отключен при загрузке: некорректные параметры");
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

        // ========== МИГРАЦИЯ ИЗ РЕЕСТРА ==========
        public static bool MigrateFromRegistry()
        {
            try
            {
                bool hasRegistryData = false;
                string authData = null;
                string pin = null;
                int? notificationsOnTop = null;
                WindowStateData windowState = null;

                // Читаем данные из реестра
                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\MaxLight\\Auth"))
                {
                    if (key != null)
                    {
                        authData = key.GetValue("AuthData")?.ToString();
                        if (!string.IsNullOrEmpty(authData)) hasRegistryData = true;
                    }
                }

                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\MaxLight"))
                {
                    if (key != null)
                    {
                        pin = key.GetValue("PIN")?.ToString();

                        var notifValue = key.GetValue("NotificationsOnTop");
                        if (notifValue != null && notifValue is int)
                        {
                            notificationsOnTop = (int)notifValue;
                        }

                        // Читаем состояние окна
                        int w = (int)key.GetValue("Width", -1);
                        int h = (int)key.GetValue("Height", -1);
                        int x = (int)key.GetValue("Left", -1);
                        int y = (int)key.GetValue("Top", -1);
                        int state = (int)key.GetValue("WindowState", 0);

                        if (w != -1 && h != -1 && x != -1 && y != -1)
                        {
                            windowState = new WindowStateData
                            {
                                Width = w,
                                Height = h,
                                Left = x,
                                Top = y,
                                State = state
                            };
                        }
                    }
                }

                if (!hasRegistryData && string.IsNullOrEmpty(pin) && windowState == null && notificationsOnTop == null)
                {
                    return false; // Нет данных для миграции
                }

                // Переносим данные в config.json
                var config = LoadConfig();

                // Переносим авторизацию
                if (hasRegistryData && !string.IsNullOrEmpty(authData))
                {
                    try
                    {
                        string decrypted = DecryptData(authData);
                        var auth = JsonConvert.DeserializeObject<dynamic>(decrypted);

                        if (auth != null)
                        {
                            string token = auth.token;
                            long? viewerId = auth.viewerId;
                            string deviceId = auth.deviceId;

                            config.Auth = new AuthData
                            {
                                Token = EncryptData(token),
                                ViewerId = viewerId,
                                DeviceId = EncryptData(deviceId ?? ""),
                                SavedAt = auth.savedAt ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка миграции авторизации: {ex.Message}");
                    }
                }

                // Переносим PIN
                if (!string.IsNullOrEmpty(pin))
                {
                    try
                    {
                        string decryptedPin = DecryptData(pin);
                        config.Pin = EncryptData(decryptedPin);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка миграции PIN: {ex.Message}");
                    }
                }

                // Переносим состояние окна
                if (windowState != null)
                {
                    config.WindowState = windowState;
                }

                // Переносим настройку уведомлений
                if (notificationsOnTop.HasValue)
                {
                    config.NotificationsOnTop = notificationsOnTop.Value == 1;
                }

                // Сохраняем config.json
                SaveConfig(config);

                // Удаляем ветку реестра
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree("SOFTWARE\\MaxLight");
                    System.Diagnostics.Debug.WriteLine("✅ Ветка реестра MaxLight удалена после миграции");
                }
                catch (ArgumentException)
                {
                    // Ветка не существует
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка удаления ветки реестра: {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка миграции: {ex.Message}");
                return false;
            }
        }
    }
}