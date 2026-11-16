using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Snug.Windows.Services;

namespace Snug.Windows;

public partial class WelcomeWindow : Window
{
    private readonly AppController _controller;
    private readonly MainWindow _mainWindow;

    public WelcomeWindow(AppController controller, MainWindow mainWindow)
    {
        InitializeComponent();
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Snug.ico");
        if (File.Exists(iconPath)) Icon = BitmapFrame.Create(new Uri(iconPath, UriKind.Absolute));
        _controller = controller;
        _mainWindow = mainWindow;
        DataContext = controller;
        Loaded += async (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            if (!_controller.ShouldAutoInstallDriver) return;

            _controller.MarkDriverInstallAttempted();
            var result = await _controller.InstallDriverAsync();
            if (!result.Success)
                System.Windows.MessageBox.Show(this, result.Message, "Snug 驱动",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        };
        Closed += (_, _) =>
        {
            if (!_controller.Welcomed) _mainWindow.ShowSettings();
        };
    }

    private async void InstallDriver_Click(object sender, RoutedEventArgs e)
    {
        var result = await _controller.InstallDriverAsync();
        if (!result.Success)
            System.Windows.MessageBox.Show(this, result.Message, "Snug 驱动", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        _controller.MarkWelcomed();
        _mainWindow.ShowSettings();
        Close();
    }
}
