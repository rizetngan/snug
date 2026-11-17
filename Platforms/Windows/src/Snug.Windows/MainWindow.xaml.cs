using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Snug.Windows.Services;

namespace Snug.Windows;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private readonly Func<Task> _exitAsync;
    private bool _allowClose;

    public MainWindow(AppController controller, Func<Task> exitAsync)
    {
        InitializeComponent();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Snug.ico");
        if (File.Exists(iconPath)) Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        _controller = controller;
        _exitAsync = exitAsync;
        DataContext = controller;
        Closing += OnClosing;
        Loaded += (_, _) => BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
    }

    public void ShowSettings()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }

    private void Preview_Click(object sender, RoutedEventArgs e) => _controller.OpenPhonePreview();

    private void PhoneModel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PhoneModelWindow(_controller.PhoneCatalog, _controller.CurrentPhoneChoice)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && dialog.SelectedChoice is { } choice)
            _controller.SetPhoneChoice(choice);
    }

    private async void InstallDriver_Click(object sender, RoutedEventArgs e)
    {
        var result = await _controller.InstallDriverAsync();
        if (!result.Success)
            System.Windows.MessageBox.Show(this, result.Message, "Snug 驱动", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    private async void Exit_Click(object sender, RoutedEventArgs e) => await _exitAsync();
}
