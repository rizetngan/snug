using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

public sealed class AppController : INotifyPropertyChanged, IDisposable
{
    private readonly DisplayEnumerator _displayEnumerator = new();
    private readonly WindowManager _windowManager = new();
    private readonly AppCatalogService _appCatalog;
    private readonly PhoneCatalog _phoneCatalog;
    private PhoneChoice _phoneChoice = PhoneChoice.Standard;
    // Desired model and actual live-display model must be separate. Unlike
    // upstream CGVirtualDisplay.reshape, the current Windows VDD cannot
    // change its mode table in-place without invalidating UU's capture.
    private PhoneChoice? _activeDisplayPhoneChoice;
    private PhoneChoice CurrentDisplayChoice => _activeDisplayPhoneChoice ?? _phoneChoice;
    private readonly UuSessionDetector _uuDetector = new();
    private readonly UuConnectionTracker _connection = new();
    private readonly PhoneSessionPresentation _phonePresentation = new();
    private readonly MouseGestureHook _gestureHook = new();
    private readonly VddDriverClient _driver = new();
    private readonly PhoneDisplayModeSwitcher _modeSwitcher = new();
    private readonly SemaphoreSlim _modeGate = new(1, 1);
    private readonly PhoneActionLane _actionLane = new();
    private readonly ZoomBook _zoomBook;
    private readonly SnugSettingsStore _settingsStore = new();
    private readonly SnugSettings _settings;
    private readonly RestoreLedger _ledger = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Snug", "phone-screen.json"));
    // Unlike the active single-window ledger, this book survives a destroyed HWND.
    private readonly LeftBehindBook _leftBehind = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Snug", "left-behind.json"));    private readonly DispatcherTimer _timer;

    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _lastDisplayScan = DateTimeOffset.MinValue;
    private DateTimeOffset _lastWindowScan = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDriverScan = DateTimeOffset.MinValue;
    private DisplayInfo? _availableSnugDisplay;
    private DisplayInfo? _phoneDisplay;
    private bool _endingSession;
    private bool _creatingSession;
    private bool _orphanCleanupStarted;
    private WindowRestoreRecord? _placed;
    private PhoneSurfaceWindow? _phoneSurface;
    private HomeBarWindow? _homeBar;
    private AppBackBarWindow? _appBackBar;
    private bool _previewOpen;
    private bool _showingPhoneList = true;
    private bool _pullingWindow;
    private WindowKey? _askingDialogKey;
    private bool _recoveringDialog;
    private bool _recoveringLostWindow;
    private int _activeModeWidth = ScreenFit.Standard.BaseWidth;
    private int _placedModeWidth = ScreenFit.Standard.BaseWidth;
    private bool _gesturesEnabled;
    private bool _phoneScreenEnabled;
    private bool _launchAtLogin;
    private bool _welcomed;
    private int _defaultZoomSteps;
    private bool _disposed;
    private string _connectionStatus = "等待 UU 远程";
    private string _displayStatus = "正在检测显示器";
    private string _activityStatus = "准备就绪";
    private string _connectionDiagnostics = "等待 UU 会话日志";
    private string _driverStatusText = "正在检测驱动";
    private bool _isConnected;
    private bool _isDriverReady;

    public ObservableCollection<DisplayInfo> Displays { get; } = [];
    public ObservableCollection<WindowInfo> Windows { get; } = [];
    public ObservableCollection<InstalledApp> InstalledApps { get; } = [];
    public ObservableCollection<RunningApp> RunningApps { get; } = [];

    public PhoneCatalog PhoneCatalog => _phoneCatalog;
    public PhoneChoice CurrentPhoneChoice => _phoneChoice;
    public string PhoneChoiceSummary => _phoneChoice.Title + "  ·  " + _phoneChoice.Screen.SizeText;

    public string ConnectionStatus { get => _connectionStatus; private set => Set(ref _connectionStatus, value); }
    public bool HasPlacedWindow => _placed is not null || _ledger.Load() is not null;
    public WindowKey? PlacedWindowKey => (_placed ?? _ledger.Load())?.Key;
    public string? PlacedAppName => (_placed ?? _ledger.Load())?.AppName;
    public string DisplayStatus { get => _displayStatus; private set => Set(ref _displayStatus, value); }
    public string ActivityStatus { get => _activityStatus; private set => Set(ref _activityStatus, value); }
    public string ConnectionDiagnostics { get => _connectionDiagnostics; private set => Set(ref _connectionDiagnostics, value); }
    public bool IsConnected { get => _isConnected; private set => Set(ref _isConnected, value); }
    public string DriverStatusText { get => _driverStatusText; private set => Set(ref _driverStatusText, value); }
    public bool IsDriverReady { get => _isDriverReady; private set => Set(ref _isDriverReady, value); }
    public bool IsDriverInstalled => _driver.IsInstalled;
    public bool ShouldAutoInstallDriver => !_driver.IsInstalled && !_settings.DriverInstallAttempted;
    public bool CanRepairDriver => !_driver.IsInstalled || _driver.IsManagedBySnug;

    private PhoneScreenSpec CurrentPhoneScreenSpec
    {
        get
        {
            var fit = new ScreenFit(CurrentDisplayChoice.Screen);
            var size = fit.SizeFor(fit.BaseWidth);
            return new PhoneScreenSpec((int)size.Width, (int)size.Height,
                ScreenFit.HomeBarHeight(fit.BaseWidth), 60, 2);
        }
    }

    public bool PhoneScreenEnabled
    {
        get => _phoneScreenEnabled;
        set
        {
            if (!Set(ref _phoneScreenEnabled, value)) return;
            SaveSettings();
            if (value) _phonePresentation.EnableAgain();
            if (!value && _phoneDisplay is not null) _ = EndPhoneSessionAsync();
        }
    }

    public bool GesturesEnabled
    {
        get => _gesturesEnabled;
        set
        {
            if (!Set(ref _gesturesEnabled, value)) return;
            SaveSettings();
            SyncGestures();
        }
    }

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set
        {
            if (!Set(ref _launchAtLogin, value)) return;
            try
            {
                StartupManager.SetEnabled(value);
                SaveSettings();
            }
            catch (Exception error)
            {
                Set(ref _launchAtLogin, !value);
                ActivityStatus = "开机启动设置失败：" + error.Message;
            }
        }
    }

    public bool Welcomed
    {
        get => _welcomed;
        private set
        {
            if (!Set(ref _welcomed, value)) return;
            SaveSettings();
        }
    }

    public int DefaultZoomSteps
    {
        get => _defaultZoomSteps;
        set
        {
            if (!Set(ref _defaultZoomSteps, Math.Clamp(value, 0, 5))) return;
            SaveSettings();
        }
    }

    public bool IsAppCatalogLoaded => _appCatalog.IsLoaded;
    public string WindowCountText => Windows.Count == 0 ? "暂无可用窗口" : $"{Windows.Count} 个窗口";
    public string InstalledAppCountText => $"{InstalledApps.Count} 个 App";
    public string RunningAppCountText => $"{RunningApps.Count} 个 App";
    public string VersionText => $"{typeof(AppController).Assembly.GetName().Version?.ToString(3) ?? "未知"} · Windows";

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppController()
    {
        _settings = _settingsStore.Load();
        _phoneCatalog = LoadPhoneCatalog();
        var savedScreen = new PhoneScreen(_settings.PhoneScreenWidth, _settings.PhoneScreenHeight,
            _settings.PhoneScreenPoints);
        if (savedScreen.IsValid && string.IsNullOrWhiteSpace(_settings.PhoneModel))
        {
            _phoneChoice = new PhoneChoice(null, savedScreen);
        }
        else if (_phoneCatalog.Find(_settings.PhoneModel) is { } savedModel)
        {
            _phoneChoice = new PhoneChoice(savedModel.Id, savedModel.Screen);
        }
        else
        {
            _phoneChoice = PhoneChoice.Standard;
        }
        _activeModeWidth = new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth;
        _placedModeWidth = _activeModeWidth;
        _appCatalog = new AppCatalogService(_windowManager);
        _zoomBook = new ZoomBook(_settings.AppZoomSteps);
        _phoneScreenEnabled = _settings.PhoneScreenEnabled;
        _gesturesEnabled = _settings.GesturesEnabled;
        _defaultZoomSteps = Math.Clamp(_settings.ZoomSteps, 0, 5);
        _welcomed = _settings.Welcomed;
        _launchAtLogin = StartupManager.IsEnabled();

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += OnTick;
    }

    private static PhoneCatalog LoadPhoneCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "phone-models.json");
        using var stream = File.OpenRead(path);
        return PhoneCatalog.Load(stream);
    }

    public void SetPhoneChoice(PhoneChoice choice)
    {
        ArgumentNullException.ThrowIfNull(choice);
        if (!choice.Screen.IsValid) throw new ArgumentException("手机屏幕参数无效。", nameof(choice));

        if (choice.Model is { } modelId)
        {
            var model = _phoneCatalog.Find(modelId)
                ?? throw new ArgumentException("机型不在 Snug 手机型号库中。", nameof(choice));
            choice = new PhoneChoice(model.Id, model.Screen);
        }

        if (_phoneChoice == choice) return;

        _phoneChoice = choice;
        _settings.PhoneModel = choice.Model ?? string.Empty;
        _settings.PhoneScreenWidth = choice.Screen.Width;
        _settings.PhoneScreenHeight = choice.Screen.Height;
        _settings.PhoneScreenPoints = choice.Screen.Points;
        SaveSettings();
        OnPropertyChanged(nameof(CurrentPhoneChoice));
        OnPropertyChanged(nameof(PhoneChoiceSummary));
        ActivityStatus = $"已选择：{choice.Title}";

        // Upstream changes the modes of the same CGVirtualDisplay in place.
        // The current Windows VDD cannot do that without destroying the device,
        // invalidating UU's selected display/capture coordinates. Preserve the
        // existing monitor and apply this new model on the next full episode.
        if (_phoneDisplay is not null)
            ActivityStatus = "已保存机型；为保持 UU 当前屏幕不掉线，下次远程会话生效。";
    }

    /// <summary>Upstream PhoneDesk.dismissScreen: release just this session's phone screen.</summary>
    public async Task DismissPhoneForEpisodeAsync()
    {
        if (_phoneDisplay is null || _endingSession) return;
        _phonePresentation.DismissForEpisode();
        await EndPhoneSessionAsync();
        if (_phoneDisplay is not null)
        {
            _phonePresentation.EnableAgain();
            ActivityStatus = "窗口还原失败；手机屏仍保留，请先处理窗口。";
        }
    }

    public void StartCatalogPreview()
    {
        RefreshWindows();
        _ = LoadInstalledAppsAsync();
    }

    public void Start()
    {
        _startedAt = DateTimeOffset.UtcNow;
        RecoverInterruptedMove();
        Refresh();
        _ = LoadInstalledAppsAsync();
        _timer.Start();
        _ = RefreshDriverStatusAsync();
    }

    public void MarkWelcomed() => Welcomed = true;

    public void MarkDriverInstallAttempted()
    {
        _settings.DriverInstallAttempted = true;
        SaveSettings();
    }

    public void Refresh()
    {
        RefreshDisplays();
        RefreshWindows();
        ActivityStatus = "已刷新";
        _ = RefreshDriverStatusAsync();
    }

    public void RefreshDisplays()
    {
        Displays.Clear();
        foreach (var display in _displayEnumerator.GetDisplays()) Displays.Add(display);
        _availableSnugDisplay = _displayEnumerator.FindSnugDisplay();

        if (_phoneDisplay is not null)
            DisplayStatus = $"Snug 手机屏 · {_phoneDisplay.Bounds.Width} × {_phoneDisplay.Bounds.Height}";
        else if (_availableSnugDisplay is not null)
            DisplayStatus = "Snug 手机屏已就绪";
        else if (IsDriverReady)
            DisplayStatus = IsConnected ? "正在创建手机屏" : "Snug 手机屏已就绪";
        else if (_driver.IsInstalled && _driver.IsManagedBySnug)
            DisplayStatus = "等待 UU 远程";
        else
            DisplayStatus = _driver.IsInstalled ? "检测到外部虚拟显示驱动" : "虚拟显示驱动未安装";

        OnPropertyChanged(nameof(Displays));
    }

    public void RefreshWindows()
    {
        var next = _windowManager.ListWindows();
        SyncCollection(Windows, next, window => window.Key);
        SyncCollection(RunningApps, _appCatalog.RefreshRunning(next), app => app.ProcessId);
        OnPropertyChanged(nameof(WindowCountText));
        OnPropertyChanged(nameof(RunningAppCountText));
    }

    private async Task LoadInstalledAppsAsync()
    {
        try
        {
            await _appCatalog.LoadInstalledAsync();
            SyncCollection(InstalledApps, _appCatalog.Installed, app => app.App.AppId);
            RefreshWindows();
            OnPropertyChanged(nameof(InstalledAppCountText));
            OnPropertyChanged(nameof(IsAppCatalogLoaded));
            ActivityStatus = $"已读取 {InstalledApps.Count} 个 App";
        }
        catch (Exception error)
        {
            ActivityStatus = "读取 App 目录失败：" + error.Message;
        }
    }

    public Task ActivateAppAsync(AppInfo app, bool preview) =>
        _actionLane.RunAsync(() => ActivateAppCoreAsync(app, preview));

    private async Task ActivateAppCoreAsync(AppInfo app, bool preview)
    {
        if (preview)
        {
            ActivityStatus = "预览模式不会启动真实 App";
            return;
        }

        var action = AppList.ActionFor(app, RunningApps.ToArray());
        if (action.Kind == AppActionKind.PullWindow && action.Window is not null)
        {
            await PullWindowAsync(action.Window, preview: false);
            return;
        }

        await OpenAppAsync(app);
    }

    public Task NewWindowAsync(AppInfo app, bool preview) =>
        _actionLane.RunAsync(() => NewWindowCoreAsync(app, preview));

    private async Task NewWindowCoreAsync(AppInfo app, bool preview)
    {
        if (preview)
        {
            ActivityStatus = "预览模式不会启动真实 App";
            return;
        }

        if (HasPlacedWindow)
        {
            await PutBackAsync();
            if (HasPlacedWindow)
            {
                ActivityStatus = "当前窗口尚未恢复，暂不能新建窗口";
                return;
            }
        }

        var running = RunningApps.FirstOrDefault(item => MatchesApp(item, app));
        if (running is null)
        {
            ActivityStatus = $"{app.Name} 尚未运行";
            return;
        }

        var source = running.Windows.FirstOrDefault();
        if (source is null)
        {
            ActivityStatus = $"{app.Name} 没有可用窗口";
            return;
        }

        var known = RunningApps.SelectMany(item => item.Windows)
            .Select(window => window.Key).ToHashSet();
        if (!_windowManager.NewWindow(source.Key, app, out var commandStatus))
        {
            ActivityStatus = commandStatus;
            return;
        }

        ActivityStatus = $"{commandStatus} · {app.Name}";
        for (var attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(250);
            RefreshWindows();
            var created = RunningApps
                .Where(item => MatchesApp(item, app))
                .SelectMany(item => item.Windows)
                .FirstOrDefault(window => !known.Contains(window.Key));
            if (created is null) continue;

            await PullWindowAsync(created, preview: false);
            return;
        }

        ActivityStatus = $"{app.Name} 未出现新窗口，操作已超时；没有继续发送其他快捷键。";
    }
    private enum ExitObservation { Finished, Asking, TimedOut, FailedToPresent }

    private static bool ProcessHasExited(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return process.HasExited;
        }
        catch (ArgumentException) { return true; }
        catch (InvalidOperationException) { return true; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    private async Task<ExitObservation> ObserveExitAsync(uint pid, Func<bool> completed,
        TimeSpan duration)
    {
        var until = DateTimeOffset.UtcNow + duration;
        while (!_disposed && DateTimeOffset.UtcNow < until)
        {
            if (completed()) return ExitObservation.Finished;
            if (_windowManager.FindAskingWindow(pid) is { } dialog)
                return await PlaceAskingDialogAsync(dialog)
                    ? ExitObservation.Asking : ExitObservation.FailedToPresent;
            await Task.Delay(250);
        }

        return completed() ? ExitObservation.Finished : ExitObservation.TimedOut;
    }

    private async Task<bool> PlaceAskingDialogAsync(WindowInfo asking)
    {
        if (_phoneDisplay is null || HasPlacedWindow) return false;
        var original = _windowManager.Capture(asking, zoomSteps: 0);
        if (original is null) return false;
        try
        {
            _ledger.Save(original);
            var display = _phoneDisplay;
            if (!await Task.Run(() =>
                    _windowManager.MoveDialogTo(asking.Key, CurrentAppArea(display))))
            {
                if (_windowManager.Restore(original) || !_windowManager.IsAlive(original.Key))
                    _ledger.Clear();
                else
                    _placed = original;
                return false;
            }

            _placed = original;
            _askingDialogKey = original.Key;
            _showingPhoneList = false;
            _phoneSurface?.Hide();
            SyncGestures();
            ShowAppBackBar(display, "应用确认", false, 0);
            ShowHomeBar(display);
            _windowManager.Focus(asking.Key);
            ActivityStatus = "应用正在询问是否保存，请在手机屏上选择。";
            return true;
        }
        catch (IOException error)
        {
            ActivityStatus = "记录保存提示窗口的位置失败：" + error.Message;
            return false;
        }
    }

    public Task CloseWindowAsync(WindowInfo window, bool preview) =>
        _actionLane.RunAsync(() => CloseWindowCoreAsync(window, preview));

    private async Task CloseWindowCoreAsync(WindowInfo window, bool preview)
    {
        if (preview)
        {
            ActivityStatus = "预览模式不会关闭真实窗口";
            return;
        }

        var placed = _placed ?? _ledger.Load();
        if (placed?.Key == window.Key)
        {
            await PutBackAsync();
            placed = _placed ?? _ledger.Load();
            if (placed?.Key == window.Key)
            {
                ActivityStatus = "窗口尚未恢复，暂不能关闭";
                return;
            }
        }

        if (!_windowManager.IsAlive(window.Key))
        {
            RefreshWindows();
            ActivityStatus = "窗口已经关闭";
            return;
        }

        if (!_windowManager.Close(window.Key))
        {
            ActivityStatus = "关闭请求未发送，窗口可能无响应";
            return;
        }

        ActivityStatus = $"正在关闭：{window.AppName}";
        var observation = await ObserveExitAsync(window.Key.ProcessId,
            () => !_windowManager.IsAlive(window.Key), TimeSpan.FromSeconds(8));
        RefreshWindows();
        ActivityStatus = observation switch
        {
            ExitObservation.Finished => $"已关闭：{window.AppName}",
            ExitObservation.Asking => "请在手机屏处理应用的保存提示。",
            ExitObservation.FailedToPresent => "发现应用确认弹窗，但暂时无法移到手机屏。",
            _ => "窗口仍未关闭；应用可能正在等待内部确认。"
        };
    }
    public Task QuitAppAsync(RunningApp app, bool preview) =>
        _actionLane.RunAsync(() => QuitAppCoreAsync(app, preview));

    private async Task QuitAppCoreAsync(RunningApp app, bool preview)
    {
        if (preview)
        {
            ActivityStatus = "预览模式不会退出真实 App";
            return;
        }

        if (!AppList.CanQuit(app.App))
        {
            ActivityStatus = $"{app.App.Name} 不支持从资源库退出";
            return;
        }

        var placed = _placed ?? _ledger.Load();
        if (placed?.Key.ProcessId == app.ProcessId)
        {
            await PutBackAsync();
            placed = _placed ?? _ledger.Load();
            if (placed?.Key.ProcessId == app.ProcessId)
            {
                ActivityStatus = "该 App 的窗口尚未恢复，暂不能退出";
                return;
            }
        }

        var requested = 0;
        foreach (var window in app.Windows)
        {
            if (_windowManager.Close(window.Key)) requested++;
        }

        if (requested == 0)
        {
            using var process = AppCatalogService.FindProcess(app);
            if (process is not null)
            {
                try { if (process.CloseMainWindow()) requested++; }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }

        if (requested == 0)
        {
            ActivityStatus = $"无法向 {app.App.Name} 发送退出请求";
            return;
        }

        ActivityStatus = $"正在退出：{app.App.Name}";
        var observation = await ObserveExitAsync((uint)app.ProcessId,
            () => ProcessHasExited(app.ProcessId), TimeSpan.FromSeconds(10));
        RefreshWindows();
        ActivityStatus = observation switch
        {
            ExitObservation.Finished => $"已退出：{app.App.Name}",
            ExitObservation.Asking => "请在手机屏处理应用的保存提示。",
            ExitObservation.FailedToPresent => "发现应用确认弹窗，但暂时无法移到手机屏。",
            _ => $"{app.App.Name} 尚未退出；不会强制结束进程。"
        };
    }
    private async Task OpenAppAsync(AppInfo app)
    {
        if (_phoneDisplay is null)
        {
            ActivityStatus = "没有可用的 Snug 手机屏";
            return;
        }

        if (_placed is not null)
        {
            await PutBackAsync();
            if (_placed is not null) return;
        }

        var knownHandles = Windows.Select(window => window.Key.Handle).ToHashSet();
        try
        {
            AppLauncher.Launch(app);
            ActivityStatus = $"正在打开：{app.Name}";

            for (var attempt = 0; attempt < 60; attempt++)
            {
                await Task.Delay(250);
                RefreshWindows();

                var matches = RunningApps.Where(running => MatchesApp(running, app)).ToArray();
                var window = matches.SelectMany(running => running.Windows)
                    .FirstOrDefault(candidate => !knownHandles.Contains(candidate.Key.Handle))
                    ?? matches.SelectMany(running => running.Windows).FirstOrDefault();

                if (window is null) continue;
                await PullWindowAsync(window, preview: false);
                return;
            }

            ActivityStatus = $"{app.Name} 已启动，但没有找到可显示的窗口";
        }
        catch (Exception error)
        {
            ActivityStatus = $"打开 {app.Name} 失败：" + error.Message;
        }
    }

    private static bool MatchesApp(RunningApp running, AppInfo app)
    {
        if (string.Equals(running.App.AppId, app.AppId, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(app.ExecutablePath)
            && string.Equals(running.App.ExecutablePath, app.ExecutablePath, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(running.App.Name, app.Name, StringComparison.OrdinalIgnoreCase)
            || running.Windows.Any(window => window.Title.Contains(app.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static void SyncCollection<T, TKey>(
        ObservableCollection<T> target,
        IReadOnlyList<T> fresh,
        Func<T, TKey> key)
        where TKey : notnull
    {
        var latest = fresh.ToDictionary(key);
        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!latest.ContainsKey(key(target[index]))) target.RemoveAt(index);
        }

        for (var index = 0; index < fresh.Count; index++)
        {
            var item = fresh[index];
            var itemKey = key(item);
            var current = -1;
            for (var candidate = 0; candidate < target.Count; candidate++)
            {
                if (EqualityComparer<TKey>.Default.Equals(key(target[candidate]), itemKey))
                {
                    current = candidate;
                    break;
                }
            }

            if (current < 0) target.Insert(Math.Min(index, target.Count), item);
            else
            {                if (!EqualityComparer<T>.Default.Equals(target[current], item)) target[current] = item;
                if (current != index) target.Move(current, Math.Min(index, target.Count - 1));
            }
        }
    }

    public async Task<DriverInstallResult> InstallDriverAsync()
    {
        ActivityStatus = "正在安装虚拟显示驱动";

        if (_placed is not null)
        {
            await PutBackAsync();
            if (_placed is not null)
                return new(false, "窗口尚未恢复，暂不能修复驱动。");
        }

        if (_driver.IsManagedBySnug && _displayEnumerator.FindSnugDisplay() is not null)
        {
            if (!await _driver.StopDisplayAsync())
                return new(false, "手机屏尚未关闭，暂不能修复驱动。");
        }

        var result = await _driver.InstallOrRepairAsync();
        ActivityStatus = result.Message;
        await RefreshDriverStatusAsync();
        RefreshDisplays();
        return result;
    }

    public async Task<bool> FinishForQuitAsync()
    {
        _timer.Stop();
        await PutBackAsync();
        if (_placed is not null)
        {
            _timer.Start();
            ActivityStatus = "窗口尚未恢复，Snug 保持运行";
            return false;
        }

        _gestureHook.Stop();
        _gestureHook.SetRegion(null);
        _homeBar?.Close();
        _homeBar = null;
        _appBackBar?.Close();
        _appBackBar = null;
        _phoneSurface?.Close();
        _phoneSurface = null;
        _phoneDisplay = null;
        if (_driver.IsManagedBySnug && _displayEnumerator.FindSnugDisplay() is not null
            && !await _driver.StopDisplayAsync())
        {
            _timer.Start();
            ActivityStatus = "手机屏尚未关闭，Snug 保持运行以便重试";
            return false;
        }
        return true;
    }

    public void ClosePreview(PhoneSurfaceWindow window)
    {
        if (!ReferenceEquals(_phoneSurface, window) || !window.IsPreview) return;
        _phoneSurface = null;
        _previewOpen = false;
    }

    public void OpenPhonePreview()
    {
        if (_phoneDisplay is not null)
        {
            ShowPhoneList();
            return;
        }
        if (_phoneSurface is { IsVisible: true } && _previewOpen)
        {
            _phoneSurface.Activate();
            return;
        }

        _previewOpen = true;
        _phoneSurface = new PhoneSurfaceWindow(this, preview: true)
        {
            Width = CurrentPhoneScreenSpec.Width,
            Height = CurrentPhoneScreenSpec.Height,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        _phoneSurface.Show();
        RefreshWindows();
    }

    // Screen.AllScreens returns DPI-virtualized desktop coordinates. Reserve
    // the EXACT same bar height used by HomeBarWindow, not doubled raw pixels.
    private WindowBounds CurrentAppArea(DisplayInfo display) => display.Bounds with
    {
        Height = Math.Max(200, display.Bounds.Height - ScreenFit.HomeBarHeight(display.Bounds.Width))
    };

    /// <summary>Only the Snug-owned monitor may change, and only to a VDD-advertised mode.</summary>
    private async Task<bool> ChangePhoneModeAsync(int width)
    {
        var fit = new ScreenFit(CurrentDisplayChoice.Screen);
        if (!fit.Widths.Contains(width) || _phoneDisplay is null) return false;
        // Windows signed VDD has a validated 20-point mode grid. Round up
        // rather than requesting one of the unsupported intermediate widths.
        width = VddResolutionProfile.SnapWidth(CurrentDisplayChoice, width);
        var pixels = fit.PixelSizeFor(width);
        var mode = new VddResolutionMode((int)pixels.Width, (int)pixels.Height, 60);
        await _modeGate.WaitAsync();
        try
        {
            var display = _phoneDisplay;
            if (display is null) return false;
            var previousGeometry = display.Bounds;
            var changingMode = !_modeSwitcher.HasMode(display, mode);
            // WinForms screen bounds are DPI-virtualized (720x1223 on this
            // user's 1080x1834 VDD). Always compare/set the raw Win32 mode.
            // TryChange is a no-op when the existing raw mode already matches.
            var switched = await Task.Run(() =>
            {
                var success = _modeSwitcher.TryChange(display, mode, out var message);
                return (success, message);
            });
            if (!switched.success)
            {
                ActivityStatus = switched.message;
                return false;
            }
            // Windows reports the newly switched native mode before it updates
            // the DPI-virtualized desktop rectangle. The former loop exited as
            // soon as EnumDisplaySettings matched, so the old 720x1223 bounds
            // were reused on a new 820x1392 display. That left the browser and
            // HomeBar covering only part of the actual virtual screen.
            DisplayInfo? found = null;
            var geometrySettled = !changingMode;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                found = _displayEnumerator.FindSnugDisplay();
                if (found is not null && _modeSwitcher.HasMode(found, mode))
                {
                    geometrySettled = !changingMode
                        || found.Bounds.Width != previousGeometry.Width
                        || found.Bounds.Height != previousGeometry.Height;
                    if (geometrySettled) break;
                }
                await Task.Delay(150);
            }
            if (found is null || !_modeSwitcher.HasMode(found, mode) || !geometrySettled)
            {
                ActivityStatus = "Windows 尚未确认 Snug 手机屏的新分辨率。";
                return false;
            }
            _phoneDisplay = found;
            _availableSnugDisplay = found;
            _activeModeWidth = width;
            _phoneSurface?.PlaceOn(found);
            _homeBar?.PlaceOn(found);
            _appBackBar?.PlaceOn(found);
            _gestureHook.SetRegion(found.Bounds, TouchScale());
            SyncGestures();
            RefreshDisplays();
            return true;
        }
        catch (Exception error)
        {
            ActivityStatus = "手机屏模式调整失败：" + error.Message;
            return false;
        }
        finally { _modeGate.Release(); }
    }

    private async Task<bool> FitMovedWindowAsync(WindowInfo window)
    {
        var fit = new ScreenFit(CurrentDisplayChoice.Screen);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (_phoneDisplay is not { } display) return false;
            var actual = _windowManager.ReadBounds(window.Key);
            if (actual is null) return false;
            var area = CurrentAppArea(display);
            if (Math.Abs(actual.Value.X - area.X) > 3 || Math.Abs(actual.Value.Y - area.Y) > 3)
            {
                ActivityStatus = "应用拒绝移动到手机屏的可用区域，已保留原窗口位置。";
                return false;
            }
            var needed = fit.WidthToFit(
                new PhoneSize(actual.Value.Width, actual.Value.Height),
                new PhoneSize(area.Width, area.Height), _activeModeWidth);
            if (needed <= _activeModeWidth)
            {
                if (actual.Value.Width > area.Width + 2 || actual.Value.Height > area.Height + 2)
                {
                    ActivityStatus = "应用的最小窗口尺寸超过手机屏最大档位，无法完整显示。";
                    return false;
                }
                return true;
            }
            if (!await ChangePhoneModeAsync(needed))
            {
                ActivityStatus = "手机屏无法切到容纳此应用的档位，窗口已返回原位置。";
                return false;
            }
            if (!_windowManager.MoveTo(window.Key, CurrentAppArea(_phoneDisplay!)))
            {
                ActivityStatus = "调整屏幕后 App 未能重新铺放。";
                return false;
            }
        }
        return true;
    }

    private async Task RestoreListModeAsync()
    {
        if (_phoneDisplay is null || !_showingPhoneList) return;
        await ChangePhoneModeAsync(new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth);
    }

    private async Task RevealPlacedWindowAsync(WindowRestoreRecord placed)
    {
        if (_phoneDisplay is null || !_windowManager.IsResponsive(placed.Key)) return;
        if (!await ChangePhoneModeAsync(_placedModeWidth)) return;
        if (_phoneDisplay is not { } display ||
            !_windowManager.MoveTo(placed.Key, CurrentAppArea(display)))
        {
            ActivityStatus = "无法在当前手机分辨率下重新铺放 App。";
            return;
        }
        _showingPhoneList = false;
        _phoneSurface?.Hide();
        SyncGestures();
        // Upstream PhoneScreenWindows presents the bottom HomeBar only while
        // a placed app is visible; it does not cover the app with a top toolbar.
        _appBackBar?.Hide();
        ShowHomeBar(display);
        _windowManager.Focus(placed.Key);
    }

    public Task PullWindowAsync(WindowInfo window, bool preview) =>
        _actionLane.RunAsync(() => PullWindowCoreAsync(window, preview));

    private async Task PullWindowCoreAsync(WindowInfo window, bool preview)
    {
        if (preview)
        {
            ActivityStatus = "预览模式不会移动真实窗口";
            return;
        }
        if (_phoneDisplay is null)
        {
            ActivityStatus = "没有可用的 Snug 手机屏";
            return;
        }
        if (_pullingWindow) return;
        _pullingWindow = true;

        try
        {
            if ((_placed ?? _ledger.Load()) is { } existing && existing.Key == window.Key)
            {
                await RevealPlacedWindowAsync(existing);
                return;
            }

            if (HasPlacedWindow)
            {
                await PutBackAsync();
                if (HasPlacedWindow) return;
            }

            if (!await ChangePhoneModeAsync(new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth))
                return;

            _connection.NoteSelfInducedChange(DateTimeOffset.UtcNow);
            var display = _phoneDisplay!;
            var canZoom = ZoomBook.IsZoomable(window.ExecutablePath, window.AppName);
            var zoomSteps = canZoom
                ? _zoomBook.StepsFor(window.ExecutablePath, window.AppName, DefaultZoomSteps)
                : 0;

            var record = await Task.Run(() =>
            {
                if (!_windowManager.IsResponsive(window.Key))
                    throw new InvalidOperationException("App 暂时无响应，未移动窗口。请稍后重试。");

                var saved = _windowManager.Capture(window, zoomSteps)
                    ?? throw new InvalidOperationException("读取窗口原始位置失败，未移动窗口。");
                _ledger.Save(saved);

                if (zoomSteps != 0 && !_windowManager.Zoom(window, zoomSteps))
                {
                    _ledger.Clear();
                    throw new InvalidOperationException("应用没有响应字号快捷键，窗口保持原位。");
                }

                if (!_windowManager.MoveTo(window.Key, CurrentAppArea(display)))
                {
                    var zoomRestored = zoomSteps == 0 || _windowManager.Zoom(window, -zoomSteps);
                    var positionRestored = _windowManager.Restore(saved);
                    if (zoomRestored && positionRestored) _ledger.Clear();
                    else
                    {
                        var pending = saved with { ZoomSteps = zoomRestored ? 0 : zoomSteps };
                        _placed = pending;
                        _ledger.Save(pending);
                    }
                    throw new InvalidOperationException("窗口没有成功移动到手机屏，已尝试恢复原状。");
                }

                return saved;
            });

            _placed = record;
            if (!await FitMovedWindowAsync(window))
            {
                await PutBackAsync();
                ShowPhoneList();
                return;
            }

            // Read the actual post-fit dimensions. This is the only safe
            // signature for matching an app window recreated by Windows.
            var filled = _windowManager.ReadBounds(window.Key);
            if (filled is null)
            {
                await PutBackAsync();
                ShowPhoneList();
                ActivityStatus = "窗口铺屏后无法读取实际大小，已尝试还原";
                return;
            }
            record = record with { FilledBounds = filled };
            _placed = record;
            _ledger.Save(record);

            display = _phoneDisplay!;
            _placedModeWidth = _activeModeWidth;
            _showingPhoneList = false;

            if (canZoom)
            {
                _zoomBook.Remember(window.ExecutablePath, window.AppName, zoomSteps);
                SaveSettings();
            }

            _phoneSurface?.Hide();
            SyncGestures();
            _appBackBar?.Hide();
            ShowHomeBar(display);
            ActivityStatus = $"已打开：{window.AppName}";
        }
        catch (Exception error)
        {
            ActivityStatus = error.Message;
        }
        finally
        {
            _pullingWindow = false;
        }
    }

    public Task PutBackAsync() => _actionLane.RunAsync(PutBackCoreAsync);

    private async Task PutBackCoreAsync()
    {
        var record = _placed ?? _ledger.Load();
        if (record is null) return;

        _connection.NoteSelfInducedChange(DateTimeOffset.UtcNow);
        var result = await Task.Run(() =>
        {
            if (!_windowManager.IsAlive(record.Key))
            {
                // Keep the original position when an app destroys the HWND.
                var remembered = _leftBehind.Add(record, DateTimeOffset.UtcNow);
                _ledger.Clear();
                return (Restored: true,
                    Message: remembered
                        ? "窗口已消失；已记录原位置，重新出现时将检查是否需要还原"
                        : "原窗口已关闭；没有可匹配的铺屏记录",
                    Remaining: (WindowRestoreRecord?)null);
            }

            if (!_windowManager.IsResponsive(record.Key))
                return (Restored: false, Message: "App 暂时无响应，保留还原记录，恢复后可重试", Remaining: (WindowRestoreRecord?)record);

            var window = new WindowInfo(record.Key, record.AppName, string.Empty,
                record.ExecutablePath, false, false);
            if (record.ZoomSteps != 0)
            {
                if (!_windowManager.Zoom(window, -record.ZoomSteps))
                    return (Restored: false, Message: "字号没有缩回，保留还原记录", Remaining: (WindowRestoreRecord?)record);
                record = record with { ZoomSteps = 0 };
                _ledger.Save(record);
            }

            if (!_windowManager.Restore(record))
                return (Restored: false, Message: "窗口位置恢复失败，保留还原记录", Remaining: (WindowRestoreRecord?)record);

            _ledger.Clear();
            return (Restored: true, Message: "窗口已还原", Remaining: (WindowRestoreRecord?)null);
        });

        _placed = result.Remaining;
        if (result.Restored && _askingDialogKey == record.Key)
            _askingDialogKey = null;
        ActivityStatus = result.Message;
        _homeBar?.Hide();
        _appBackBar?.Hide();
        if (result.Restored) _placedModeWidth = new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth;
        if (_phoneSurface is { IsVisible: false } && _phoneDisplay is not null) _phoneSurface.Show();
        SyncGestures();
    }

    public Task ReturnToPlacedAsync() => _actionLane.RunAsync(ReturnToPlacedCoreAsync);

    private async Task ReturnToPlacedCoreAsync()
    {
        if ((_placed ?? _ledger.Load()) is { } placed)
            await RevealPlacedWindowAsync(placed);
    }

    public void ShowPhoneList()
    {
        _showingPhoneList = true;
        OnPropertyChanged(nameof(PlacedWindowKey));
        OnPropertyChanged(nameof(PlacedAppName));
        _homeBar?.Hide();
        _appBackBar?.Hide();
        if (_phoneSurface is { IsVisible: false }) _phoneSurface.Show();
        SyncGestures();
        RefreshWindows();
        _ = RestoreListModeAsync();
    }

    private async Task CompleteAskingDialogAsync()
    {
        try
        {
            await PutBackAsync();
            if (!HasPlacedWindow)
            {
                ShowPhoneList();
                ActivityStatus = "已返回 App 资源库";
            }
        }
        finally
        {
            _recoveringDialog = false;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // On Windows 10, the secondary monitor taskbar may promote itself after
        // the VDD mode settles. Keep upstream's HomeBar visible without taking
        // keyboard focus; the method is a no-op unless that taskbar overlaps it.
        _homeBar?.EnsureAboveTaskbar();

        if (_askingDialogKey is { } asking
            && !_recoveringDialog && !_windowManager.IsAlive(asking))
        {
            _recoveringDialog = true;
            _ = CompleteAskingDialogAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var signals = _uuDetector.Sample();
        ConnectionDiagnostics = _uuDetector.LastSignalSummary + " · " + _uuDetector.LocalControllerSummary;
        foreach (var transition in _connection.Update(signals, now))
            _phonePresentation.OnTransition(transition.Kind);

        IsConnected = _connection.IsConnected;
        ConnectionStatus = IsConnected ? "UU 远程已连接" : "等待 UU 远程";

        if (now - _lastDisplayScan > TimeSpan.FromSeconds(2))
        {
            _lastDisplayScan = now;
            RefreshDisplays();
        }
        if (now - _lastWindowScan > TimeSpan.FromSeconds(2))
        {
            _lastWindowScan = now;
            RefreshWindows();
            CheckLeftBehindReturns(now);
        }

        if (!_recoveringLostWindow && !_pullingWindow && !_recoveringDialog
            && _placed is { } current && !_windowManager.IsAlive(current.Key))
        {
            _recoveringLostWindow = true;
            _ = RecoverLostWindowAsync();
        }
        if (now - _lastDriverScan > TimeSpan.FromSeconds(4))
        {
            _lastDriverScan = now;
            _ = RefreshDriverStatusAsync();
        }

        if (_phonePresentation.ShouldPresent(_connection.WantsPhoneScreen(now)) && PhoneScreenEnabled)
        {
            if (_phoneDisplay is null && !_creatingSession) _ = StartPhoneSessionAsync();
        }
        else if (_phoneDisplay is not null && !_previewOpen && !_endingSession)
        {
            _ = EndPhoneSessionAsync();
        }
        else if (!_orphanCleanupStarted
            && now - _startedAt > TimeSpan.FromSeconds(4)
            && (!_connection.WantsPhoneScreen(now) || !PhoneScreenEnabled)
            && _driver.IsManagedBySnug)
        {
            _orphanCleanupStarted = true;
            _ = ClearOrphanedDisplayAsync();
        }
    }

    private async Task RefreshDriverStatusAsync()
    {
        var online = await _driver.PingAsync();
        IsDriverReady = online && _driver.IsManagedBySnug;
        DriverStatusText = _driver.IsRebootRequired
            ? "已安装 · 重启后生效"
            : _driver.IsManagedBySnug && !_driver.IsInstallComplete
                ? "安装未完成 · 可修复"
                : IsDriverReady
                    ? "已就绪"
                    : _driver.IsInstalled
                        ? (_driver.IsManagedBySnug ? "已安装 · 等待连接" : "检测到外部驱动")
                        : "未安装";
        OnPropertyChanged(nameof(CanRepairDriver));
        OnPropertyChanged(nameof(IsDriverInstalled));
        if (_phoneDisplay is null && !IsDriverReady)
            DisplayStatus = _driver.IsInstalled && _driver.IsManagedBySnug
                ? "等待 UU 远程"
                : _driver.IsInstalled ? "检测到外部虚拟显示驱动" : "虚拟显示驱动未安装";
    }

    private async Task RestartPhoneSessionAsync()
    {
        if (_creatingSession || _endingSession || _phoneDisplay is null) return;
        _creatingSession = true;
        try
        {
            await EndPhoneSessionAsync();
            if (_phoneDisplay is not null)
                ActivityStatus = "当前应用窗口尚未恢复，新机型将在下次连接时应用";
        }
        finally
        {
            _creatingSession = false;
        }
    }

    private async Task StartPhoneSessionAsync()
    {
        if (_creatingSession || _disposed) return;
        _creatingSession = true;
        _orphanCleanupStarted = false;
        DisplayStatus = "正在创建手机屏";
        ActivityStatus = "正在创建手机屏";

        try
        {
            if (!_driver.IsManagedBySnug)
            {
                await RefreshDriverStatusAsync();
                throw new InvalidOperationException(_driver.IsInstalled
                    ? "检测到其他软件管理的虚拟显示驱动，Snug 未接管它。"
                    : "虚拟显示驱动尚未安装，请在设置中安装驱动。");
            }
            if (_driver.IsRebootRequired)
                throw new InvalidOperationException("驱动安装完成，重启 Windows 后即可使用 Snug 手机屏。");
            if (!_driver.IsInstallComplete)
                throw new InvalidOperationException("虚拟显示驱动安装未完成，请在设置中修复驱动。");
            _connection.NoteSelfInducedChange(DateTimeOffset.UtcNow);
            _activeDisplayPhoneChoice = _phoneChoice;
            _driver.WritePhoneProfile(_activeDisplayPhoneChoice);
            if (!await _driver.StartDisplayAsync())
                throw new InvalidOperationException("无法启动 Snug 手机屏。请在设置中修复虚拟显示驱动。");
            await RefreshDriverStatusAsync();

            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            DisplayInfo? display = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                display = _displayEnumerator.FindSnugDisplay();
                if (display is not null) break;
                await Task.Delay(250);
            }

            if (display is null)
                throw new InvalidOperationException("驱动已响应，但 Windows 尚未识别 Snug Phone 显示器。");

            ActivatePhoneDisplay(display);
            if (await ChangePhoneModeAsync(new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth))
            {
                DisplayStatus = $"Snug 手机屏 · {_phoneDisplay!.Bounds.Width} × {_phoneDisplay.Bounds.Height}";
                ActivityStatus = "手机屏已就绪";
            }
            else
            {
                DisplayStatus = "手机屏分辨率尚未匹配机型";
            }
        }
        catch (Exception error)
        {
            ActivityStatus = error.Message;
            DisplayStatus = "手机屏创建失败";
            _ = RefreshDriverStatusAsync();
            if (_driver.IsManagedBySnug)
                await _driver.StopDisplayAsync();
        }
        finally
        {
            _creatingSession = false;
        }
    }

    private void ActivatePhoneDisplay(DisplayInfo display)
    {
        if (_previewOpen)
        {
            _phoneSurface?.Close();
            _phoneSurface = null;
            _previewOpen = false;
        }
        if (_phoneSurface is null || !_phoneSurface.IsVisible || _phoneSurface.IsPreview)
        {
            _phoneSurface?.Close();
            _phoneSurface = new PhoneSurfaceWindow(this, preview: false);
            _phoneSurface.PlaceOn(display);
            _phoneSurface.Show();
        }

        _phoneDisplay = display;
        _activeModeWidth = new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth;
        _placedModeWidth = _activeModeWidth;
        _showingPhoneList = true;
        _gestureHook.SetRegion(display.Bounds, TouchScale());
        SyncGestures();
    }

    private void ShowHomeBar(DisplayInfo display)
    {
        _homeBar ??= new HomeBarWindow(this);
        _homeBar.PlaceOn(display);
        _homeBar.Show();
    }

    private void ShowAppBackBar(DisplayInfo display, string appName, bool canZoom, int zoomSteps)
    {
        _appBackBar ??= new AppBackBarWindow(this);
        _appBackBar.SetApp(appName, canZoom, zoomSteps);
        _appBackBar.PlaceOn(display);
        _appBackBar.Show();
    }

    public void AdjustCurrentAppZoom(int delta)
    {
        var record = _placed ?? _ledger.Load();
        if (record is null || !ZoomBook.IsZoomable(record.ExecutablePath, record.AppName)) return;
        var next = Math.Clamp(record.ZoomSteps + delta, ZoomBook.MinimumSteps, ZoomBook.MaximumSteps);
        if (next == record.ZoomSteps) return;

        var window = new WindowInfo(record.Key, record.AppName, string.Empty,
            record.ExecutablePath, false, false);
        if (!_windowManager.Zoom(window, delta))
        {
            ActivityStatus = "字号调整失败，窗口保持原状";
            return;
        }

        var updated = record with { ZoomSteps = next };
        _placed = updated;
        _ledger.Save(updated);
        _zoomBook.Remember(record.ExecutablePath, record.AppName, next);
        SaveSettings();
        _appBackBar?.SetZoomState(next);
        ActivityStatus = $"{record.AppName} · 字号 {next:+#;-#;0} 级";
    }

    private double TouchScale() => (double)Math.Max(1, _activeModeWidth)
        / Math.Max(1, new ScreenFit(CurrentDisplayChoice.Screen).BaseWidth);

    private void SyncGestures()
    {
        if (!GesturesEnabled || _phoneDisplay is null || !_connection.IsConnected
            || _phoneSurface?.IsVisible == true)
        {
            _gestureHook.Stop();
            _gestureHook.SetRegion(null);
            return;
        }

        _gestureHook.SetRegion(_phoneDisplay.Bounds, TouchScale());
        if (!_gestureHook.IsRunning && !_gestureHook.Start())
            ActivityStatus = "手势监听启动失败";
    }

    private async Task EndPhoneSessionAsync()
    {
        if (_endingSession) return;
        _endingSession = true;
        await PutBackAsync();
        if (_placed is not null)
        {
            ActivityStatus = "窗口尚未恢复，保留手机屏以便重试";
            _endingSession = false;
            return;
        }

        _gestureHook.Stop();
        _gestureHook.SetRegion(null);
        _homeBar?.Close();
        _homeBar = null;
        _appBackBar?.Close();
        _appBackBar = null;
        _phoneSurface?.Close();
        _phoneSurface = null;
        _phoneDisplay = null;
        _activeDisplayPhoneChoice = null;
        _previewOpen = false;

        if (_driver.IsManagedBySnug)
        {
            _connection.NoteSelfInducedChange(DateTimeOffset.UtcNow);
            if (!await _driver.StopDisplayAsync())
                ActivityStatus = "手机屏已退出，系统显示器正在关闭；稍后会自动重试";
        }

        DisplayStatus = "等待 UU 远程";
        _endingSession = false;
        RefreshDisplays();
    }

    private async Task ClearOrphanedDisplayAsync()
    {
        if (!_driver.IsManagedBySnug) return;
        if (_displayEnumerator.FindSnugDisplay() is null) return;

        _connection.NoteSelfInducedChange(DateTimeOffset.UtcNow);
        if (await _driver.StopDisplayAsync())
        {
            await Task.Delay(800);
            RefreshDisplays();
        }
        else
        {
            await Task.Delay(2500);
            _orphanCleanupStarted = false;
        }
    }

    private async Task<bool> WaitForDriverDisplayGoneAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_displayEnumerator.FindSnugDisplay() is null) return true;
            await Task.Delay(250);
        }
        return _displayEnumerator.FindSnugDisplay() is null;
    }

    private async Task RecoverLostWindowAsync()
    {
        try
        {
            await PutBackAsync();
            if (!HasPlacedWindow)
                ShowPhoneList();
        }
        catch (IOException error) { ActivityStatus = "记录关闭窗口状态失败：" + error.Message; }
        catch (UnauthorizedAccessException error) { ActivityStatus = "记录关闭窗口状态失败：" + error.Message; }
        finally { _recoveringLostWindow = false; }
    }

    private void CheckLeftBehindReturns(DateTimeOffset now)
    {
        if (_previewOpen || _placed is not null || _pullingWindow ||
            _recoveringLostWindow || _ledger.Load() is not null)
            return;

        try { _leftBehind.Prune(now); }
        catch (IOException error)
        {
            ActivityStatus = "维护窗口恢复记录失败：" + error.Message;
            return;
        }
        catch (UnauthorizedAccessException error)
        {
            ActivityStatus = "维护窗口恢复记录失败：" + error.Message;
            return;
        }
        if (_leftBehind.Entries.Count == 0) return;
        foreach (var window in Windows)
        {
            if (window.IsMinimized || !_windowManager.IsResponsive(window.Key)) continue;
            if (_windowManager.ReadBounds(window.Key) is not { } actual) continue;
            if (_leftBehind.Match(window, actual, now) is not { } match) continue;

            var old = match.Record;
            // On Windows a recreated process need not retain old text zoom.
            // Undo zoom only for the same process and a supported editor.
            var safeZoom = old.Key.ProcessId == window.Key.ProcessId
                && ZoomBook.IsZoomable(window.ExecutablePath, window.AppName)
                ? old.ZoomSteps : 0;
            if (safeZoom != 0 && !_windowManager.Zoom(window, -safeZoom))
                return;

            var restored = old with { Key = window.Key, ZoomSteps = 0 };
            if (!_windowManager.Restore(restored))
            {
                if (safeZoom != 0) _windowManager.Zoom(window, safeZoom);
                return;
            }

            try { _leftBehind.Remove(match); }
            catch (IOException error)
            {
                ActivityStatus = "窗口已恢复，但记录更新失败：" + error.Message;
                return;
            }
            catch (UnauthorizedAccessException error)
            {
                ActivityStatus = "窗口已恢复，但记录更新失败：" + error.Message;
                return;
            }
            ActivityStatus = "已恢复重新出现的窗口：" + window.Title;
            return;
        }
    }
    private void RecoverInterruptedMove()
    {
        var record = _ledger.Load();
        if (record is null) return;
        if (!_windowManager.IsAlive(record.Key))
        {
            try
            {
                _leftBehind.Add(record, DateTimeOffset.UtcNow);
                _ledger.Clear();
            }
            catch (IOException error)
            {
                ActivityStatus = "暂时无法保存窗口恢复记录：" + error.Message;
            }
            catch (UnauthorizedAccessException error)
            {
                ActivityStatus = "暂时无法保存窗口恢复记录：" + error.Message;
            }
            return;
        }
        if (!_windowManager.IsResponsive(record.Key))
        {
            ActivityStatus = "发现未完成的窗口恢复；App 暂时无响应，已保留记录";
            return;
        }

        var window = new WindowInfo(record.Key, record.AppName, string.Empty,
            record.ExecutablePath, false, false);
        if (record.ZoomSteps != 0)
        {
            if (!_windowManager.Zoom(window, -record.ZoomSteps))
            {
                ActivityStatus = "发现未完成的字号还原，记录已保留";
                return;
            }
            record = record with { ZoomSteps = 0 };
            _ledger.Save(record);
        }
        if (_windowManager.Restore(record)) _ledger.Clear();
        else ActivityStatus = "发现未完成的窗口还原，记录已保留";
    }

    private void SaveSettings()
    {
        _settings.PhoneScreenEnabled = _phoneScreenEnabled;
        _settings.GesturesEnabled = _gesturesEnabled;
        _settings.ZoomSteps = _defaultZoomSteps;
        _settings.AppZoomSteps = new Dictionary<string, int>(_zoomBook.Remembered, StringComparer.OrdinalIgnoreCase);
        _settings.LaunchAtLogin = _launchAtLogin;
        _settings.Welcomed = _welcomed;
        _settings.PhoneModel = _phoneChoice.Model ?? string.Empty;
        _settings.PhoneScreenWidth = _phoneChoice.Screen.Width;
        _settings.PhoneScreenHeight = _phoneChoice.Screen.Height;
        _settings.PhoneScreenPoints = _phoneChoice.Screen.Points;
        try { _settingsStore.Save(_settings); }
        catch (Exception error) { ActivityStatus = "设置保存失败：" + error.Message; }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _gestureHook.Dispose();
        _homeBar?.Close();
        _appBackBar?.Close();
        _phoneSurface?.Close();
    }
}
