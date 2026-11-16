param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
$dotnet = Join-Path $Root '.tools\dotnet\dotnet.exe'
$dll = Join-Path $Root 'artifacts\verify-build\Snug.Windows.dll'
$artifact = Join-Path $Root 'artifacts'
if (-not (Test-Path $dotnet) -or -not (Test-Path $dll)) {
    throw 'Release verification build missing. Build the solution first.'
}

$preview = Start-Process -FilePath $dotnet -ArgumentList ('"' + $dll + '" --catalog-preview') -PassThru
try {
    Start-Sleep -Seconds 3
    if ($preview.HasExited) { throw 'Phone preview exited at launch.' }
    $desktop = [System.Windows.Automation.AutomationElement]::RootElement
    $pidMatch = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $preview.Id)
    $windowKind = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::Window)
    $windowFilter = [System.Windows.Automation.AndCondition]::new($pidMatch, $windowKind)
    function Resolve-PhoneWindow {
        $candidates = $desktop.FindAll([System.Windows.Automation.TreeScope]::Children, $windowFilter)
        for ($i = 0; $i -lt $candidates.Count; $i++) {
            $candidate = $candidates.Item($i)
            $rectangle = $candidate.Current.BoundingRectangle
            if ($rectangle.Width -gt 200 -and $rectangle.Height -gt 300)
                { return $candidate }
        }
        return $null
    }
    $window = Resolve-PhoneWindow
    if ($null -eq $window) { throw 'Phone surface UI missing.' }

    function Find-Control([string]$name) {
        $match = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty, $name)
        return $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $match)
    }
    function Require-Control([string]$name) {
        $element = Find-Control $name
        if ($null -eq $element) { throw "Missing UI element: $name" }
        return $element
    }
    function Find-VisibleAction([string]$name) {
        $match = [System.Windows.Automation.AndCondition]::new(
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::NameProperty, $name),
            [System.Windows.Automation.PropertyCondition]::new(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Button))
        $items = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $match)
        for ($i = 0; $i -lt $items.Count; $i++) {
            $candidate = $items.Item($i)
            if (-not $candidate.Current.IsOffscreen) { return $candidate }
        }
        return $null
    }
    function Press([string]$name) {
        $element = Find-VisibleAction $name
        if (-not $element) { throw "Visible button not found: $name" }
        $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 200
    }
    function Capture-Phone([string]$name) {
        if (-not $Capture) { return }
        $bounds = $window.Current.BoundingRectangle
        for ($retry = 0; $retry -lt 10; $retry++) {
            if ($bounds.Width -gt 200 -and $bounds.Height -gt 300 -and $bounds.Width -le 3000 -and $bounds.Height -le 3000) { break }
            Start-Sleep -Milliseconds 250
            $candidate = Resolve-PhoneWindow
            if ($candidate) { $bounds = $candidate.Current.BoundingRectangle }
        }
        $width = [int][Math]::Round($bounds.Width)
        $height = [int][Math]::Round($bounds.Height)
        if ($width -le 200 -or $height -le 300 -or $width -gt 3000 -or $height -gt 3000) {
            throw "Unexpected preview window dimensions: $width x $height."
        }
        $bitmap = [System.Drawing.Bitmap]::new($width, $height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CopyFromScreen([int]$bounds.Left, [int]$bounds.Top, 0, 0,
                [System.Drawing.Size]::new($width, $height))
            $filename = Join-Path $artifact ("phone-parity-$name.png")
            $bitmap.Save($filename, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output "SCREENSHOT=$filename"
        }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }

    foreach ($item in @('手机型号', '搜索 App', '运行中', '全部')) {
        [void](Require-Control $item)
    }
    Capture-Phone 'home'
    Press '全部'
    function Catalog-IsLoading {
        # Both tabs declare a loading label. Only the visible label represents
        # an unfinished catalog; hidden WPF UIA elements must not block smoke.
        $match = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::NameProperty, '正在读取')
        $labels = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $match)
        for ($i = 0; $i -lt $labels.Count; $i++) {
            if (-not $labels.Item($i).Current.IsOffscreen) { return $true }
        }
        return $false
    }
    $loadedAfterMs = 0
    while ((Catalog-IsLoading) -and $loadedAfterMs -lt 45000) {
        Start-Sleep -Milliseconds 500
        $loadedAfterMs += 500
    }
    if (Catalog-IsLoading) {
        throw 'The visible installed-app catalog did not finish loading within 45 seconds.'
    }
    Write-Output "ALL_APPS_LOADED_MS=$loadedAfterMs"
    Capture-Phone 'all'

    Press '搜索 App'
    $search = Require-Control '搜索 App 和窗口'
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Chrome')
    Start-Sleep -Milliseconds 450
    Capture-Phone 'search'
    Press '取消搜索'

    Press '运行中'
    # The first visible app card must stay fully visible for inline confirmation.
    $scroller = Require-Control 'App 列表'
    $scrollPattern = $null
    if ($scroller.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$scrollPattern)) {
        $scrollPattern.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, 0)
        Start-Sleep -Milliseconds 300
    }
    $quit = Find-VisibleAction '退出 App'
    if ($null -ne $quit) {
        $quit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 300
        [void](Find-VisibleAction '确认退出')
        Capture-Phone 'confirmation'
        Press '取消退出'
    }

    Press '退出手机屏'
    [void](Require-Control '退出手机屏？')
    if (-not (Find-VisibleAction '确认退出手机屏')) {
        throw 'Upstream inline phone-dismissal confirmation is missing.'
    }
    Capture-Phone 'dismiss'
    Press '取消退出手机屏'

    Press '手机型号'
    $picker = Require-Control '搜索手机型号'
    $picker.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('iPhone13')
    Start-Sleep -Milliseconds 400
    [void](Require-Control 'iPhone 13 Pro Max')
    Capture-Phone 'model'
    Press '返回 App'
    Write-Output 'PHONE_UI_SMOKE_PASS: home, all, search, confirmation, model, return.'
}
finally {
    if (-not $preview.HasExited) {
        Stop-Process -Id $preview.Id -Force -ErrorAction SilentlyContinue
    }
}
