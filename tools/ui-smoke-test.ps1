<#
.SYNOPSIS
    End-to-end smoke test for the Reactor Todo sample, driven through Windows UI Automation.

.DESCRIPTION
    Launches the built app, drives its real controls (add / toggle / filter / rename /
    delete / clear-completed), restarts it to prove persistence, then restores your
    existing %LOCALAPPDATA%\ReactorTodo\todos.json so the run leaves no trace.

    No test framework needed: WinUI has no headless host, so the checks run against the
    live window through UIA. Exits 0 when every check passes, 1 otherwise.

.EXAMPLE
    pwsh -File tools\ui-smoke-test.ps1
    pwsh -File tools\ui-smoke-test.ps1 -Arch arm64
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86', 'arm64')]
    [string]$Arch = 'x64',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$ExePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class FocusHelper {
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool altTab);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowThreadProcessId(IntPtr h, IntPtr pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(IntPtr a, IntPtr b, bool attach);
    [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);

    // Real pointer input: WinUI only raises pointer events for genuine mouse messages.
    public static void ClickAt(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(250);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);   // left down
        System.Threading.Thread.Sleep(60);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);   // left up
    }

    // Keyboard checks (SendKeys) need a really activated window; Windows may refuse to
    // hand focus to a background process, so try the usual escalation ladder.
    public static bool TryActivate(IntPtr h) {
        if (h == IntPtr.Zero) return false;
        if (GetForegroundWindow() == h) return true;
        SetForegroundWindow(h);
        if (WaitForeground(h)) return true;
        SwitchToThisWindow(h, false);
        if (WaitForeground(h)) return true;
        IntPtr fg = GetForegroundWindow();
        IntPtr tidFg = GetWindowThreadProcessId(fg, IntPtr.Zero);
        IntPtr tidMe = GetCurrentThreadId();
        AttachThreadInput(tidMe, tidFg, true);
        SetForegroundWindow(h);
        AttachThreadInput(tidMe, tidFg, false);
        return WaitForeground(h);
    }

    static bool WaitForeground(IntPtr h) {
        for (int i = 0; i < 10; i++) {
            System.Threading.Thread.Sleep(120);
            if (GetForegroundWindow() == h) return true;
        }
        return GetForegroundWindow() == h;
    }
}
"@

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) {
    $ExePath = Join-Path $repoRoot "src\TodoApp\bin\$Arch\$Configuration\net10.0-windows10.0.26100.0\win-$Arch\TodoApp.exe"
}
$ExePath = [System.IO.Path]::GetFullPath($ExePath)
if (-not (Test-Path $ExePath)) {
    throw "Executable not found: $ExePath`nBuild it first: dotnet build src\TodoApp\TodoApp.csproj -p:Platform=$Arch"
}

$todosFile = Join-Path $env:LOCALAPPDATA 'ReactorTodo\todos.json'
$backupFile = "$todosFile.smoketest.bak"
$settingsFile = Join-Path $env:LOCALAPPDATA 'ReactorTodo\settings.json'
$settingsBackupFile = "$settingsFile.smoketest.bak"

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

$script:failures = 0
$script:checks = 0

function Assert-That([bool]$condition, [string]$message) {
    $script:checks++
    if ($condition) {
        Write-Host "  [PASS] $message" -ForegroundColor Green
    }
    else {
        $script:failures++
        Write-Host "  [FAIL] $message" -ForegroundColor Red
    }
}

function Get-AppWindow {
    # The window title is localized (e.g. "Reactor 待办"), so look the window up by handle.
    $window = $AE::FromHandle($script:procHandle)
    if (-not $window) { throw 'The app window was not found through UI Automation.' }
    return $window
}

function Find-Elements($root, $controlType) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $controlType)
    return @($root.FindAll($TS::Descendants, $condition))
}

function Find-Button($root, [string]$name) {
    return @(Find-Elements $root $CT::Button | Where-Object { $_.Current.Name -eq $name })[0]
}

function Get-RowAutomationNames($root) {
    return @(Find-Elements $root $CT::CheckBox | ForEach-Object { $_.Current.Name })
}

function Start-App {
    $process = Start-Process -FilePath $ExePath -PassThru
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) { throw "The app exited during startup with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne 0) { break }
    }
    Start-Sleep -Milliseconds 1000
    $script:procHandle = $process.MainWindowHandle
    return $process
}

function Stop-App($process) {
    if (-not $process) { return }
    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 700
    }
    if (-not $process.HasExited) { $process.Kill() }
    Start-Sleep -Milliseconds 400
}

function Invoke-Button($root, [string]$name) {
    $button = Find-Button $root $name
    if (-not $button) { throw "Button '$name' not found." }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 500
}

function Set-ComposerText($root, [string]$text) {
    $box = @(Find-Elements $root $CT::Edit)[0]
    if (-not $box) { throw 'The composer text box was not found.' }
    $box.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
    $box.SetFocus()
    Start-Sleep -Milliseconds 300
}

function Select-Filter($root, [string]$namePattern) {
    $item = @(Find-Elements $root $CT::ListItem | Where-Object { $_.Current.Name -like $namePattern })[0]
    if (-not $item) { throw "Filter '$namePattern' not found." }
    $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 600
}

function Get-FilterNames($root) {
    return @(Find-Elements $root $CT::ListItem | ForEach-Object { $_.Current.Name } | Where-Object { $_ })
}

# Confirmation dialogs surface as a child window whose title ends with 待办？
function Get-ConfirmDialog($root) {
    return @(Find-Elements $root $CT::Window | Where-Object { $_.Current.Name -like '*待办？' })[0]
}

function Get-DialogButton($dialog, [string]$name) {
    return @(Find-Elements $dialog $CT::Button | Where-Object { $_.Current.Name -eq $name })[0]
}

# Settings live behind two icon buttons in the title bar; their automation ids are stable
# across languages, and the flyout items are ordered: theme = system/light/dark,
# language = system/中文/English/日本語.
function Find-SettingsButton($root, [string]$automationId) {
    return @(Find-Elements $root $CT::Button | Where-Object { $_.Current.AutomationId -eq $automationId })[0]
}

function Open-SettingsMenu($root, [string]$automationId) {
    $button = Find-SettingsButton $root $automationId
    if (-not $button) { throw "settings button '$automationId' not found" }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 900
    return $button
}

function Click-SettingsButton($root, [string]$automationId) {
    # The screen remounts on every theme change, so always re-resolve the control first.
    $button = Find-SettingsButton $root $automationId
    if (-not $button) { throw "settings button '$automationId' not found" }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 1400
}

function Select-SettingsItem($root, [int]$index) {
    $items = Find-Elements $root $CT::MenuItem
    if ($items.Count -le $index) { throw "menu item $index not found ($($items.Count) items)" }
    # WinUI RadioMenuFlyoutItem exposes TogglePattern.
    $items[$index].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 1100
}

function Test-SettingsItemChecked($root, [int]$index) {
    $items = Find-Elements $root $CT::MenuItem
    if ($items.Count -le $index) { return $false }
    return $items[$index].GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq `
        [System.Windows.Automation.ToggleState]::On
}

function Invoke-Element($element) {
    $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 700
}

# Keyboard checks need the window to really own the foreground; Windows may refuse that
# for a background process, in which case SendKeys would type into some other window.
function Test-Focus($process) {
    if (-not $process) { return $false }
    $process.Refresh()
    return [FocusHelper]::TryActivate($process.MainWindowHandle)
}

$process = $null
try {
    Write-Host "`n== Reactor Todo UI smoke test ==" -ForegroundColor Cyan
    Write-Host "exe: $ExePath"

    Get-Process -Name TodoApp -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill() }
    Start-Sleep -Milliseconds 500

    # Keep the user's real data intact across the run.
    if (Test-Path $todosFile) { Move-Item $todosFile $backupFile -Force }
    if (Test-Path $todosFile) { Remove-Item $todosFile -Force }
    if (Test-Path $settingsFile) { Move-Item $settingsFile $settingsBackupFile -Force }
    '{"theme":"System","language":"ChineseSimplified"}' | Set-Content $settingsFile -Encoding utf8

    # ---- 1. empty start ----
    Write-Host "`n[1] cold start" -ForegroundColor Yellow
    $process = Start-App
    $window = Get-AppWindow
    # The window title is localized (Reactor 待办 / Reactor Todo / Reactor やること).
    Assert-That ($process.MainWindowTitle -match '^Reactor ') "window title is present ($($process.MainWindowTitle))"
    Assert-That ((Get-RowAutomationNames $window).Count -eq 0) 'starts with an empty list'

    # ---- 2. add through the button ----
    Write-Host "`n[2] add items" -ForegroundColor Yellow
    Set-ComposerText $window '买牛奶'
    Invoke-Button $window '添加'
    Set-ComposerText $window '写周报'
    Invoke-Button $window '添加'
    Assert-That ((Get-RowAutomationNames $window).Count -eq 2) 'clicking 添加 adds two rows'
    Assert-That ((Get-FilterNames $window) -join '|' -match '全部 2') 'filter bar shows 全部 2'

    # ---- 3. third row (keyboard entry is covered in step 10, which needs real focus) ----
    Set-ComposerText $window '第三条待办'
    Invoke-Button $window '添加'
    Assert-That ((Get-RowAutomationNames $window).Count -eq 3) 'a third row can be added'

    # ---- 4. toggle completion ----
    Write-Host "`n[4] toggle completion" -ForegroundColor Yellow
    $firstCheckbox = @(Find-Elements $window $CT::CheckBox)[0]
    $firstCheckbox.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 600
    $filters = (Get-FilterNames $window) -join '|'
    Assert-That ($filters -match '未完成 2' -and $filters -match '已完成 1') 'counters update to 未完成 2 / 已完成 1'

    # ---- 5. filter ----
    Write-Host "`n[5] filters" -ForegroundColor Yellow
    Select-Filter $window '未完成*'
    Assert-That ((Get-RowAutomationNames $window).Count -eq 2) 'Active filter hides the completed row'
    Select-Filter $window '已完成*'
    Assert-That ((Get-RowAutomationNames $window).Count -eq 1) 'Completed filter shows only the completed row'
    Select-Filter $window '全部*'
    Assert-That ((Get-RowAutomationNames $window).Count -eq 3) 'All filter shows every row'

    # ---- 6. inline rename (rename the last row: step 8 deletes the first one) ----
    Write-Host "`n[6] inline rename" -ForegroundColor Yellow
    $editButton = @(Find-Elements $window $CT::Button | Where-Object { $_.Current.Name -eq '编辑' })[-1]
    $editButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 600
    Assert-That ($null -ne (Find-Button $window '保存')) 'row switches into edit mode'

    $editors = Find-Elements $window $CT::Edit
    Assert-That ($editors.Count -eq 2) 'edit mode exposes composer + row editor'
    $editors[1].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('第三条待办（已改名）')
    Start-Sleep -Milliseconds 300
    Invoke-Button $window '保存'
    Start-Sleep -Milliseconds 600
    $texts = @(Find-Elements $window $CT::Text | ForEach-Object { $_.Current.Name })
    Assert-That ($texts -contains '第三条待办（已改名）') 'saving commits the rename'

    # ---- 7. layout geometry ----
    Write-Host "`n[7] layout" -ForegroundColor Yellow
    $titles = @(Find-Elements $window $CT::Text | Where-Object { $_.Current.Name -match '买牛奶|写周报|第三条待办（已改名）' })
    $deletes = @(Find-Elements $window $CT::Button | Where-Object { $_.Current.Name -eq '删除' })
    $windowRect = $window.Current.BoundingRectangle
    $geometryOk = $true
    $rowsAligned = $true
    if ($titles.Count -ge 2 -and $deletes.Count -ge 2) {
        $spacing = $titles[1].Current.BoundingRectangle.Y - $titles[0].Current.BoundingRectangle.Y
        for ($i = 0; $i -lt [Math]::Min($titles.Count, $deletes.Count); $i++) {
            $titleRect = $titles[$i].Current.BoundingRectangle
            $deleteRect = $deletes[$i].Current.BoundingRectangle
            if ([double]::IsInfinity($titleRect.X) -or [double]::IsInfinity($deleteRect.X)) { $geometryOk = $false; continue }
            if (-not ($deleteRect.X -gt $titleRect.X)) { $geometryOk = $false }
            if ($deleteRect.X + $deleteRect.Width -gt $windowRect.X + $windowRect.Width) { $geometryOk = $false }
            if ([Math]::Abs($titleRect.Y - $deleteRect.Y) -ge 24) { $rowsAligned = $false }
        }
    }
    else { $geometryOk = $false }
    Assert-That $geometryOk 'every row is laid out with its actions to the right and inside the window'
    Assert-That $rowsAligned 'row actions sit on the same line as their title'

    # the checklist must fill the middle of the window, with the footer pinned to the bottom
    # Several controls expose ScrollPattern (the filter bar, the combo boxes), so take the
    # tallest scrollable element: that is the checklist viewport.
    $scrollHost = $null
    $scrollHostHeight = 0
    foreach ($candidate in (Find-Elements $window $CT::Pane) + (Find-Elements $window $CT::List)) {
        try {
            [void]$candidate.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
            $height = $candidate.Current.BoundingRectangle.Height
            if ($height -gt $scrollHostHeight) {
                $scrollHost = $candidate
                $scrollHostHeight = $height
            }
        }
        catch { }
    }
    $windowBottom = $window.Current.BoundingRectangle.Y + $window.Current.BoundingRectangle.Height
    $summaryText = @(Find-Elements $window $CT::Text | Where-Object { $_.Current.Name -match '项未完成|remaining|残り' })[0]

    if ($scrollHost) {
        $hostHeight = $scrollHost.Current.BoundingRectangle.Height
        Assert-That ($hostHeight -gt 200) "the checklist fills the middle of the window ($([int]$hostHeight)px tall)"
    }
    else {
        Write-Host '  [SKIP] no scroll host exposed while the list is short' -ForegroundColor DarkYellow
    }

    if ($summaryText) {
        $summaryBottom = $summaryText.Current.BoundingRectangle.Y + $summaryText.Current.BoundingRectangle.Height
        Assert-That (($windowBottom - $summaryBottom) -lt 80) "the summary/footer sits at the bottom ($([int]($windowBottom - $summaryBottom))px above the edge)"
    }

    # ---- 8. destructive actions ask for confirmation ----
    Write-Host "`n[8] destructive actions" -ForegroundColor Yellow

    # 8a - clearing completed asks first, and cancelling is a no-op
    Invoke-Button $window '清除已完成'
    Start-Sleep -Milliseconds 600
    $dialog = Get-ConfirmDialog $window
    Assert-That ($null -ne $dialog) '清除已完成 opens a confirmation dialog'
    if ($dialog) {
        Invoke-Element (Get-DialogButton $dialog '取消')
    }
    Assert-That ($null -eq (Get-ConfirmDialog $window)) 'cancelling closes the clear dialog'
    Assert-That (((Get-FilterNames $window) -join '|') -match '已完成 1') 'cancelling keeps the completed row'

    # 8b - deleting asks first: cancel keeps the row, confirming removes exactly one
    $lastDelete = @(Find-Elements $window $CT::Button | Where-Object { $_.Current.Name -eq '删除' })[-1]
    Invoke-Element $lastDelete
    $dialog = Get-ConfirmDialog $window
    Assert-That ($null -ne $dialog) '删除 opens a confirmation dialog'
    if ($dialog) {
        $dialogButtons = @(Find-Elements $dialog $CT::Button | ForEach-Object { $_.Current.Name })
        Assert-That (($dialogButtons -contains '取消') -and ($dialogButtons -contains '删除')) "the dialog offers 取消 + 删除 ($($dialogButtons -join ' | '))"
        Assert-That ($dialogButtons.Count -eq 2) 'the dialog renders no stray built-in primary button'
        Invoke-Element (Get-DialogButton $dialog '取消')
    }
    Assert-That (@(Get-RowAutomationNames $window).Count -eq 3) 'cancelling keeps every row'

    $lastDelete = @(Find-Elements $window $CT::Button | Where-Object { $_.Current.Name -eq '删除' })[-1]
    Invoke-Element $lastDelete
    $dialog = Get-ConfirmDialog $window
    if ($dialog) { Invoke-Element (Get-DialogButton $dialog '删除') }
    Assert-That (@(Get-RowAutomationNames $window).Count -eq 2) 'confirming removes exactly one row'

    # 8c - now the clear actually goes through
    Invoke-Button $window '清除已完成'
    Start-Sleep -Milliseconds 600
    $dialog = Get-ConfirmDialog $window
    if ($dialog) { Invoke-Element (Get-DialogButton $dialog '清除') }
    Assert-That (((Get-FilterNames $window) -join '|') -match '已完成 0') 'confirming drops the completed rows'
    Assert-That (-not (Find-Button $window '清除已完成').Current.IsEnabled) 'the clear button disables itself when nothing is completed'

    # ---- 9. persistence ----
    Write-Host "`n[9] persistence" -ForegroundColor Yellow
    $beforeRestart = Get-RowAutomationNames $window
    Stop-App $process
    $process = $null
    Assert-That (Test-Path $todosFile) "state file written: $todosFile"

    $process = Start-App
    $window = Get-AppWindow
    $afterRestart = Get-RowAutomationNames $window
    Assert-That ($afterRestart.Count -eq $beforeRestart.Count) "rows restored after restart ($($afterRestart.Count))"
    $texts = @(Find-Elements $window $CT::Text | ForEach-Object { $_.Current.Name })
    Assert-That ($texts -contains '写周报') 'the surviving row came back after the restart'

    # ---- 10. pointer + keyboard (both need a really activated window) ----
    Write-Host "`n[10] pointer and keyboard" -ForegroundColor Yellow
    if (Test-Focus $process) {
        # clicking the title text toggles the item, just like the checkbox
        $countersBefore = (Get-FilterNames $window) -join '|'
        $toggled = $false
        foreach ($attempt in 1..2) {
            # A click can be swallowed when another window steals focus mid-run, so retry once.
            [void](Test-Focus $process)
            $title = @(Find-Elements $window $CT::Text | Where-Object { $_.Current.Name -eq '写周报' })[0]
            if (-not $title) { break }
            $titleRect = $title.Current.BoundingRectangle
            [FocusHelper]::ClickAt([int]($titleRect.X + $titleRect.Width / 2), [int]($titleRect.Y + $titleRect.Height / 2))
            Start-Sleep -Milliseconds 900
            if (((Get-FilterNames $window) -join '|') -ne $countersBefore) { $toggled = $true; break }
        }
        Assert-That $toggled 'clicking the title text toggles the item'

        if ($toggled) {
            [void](Test-Focus $process)
            $title = @(Find-Elements $window $CT::Text | Where-Object { $_.Current.Name -eq '写周报' })[0]
            if ($title) {
                $titleRect = $title.Current.BoundingRectangle
                [FocusHelper]::ClickAt([int]($titleRect.X + $titleRect.Width / 2), [int]($titleRect.Y + $titleRect.Height / 2))
                Start-Sleep -Milliseconds 900
            }
            Assert-That (((Get-FilterNames $window) -join '|') -eq $countersBefore) 'clicking it again toggles back'
        }

        $beforeKeyboard = @(Get-RowAutomationNames $window).Count
        Set-ComposerText $window '键盘输入'
        [System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
        Start-Sleep -Milliseconds 800
        Assert-That (@(Get-RowAutomationNames $window).Count -eq $beforeKeyboard + 1) 'Enter in the composer adds a row'
    }
    else {
        Write-Host '  [SKIP] pointer/keyboard checks - Windows would not give the app foreground,' -ForegroundColor DarkYellow
        Write-Host '         so clicks and keystrokes would land in another window. UIA checks above still ran.' -ForegroundColor DarkYellow
    }

    # ---- 11. language + theme switching ----
    Write-Host "`n[11] language and theme" -ForegroundColor Yellow
    $themeButton = Find-SettingsButton $window 'ThemeButton'
    $languageButton = Find-SettingsButton $window 'LanguageButton'
    Assert-That ($null -ne $themeButton -and $null -ne $languageButton) 'two settings icon buttons are present'

    if ($themeButton -and $languageButton) {
        $windowTop = $window.Current.BoundingRectangle.Y
        $windowLeft = $window.Current.BoundingRectangle.X
        $windowWidth = $window.Current.BoundingRectangle.Width
        $themeRect = $themeButton.Current.BoundingRectangle
        Assert-That (($themeRect.Y - $windowTop) -gt 60) 'they sit in the page content, not in the window frame'
        Assert-That (($themeRect.X - $windowLeft) -gt ($windowWidth / 2)) 'they sit in the top-right corner of the page'
        Assert-That ($themeRect.Width -lt 60) 'they are icon-only (no text label)'

        # theme button cycles follow-system -> light -> dark -> follow-system, with no flyout
        Click-SettingsButton $window 'ThemeButton'
        Assert-That (@(Find-Elements $window $CT::MenuItem).Count -eq 0) 'the theme button opens no flyout'
        $savedSettings = if (Test-Path $settingsFile) { Get-Content $settingsFile -Raw } else { '' }
        Assert-That ($savedSettings -match '"theme":\s*"Light"') 'click 1 of the theme button -> Light'

        Click-SettingsButton $window 'ThemeButton'
        $savedSettings = if (Test-Path $settingsFile) { Get-Content $settingsFile -Raw } else { '' }
        Assert-That ($savedSettings -match '"theme":\s*"Dark"') 'click 2 -> Dark'

        Click-SettingsButton $window 'ThemeButton'
        $savedSettings = if (Test-Path $settingsFile) { Get-Content $settingsFile -Raw } else { '' }
        Assert-That ($savedSettings -match '"theme":\s*"System"') 'click 3 -> follow system'

        # language flyout: system / 中文 / English / 日本語
        [void](Open-SettingsMenu $window 'LanguageButton')
        $languageMenu = Find-Elements $window $CT::MenuItem
        Assert-That ($languageMenu.Count -eq 4) "the language flyout lists four choices ($($languageMenu.Count))"

        Select-SettingsItem $window 2   # English
        Assert-That (@(Find-Elements $window $CT::Button | ForEach-Object { $_.Current.Name }) -contains 'Add') 'switching to English relabels the UI'
        [void](Open-SettingsMenu $window 'LanguageButton')
        Assert-That (Test-SettingsItemChecked $window 2) 'the check-mark is on English'

        Select-SettingsItem $window 1   # 简体中文
        Assert-That (@(Find-Elements $window $CT::Text | ForEach-Object { $_.Current.Name }) -contains '待办清单') 'switching back restores Chinese'
    }
}
finally {
    Stop-App $process
    if (Test-Path $todosFile) { Remove-Item $todosFile -Force }
    if (Test-Path $backupFile) { Move-Item $backupFile $todosFile -Force }
    if (Test-Path $settingsFile) { Remove-Item $settingsFile -Force }
    if (Test-Path $settingsBackupFile) { Move-Item $settingsBackupFile $settingsFile -Force }
}

Write-Host ''
if ($script:failures -eq 0) {
    Write-Host "RESULT: all $($script:checks) checks passed" -ForegroundColor Green
    exit 0
}
Write-Host "RESULT: $($script:failures) of $($script:checks) checks failed" -ForegroundColor Red
exit 1









