# Measures the optical size / stroke weight of an icon button, so icon families can be compared.
#
# Prints, per button: button rect, ink bounding box, ink pixel count, and a stroke-width proxy
# (mean length of horizontal ink runs inside the glyph, which approximates stroke thickness).
#
# Usage:  pwsh -File tools/measure-icon-ink.ps1 [-Theme dark] [-AutomationIds ThemeButton,LanguageButton]

param(
    [string]$Theme = 'Light',
    [string]$AutomationIds = 'ThemeButton,LanguageButton'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class InkWin32 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
"@

$repoRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repoRoot 'src\TodoApp\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TodoApp.exe'
$settingsPath = Join-Path $env:LOCALAPPDATA 'ReactorTodo\settings.json'
$captureDir = Join-Path $repoRoot '.ink'

# start the app with the requested theme
Get-Process -Name TodoApp -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit(2000) }
Start-Sleep -Milliseconds 700
if (Test-Path $settingsPath) {
    "{`"theme`":`"$Theme`",`"language`":`"ChineseSimplified`"}" | Set-Content $settingsPath -Encoding utf8
}

$proc = Start-Process -FilePath $exe -PassThru
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $proc.Refresh()
    if ($proc.MainWindowHandle -ne 0) { break }
}
Start-Sleep -Milliseconds 2200
$hwnd = $proc.MainWindowHandle

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$window = $AE::FromHandle($hwnd)

function Find-Button([string]$automationId) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)
    $buttons = @($window.FindAll($TS::Descendants, $condition))
    @($buttons | Where-Object { $_.Current.AutomationId -eq $automationId })[0]
}

function Capture-Window {
    $rect = New-Object 'InkWin32+RECT'
    [void][InkWin32]::GetWindowRect($hwnd, [ref]$rect)
    $bitmap = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    [void][InkWin32]::PrintWindow($hwnd, $hdc, 2)
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    return [pscustomobject]@{ Bitmap = $bitmap; Origin = $rect }
}

function Measure-Ink($element, [string]$label, $capture) {
    $rect = $element.Current.BoundingRectangle
    $bitmap = $capture.Bitmap
    $ox = [int]($rect.X - $capture.Origin.Left)
    $oy = [int]($rect.Y - $capture.Origin.Top)

    $width = [int]$rect.Width
    $height = [int]$rect.Height

    # The icon may be darker or lighter than its button, so detect ink by distance from the
    # button's own background colour (sampled from a corner) instead of a fixed threshold.
    $bg = $bitmap.GetPixel($ox + 1, $oy + 1)
    $bgLum = 0.299 * $bg.R + 0.587 * $bg.G + 0.114 * $bg.B
    $minX = $width; $maxX = -1; $minY = $height; $maxY = -1; $ink = 0
    $runs = New-Object System.Collections.Generic.List[int]

    for ($dy = 0; $dy -lt $height; $dy++) {
        $run = 0
        for ($dx = 0; $dx -lt $width; $dx++) {
            $pixel = $bitmap.GetPixel($ox + $dx, $oy + $dy)
            $luminance = 0.299 * $pixel.R + 0.587 * $pixel.G + 0.114 * $pixel.B
            if ([Math]::Abs($luminance - $bgLum) -gt 60) {
                $ink++
                if ($dx -lt $minX) { $minX = $dx }
                if ($dx -gt $maxX) { $maxX = $dx }
                if ($dy -lt $minY) { $minY = $dy }
                if ($dy -gt $maxY) { $maxY = $dy }
                $run++
            }
            elseif ($run -gt 0) {
                $runs.Add($run)
                $run = 0
            }
        }

        if ($run -gt 0) { $runs.Add($run) }
    }

    if ($maxX -lt 0) {
        Write-Output ("{0,-10} button={1}x{2}  NO INK" -f $label, $width, $height)
        return
    }

    $sorted = @($runs | Sort-Object)
    $mean = if ($sorted.Count) { ($sorted | Measure-Object -Average).Average } else { 0 }
    $median = if ($sorted.Count) { $sorted[[int]($sorted.Count / 2)] } else { 0 }

    Write-Output ("{0,-10} button={1}x{2}  ink={3}x{4} at ({5},{6})  inkPixels={7}  strokeWidth mean={8:N2}px median={9}px" -f `
        $label, $width, $height, ($maxX - $minX + 1), ($maxY - $minY + 1), $minX, $minY, $ink, $mean, $median)
}

New-Item -ItemType Directory -Force -Path $captureDir | Out-Null
$capture = Capture-Window
foreach ($id in ($AutomationIds -split ',')) {
    $button = Find-Button $id.Trim()
    if ($null -eq $button) { Write-Output "$($id.Trim()): button not found"; continue }
    Measure-Ink $button $id.Trim() $capture
}

$capture.Bitmap.Save((Join-Path $captureDir "window-$Theme.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$capture.Bitmap.Dispose()

$proc.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 500
if (-not $proc.HasExited) { $proc.Kill() }

