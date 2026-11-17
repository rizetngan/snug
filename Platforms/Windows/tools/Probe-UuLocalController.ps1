param(
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$Label = "unlabeled",
    [string]$UuRoot = "D:\GameViewer",
    [switch]$IncludeCli
)
$ErrorActionPreference = "Stop"
$serverIds = @(Get-Process -Name GameViewerServer -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$clientIds = @(Get-Process -Name GameViewer -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$logs = @()
$folder = Join-Path $UuRoot "log\server\log"
if (Test-Path -LiteralPath $folder) {
    foreach ($file in Get-ChildItem -LiteralPath $folder -Filter "*_controlled_*.slog" -File) {
        if ($file.BaseName -notmatch "_(\d+)$" -or $serverIds -notcontains [int]$Matches[1]) { continue }
        $locked = $false
        $readable = $true
        try {
            $stream = [IO.File]::Open($file.FullName,"Open","Read","None")
            $stream.Dispose()
        } catch [IO.IOException] {
            if (($_.Exception.HResult -band 0xFFFF) -in 32,33) { $locked = $true }
            else { $readable = $false }
        } catch [UnauthorizedAccessException] { $readable = $false }
        $logs += [pscustomobject]@{
            File = $file.Name; Bytes = $file.Length
            ModifiedUtc = $file.LastWriteTimeUtc.ToString("o")
            SharingViolation = $locked; ProbeReadable = $readable
        }
    }
}
# Only read status once on request. Device status is NOT independent evidence
# of an inbound session. Record only counts/schema, never raw device IDs.
$cli = $null
if ($IncludeCli) {
    $exe = Join-Path $UuRoot "bin\uuyc-cli.exe"
    if (Test-Path -LiteralPath $exe) {
        try {
            $json = (& $exe device status --json 2>$null | Out-String) | ConvertFrom-Json
            $cli = [pscustomobject]@{
                Success = [bool]$json.success
                HasActiveConnections = $json.data.hasActiveConnections
                Total = $json.data.total
                ConnectionCount = @($json.data.connections | Where-Object { $null -ne $_ }).Count
                ConnectionFieldNames = @($json.data.connections |
                    ForEach-Object { $_.PSObject.Properties.Name } | Sort-Object -Unique)
            }
        } catch { $cli = [pscustomobject]@{ Success = $false } }
    }
}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
$trees = @()
foreach ($procId in $clientIds) {
    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,[int]$procId))
    foreach ($window in $windows) {
        try {
            $nodes = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            $anchorCount = 0
            foreach ($node in $nodes) {
                if ($node.Current.Name -eq "本机被控中") { $anchorCount++ }
            }
            $trees += [pscustomobject]@{
                ProcessId = $procId
                Class = $window.Current.ClassName
                Bounds = $window.Current.BoundingRectangle.ToString()
                MinimizedOrEmpty = $window.Current.BoundingRectangle.IsEmpty
                Descendants = $nodes.Count
                LocalControlledAnchors = $anchorCount
            }
        } catch { $trees += [pscustomobject]@{ProcessId=$procId; Error=$_.Exception.GetType().Name} }
    }
}
$exe = Join-Path $UuRoot "bin\GameViewer.exe"
$version = if (Test-Path -LiteralPath $exe) { (Get-Item -LiteralPath $exe).VersionInfo.ProductVersion } else { "" }
$output = [pscustomobject]@{
    CapturedUtc = [DateTime]::UtcNow.ToString("o")
    Label = $Label; UuVersion = $version
    ServerProcessIds = $serverIds; ClientProcessIds = $clientIds
    ControlledLogMetadata = $logs
    CliStatusSummary = $cli
    LocalAccessibilityTrees = $trees
}
$output | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$output | ConvertTo-Json -Depth 8