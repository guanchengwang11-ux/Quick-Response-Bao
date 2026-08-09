param([Parameter(Mandatory = $true)][string]$Exe)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Windows.Forms
$previousTestInstance = $env:QRB_UI_TEST_INSTANCE_ID
$env:QRB_UI_TEST_INSTANCE_ID = "library$PID$([Guid]::NewGuid().ToString('N').Substring(0,12))"

function Find-ById([System.Windows.Automation.AutomationElement]$Root, [string]$Id, [int]$TimeoutMilliseconds = 5000) {
  $condition = New-Object System.Windows.Automation.PropertyCondition -ArgumentList ([System.Windows.Automation.AutomationElement]::AutomationIdProperty), $Id
  $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
  do {
    $element = $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($element) { return $element }
    Start-Sleep -Milliseconds 50
  } while ([DateTime]::UtcNow -lt $deadline)
  throw "Automation element was not found: $Id"
}

function Invoke-Element([System.Windows.Automation.AutomationElement]$Element) {
  $pattern = $null
  if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) { $pattern.Invoke(); return }
  if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) { $pattern.Select(); return }
  if ($Element.Current.IsKeyboardFocusable) { $Element.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait('{ENTER}'); return }
  throw "Element cannot be invoked: $($Element.Current.AutomationId)"
}

$process = Start-Process -FilePath (Resolve-Path $Exe) -PassThru
try {
  $deadline = [DateTime]::UtcNow.AddSeconds(12)
  do { Start-Sleep -Milliseconds 100; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
  if ($process.MainWindowHandle -eq 0) { throw 'Main window was not created.' }
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
  Invoke-Element (Find-ById $root 'LibraryNavigationItem')
  Start-Sleep -Milliseconds 500

  $search = Find-ById $root 'SearchBox'
  ($search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue('risk')
  $search.SetFocus(); [System.Windows.Forms.SendKeys]::SendWait('{TAB}'); Start-Sleep -Milliseconds 350
  $process.Refresh(); if ($process.HasExited) { throw 'The process exited after SearchBox Tab.' }
  $forward = [System.Windows.Automation.AutomationElement]::FocusedElement
  if ($forward.Current.AutomationId -eq 'SearchBox') { throw 'Tab did not move focus forward.' }
  [System.Windows.Forms.SendKeys]::SendWait('+{TAB}'); Start-Sleep -Milliseconds 350
  $reverse = [System.Windows.Automation.AutomationElement]::FocusedElement
  if ($reverse.Current.AutomationId -ne 'SearchBox') { throw 'Shift+Tab did not return focus to SearchBox.' }

  ($search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue('')
  Invoke-Element (Find-ById $root 'SummaryFilterButton')
  $desktop = [System.Windows.Automation.AutomationElement]::RootElement
  $operator = Find-ById $desktop 'TextOperatorBox'
  $selection = ($operator.GetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern)).Current.GetSelection()
  $operatorLabel = if ($selection.Count -gt 0) { $selection[0].Current.Name } else { '' }
  if ($operatorLabel -match 'OperatorOption|Label\s*=|Value\s*=') { throw "Internal operator object leaked into UI: $operatorLabel" }
  $filterText = Find-ById $desktop 'TextValueBox'
  ($filterText.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue('issue')
  Start-Sleep -Milliseconds 250
  $preview = (Find-ById $desktop 'FilterPreviewCountText').Current.Name
  Invoke-Element (Find-ById $desktop 'FilterApplyButton')
  Start-Sleep -Milliseconds 250
  $process.Refresh(); if ($process.HasExited) { throw 'The process exited after applying a filter.' }

  Invoke-Element (Find-ById $root 'SummaryFilterButton')
  Invoke-Element (Find-ById $desktop 'FilterClearButton')
  Start-Sleep -Milliseconds 250
  $process.Refresh(); if ($process.HasExited) { throw 'The process exited after clearing a filter.' }

  [pscustomobject]@{
    SearchTab = 'Pass'
    SearchShiftTab = 'Pass'
    OperatorLabel = $operatorLabel
    PreviewText = $preview
    FilterApplyAndClose = 'Pass'
    FilterClearAndClose = 'Pass'
  }
}
finally {
  if (-not $process.HasExited) { Stop-Process -Id $process.Id }
  $env:QRB_UI_TEST_INSTANCE_ID = $previousTestInstance
}
