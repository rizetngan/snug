param(
    [ValidateSet('Install', 'StartDisplay', 'StopDisplay', 'Uninstall')]
    [string]$Action = 'Install'
)

$ErrorActionPreference = 'Stop'
$DriverRoot = 'C:\VirtualDisplayDriver'
$HardwareId = 'Root\MttVDD'
$DisplayClassGuid = '4D36E968-E325-11CE-BFC1-08002BE10318'
$ExpectedVddZipHash = 'E24210692B442B39AF763536330CE78B423F19342B7A7792C26DE3944E418B3A'
$ExpectedNefconZipHash = '28292F4B1417484DB6AD23C47FFC18EA99D3DE3BDB3BBBE60C8ED11703A3780A'
$ExpectedVddSignerThumbprint = '3CF8CF26D8BA266C3A483AB7D26D4A818E317D76'
$ExpectedNefconSignerThumbprint = 'FF0CCF318FD8C14775023A0B52C28FFCCA59D7F7'
$ProgramDataRoot = Join-Path $env:ProgramData 'Snug'
$MarkerPath = Join-Path $ProgramDataRoot 'driver-ownership.json'
$LogDirectory = Join-Path $ProgramDataRoot 'Logs'
$NefconPath = Join-Path $PSScriptRoot 'Tools\nefconc.exe'
$TaskSddl = 'D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;AU)'
$script:RebootRequired = $false

New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
$LogPath = Join-Path $LogDirectory 'driver-install.log'

function Write-Log([string]$Message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $Message
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Read-Marker {
    try {
        if (Test-Path $MarkerPath) { return Get-Content -LiteralPath $MarkerPath -Raw | ConvertFrom-Json }
    } catch { }
    return $null
}

function Write-Marker([string]$State) {
    New-Item -ItemType Directory -Path $ProgramDataRoot -Force | Out-Null
    @{
        managedBySnug = $true
        installState = $State
        driverVersion = '25.7.23'
        nefconVersion = '1.21.1'
        hardwareId = $HardwareId
        installedAt = [DateTime]::UtcNow.ToString('o')
        rebootRequired = $script:RebootRequired
    } | ConvertTo-Json | Set-Content -LiteralPath $MarkerPath -Encoding UTF8
}

function Assert-SignedFile([string]$Path, [string]$ExpectedThumbprint, [string]$ExpectedSubject) {
    $signature = Get-AuthenticodeSignature -FilePath $Path
    if ($signature.Status -ne 'Valid') {
        throw "Signature validation failed: $([IO.Path]::GetFileName($Path)) ($($signature.Status))."
    }
    if ($ExpectedThumbprint -and $signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint) {
        throw "Unexpected signer certificate: $([IO.Path]::GetFileName($Path))."
    }
    if ($ExpectedSubject -and $signature.SignerCertificate.Subject -notmatch $ExpectedSubject) {
        throw "Unexpected publisher: $([IO.Path]::GetFileName($Path))."
    }
}

function Invoke-Nefcon([string]$Executable, [string[]]$Arguments) {
    Write-Log ("NefCon: " + ($Arguments -join ' '))
    & $Executable @Arguments 2>&1 | ForEach-Object {
        Add-Content -LiteralPath $LogPath -Value ([string]$_) -Encoding UTF8
    }
    if ($LASTEXITCODE -eq 3010) {
        $script:RebootRequired = $true
        Write-Log 'NefCon reported that Windows must be restarted to finish this driver operation.'
        return
    }
    if ($LASTEXITCODE -ne 0) {
        throw "NefCon failed with exit code $LASTEXITCODE."
    }
}

function Get-SnugDevice {
    $devices = Get-PnpDevice -PresentOnly -Class Display -ErrorAction SilentlyContinue
    foreach ($candidate in $devices) {
        $hardwareIds = Get-PnpDeviceProperty -InstanceId $candidate.InstanceId -KeyName 'DEVPKEY_Device_HardwareIds' -ErrorAction SilentlyContinue
        if ($hardwareIds -and ($hardwareIds.Data -contains $HardwareId)) {
            return $candidate
        }
    }
    return $null
}

function Wait-ForVdd {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        $device = Get-SnugDevice
        $driverInf = $null
        if ($device) {
            $driverInf = (Get-PnpDeviceProperty -InstanceId $device.InstanceId -KeyName 'DEVPKEY_Device_DriverInfPath' -ErrorAction SilentlyContinue).Data
        }
        if ($device -and $device.Status -eq 'OK' -and $driverInf) {
            try {
                $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
                    '.', 'MTTVirtualDisplayPipe', [System.IO.Pipes.PipeDirection]::InOut)
                $pipe.Connect(700)
                $pipe.Dispose()
                return $true
            } catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Wait-ForDeviceGone {
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (-not (Get-SnugDevice)) { return $true }
        Start-Sleep -Milliseconds 300
    }
    return -not (Get-SnugDevice)
}

function Apply-PhoneProfile {
    $profilePath = Join-Path $ProgramDataRoot 'phone-profile.json'
    if (-not (Test-Path -LiteralPath $profilePath)) {
        Write-Log 'No saved phone profile; keeping the default VDD mode.'
        return
    }

    try {
        $phoneProfile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    } catch {
        throw "Phone profile is unreadable: $($_.Exception.Message)"
    }
    if ($phoneProfile.schemaVersion -ne 1) { throw 'Unsupported Snug phone profile version.' }

    $modes = @($phoneProfile.modes)
    if ($modes.Count -lt 1 -or $modes.Count -gt 80) { throw 'Phone profile must contain between 1 and 80 display modes.' }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($mode in $modes) {
        $width = [int]$mode.width
        $height = [int]$mode.height
        $refreshRate = [int]$mode.refreshRate
        if ($width -lt 800 -or $width -gt 4320 -or $height -lt 1000 -or $height -gt 8000 -or $refreshRate -ne 60) {
            throw 'Phone profile contains an unsupported display mode.'
        }
        if (-not $seen.Add("$($width)x$($height)")) { throw 'Phone profile contains duplicate display modes.' }
    }

    $settingsPath = Join-Path $DriverRoot 'vdd_settings.xml'
    if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'The installed VDD settings file is missing.' }
    [xml]$settings = Get-Content -LiteralPath $settingsPath -Raw
    $resolutions = $settings.SelectSingleNode('/vdd_settings/resolutions')
    if ($null -eq $resolutions) {
        $resolutions = $settings.CreateElement('resolutions')
        $global = $settings.SelectSingleNode('/vdd_settings/global')
        if ($global) { [void]$global.ParentNode.InsertAfter($resolutions, $global) }
        else { [void]$settings.DocumentElement.AppendChild($resolutions) }
    }

    $currentModes = @($resolutions.SelectNodes('resolution') | ForEach-Object {
        '{0}x{1}@{2}' -f $_.width, $_.height, $_.refresh_rate
    })
    $desiredModes = @($modes | ForEach-Object {
        '{0}x{1}@{2}' -f [int]$_.width, [int]$_.height, [int]$_.refreshRate
    })
    if ($currentModes.Count -eq $desiredModes.Count -and
        [string]::Join('|', $currentModes) -ceq [string]::Join('|', $desiredModes)) {
        Write-Log ("VDD profile already matches {0}; no driver settings changed." -f $phoneProfile.model)
        return
    }

    $backupDirectory = Join-Path $ProgramDataRoot 'Backups'
    New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
    $backup = Join-Path $backupDirectory ('vdd_settings_profile_' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.xml')
    Copy-Item -LiteralPath $settingsPath -Destination $backup -Force

    while ($resolutions.HasChildNodes) {
        [void]$resolutions.RemoveChild($resolutions.FirstChild)
    }
    foreach ($mode in $modes) {
        $resolution = $settings.CreateElement('resolution')
        foreach ($entry in @(
            @{ Name = 'width'; Value = [int]$mode.width },
            @{ Name = 'height'; Value = [int]$mode.height },
            @{ Name = 'refresh_rate'; Value = [int]$mode.refreshRate }
        )) {
            $element = $settings.CreateElement($entry.Name)
            $element.InnerText = [string]$entry.Value
            [void]$resolution.AppendChild($element)
        }
        [void]$resolutions.AppendChild($resolution)
    }
    $settings.Save($settingsPath)
    Write-Log ("Applied {0} VDD modes for {1}." -f $modes.Count, $phoneProfile.model)
}

function Initialize-PhoneProfileAccess {
    $profilePath = Join-Path $ProgramDataRoot 'phone-profile.json'
    if (-not (Test-Path -LiteralPath $profilePath)) {
        '{"schemaVersion":1,"model":"Apple iPhone 17 Pro Max","modes":[{"width":1080,"height":1900,"refreshRate":60}]}' |
            Set-Content -LiteralPath $profilePath -Encoding UTF8
    }
    $icacls = Join-Path $env:WINDIR 'System32\icacls.exe'
    & $icacls $profilePath '/grant' '*S-1-5-11:(M)' '/inheritance:e' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Unable to grant Snug phone-profile write permission.' }
    Write-Log 'Granted authenticated users modify access to the validated phone-profile file only.'
}
function Get-SnugTaskFolder {
    $service = New-Object -ComObject 'Schedule.Service'
    $service.Connect()
    $rootFolder = $service.GetFolder('\')
    try {
        $folder = $service.GetFolder('\Snug')
    } catch {
        $folder = $rootFolder.CreateFolder('Snug', $null)
    }
    $folder.SetSecurityDescriptor($TaskSddl, 0)
    return @{ Service = $service; Folder = $folder }
}

function Register-SnugTasks([string]$ScriptPath) {
    $tasks = Get-SnugTaskFolder
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 2) -MultipleInstances IgnoreNew -StartWhenAvailable
    $powerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $startArguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $ScriptPath + '" -Action StartDisplay'
    $stopArguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $ScriptPath + '" -Action StopDisplay'
    $startAction = New-ScheduledTaskAction -Execute $powerShell -Argument $startArguments
    $stopAction = New-ScheduledTaskAction -Execute $powerShell -Argument $stopArguments
    Register-ScheduledTask -TaskName 'StartPhoneDisplay' -TaskPath '\Snug\' -Action $startAction -Principal $principal -Settings $settings -Force | Out-Null
    Register-ScheduledTask -TaskName 'StopPhoneDisplay' -TaskPath '\Snug\' -Action $stopAction -Principal $principal -Settings $settings -Force | Out-Null
    $folder = $tasks.Service.GetFolder('\Snug')
    foreach ($taskName in @('StartPhoneDisplay', 'StopPhoneDisplay')) {
        $task = $folder.GetTask($taskName)
        $task.SetSecurityDescriptor($TaskSddl, 0)
    }
    Write-Log 'Created fixed SYSTEM tasks for starting and stopping the Snug display node.'
}

function Remove-SnugTasks {
    try {
        Unregister-ScheduledTask -TaskName 'StartPhoneDisplay' -TaskPath '\Snug\' -Confirm:$false -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName 'StopPhoneDisplay' -TaskPath '\Snug\' -Confirm:$false -ErrorAction SilentlyContinue
        $service = New-Object -ComObject 'Schedule.Service'
        $service.Connect()
        try {
            $folder = $service.GetFolder('\Snug')
            if (@($folder.GetTasks(0)).Count -eq 0) {
                $service.GetFolder('\').DeleteFolder('Snug', 0)
            }
        } catch { }
    } catch {
        Write-Log "Scheduled task cleanup skipped: $($_.Exception.Message)"
    }
}

try {
    Write-Log "=== Snug driver action: $Action ==="
    if (-not [Environment]::Is64BitOperatingSystem) { throw 'This build requires 64-bit Windows.' }

    if ($Action -eq 'Install' -or $Action -eq 'Uninstall') {
        if (-not (Test-Admin)) { throw 'Administrator permission is required.' }
    }

    $marker = Read-Marker
    $device = Get-SnugDevice

    if ($Action -eq 'StartDisplay') {
        if (-not ($marker -and $marker.managedBySnug)) { throw 'Snug does not own the installed driver package.' }
        if (-not (Test-Path $NefconPath)) { throw 'NefCon is missing. Repair the Snug driver installation.' }
        Assert-SignedFile $NefconPath $ExpectedNefconSignerThumbprint 'Nefarius Software Solutions'
        $inf = Join-Path $DriverRoot 'MttVDD.inf'
        if (-not (Test-Path $inf)) { throw 'The installed VDD INF is missing.' }
        Apply-PhoneProfile
        Invoke-Nefcon $NefconPath @('install', $inf, $HardwareId, '--no-duplicates')
        if (-not (Wait-ForVdd)) { throw 'The Snug display device was created, but its driver did not become ready.' }
        Write-Log 'Snug Phone display node is ready.'
        exit 0
    }

    if ($Action -eq 'StopDisplay') {
        if ($marker -and $marker.managedBySnug -and $device) {
            if (-not (Test-Path $NefconPath)) { throw 'NefCon is missing. Repair the Snug driver installation.' }
            Invoke-Nefcon $NefconPath @('remove', $HardwareId)
            if (-not (Wait-ForDeviceGone)) { throw 'Snug Phone display node is still present.' }
            Write-Log 'Snug Phone display node removed; driver package remains installed.'
        }
        exit 0
    }

    if ($Action -eq 'Uninstall') {
        if (-not ($marker -and $marker.managedBySnug)) {
            throw 'Snug did not install this driver; an externally managed driver will not be removed.'
        }
        Remove-SnugTasks
        if ($device -and (Test-Path $NefconPath)) {
            Invoke-Nefcon $NefconPath @('remove', $HardwareId)
            if (-not (Wait-ForDeviceGone)) { throw 'The Snug display device could not be removed.' }
        }
        $nefcon = $NefconPath
        $temporary = $null
        if (-not (Test-Path $nefcon)) {
            $temporary = Join-Path $env:TEMP ('Snug-NefCon-' + [Guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $temporary -Force | Out-Null
            $archive = Join-Path $PSScriptRoot 'Tools\nefcon_v1.21.1.zip'
            if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $ExpectedNefconZipHash) { throw 'NefCon package checksum mismatch.' }
            Expand-Archive -LiteralPath $archive -DestinationPath $temporary -Force
            $nefcon = Join-Path $temporary 'x64\nefconc.exe'
        }
        Assert-SignedFile $nefcon $ExpectedNefconSignerThumbprint 'Nefarius Software Solutions'
        Invoke-Nefcon $nefcon @('--remove-driver-store-package', '--inf-name', 'MttVDD.inf', '--class-guid', $DisplayClassGuid)
        if ($temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force -ErrorAction SilentlyContinue }
        Remove-Item -LiteralPath $MarkerPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $DriverRoot -Recurse -Force -ErrorAction SilentlyContinue
        Write-Log 'Snug-managed VDD package and display node removed.'
        exit 0
    }

    if (-not (Test-Admin)) { throw 'Administrator permission is required.' }
    if ($device -and -not ($marker -and $marker.managedBySnug)) {
        throw 'A Virtual Display Driver device already exists and is not managed by Snug. Its settings were left untouched.'
    }
    if (-not ($marker -and $marker.managedBySnug)) {
        $externalConfig = (Test-Path (Join-Path $DriverRoot 'vdd_settings.xml')) -or (Test-Path 'HKLM:\SOFTWARE\MikeTheTech\VirtualDisplayDriver')
        if ($externalConfig) { throw 'An existing Virtual Display Driver installation was found. Snug will not overwrite its configuration.' }
    }

    $driverZip = Join-Path $PSScriptRoot 'VDD\VirtualDisplayDriver-x86.Driver.Only.zip'
    $nefZip = Join-Path $PSScriptRoot 'Tools\nefcon_v1.21.1.zip'
    $settingsTemplate = Join-Path $PSScriptRoot 'snug_vdd_settings.xml'
    $edidTemplate = Join-Path $PSScriptRoot 'snug_user_edid.bin'
    foreach ($path in @($driverZip, $nefZip, $settingsTemplate, $edidTemplate)) {
        if (-not (Test-Path $path)) { throw "Required installer asset is missing: $path" }
    }
    if ((Get-FileHash $driverZip -Algorithm SHA256).Hash -ne $ExpectedVddZipHash) { throw 'Virtual Display Driver package checksum mismatch.' }
    if ((Get-FileHash $nefZip -Algorithm SHA256).Hash -ne $ExpectedNefconZipHash) { throw 'NefCon package checksum mismatch.' }

    $work = Join-Path $env:TEMP ('Snug-VDD-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work -Force | Out-Null
    try {
        $driverExtract = Join-Path $work 'VDD'
        $nefExtract = Join-Path $work 'NefCon'
        Expand-Archive -LiteralPath $driverZip -DestinationPath $driverExtract -Force
        Expand-Archive -LiteralPath $nefZip -DestinationPath $nefExtract -Force
        $package = Join-Path $driverExtract 'VirtualDisplayDriver'
        $inf = Join-Path $package 'MttVDD.inf'
        $dll = Join-Path $package 'MttVDD.dll'
        $cat = Join-Path $package 'mttvdd.cat'
        $nefcon = Join-Path $nefExtract 'x64\nefconc.exe'
        foreach ($path in @($inf, $dll, $cat, $nefcon)) {
            if (-not (Test-Path $path)) { throw "Package is incomplete: $path" }
        }

        Assert-SignedFile $dll $ExpectedVddSignerThumbprint $null
        Assert-SignedFile $cat $ExpectedVddSignerThumbprint $null
        Assert-SignedFile $nefcon $ExpectedNefconSignerThumbprint 'Nefarius Software Solutions'
        if (-not (Select-String -LiteralPath $inf -Pattern 'Root\\MttVDD' -Quiet)) { throw 'The driver INF does not declare the expected hardware ID.' }

        if (Test-Path (Join-Path $DriverRoot 'vdd_settings.xml')) {
            $backupDirectory = Join-Path $ProgramDataRoot 'Backups'
            New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
            $backup = Join-Path $backupDirectory ('vdd_settings_' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.xml')
            Copy-Item (Join-Path $DriverRoot 'vdd_settings.xml') $backup -Force
            Write-Log "Existing Snug-managed VDD configuration backed up to $backup"
        }

        Write-Marker 'installing'
        New-Item -ItemType Directory -Path $DriverRoot -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Tools') -Force | Out-Null
        Copy-Item $dll,$inf,$cat -Destination $DriverRoot -Force
        Copy-Item $settingsTemplate (Join-Path $DriverRoot 'vdd_settings.xml') -Force
        Copy-Item $edidTemplate (Join-Path $DriverRoot 'user_edid.bin') -Force
        Copy-Item $nefcon (Join-Path $PSScriptRoot 'Tools\nefconc.exe') -Force
        $persistentNefcon = Join-Path $PSScriptRoot 'Tools\nefconc.exe'
        Assert-SignedFile $persistentNefcon $ExpectedNefconSignerThumbprint 'Nefarius Software Solutions'
        Invoke-Nefcon $persistentNefcon @('--install-driver', '--inf-path', $inf)
        Initialize-PhoneProfileAccess
        Register-SnugTasks $PSCommandPath
        Write-Marker 'installed'
        Write-Log 'Official signed Virtual Display Driver package installed. Snug Phone display is created only during a UU session.'
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }

    exit 0
}
catch {
    Write-Log ("ERROR: " + $_.Exception.Message)
    Write-Error $_.Exception.Message
    exit 1
}