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
        this.DialogResult = true;
        this.Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("📢 UpdateDialog: пользователь нажал Пропустить");
        UpdateAccepted = false;
        this.DialogResult = false;
        this.Close();
    }
}