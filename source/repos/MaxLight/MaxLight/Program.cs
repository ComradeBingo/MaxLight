using System;
using System.Diagnostics;
using System.Windows.Forms;
using Velopack;
using Velopack.Sources;

namespace MaxLight
{
    internal static class Program
    {
       
        [STAThread]
        static void Main()
        {
            // Инициализация Velopack
            VelopackApp.Build().Run();

            // Проверка обновлений в фоне
            CheckForUpdatesAsync();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        private static async void CheckForUpdatesAsync()
        {
            try
            {
                // GitHub релизы как источник обновлений
                var mgr = new UpdateManager(new GithubSource("https://github.com/ComradeBingo/MaxLight", null, false));

                // Проверяем наличие новой версии
                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion != null)
                {
                    // Скачиваем обновление
                    await mgr.DownloadUpdatesAsync(newVersion);

                    // Применяем обновления и перезапускаем
                    mgr.ApplyUpdatesAndRestart(newVersion);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка проверки обновлений: {ex.Message}");
            }
        }
    }
}