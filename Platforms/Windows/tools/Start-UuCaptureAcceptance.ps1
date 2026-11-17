param(
    [Parameter(Mandatory=$true)][string]$DotnetPath,
    [Parameter(Mandatory=$true)][string]$CandidateDll,
    [Parameter(Mandatory=$true)][string]$HelperDll,
    [Parameter(Mandatory=$true)][string]$ReportPath,
    [switch]$ConfirmedDisconnected,
    [switch]$Preflight
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
$primary=[Windows.Forms.Screen]::PrimaryScreen.DeviceName
$originalScreens=@([Windows.Forms.Screen]::AllScreens|ForEach-Object{$_.DeviceName+' '+$_.Bounds.ToString()})
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
    $helper=Start-Process $DotnetPath -ArgumentList @('"'+$HelperDll+'"','--pilot-disconnected',$id,$sid,'300') -Verb RunAs -PassThru
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
        if([Windows.Forms.Screen]::AllScreens.Count-eq1){break}
        Start-Sleep -Milliseconds 250
    }
    if([Windows.Forms.Screen]::AllScreens.Count-ne1-or[Windows.Forms.Screen]::PrimaryScreen.DeviceName-ne$primary){
        throw 'Expected unchanged single physical display after normal exit'
    }
    $baseline=@([Windows.Forms.Screen]::AllScreens|ForEach-Object{$_.DeviceName+" "+$_.Bounds.ToString()})
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
    $limit=[DateTime]::UtcNow.AddSeconds(230)
    $last=$null; $readyForPhone=$false; $baselineDeadline=[DateTime]::UtcNow.AddSeconds(20)
    while([DateTime]::UtcNow-lt$limit-and!$candidate.HasExited-and!$helper.HasExited){
        $screens=@([Windows.Forms.Screen]::AllScreens|ForEach-Object{$_.DeviceName+' '+$_.Bounds.ToString()})
        $hasLedger=Test-Path $ledger
        $phase='Awaiting'
        if(Test-Path $auditPath){
            try{$phase=(Get-Content -LiteralPath $auditPath -Tail 1 -Encoding UTF8|ConvertFrom-Json).phase}catch{}
        }
        $snapshot=[ordered]@{screens=$screens;placed=$hasLedger;phase=$phase}
        if(!$readyForPhone-and$phase-eq'Inactive'-and(($screens-join';')-eq($baseline-join';'))-and!$hasLedger){
            $readyForPhone=$true;Record 'ReadyForPhone' $true
        }
        if(!$readyForPhone-and[DateTime]::UtcNow-gt$baselineDeadline){throw 'Verified inactive baseline not reached'}
        $key=$snapshot|ConvertTo-Json -Compress
        if($key-ne$last){Record 'Topology' $snapshot;$last=$key}
        Start-Sleep -Milliseconds 500
    }
    Record 'ObservationEnded' $true
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