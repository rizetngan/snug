using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Snug.Windows.Core;
using Snug.Windows.Interop;
using Snug.Windows.Services;

namespace Snug.Windows;

/// <summary>
/// Windows implementation of upstream PhoneHomeView + PhoneSearchPage + PhoneAppCard.
/// One scaled phone canvas, independent search/model pages and in-phone confirmations.
/// </summary>
public partial class PhoneSurfaceWindow : Window
{
    private readonly AppController _controller;
    private DisplayInfo? _targetDisplay;
    private bool _showAll;
    private RunningApp? _pendingQuit;
    private WindowInfo? _pendingClose;
    private bool _pendingDismissScreen;
    private bool _busy;
    private IntPtr _previousForeground;

    public bool IsPreview { get; }
    public AppController Controller => _controller;
    public ObservableCollection<RunningApp> CurrentRunningApps { get; } = [];
    public ObservableCollection<RunningApp> VisibleRunningApps { get; } = [];
    public ObservableCollection<InstalledApp> VisibleAllApps { get; } = [];
    public ObservableCollection<AppLetterGroup> VisibleGroups { get; } = [];
    public ObservableCollection<AppInfo> SearchApps { get; } = [];
    public ObservableCollection<WindowInfo> SearchWindows { get; } = [];

    public PhoneSurfaceWindow(AppController controller, bool preview)
    {
        InitializeComponent();
        _controller = controller;
        IsPreview = preview;
        if (preview) UpdatePreviewCanvas();
        DataContext = this;
        Opacity = 0;
        CloseButton.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
        DismissPhoneButton.Visibility = preview ? Visibility.Collapsed : Visibility.Visible;
        PhoneModelPicker.ChoiceSelected += (_, choice) =>
        {
            HidePhoneModel();
            if (choice == _controller.CurrentPhoneChoice) return;
            _controller.SetPhoneChoice(choice);
        };
        PhoneModelPicker.CloseRequested += (_, _) => HidePhoneModel();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Closed += (_, _) => _controller.ClosePreview(this);
    }

    private void UpdatePreviewCanvas()
    {
        var fit = new ScreenFit(_controller.CurrentPhoneChoice.Screen);
        var pixels = fit.PixelSizeFor(fit.BaseWidth);
        var canvas = PhoneCanvasLayout.ForDisplay((int)pixels.Width, (int)pixels.Height);
        PhoneCanvas.Width = canvas.Width;
        PhoneCanvas.Height = canvas.Height;
    }

    public void PlaceOn(DisplayInfo display)
    {
        _targetDisplay = display;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0;
        Top = 0;
        Width = display.Bounds.Width / display.DpiScaleX;
        Height = display.Bounds.Height / display.DpiScaleY;
        // Keep upstream's 540-point design grid on every phone. The old
        // pixelWidth/2 layout shrank to 360 points on a 720px iPhone 13 Pro
        // virtual monitor, inflating fonts and clipping all app row labels.
        var canvas = PhoneCanvasLayout.ForDisplay(display.Bounds.Width, display.Bounds.Height);
        PhoneCanvas.Width = canvas.Width;
        PhoneCanvas.Height = canvas.Height;
        if (IsLoaded) ApplyPlacement();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _controller.InstalledApps.CollectionChanged += OnCatalogChanged;
        _controller.RunningApps.CollectionChanged += OnCatalogChanged;
        _controller.PropertyChanged += OnControllerChanged;
        RefreshLists();
        ApplyPlacement();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _controller.InstalledApps.CollectionChanged -= OnCatalogChanged;
        _controller.RunningApps.CollectionChanged -= OnCatalogChanged;
        _controller.PropertyChanged -= OnControllerChanged;
    }

    private void OnCatalogChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(RefreshLists);

    private void OnControllerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsPreview && e.PropertyName == nameof(AppController.CurrentPhoneChoice))
            Dispatcher.BeginInvoke(UpdatePreviewCanvas);

        if (e.PropertyName is nameof(AppController.ActivityStatus)
            or nameof(AppController.PlacedWindowKey)
            or nameof(AppController.PlacedAppName)
            or nameof(AppController.IsAppCatalogLoaded))
            Dispatcher.BeginInvoke(RefreshLists);
    }

    private static void UpdateCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        var latest = values.ToArray();
        if (target.SequenceEqual(latest)) return;
        target.Clear();
        foreach (var value in latest) target.Add(value);
    }

    private void RefreshLists()
    {
        if (!IsLoaded) return;

        var running = _controller.RunningApps.ToArray();
        var installed = _controller.InstalledApps.ToArray();
        var placedKey = _controller.PlacedWindowKey;
        var current = placedKey is null ? [] : running
            .Where(app => app.ProcessId == placedKey.Value.ProcessId).ToArray();
        var others = current.Length == 0 ? running : running
            .Where(app => app.ProcessId != placedKey!.Value.ProcessId).ToArray();

        UpdateCollection(CurrentRunningApps, current);
        UpdateCollection(VisibleRunningApps, others);
        UpdateCollection(VisibleAllApps, installed);
        UpdateCollection(VisibleGroups, AppList.Grouped(installed));

        CurrentSection.Visibility = current.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        RunningEmpty.Visibility = _controller.IsAppCatalogLoaded && running.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;
        RunningLoading.Visibility = !_controller.IsAppCatalogLoaded
            ? Visibility.Visible : Visibility.Collapsed;
        AllEmpty.Visibility = _controller.IsAppCatalogLoaded && installed.Length == 0
            ? Visibility.Visible : Visibility.Collapsed;
        AllLoading.Visibility = !_controller.IsAppCatalogLoaded
            ? Visibility.Visible : Visibility.Collapsed;

        ReturnButton.Visibility = placedKey is null ? Visibility.Collapsed : Visibility.Visible;
        ReturnAppName.Text = _controller.PlacedAppName ?? "App";

        var status = _controller.ActivityStatus;
        var showNotice = status.Contains("失败", StringComparison.Ordinal)
            || status.Contains("无法", StringComparison.Ordinal)
            || status.Contains("无响应", StringComparison.Ordinal)
            || status.Contains("尚未恢复", StringComparison.Ordinal);
        NoticeText.Text = showNotice ? status : string.Empty;
        NoticeBanner.Visibility = showNotice ? Visibility.Visible : Visibility.Collapsed;

        RefreshSearch();
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        RunningContent.Visibility = _showAll ? Visibility.Collapsed : Visibility.Visible;
        AllContent.Visibility = _showAll ? Visibility.Visible : Visibility.Collapsed;
        var selected = (System.Windows.Media.Brush)FindResource("PhoneCard");
        var deselected = System.Windows.Media.Brushes.Transparent;
        RunningTab.Background = PinnedRunningTab.Background = _showAll ? deselected : selected;
        AllTab.Background = PinnedAllTab.Background = _showAll ? selected : deselected;
        RunningTab.FontWeight = PinnedRunningTab.FontWeight = _showAll ? FontWeights.Medium : FontWeights.SemiBold;
        AllTab.FontWeight = PinnedAllTab.FontWeight = _showAll ? FontWeights.SemiBold : FontWeights.Medium;
    }

    private void HomeScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        PinnedTabs.Visibility = e.VerticalOffset > 150
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RunningTab_Click(object sender, RoutedEventArgs e)
    {
        _showAll = false;
        UpdateTabs();
    }

    private void AllTab_Click(object sender, RoutedEventArgs e)
    {
        _showAll = true;
        UpdateTabs();
    }

    private void ApplyPlacement()
    {
        if (_targetDisplay is { } display)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var placed = NativeMethods.SetWindowPos(handle, IntPtr.Zero,
                display.Bounds.X, display.Bounds.Y,
                display.Bounds.Width, display.Bounds.Height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
            if (!placed)
            {
                Left = display.Bounds.X / display.DpiScaleX;
                Top = display.Bounds.Y / display.DpiScaleY;
            }
        }
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            TimeSpan.FromMilliseconds(180)));
    }

    private void RememberKeyboardFocus()
    {
        var current = NativeMethods.GetForegroundWindow();
        var ownHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _previousForeground = current == ownHandle ? IntPtr.Zero : current;
    }

    private void RestoreKeyboardFocus()
    {
        var ownHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (_previousForeground != IntPtr.Zero &&
            NativeMethods.GetForegroundWindow() == ownHandle)
            NativeMethods.SetForegroundWindow(_previousForeground);
        _previousForeground = IntPtr.Zero;
    }

    private void OpenSearch_Click(object sender, RoutedEventArgs e)
    {
        RememberKeyboardFocus();
        ClearConfirmation();
        SearchQueryBox.Clear();
        RefreshSearch();
        SearchOverlay.Visibility = Visibility.Visible;
        Activate();
        Dispatcher.BeginInvoke(() => SearchQueryBox.Focus(),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RefreshSearch()
    {
        var query = SearchQueryBox.Text.Trim();
        var searching = SearchOverlay.Visibility == Visibility.Visible
            && SearchKeys.Normalize(query).Length > 0;
        SearchResults.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        SearchBrowse.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        if (!searching)
        {
            UpdateCollection(SearchApps, Array.Empty<AppInfo>());
            UpdateCollection(SearchWindows, Array.Empty<WindowInfo>());
            return;
        }

        var result = AppList.Search(query,
            _controller.InstalledApps.ToArray(), _controller.RunningApps.ToArray());
        UpdateCollection(SearchApps, result.Apps);
        UpdateCollection(SearchWindows, result.Windows);
        SearchAppsSection.Visibility = result.Apps.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
        SearchWindowsSection.Visibility = result.Windows.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
        SearchEmpty.Visibility = result.IsEmpty
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchQuery_TextChanged(object sender, TextChangedEventArgs e) => RefreshSearch();
    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchQueryBox.Clear();
        SearchQueryBox.Focus();
    }

    private void CloseSearch_Click(object sender, RoutedEventArgs e) => HideSearch();

    private void HideSearch()
    {
        SearchOverlay.Visibility = Visibility.Collapsed;
        SearchQueryBox.Clear();
        ClearConfirmation();
        RestoreKeyboardFocus();
    }

    private void PhoneModel_Click(object sender, RoutedEventArgs e)
    {
        RememberKeyboardFocus();
        ClearConfirmation();
        PhoneModelPicker.Configure(_controller.PhoneCatalog, _controller.CurrentPhoneChoice);
        PhoneModelOverlay.Visibility = Visibility.Visible;
        Activate();
        Dispatcher.BeginInvoke(PhoneModelPicker.FocusSearch,
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void HidePhoneModel()
    {
        PhoneModelOverlay.Visibility = Visibility.Collapsed;
        RestoreKeyboardFocus();
    }

    private void ClearConfirmation()
    {
        _pendingClose = null;
        _pendingQuit = null;
        _pendingDismissScreen = false;
        ConfirmationOverlay.Visibility = Visibility.Collapsed;
    }

    private void ShowQuitConfirmation(RunningApp app)
    {
        _pendingClose = null;
        _pendingDismissScreen = false;
        ConfirmDescription.Text = "关闭或退出可能有未保存内容，应用可能要求先保存。";
        _pendingQuit = app;
        ConfirmTitle.Text = $"退出 {app.App.Name}？";
        ConfirmActionButton.Content = "退出";
        ConfirmationOverlay.Visibility = Visibility.Visible;
    }

    private void ShowCloseConfirmation(WindowInfo window)
    {
        _pendingQuit = null;
        _pendingDismissScreen = false;
        ConfirmDescription.Text = "关闭或退出可能有未保存内容，应用可能要求先保存。";
        _pendingClose = window;
        ConfirmTitle.Text = $"关闭「{window.Title}」？";
        ConfirmActionButton.Content = "关闭";
        ConfirmationOverlay.Visibility = Visibility.Visible;
    }

    private void DismissPhone_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreview) { Close(); return; }
        ClearConfirmation();
        _pendingDismissScreen = true;
        ConfirmTitle.Text = "退出手机屏？";
        ConfirmDescription.Text = "窗口会回到原位置；本次远程不会再创建手机屏，下次远程连接时恢复。";
        ConfirmActionButton.Content = "退出手机屏";
        ConfirmationOverlay.Visibility = Visibility.Visible;
    }

    private void CancelConfirm_Click(object sender, RoutedEventArgs e) => ClearConfirmation();

    private async void ConfirmAction_Click(object sender, RoutedEventArgs e)
    {
        var quit = _pendingQuit;
        var close = _pendingClose;
        var dismissScreen = _pendingDismissScreen;
        ClearConfirmation();
        if (dismissScreen)
            await WithBusyAsync("正在退出手机屏", _controller.DismissPhoneForEpisodeAsync);
        else if (quit is not null)
            await WithBusyAsync($"正在退出 {quit.App.Name}", () =>
                _controller.QuitAppAsync(quit, IsPreview));
        else if (close is not null)
            await WithBusyAsync($"正在关闭 {close.Title}", () =>
                _controller.CloseWindowAsync(close, IsPreview));
    }

    private async Task WithBusyAsync(string title, Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        BusyTitle.Text = title;
        BusySubtitle.Text = string.Empty;
        BusyOverlay.Visibility = Visibility.Visible;
        try { await action(); }
        catch (Exception error)
        {
            BusyOverlay.Visibility = Visibility.Collapsed;
            NoticeText.Text = error.Message;
            NoticeBanner.Visibility = Visibility.Visible;
        }
        finally
        {
            BusyOverlay.Visibility = Visibility.Collapsed;
            _busy = false;
            RefreshLists();
        }
    }

    private async void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AppInfo app) return;
        if (SearchOverlay.Visibility == Visibility.Visible) HideSearch();
        ClearConfirmation();
        await WithBusyAsync($"正在打开 {app.Name}", () => _controller.ActivateAppAsync(app, IsPreview));
    }

    private async void SearchApp_Click(object sender, RoutedEventArgs e) =>
        OpenApp_Click(sender, e);

    private async void NewWindow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AppInfo app) return;
        ClearConfirmation();
        await WithBusyAsync($"正在新建 {app.Name}", () => _controller.NewWindowAsync(app, IsPreview));
    }

    private async void OpenWindow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not WindowInfo window) return;
        if (SearchOverlay.Visibility == Visibility.Visible) HideSearch();
        ClearConfirmation();
        await WithBusyAsync($"正在打开 {window.AppName}", () => _controller.PullWindowAsync(window, IsPreview));
    }

    private async void SearchWindow_Click(object sender, RoutedEventArgs e) =>
        OpenWindow_Click(sender, e);

    private void QuitApp_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is RunningApp app)
            ShowQuitConfirmation(app);
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is WindowInfo window)
            ShowCloseConfirmation(window);
    }

    private async void ReturnToPlaced_Click(object sender, RoutedEventArgs e)
    {
        ClearConfirmation();
        await WithBusyAsync("正在返回 App", _controller.ReturnToPlacedAsync);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
