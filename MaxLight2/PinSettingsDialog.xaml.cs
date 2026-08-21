using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;

namespace MaxLight;

public partial class PinSettingsDialog : Window
{
    public PinSettingsDialog()
    {
        InitializeComponent();
        this.Loaded += PinSettingsDialog_Loaded;
    }

    private void PinSettingsDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // Устанавливаем фокус на поле текущего PIN
        txtCurrentPin.Focus();
        Debug.WriteLine("🔐 Изменение PIN: фокус на поле текущего PIN");
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        string currentPin = txtCurrentPin.Password;
        string newPin = txtNewPin.Password;

        if (!ConfigManager.VerifyPinCode(currentPin))
        {
            ShowStatus("❌ Неверный текущий PIN-код", "#E74C3C");
            txtCurrentPin.Clear();
            txtCurrentPin.Focus();
            return;
        }

        if (string.IsNullOrEmpty(newPin))
        {
            var result = MessageBox.Show("Вы уверены, что хотите удалить PIN-код?\n\n" +
                "Внимание: при следующем запуске приложение не будет запрашивать PIN.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ConfigManager.RemovePinCode();
                ShowStatus("✅ PIN-код удален", "#2ECC71");
                DialogResult = true;
                Close();
            }
        }
        else if (Regex.IsMatch(newPin, @"^\d{4,6}$"))
        {
            ConfigManager.SavePinCode(newPin);
            ShowStatus("✅ PIN-код обновлен", "#2ECC71");
            DialogResult = true;
            Close();
        }
        else
        {
            ShowStatus("❌ PIN должен содержать 4-6 цифр", "#E74C3C");
            txtNewPin.Clear();
            txtNewPin.Focus();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowStatus(string message, string color)
    {
        lblStatus.Text = message;
        lblStatus.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        lblStatus.Visibility = Visibility.Visible;
    }
}