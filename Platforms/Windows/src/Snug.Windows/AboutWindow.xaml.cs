using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Snug.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var assembly = typeof(AboutWindow).Assembly;
        var version = assembly.GetName().Version?.ToString(3) ?? "开发版";
        var upstream = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "SnugUpstreamVersion")?.Value ?? "2.5.0";
        VersionLabel.Text = $"{version} · 基于 Snug {upstream}";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Snug.ico");
        if (File.Exists(iconPath))
        {
            var decoder = BitmapDecoder.Create(new Uri(iconPath, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var icon = decoder.Frames.OrderByDescending(frame => frame.PixelWidth).First();
            Icon = icon;
            ProductIcon.Source = icon;
        }
    }

    private void OpenLink(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.Scheme != Uri.UriSchemeHttps || e.Uri.Host != "github.com") return;
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception)
        {
            System.Windows.MessageBox.Show(this, "无法打开链接，请在浏览器中访问：" + e.Uri.AbsoluteUri,
                "Snug", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}