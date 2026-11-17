param(
    [ValidateSet('Install', 'Stop', 'Resume', 'Uninstall')]
    [string]$Action = 'Install'
)

$ErrorActionPreference = 'Stop'
$TaskName = 'UuCaptureMonitor'
$TaskPath = '\Snug\'
$TaskSddl = 'D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;AU)'
$ProgramDataRoot = Join-Path $env:ProgramData 'Snug'
$ConfigPath = Join-Path $ProgramDataRoot 'uu-capture-monitor.json'
$LogDirectory = Join-Path $ProgramDataRoot 'Logs'
$LogPath = Join-Path $LogDirectory 'uu-capture-monitor-install.log'
$AppRoot = Split-Path -Parent $PSScriptRoot
$Dotnet = Join-Path $AppRoot 'Runtime\dotnet.exe'
$Probe = Join-Path $PSScriptRoot 'UuCaptureProbe.dll'

New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null

function Write-Log([string]$Message) {
    Add-Content -LiteralPath $LogPath -Value ('{0:O} {1}' -f [DateTimeOffset]::UtcNow, $Message) -Encoding UTF8
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-MonitorTask {
    try {
        return Get-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath -ErrorAction Stop
    } catch {
        return $null
    }
}

function Stop-MonitorTask {
    $task = Get-MonitorTask
    if (-not $task -or $task.State -ne 'Running') { return }
    Stop-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 250
        $task = Get-MonitorTask
    } while ($task -and $task.State -eq 'Running' -and [DateTimeOffset]::UtcNow -lt $deadline)
    if ($task -and $task.State -eq 'Running') {
        throw 'The UU capture monitor did not stop within 20 seconds.'
    }
    Write-Log 'Stopped the previous UU capture monitor before changing its binaries or configuration.'
}

function Ensure-TaskFolder {
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $root = $service.GetFolder('\')
    try {
        $folder = $service.GetFolder('\Snug')
    } catch {
        $folder = $root.CreateFolder('Snug', $null)
    }
    $folder.SetSecurityDescriptor($TaskSddl, 0)
}

function Remove-EmptyTaskFolder {
    try {
        $service = New-Object -ComObject 'Schedule.Service'
        $service.Connect()
        $folder = $service.GetFolder('\Snug')
        if (@($folder.GetTasks(0)).Count -eq 0) {
            $service.GetFolder('\').DeleteFolder('Snug', 0)
        }
    } catch { }
}

try {
    if ($Action -eq 'Stop') {
        if (-not (Test-Admin)) { throw 'Administrator permission is required to stop the system monitor.' }
        Stop-MonitorTask
        exit 0
    }

    if ($Action -eq 'Resume') {
        if (-not (Test-Admin)) { throw 'Administrator permission is required to resume the system monitor.' }
        if (Get-MonitorTask) {
            $task = Get-MonitorTask
            if ($task.State -ne 'Running') {
                Start-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
            }
        }
        exit 0
    }

    if ($Action -eq 'Uninstall') {
        if (-not (Test-Admin)) { throw 'Administrator permission is required to remove the system monitor.' }
        Stop-MonitorTask
        Unregister-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath -Confirm:$false -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $ConfigPath -Force -ErrorAction SilentlyContinue
        Remove-EmptyTaskFolder
        Write-Log 'Removed the UU capture monitor task and its user-scoped pipe configuration.'
        exit 0
    }

    if (-not (Test-Admin)) { throw 'Administrator permission is required to install the system monitor.' }
    if (-not (Test-Path -LiteralPath $Dotnet) -or -not (Test-Path -LiteralPath $Probe)) {
        throw 'The bundled .NET runtime or UU capture monitor is missing.'
    }

    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $identity.User) { throw 'The interactive installation user SID could not be determined.' }
    $userSid = $identity.User.Value
    $feedId = $null
    if (Test-Path -LiteralPath $ConfigPath) {
        try {
            $existing = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
            $parsedFeedId = [Guid]::Empty
            if ($existing.schemaVersion -eq 1 -and $existing.userSid -eq $userSid -and
                [Guid]::TryParse([string]$existing.feedId, [ref]$parsedFeedId) -and
                $parsedFeedId -ne [Guid]::Empty) {
                $feedId = $parsedFeedId.ToString('D')
            }
        } catch { }
    }
    if (-not $feedId) { $feedId = [Guid]::NewGuid().ToString('D') }

    Stop-MonitorTask
    $configuration = [ordered]@{
        schemaVersion = 1
        feedId = $feedId
        userSid = $userSid
        installedAt = [DateTimeOffset]::UtcNow.ToString('O')
    }
    $temporary = $ConfigPath + '.tmp'
    $configuration | ConvertTo-Json -Compress | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $ConfigPath -Force

    $icacls = Join-Path $env:WINDIR 'System32\icacls.exe'
    & $icacls $ConfigPath '/inheritance:r' '/grant:r' '*S-1-5-18:F' '*S-1-5-32-544:F' ("*{0}:R" -f $userSid) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to protect the monitor configuration ACL.' }

    Ensure-TaskFolder
    $arguments = '"{0}" --service {1} {2}' -f $Probe, $feedId, $userSid
    $taskAction = New-ScheduledTaskAction -Execute $Dotnet -Argument $arguments -WorkingDirectory $AppRoot
    $trigger = New-ScheduledTaskTrigger -AtStartup
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

    Register-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath -Action $taskAction -Trigger $trigger -Principal $principal -Settings $settings -Description 'Observes UU display-capture lifecycle for Snug. Sends fixed metadata to the signed-in local user through a restricted one-way named pipe.' -Force | Out-Null

    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $task = $service.GetFolder('\Snug').GetTask($TaskName)
    $task.SetSecurityDescriptor($TaskSddl, 0)

    $monitorLog = Join-Path $LogDirectory 'uu-capture-monitor.log'
    if (Test-Path $monitorLog) { Move-Item -LiteralPath $monitorLog -Destination ($monitorLog + '.previous') -Force }

    Start-ScheduledTask -TaskName $TaskName -TaskPath $TaskPath
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(20)
    $ready = $false
    do {
        Start-Sleep -Milliseconds 250
        $task = Get-MonitorTask
        if ($task -and $task.State -eq 'Running' -and (Test-Path $monitorLog) -and (Select-String -LiteralPath $monitorLog -Pattern 'Monitor started' -Quiet)) {
            $ready = $true
            break
        }
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    if (-not $ready) {
        $task = Get-MonitorTask
        $result = if ($task) { (Get-ScheduledTaskInfo -TaskName $TaskName -TaskPath $TaskPath).LastTaskResult } else { 'missing' }
        $state = if ($task) { $task.State } else { 'missing' }
        throw "The UU capture monitor did not become ready. Task state: $state; result: $result."
    }

    Write-Log ("Installed and started the SYSTEM UU capture monitor for user SID {0}." -f $userSid)
    exit 0
}
catch {
    Write-Log ("ERROR: " + $_.Exception.Message)
    Write-Error $_.Exception.Message
    exit 1
}
