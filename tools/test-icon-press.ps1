# Drives a real mouse press over an icon button and reports what the app logged.
# Used to verify press feedback, because UIA's InvokePattern does not produce pointer-down events.
param(
    [string]$AutomationId = 'LanguageButton',
    [int]$HoldMilliseconds = 220
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class RealInput {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, UIntPtr extra);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern bool SetCursorPos2(int x, int y);
  public const uint LEFTDOWN = 0x0002;
  public const uint LEFTUP = 0x0004;
  public static void Click(int x, int y, int hold) {
    SetCursorPos(x, y);
    System.Threading.Thread.Sleep(60);
    mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
    System.Threading.Thread.Sleep(hold);
    mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
  }
}
"@

$repoRoot = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $repoRoot 'src\TodoApp\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\TodoApp.exe'
$log = Join-Path (Split-Path $exe) 'press-log.txt'

Get-Process -Name TodoApp -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill(); $_.WaitForExit(2000) }
Start-Sleep -Milliseconds 600
Remove-Item $log -ErrorAction SilentlyContinue
$env:DSH_PRESS_PROBE = '1'
$proc = Start-Process -FilePath $exe -PassThru
for ($i = 0; $i -lt 40; $i++) { Start-Sleep -Milliseconds 500; $proc.Refresh(); if ($proc.MainWindowHandle -ne 0) { break } }
Start-Sleep -Milliseconds 2500

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]
$window = $AE::FromHandle($proc.MainWindowHandle)
$cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Button)
$button = @(@($window.FindAll($TS::Descendants, $cond)) | Where-Object { $_.Current.AutomationId -eq $AutomationId })[0]
if ($null -eq $button) { throw "button $AutomationId not found" }

$rect = $button.Current.BoundingRectangle
$x = [int]($rect.X + $rect.Width / 2)
$y = [int]($rect.Y + $rect.Height / 2)
Write-Output "target $AutomationId at ($x, $y) size $($rect.Width)x$($rect.Height)"

[void][RealInput]::SetForegroundWindow($proc.MainWindowHandle)
Start-Sleep -Milliseconds 400
$fg = [RealInput]::GetForegroundWindow()
Write-Output ("foreground match: {0} (ours={1}, fg={2})" -f ($fg -eq $proc.MainWindowHandle), $proc.MainWindowHandle, $fg)
[RealInput]::Click($x, $y, $HoldMilliseconds)
Start-Sleep -Milliseconds 900

# did the flyout open? that proves the click landed on the button
$menuCond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Menu)
$menus = @($window.FindAll($TS::Descendants, $menuCond))
Write-Output ("menus after click: {0}" -f $menus.Count)

Write-Output '=== app log ==='
Get-Content $log -ErrorAction SilentlyContinue
$env:DSH_PRESS_PROBE = ''
$proc.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 500
if (-not $proc.HasExited) { $proc.Kill() }

