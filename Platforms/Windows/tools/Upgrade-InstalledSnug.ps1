param(
    [Parameter(Mandatory=$true)][string]$InstallerPath,
    [Parameter(Mandatory=$true)][string]$ExpectedSha256,
    [string]$LedgerPath = "",
    [switch]$Preflight
)

$ErrorActionPreference = 'Stop'
$expectedVersion = [regex]::Match([IO.Path]::GetFileName($InstallerPath), 'Snug-Setup-(\d+\.\d+\.\d+)-x64\.exe$').Groups[1].Value
if ([string]::IsNullOrEmpty($expectedVersion)) { throw 'Unexpected Snug installer filename.' }
$diagnostics = 'F:\snug for Windows\diagnostics'
New-Item -ItemType Directory -Path $diagnostics -Force | Out-Null
$log = Join-Path $diagnostics ("UPGRADE_INSTALLED_" + $expectedVersion + ".log")
function Write-Audit([string]$message) {
    $line = (Get-Date -Format o) + ' ' + $message
    Add-Content -LiteralPath $log -Value $line -Encoding UTF8
    Write-Output $message
}

$installer = [IO.Path]::GetFullPath($InstallerPath)
if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Installer is missing.' }
$actualHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
if ($actualHash -ine $ExpectedSha256) { throw 'Installer checksum does not match; refusing upgrade.' }
$installedDll = 'C:\Program Files\Snug\Snug.Windows.dll'
$installedBefore = if (Test-Path -LiteralPath $installedDll) { (Get-Item $installedDll).VersionInfo.ProductVersion } else { 'absent' }
if ([string]::IsNullOrEmpty($LedgerPath)) { $LedgerPath = Join-Path $env:LOCALAPPDATA 'Snug\phone-screen.json' }
$ledger = [IO.Path]::GetFullPath($LedgerPath)
if (Test-Path -LiteralPath $ledger) { throw 'A live placed-window ledger exists. Restore it before upgrading Snug.' }

$processes = @(Get-CimInstance Win32_Process | Where-Object {
    $_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'Snug\.Windows\.dll'
})
Write-Audit "Preflight installer=$installer sha256=$actualHash installed=$installedBefore"
Write-Audit ('Existing Snug process IDs: ' + (($processes | ForEach-Object { $_.ProcessId }) -join ','))
if ($Preflight) {
    Write-Audit 'Preflight successful: no processes have been closed and no installation was attempted.'
    exit 0
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    # UAC is requested while Snug is STILL RUNNING. Canceling leaves the
    # current phone-screen session unchanged. Only an elevated child can exit it.
    $args = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath +
        '" -InstallerPath "' + $installer + '" -ExpectedSha256 ' + $ExpectedSha256 +
        ' -LedgerPath "' + $ledger + '"'
    Write-Audit 'Requesting standard Windows UAC approval. Existing Snug remains running until consent.'
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $args
    exit 0
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root = [System.Windows.Automation.AutomationElement]::RootElement
foreach ($process in $processes) {
    # Do not silently terminate other applications: Snug alone owns this named mutex.
    $p = Get-CimInstance Win32_Process -Filter "ProcessId=$($process.ProcessId)"
    if ($null -eq $p -or $p.CommandLine -notmatch 'Snug\.Windows\.dll') { continue }
    try {
        $wake = [System.Threading.EventWaitHandle]::OpenExisting('Local\SnugForWindows.Activate')
        try { [void]$wake.Set() } finally { $wake.Dispose() }
    } catch [System.Threading.WaitHandleCannotBeOpenedException] { }
    Start-Sleep -Milliseconds 1000
    $condition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, [int]$p.ProcessId)
    $windows = $root.FindAll([System.Windows.Automation.TreeScope]::Children, $condition)
    $exit = $null
    foreach ($window in $windows) {
        if ($window.Current.Name -ne 'Snug') { continue }
        $btnCondition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Button)
        $buttons = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $btnCondition)
        foreach ($button in $buttons) {
            if ($button.Current.IsEnabled -and $button.Current.Name -match '退出 Snug') {
                $exit = $button
                break
            }
        }
    }
    if ($null -eq $exit) { throw "Snug PID $($p.ProcessId) has no graceful Exit action; no installation attempted." }
    Write-Audit "Invoking graceful Snug exit for PID $($p.ProcessId)."
    $exit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $stopped = $false
    for ($i = 0; $i -lt 48; $i++) {
        Start-Sleep -Milliseconds 250
        if (!$([bool](Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue))) {
            $stopped = $true
            break
        }
    }
    if (!$stopped) { throw "Snug PID $($p.ProcessId) did not exit gracefully; aborting installation." }
}
if (Test-Path -LiteralPath $ledger) { throw 'A placed-window ledger appeared; aborting installation.' }

# The installer runs in normal Inno Setup silent mode, not by bypassing Windows UAC.
# The owned VDD driver is installed/updated by its existing idempotent installer.
$setupLog = Join-Path $diagnostics ("INSTALL_" + $expectedVersion + ".log")
Write-Audit "Starting signed-driver-preserving installer after UAC consent."
$args = @('/VERYSILENT', '/NORESTART', '/SUPPRESSMSGBOXES', ('/LOG="' + $setupLog + '"'))
$installerProcess = Start-Process -FilePath $installer -ArgumentList $args -PassThru -Wait
Write-Audit "Installer exit code: $($installerProcess.ExitCode)."
if ($installerProcess.ExitCode -notin @(0,3010)) { throw 'Installer did not finish successfully. Inspect setup log.' }
if (!(Test-Path -LiteralPath $installedDll)) { throw 'Installed Snug binary is missing.' }
$installedAfter = (Get-Item -LiteralPath $installedDll).VersionInfo.ProductVersion
Write-Audit "Installed binary version now: $installedAfter"
if (!$installedAfter.StartsWith($expectedVersion, [StringComparison]::Ordinal)) {
    throw "Installed version check failed; expected $expectedVersion."
}
Write-Audit 'UPGRADE_VERIFIED=PASS. Open Snug from Start menu; it will run at next user sign-in.'