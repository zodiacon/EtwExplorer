param([string]$Exe = ".\EtwExplorer\bin\Debug\net11.0-windows\EtwExplorer.exe")
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @"
using System; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Text;
public static class Win {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  public static List<IntPtr> Find(uint pid) {
	var r = new List<IntPtr>();
	EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid && IsWindowVisible(h)) r.Add(h); return true; }, IntPtr.Zero);
	return r;
  }
  public static string Title(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
}
"@
$A = [System.Windows.Automation.AutomationElement]
$C = [System.Windows.Automation.ControlType]
$P = [System.Windows.Automation.AutomationElement]
function Cond($type) { New-Object System.Windows.Automation.PropertyCondition($P::ControlTypeProperty, $type) }
function Wait-Windows($procId, $count, $sec = 20) {
  $t = [DateTime]::Now
  while (([DateTime]::Now - $t).TotalSeconds -lt $sec) { $w = [Win]::Find($procId); if ($w.Count -ge $count) { return $w }; Start-Sleep -Milliseconds 300 }
  throw "Timed out waiting for $count window(s)"
}
$proc = Start-Process $Exe -PassThru
try {
  $hw = Wait-Windows $proc.Id 1
  $main = $A::FromHandle($hw[0])
  "Main window: '$([Win]::Title($hw[0]))'"
  $buttons = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::Button))
  $open = $null
  foreach ($b in $buttons) {
	$txt = $b.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::Text))
	if ($txt -and $txt.Current.Name -eq 'Open Provider...') { $open = $b; break }
  }
  if (-not $open) { throw "Open Provider button not found (buttons=$($buttons.Count))" }
  $open.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  $hw = Wait-Windows $proc.Id 2
  $dlgH = $hw | Where-Object { $_ -ne $main.Current.NativeWindowHandle } | Select-Object -First 1
  $dlg = $A::FromHandle($dlgH)
  "Dialog: '$([Win]::Title($dlgH))'"
  $edit = $dlg.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::Edit))
  $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Kernel-Process')
  Start-Sleep -Milliseconds 800
  $rows = $dlg.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::DataItem))
  "Filtered rows: $($rows.Count)"
  $row = $null
  foreach ($r in $rows) { if ($r.Current.Name -match 'Microsoft-Windows-Kernel-Process') { $row = $r; break } }
  if (-not $row) { $row = $rows[0] }
  $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
  $ok = $dlg.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::Button)) | Where-Object { $_.Current.Name -eq 'OK' } | Select-Object -First 1
  $ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  Start-Sleep -Seconds 3
  $tabs = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::TabItem))
  "Tabs: $($tabs.Count) -> $(($tabs | ForEach-Object { $_.Current.Name }) -join ', ')"
  $texts = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::Text)) | ForEach-Object { $_.Current.Name }
  "Status has provider: $([bool]($texts -contains 'Microsoft-Windows-Kernel-Process'))"
  "Title: '$([Win]::Title($main.Current.NativeWindowHandle))'"
  # exercise the Events tab filter box and the Strings tab
  foreach ($t in $tabs) { if ($t.Current.Name -eq 'Events') { $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() } }
  Start-Sleep -Milliseconds 800
  $edits = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::Edit))
  if ($edits.Count -gt 0) {
	$edits[0].GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('processstart')
	Start-Sleep -Milliseconds 800
	$items = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::DataItem))
	"Events filtered by 'processstart': $($items.Count) rows"
  }
  foreach ($t in $tabs) { if ($t.Current.Name -eq 'Strings') { $t.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() } }
  Start-Sleep -Milliseconds 800
  $items = $main.FindAll([System.Windows.Automation.TreeScope]::Descendants, (Cond $C::DataItem))
  "Strings rows: $($items.Count)"
  "Process alive: $(-not $proc.HasExited)"
}
finally {
  if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}

