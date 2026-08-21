using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace MaxLight;

public partial class PinSetupWindow : Window
{
    public bool PinSet { get; private set; } = false;
    public string? PinCode { get; private set; }

    public PinSetupWindow()
    {
        InitializeComponent();
        this.Loaded += PinSetupWindow_Loaded;
    }

    private void PinSetupWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Устанавливаем фокус на поле ввода PIN
        txtPin.Focus();
        Debug.WriteLine("🔐 Установка PIN: фокус на поле ввода");
    }

    private void TxtPin_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ValidatePin();
    }

    private void ValidatePin()
    {
        string pin = txtPin.Password;
        string confirm = txtPinConfirm.Password;

        bool pinValid = false;
        bool confirmValid = false;

        if (string.IsNullOrEmpty(pin))
        {
            lblPinError.Text = "Введите PIN-код";
            lblPinError.Visibility = Visibility.Visible;
        }
        else if (!Regex.IsMatch(pin, @"^\d{4,6}$"))
        {
            lblPinError.Text = "PIN должен содержать 4-6 цифр";
            lblPinError.Visibility = Visibility.Visible;
        }
        else
        {
            lblPinError.Visibility = Visibility.Collapsed;
            pinValid = true;
        }

        if (string.IsNullOrEmpty(confirm))
        {
            lblConfirmError.Text = "Подтвердите PIN-код";
            lblConfirmError.Visibility = Visibility.Visible;
        }
        else if (pin != confirm)
        {
            lblConfirmError.Text = "PIN-коды не совпадают";
            lblConfirmError.Visibility = Visibility.Visible;
        }
        else
        {
            lblConfirmError.Visibility = Visibility.Collapsed;
            confirmValid = true;
        }

        btnSave.IsEnabled = pinValid && confirmValid;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        PinCode = txtPin.Password;
        PinSet = true;
        DialogResult = true;
        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        PinSet = false;
        PinCode = null;
        DialogResult = false;
        Close();
    }
}