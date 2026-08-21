using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MaxLight;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        txtTitle.Text = "Max Light";
        txtVersion.Text = $"Версия {version}";

        LoadIcon();
    }

    private void LoadIcon()
    {
        try
        {
            imgIcon.Source = new BitmapImage(new Uri("pack://application:,,,/app.png"));
        }
        catch { }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            Close();
        else
            DragMove();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/ComradeBingo/MaxLight",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
        //mainWindow?.ResetUnreadCount(); нахер это здесь?
        Close();
    }
}