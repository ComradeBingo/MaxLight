using System.Diagnostics;
using System.Windows;

namespace MaxLight;

public partial class UpdateDialog : Window
{
    public bool UpdateAccepted { get; private set; } = false;

    public UpdateDialog(string version, string releaseNotes, string portableHint = "")
    {
        InitializeComponent();

        Debug.WriteLine($"📢 UpdateDialog: version={version}");
        Debug.WriteLine($"📢 UpdateDialog: releaseNotes length={releaseNotes?.Length ?? 0}");

        txtVersion.Text = $"Версия {version}{portableHint}";
        txtReleaseNotes.Text = releaseNotes ?? "📝 Описание изменений не найдено.";
    }

    private void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("📢 UpdateDialog: пользователь нажал Обновить");
        UpdateAccepted = true;

        // 1. Помечаем событие как обработанное, чтобы DragMove не перехватил его
        e.Handled = true;

        // 2. Закрываем модальное окно через DialogResult
        this.DialogResult = true;
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("📢 UpdateDialog: пользователь нажал Пропустить");
        UpdateAccepted = false;

        // Делаем то же самое для кнопки пропуска
        e.Handled = true;
        this.DialogResult = false;
    }
}