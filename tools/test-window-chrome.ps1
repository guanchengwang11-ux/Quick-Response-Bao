param([Parameter(Mandatory = $true)][string]$Exe)
$ErrorActionPreference = 'Stop'
if (Get-Process QuickResponseBao -ErrorAction SilentlyContinue) { throw 'Close Quick Response Bao before running the window chrome test.' }
Add-Type -AssemblyName UIAutomationClient
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class QrbWindowTestNative {
  [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct Placement { public int Length, Flags, ShowCommand; public Point Min, Max; public Rect Normal; }
  [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr handle, ref Placement placement);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr handle, int command);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr handle);
}
'@
function Get-WindowState([IntPtr]$Handle) { $value = New-Object QrbWindowTestNative+Placement; $value.Length = [Runtime.InteropServices.Marshal]::SizeOf($value); [QrbWindowTestNative]::GetWindowPlacement($Handle, [ref]$value) | Out-Null; $value.ShowCommand }
function Invoke-TitleButton([IntPtr]$Handle, [string]$Id) {
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
  $condition = New-Object System.Windows.Automation.PropertyCondition -ArgumentList ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), $Id
  $button = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
  if (-not $button) { throw "Missing title-bar button: $Id" }
  ($button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)).Invoke()
  Start-Sleep -Milliseconds 500
}
$process = Start-Process -FilePath (Resolve-Path $Exe) -PassThru
try {
  $deadline = [DateTime]::UtcNow.AddSeconds(10); do { Start-Sleep -Milliseconds 100; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
  $handle = $process.MainWindowHandle; if ($handle -eq 0) { throw 'Main window was not created.' }
  Invoke-TitleButton $handle 'TitleBarMinimizeButton'; if ((Get-WindowState $handle) -ne 2) { throw 'Minimize did not produce SW_SHOWMINIMIZED.' }
  [QrbWindowTestNative]::ShowWindow($handle, 9) | Out-Null
  Invoke-TitleButton $handle 'TitleBarMaximizeButton'; if ((Get-WindowState $handle) -ne 3) { throw 'Maximize did not produce SW_SHOWMAXIMIZED.' }
  Invoke-TitleButton $handle 'TitleBarMaximizeButton'; if ((Get-WindowState $handle) -ne 1) { throw 'Restore did not produce SW_SHOWNORMAL.' }
  Invoke-TitleButton $handle 'TitleBarCloseButton'; if (-not $process.HasExited -and [QrbWindowTestNative]::IsWindowVisible($handle)) { throw 'Close did not hide the main window.' }
  if ($process.HasExited) { throw 'Close terminated the listener instead of hiding to tray.' }
  $secondary = Start-Process -FilePath (Resolve-Path $Exe) -PassThru; Start-Sleep -Seconds 1; $process.Refresh()
  if (-not $secondary.HasExited -or -not [QrbWindowTestNative]::IsWindowVisible($process.MainWindowHandle)) { throw 'Single-instance tray reactivation failed.' }
  [pscustomobject]@{ Minimize = 'Pass'; Maximize = 'Pass'; Restore = 'Pass'; CloseToTray = 'Pass'; TrayReopen = 'Pass' }
}
finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id } }
