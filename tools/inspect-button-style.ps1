$ErrorActionPreference = 'Stop'
function Hex($c) { '#{0:X2}{1:X2}{2:X2}' -f $c.R, $c.G, $c.B }
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ShotWin32 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
}
"@

$repoRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repoRoot 'src\TodoApp\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TodoApp.exe'
$settingsPath = Join-Path $env:LOCALAPPDATA 'ReactorTodo\settings.json'
Get-Process -Name TodoApp -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit(2000) }
Start-Sleep -Milliseconds 700
'{"theme":"Light","language":"ChineseSimplified"}' | Set-Content $settingsPath -Encoding utf8

$proc = Start-Process -FilePath $exe -PassThru
for ($i = 0; $i -lt 40; $i++) { Start-Sleep -Milliseconds 500; $proc.Refresh(); if ($proc.MainWindowHandle -ne 0) { break } }
Start-Sleep -Milliseconds 2200
$hwnd = $proc.MainWindowHandle

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$window = $AE::FromHandle($hwnd)
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)
$buttons = @($window.FindAll($TS::Descendants, $cond))

$rect = New-Object 'ShotWin32+RECT'
[void][ShotWin32]::GetWindowRect($hwnd, [ref]$rect)
$bmp = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc(); [void][ShotWin32]::PrintWindow($hwnd, $hdc, 2); $g.ReleaseHdc($hdc); $g.Dispose()

foreach ($id in 'ThemeButton', 'LanguageButton') {
    $btn = @($buttons | Where-Object { $_.Current.AutomationId -eq $id })[0]
    if ($null -eq $btn) { Write-Output "$id : not found"; continue }
    $r = $btn.Current.BoundingRectangle
    $ox = [int]($r.X - $rect.Left); $oy = [int]($r.Y - $rect.Top)
    $w = [int]$r.Width; $h = [int]$r.Height
    $corner = $bmp.GetPixel($ox + 2, $oy + 2)
    $edge = $bmp.GetPixel($ox, $oy + [int]($h / 2))
    $centre = $bmp.GetPixel($ox + [int]($w / 2), $oy + [int]($h / 2))
    $pageBg = $bmp.GetPixel($ox - 12, $oy + [int]($h / 2))
    Write-Output ("{0,-14} rect={1}x{2}  pageBg={3}  edge={4}  corner+2={5}  centre={6}" -f `
        $id, $w, $h, (Hex $pageBg), (Hex $edge), (Hex $corner), (Hex $centre))
}

function Hex($c) { '#{0:X2}{1:X2}{2:X2}' -f $c.R, $c.G, $c.B }

$outDir = Join-Path $repoRoot '.ink'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$bmp.Save((Join-Path $outDir 'buttons-light.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$proc.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 500
if (-not $proc.HasExited) { $proc.Kill() }

