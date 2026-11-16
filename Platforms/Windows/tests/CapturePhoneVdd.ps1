param(
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\phone-live-pmv2.png'),
    [string]$DeviceName = ''
)

$ErrorActionPreference = 'Stop'

# A normal Windows PowerShell 5.1 screenshot runs DPI unaware. On this host the
# Snug monitor is physically 1080x1834 but would be incorrectly clipped to
# 720x1223. Set PER_MONITOR_AWARE_V2 BEFORE asking WinForms for screen bounds.
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class SnugCaptureDpi {
    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
}
"@

$previousContext = [SnugCaptureDpi]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
if ($previousContext -eq [IntPtr]::Zero) {
    throw 'Cannot switch capture thread to Per-Monitor V2 DPI awareness.'
}

try {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $screens = @([System.Windows.Forms.Screen]::AllScreens | Where-Object {
        -not $_.Primary -and $_.Bounds.Height -gt $_.Bounds.Width -and
        (-not $DeviceName -or $_.DeviceName -eq $DeviceName)
    })
    if ($screens.Count -ne 1) {
        throw "Expected one Snug portrait display, found $($screens.Count); pass -DeviceName if multiple."
    }

    $bounds = $screens[0].Bounds
    if ($bounds.Width -lt 540 -or $bounds.Height -lt 800) {
        throw "Invalid physical phone-screen dimensions: $($bounds.Width) x $($bounds.Height)."
    }

    $directory = Split-Path -Parent $OutputPath
    if (-not (Test-Path $directory)) { [void](New-Item $directory -ItemType Directory -Force) }

    $image = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    try {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bounds.Size)
        $image.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $image.Dispose()
    }

    Write-Output "PHONE_VDD_CAPTURE=$OutputPath"
    Write-Output "DEVICE=$($screens[0].DeviceName)"
    Write-Output "PHYSICAL_PIXELS=$($bounds.Width)x$($bounds.Height)"
    Write-Output "PNG_BYTES=$((Get-Item $OutputPath).Length)"
}
finally {
    [void][SnugCaptureDpi]::SetThreadDpiAwarenessContext($previousContext)
}