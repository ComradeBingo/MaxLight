using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MaxLight;

public partial class PinEntryWindow : Window
{
    private int _attempts = 0;
    private const int MaxAttempts = 5;
    private bool _isLocked = false;

    public bool IsPinVerified { get; private set; } = false;

    public PinEntryWindow()
    {
        InitializeComponent();
        this.Loaded += PinEntryWindow_Loaded;
        this.Closed += PinEntryWindow_Closed;
    }

    private void PinEntryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        txtPin.Focus();
        Debug.WriteLine("🔐 Окно PIN: фокус на поле ввода");
    }

    private void PinEntryWindow_Closed(object sender, EventArgs e)
    {
        if (!IsPinVerified)
        {
            Debug.WriteLine("❌ PIN не подтвержден - завершение приложения");

            // Полностью завершаем приложение
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Закрываем все окна кроме текущего
                for (int i = Application.Current.Windows.Count - 1; i >= 0; i--)
                {
                    var window = Application.Current.Windows[i];
                    if (window != this && window != null)
                    {
                        window.Close();
                    }
                }

                // Завершаем приложение
                Application.Current.Shutdown();
            });
        }
    }

    private void TxtPin_PasswordChanged(object sender, RoutedEventArgs e)
    {
        btnUnlock.IsEnabled = !string.IsNullOrEmpty(txtPin.Password) && !_isLocked;
        lblError.Visibility = Visibility.Collapsed;
    }

    private void TxtPin_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && btnUnlock.IsEnabled)
        {
            BtnUnlock_Click(sender, e);
        }
    }

    private void BtnUnlock_Click(object sender, RoutedEventArgs e)
    {
        if (_isLocked) return;

        string enteredPin = txtPin.Password;

        if (ConfigManager.VerifyPinCode(enteredPin))
        {
            IsPinVerified = true;
            DialogResult = true;
            Close();
        }
        else
        {
            _attempts++;
            int remaining = MaxAttempts - _attempts;

            lblError.Visibility = Visibility.Visible;
            lblError.Text = "❌ Неверный PIN-код";

            if (remaining > 0)
            {
                lblAttempts.Visibility = Visibility.Visible;
                lblAttempts.Text = $"Осталось попыток: {remaining}";
                txtPin.Clear();
                txtPin.Focus();
            }
            else
            {
                _isLocked = true;
                lblAttempts.Visibility = Visibility.Visible;
                lblAttempts.Text = "🔒 Приложение заблокировано";
                lblAttempts.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(231, 76, 60));
                btnUnlock.IsEnabled = false;
                txtPin.IsEnabled = false;

                IsPinVerified = false;
                DialogResult = false;
                Close();
            }
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        IsPinVerified = false;
        DialogResult = false;
        Close();
    }
}