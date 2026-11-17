param(
    [Parameter(Mandatory=$true)][string]$DotnetPath,
    [Parameter(Mandatory=$true)][string]$CandidateDll,
    [Parameter(Mandatory=$true)][string]$HelperDll,
    [Parameter(Mandatory=$true)][string]$ReportPath,
    [switch]$ConfirmedDisconnected,
    [switch]$Preflight,
    [switch]$RequirePlacedWindow
)
$ErrorActionPreference='Stop'
$ledger=Join-Path $env:LOCALAPPDATA 'Snug\phone-screen.json'
$settings=Join-Path $env:LOCALAPPDATA 'Snug\settings.json'
$installed=Join-Path $env:ProgramFiles 'Snug\Snug.Windows.dll'
$launcher=Join-Path $env:ProgramFiles 'Snug\SnugLauncher.exe'
foreach($f in @($DotnetPath,$CandidateDll,$HelperDll,$installed,$launcher,$settings)){
    if(!(Test-Path -LiteralPath $f -PathType Leaf)){throw "Required file missing: $f"}
}
if((Test-Path $ReportPath)-or(Test-Path ($ReportPath+'.states.jsonl'))){throw 'Report must be a new file'}
if(Test-Path $ledger){throw 'Restore placed window before acceptance'}
if(!(Get-Content $settings -Raw | ConvertFrom-Json).PhoneScreenEnabled){throw 'Phone screen is disabled'}
if([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Run the supervisor as the ordinary interactive user'}
function Runtime {
    @(Get-CimInstance Win32_Process | Where-Object {$_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'Snug\.Windows\.dll'})
}
$old=@(Runtime)
if($old.Count-ne 1 -or $old[0].CommandLine -notlike "*$installed*"){throw 'Expected exactly one installed Snug runtime'}
if(@(Get-CimInstance Win32_Process -Filter "Name='GameViewerServer.exe'").Count-ne 1){throw 'Expected one UU server generation'}
if(@(Get-CimInstance Win32_Process|Where-Object{$_.Name-eq'dotnet.exe'-and$_.CommandLine-like'*UuCaptureProbe.dll*--pilot*'}).Count){throw 'Existing capture pilot'}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System.Runtime.InteropServices;
public static class SnugPilotPipe {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    public static extern bool WaitNamedPipe(string name,uint milliseconds);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct RECT { public int Left,Top,Right,Bottom; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    public struct MONITORINFOEX {
        public int cbSize; public RECT rcMonitor,rcWork; public uint dwFlags;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr,SizeConst=32)] public string szDevice;
    }
    public delegate bool Callback(System.IntPtr monitor,System.IntPtr hdc,ref RECT rect,System.IntPtr data);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(System.IntPtr hdc,System.IntPtr clip,Callback callback,System.IntPtr data);
    [DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern bool GetMonitorInfo(System.IntPtr monitor,ref MONITORINFOEX info);
    public class ScreenInfo {
        public string Name {get;set;}
        public bool Primary {get;set;}
        public string Description {get;set;}
    }
    public static ScreenInfo[] Screens(){
        var list=new System.Collections.Generic.List<ScreenInfo>();
        Callback cb=delegate(System.IntPtr m,System.IntPtr h,ref RECT r,System.IntPtr d){
            var i=new MONITORINFOEX(); i.cbSize=Marshal.SizeOf(typeof(MONITORINFOEX));
            if(!GetMonitorInfo(m,ref i)) return true;
            RECT a=i.rcMonitor;
            list.Add(new ScreenInfo{Name=i.szDevice,Primary=(i.dwFlags&1)!=0,Description=i.szDevice+" {X="+a.Left+",Y="+a.Top+",Width="+(a.Right-a.Left)+",Height="+(a.Bottom-a.Top)+"}"});
            return true;
        };
        if(!EnumDisplayMonitors(System.IntPtr.Zero,System.IntPtr.Zero,cb,System.IntPtr.Zero)) throw new System.InvalidOperationException("Monitor enumeration failed");
        return list.ToArray();
    }
}
"@
$root=[System.Windows.Automation.AutomationElement]::RootElement
$tree=[System.Windows.Automation.TreeScope]
$pp=[System.Windows.Automation.AutomationElement]::ProcessIdProperty
$cp=[System.Windows.Automation.AutomationElement]::ControlTypeProperty
function Stop-SnugGracefully([int]$procId,[string]$expectedDll){
    if(Test-Path $ledger){throw 'Placed window exists: preserve runtime'}
    $p=Get-CimInstance Win32_Process -Filter "ProcessId=$procId"
    if(!$p){return}
    if($p.CommandLine -notlike "*$expectedDll*"){throw 'Unexpected runtime identity'}
    $wake=[System.Threading.EventWaitHandle]::OpenExisting('Local\SnugForWindows.Activate')
    try{[void]$wake.Set()}finally{$wake.Dispose()}
    Start-Sleep -Milliseconds 650
    $exit=$null
    foreach($w in $root.FindAll($tree::Children,[System.Windows.Automation.PropertyCondition]::new($pp,$procId))){
        if($w.Current.Name-ne'Snug'){continue}
        foreach($n in $w.FindAll($tree::Descendants,[System.Windows.Automation.PropertyCondition]::new($cp,[System.Windows.Automation.ControlType]::Button))){
            if($n.Current.Name-eq'退出 Snug'-and$n.Current.IsEnabled){$exit=$n;break}
        }
    }
    if(!$exit){throw 'Normal exit action not available'}
    if(Test-Path $ledger){throw 'Placement appeared before exit'}
    $exit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    for($i=0;$i-lt80;$i++){
        if(!(Get-Process -Id $procId -ErrorAction SilentlyContinue)){return}
        Start-Sleep -Milliseconds 250
    }
    throw 'Snug normal exit did not finish'
}
$primary=([SnugPilotPipe]::Screens()|Where-Object Primary|Select-Object -First 1).Name
$originalScreens=@([SnugPilotPipe]::Screens()|ForEach-Object{$_.Description})
'PREFLIGHT=PASS'
'PRIMARY='+$primary
'INITIAL_SCREENS='+($originalScreens-join';')
if($Preflight){return}
if(!$ConfirmedDisconnected){throw 'Operator must confirm all UU incoming sessions disconnected'}
$id=[Guid]::NewGuid().ToString('N')
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$utf8=[Text.UTF8Encoding]::new($false)
$report=[IO.StreamWriter]::new([IO.FileStream]::new($ReportPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read),$utf8)
$report.AutoFlush=$true
function Record([string]$kind,$value){
    $row=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');kind=$kind;value=$value}
    $report.WriteLine(($row|ConvertTo-Json -Depth 5 -Compress))
    Write-Output ($kind+' '+($value|ConvertTo-Json -Depth 5 -Compress))
}
$oldStopped=$false
$candidate=$null
$helper=$null
try{
    Record 'OperatorConfirmedEmpty' $true
    # Normal UAC request first. Declining it leaves installed runtime untouched.
    $helper=Start-Process $DotnetPath -ArgumentList @('"'+$HelperDll+'"','--pilot-disconnected',$id,$sid,'600') -Verb RunAs -PassThru
    Record 'HelperStarted' $true
    # Wait for the helper's outbound pipe to exist without consuming its sole connection.
    $ready=$false
    for($i=0;$i-lt40;$i++){
        if($helper.HasExited){throw 'Capture helper exited before startup'}
        if([SnugPilotPipe]::WaitNamedPipe(('\\.\pipe\Snug-UU-Capture-Pilot-'+$id),250)){$ready=$true;break}
        Start-Sleep -Milliseconds 250
    }
    if(!$ready){throw 'Capture helper pipe not ready'}
    Stop-SnugGracefully ([int]$old[0].ProcessId) $installed
    $oldStopped=$true
    # This supervised acceptance targets the currently verified single
    # physical-display test machine. Do not guess which display is Snug's.
    for($i=0;$i-lt40;$i++){
        if(([SnugPilotPipe]::Screens()).Count-eq1){break}
        Start-Sleep -Milliseconds 250
    }
    if(([SnugPilotPipe]::Screens()).Count-ne1-or([SnugPilotPipe]::Screens()|Where-Object Primary|Select-Object -First 1).Name-ne$primary){
        throw 'Expected unchanged single physical display after normal exit'
    }
    $baseline=@([SnugPilotPipe]::Screens()|ForEach-Object{$_.Description})
    Record 'AfterNormalExit' $baseline
    $previous=$env:SNUG_UU_CAPTURE_PILOT
    $priorAudit=$env:SNUG_UU_CAPTURE_PILOT_AUDIT; $auditPath=$ReportPath+".states.jsonl"
    try{
        $env:SNUG_UU_CAPTURE_PILOT=$id
        $env:SNUG_UU_CAPTURE_PILOT_AUDIT=$auditPath
        $candidate=Start-Process $DotnetPath -ArgumentList @('"'+$CandidateDll+'"','--background') -WorkingDirectory ([IO.Path]::GetDirectoryName($CandidateDll)) -PassThru
    }finally{$env:SNUG_UU_CAPTURE_PILOT=$previous; $env:SNUG_UU_CAPTURE_PILOT_AUDIT=$priorAudit}
    Record 'CandidateStarted' $candidate.Id
    # Poll physical topology and lifecycle diagnostics only; no screenshots,
    # user application names, window content or input events.
    $limit=[DateTime]::UtcNow.AddSeconds(540)
    $last=$null; $readyForPhone=$false; $baselineDeadline=[DateTime]::UtcNow.AddSeconds(20); $sawActive=$false; $sawScreenWhileActive=$false; $sawPlacedWindow=$false; $inactiveSince=$null; $endedSince=$null; $stableSince=$null; $releaseConfirmed=$false; $screenPresentAtEnded=$false; $appAuditPath=$auditPath+'.app.jsonl'
    while([DateTime]::UtcNow-lt$limit-and!$candidate.HasExited-and!$helper.HasExited){
        $screens=@([SnugPilotPipe]::Screens()|ForEach-Object{$_.Description})
        $hasLedger=Test-Path $ledger
        $phase='Awaiting'
        if(Test-Path $auditPath){
            try{$phase=(Get-Content -LiteralPath $auditPath -Tail 1 -Encoding UTF8|ConvertFrom-Json).phase}catch{}
        }
        $now=[DateTime]::UtcNow
        $snapshot=[ordered]@{screens=$screens;placed=$hasLedger;phase=$phase}
        if($hasLedger){$sawPlacedWindow=$true}
        if(!$readyForPhone-and$phase-eq'Inactive'-and(($screens-join';')-eq($baseline-join';'))-and!$hasLedger){
            $readyForPhone=$true;Record 'ReadyForPhone' $true
        }
        if(!$readyForPhone-and$now-gt$baselineDeadline){throw 'Verified inactive baseline not reached'}
        if($phase-eq'Active'){
            $sawActive=$true
            if($screens.Count-gt$baseline.Count){$sawScreenWhileActive=$true}
            if($null-ne$inactiveSince){$inactiveSince=$null;$endedSince=$null;$stableSince=$null;$screenPresentAtEnded=$false}
        }
        if($sawActive-and$phase-eq'Inactive'-and$null-eq$inactiveSince){
            $inactiveSince=$now;Record 'InactiveObserved' $true
        }

        $auditRows=@()
        if(Test-Path $appAuditPath){
            $auditRows=@(Get-Content -LiteralPath $appAuditPath -Tail 160 -Encoding UTF8|ForEach-Object{try{$_|ConvertFrom-Json}catch{}})
        }
        $endedRow=$auditRows|Where-Object{$_.name-eq'SessionState'-and$_.value.transitions -contains 'Ended'}|Select-Object -Last 1
        if($null-eq$endedSince-and$null-ne$endedRow){
            $endedSince=[DateTimeOffset]::Parse($endedRow.utc).UtcDateTime
            $screenPresentAtEnded=[bool]$endedRow.value.phoneDisplay -and [bool]$endedRow.value.snugDisplay
            Record 'EndedObserved' @{screenPresent=$screenPresentAtEnded;connected=$endedRow.value.connected;wantsPhoneScreen=$endedRow.value.wantsPhoneScreen;dismissed=$endedRow.value.presentationDismissed;phoneScreenEnabled=$endedRow.value.phoneScreenEnabled}
            if(!$screenPresentAtEnded){
                Record 'AutoReleaseNotProven' 'Phone display was already absent when UU episode reached Ended'
                break
            }
        }

        $endStart=$null;$displayStop=$null;$putBack=$null
        if($null-ne$endedSince){
            $endStart=$auditRows|Where-Object{$_.name-eq'EndPhoneSession'-and$_.value.stage-eq'begin'-and[DateTimeOffset]::Parse($_.utc).UtcDateTime -ge $endedSince}|Select-Object -First 1
            $displayStop=$auditRows|Where-Object{$_.name-eq'EndPhoneSession'-and$_.value.stage-eq'display-stop'-and$_.value.stopped-eq$true-and[DateTimeOffset]::Parse($_.utc).UtcDateTime -ge $endedSince}|Select-Object -Last 1
            $putBack=$auditRows|Where-Object{$_.name-eq'EndPhoneSession'-and$_.value.stage-eq'after-putback'-and[DateTimeOffset]::Parse($_.utc).UtcDateTime -ge $endedSince}|Select-Object -Last 1
        }
        $returned=(($screens-join';')-eq($baseline-join';'))-and!$hasLedger
        $windowRestored=(!$RequirePlacedWindow)-or($sawPlacedWindow-and$null-ne$putBack-and$putBack.value.putBackStatus-eq'窗口已还原')
        if($sawActive-and$sawScreenWhileActive-and$null-ne$inactiveSince-and$null-ne$endedSince-and$screenPresentAtEnded-and$null-ne$endStart-and$null-ne$displayStop-and$returned-and$windowRestored){
            if($null-eq$stableSince){$stableSince=$now}
            if($now-$stableSince -ge [TimeSpan]::FromSeconds(2)){
                $releaseConfirmed=$true
                Record 'AutoReleaseConfirmed' @{secondsAfterInactive=[Math]::Round(($now-$inactiveSince).TotalSeconds,2);secondsAfterEnded=[Math]::Round(($now-$endedSince).TotalSeconds,2);screenCount=$screens.Count;placed=$hasLedger;windowRestored=$windowRestored;putBackStatus=$putBack.value.putBackStatus}
                $key=$snapshot|ConvertTo-Json -Compress
                if($key-ne$last){Record 'Topology' $snapshot;$last=$key}
                break
            }
        }else{$stableSince=$null}
        if($null-ne$inactiveSince-and$now-$inactiveSince -ge [TimeSpan]::FromSeconds(45)-and!$releaseConfirmed){
            Record 'AutoReleaseNotObserved' @{secondsAfterInactive=45;endedObserved=($null-ne$endedSince);screenPresentAtEnded=$screenPresentAtEnded;screenCount=$screens.Count;placed=$hasLedger;sawPlacedWindow=$sawPlacedWindow;endStartedAfterEnded=($null-ne$endStart);displayStopConfirmed=($null-ne$displayStop);windowRestored=$windowRestored}
            break
        }
        $key=$snapshot|ConvertTo-Json -Compress
        if($key-ne$last){Record 'Topology' $snapshot;$last=$key}
        Start-Sleep -Milliseconds 500
    }
    Record 'ObservationEnded' @{releaseConfirmed=$releaseConfirmed;sawActive=$sawActive;sawScreenWhileActive=$sawScreenWhileActive;sawPlacedWindow=$sawPlacedWindow;endedObserved=($null-ne$endedSince);screenPresentAtEnded=$screenPresentAtEnded}
}finally{
    try{
        if($candidate-and!$candidate.HasExited){
            # Do not close or force restore a user's currently placed window.
            if(Test-Path $ledger){Record 'CleanupBlocked' 'Placed window still present; leave candidate running'}
            else{Stop-SnugGracefully $candidate.Id $CandidateDll;Record 'CandidateNormalExit' $true}
        }
        if($oldStopped-and@(Runtime).Count-eq0){
            Start-Process $launcher -ArgumentList '--background'
            Record 'InstalledRestarted' $true
        }
        if($helper-and!$helper.HasExited){
            Record 'HelperBounded' 'Helper stops on reader disconnect or its own 300s limit'
        }
    }finally{$report.Dispose()}
}