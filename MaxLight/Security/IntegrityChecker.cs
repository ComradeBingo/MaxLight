using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MaxLight.Security
{
    public static class IntegrityChecker
    {
        private static readonly string[] BlacklistedModules = new[]
        {
            "dnlib", "harmony", "easyhook", "mhook", "detours",
            "minhook", "inject", "cheat"
        };

        public static bool IsIntegrityPassed { get; private set; } = false;
        private static Timer _periodicTimer;
        private static bool _isDevelopmentMode = false;

        public static bool PerformFullCheck()
        {
            _isDevelopmentMode = IsDevelopmentEnvironment();

            if (_isDevelopmentMode)
            {
                Debug.WriteLine("Режим разработки: проверка целостности ослаблена");
                IsIntegrityPassed = true;
                return true;
            }

            try
            {
                if (!VerifyExecutableIntegrity())
                {
                    MessageBox.Show("Нарушена целостность исполняемого файла.", "Ошибка безопасности",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    IsIntegrityPassed = false;
                    return false;
                }

                IsIntegrityPassed = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Integrity check failed: {ex.Message}");
                IsIntegrityPassed = false;
                return false;
            }
        }

        private static bool IsDevelopmentEnvironment()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                string pdbPath = Path.ChangeExtension(exePath, "pdb");
                if (File.Exists(pdbPath)) return true;

                if (Debugger.IsAttached) return true;

                return false;
            }
            catch { return false; }
        }

        public static void StartPeriodicCheck()
        {
            if (_isDevelopmentMode || IsDevelopmentEnvironment())
            {
                Debug.WriteLine("Режим разработки: периодическая проверка целостности отключена");
                return;
            }

            if (_periodicTimer != null) return;

            _periodicTimer = new Timer();
            _periodicTimer.Interval = 30000;
            _periodicTimer.Tick += async (s, e) =>
            {
                await Task.Run(() =>
                {
                    if (!VerifyExecutableIntegrity() || DetectSuspiciousModules())
                    {
                        Environment.Exit(0);
                    }
                });
            };
            _periodicTimer.Start();
        }

        public static bool VerifyExecutableIntegrity()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(exePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    string currentHash = Convert.ToBase64String(hash);

                    // Сохраняем хеш для обновлений
                    string hashPath = Path.Combine(Path.GetTempPath(), "MaxLight_hash.txt");
                    if (File.Exists(hashPath))
                    {
                        string savedHash = File.ReadAllText(hashPath);
                        if (savedHash != currentHash)
                        {
                            // Хеш изменился - возможно обновление
                            File.WriteAllText(hashPath, currentHash);
                        }
                    }

                    return stream.Length > 100000;
                }
            }
            catch { return false; }
        }

        private static bool DetectSuspiciousModules()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var loadedModules = currentProcess.Modules.Cast<ProcessModule>()
                    .Select(m => Path.GetFileName(m.FileName).ToLower())
                    .ToList();

                foreach (var module in loadedModules)
                {
                    if (BlacklistedModules.Any(b => module.Contains(b)))
                    {
                        Debug.WriteLine($"Suspicious module detected: {module}");
                        return true;
                    }
                }

                return false;
            }
            catch { return false; }
        }

        public static string GetCurrentFileHash()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(exePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
            catch { return null; }
        }
    }
}