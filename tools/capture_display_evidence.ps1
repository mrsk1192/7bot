param(
    [string]$OutputDir = "C:\AI\7agent2\logs\display_evidence"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Evidence {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [StructLayout(LayoutKind.Sequential)] public struct RECT {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
  }
}
"@

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$bundleDir = Join-Path $OutputDir $timestamp
New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null

$proc = Get-Process 7DaysToDie -ErrorAction Stop | Select-Object -First 1
$rect = New-Object Win32Evidence+RECT
[Win32Evidence]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null

$windowInfo = [pscustomobject]@{
    ProcessId = $proc.Id
    Responding = $proc.Responding
    StartTime = $proc.StartTime
    MainWindowTitle = $proc.MainWindowTitle
    Left = $rect.Left
    Top = $rect.Top
    Width = ($rect.Right - $rect.Left)
    Height = ($rect.Bottom - $rect.Top)
}
$windowInfo | ConvertTo-Json -Depth 4 | Set-Content -Path (Join-Path $bundleDir "window_info.json") -Encoding UTF8

$captureStatus = [ordered]@{
    ok = $false
    note = ""
}
try {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    $desktopPath = Join-Path $bundleDir "desktop.png"
    $bmp.Save($desktopPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bmp.Dispose()
    $captureStatus.ok = $true
    $captureStatus.note = "Desktop capture saved."
}
catch {
    $captureStatus.ok = $false
    $captureStatus.note = $_.Exception.Message
}
$captureStatus | ConvertTo-Json | Set-Content -Path (Join-Path $bundleDir "capture_status.json") -Encoding UTF8

$playerLog = "C:\Users\tetsuomorisaki\AppData\Local\Temp\The Fun Pimps\7 Days To Die\Player.log"
if (Test-Path $playerLog) {
    Get-Content $playerLog -Tail 300 | Set-Content -Path (Join-Path $bundleDir "Player.tail.log") -Encoding UTF8
}

$roamingLogDir = "C:\Users\tetsuomorisaki\AppData\Roaming\7DaysToDie\logs"
if (Test-Path $roamingLogDir) {
    Get-ChildItem $roamingLogDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
        ForEach-Object {
            Copy-Item $_.FullName -Destination (Join-Path $bundleDir $_.Name) -Force
        }
}

$code = @"
import json
import urllib.request

base = 'http://127.0.0.1:18771'
payload = {}
for path in [
    '/api/get_version',
    '/api/get_state',
    '/api/get_player_rotation',
    '/api/get_look_target',
    '/api/get_interaction_context'
]:
    try:
        with urllib.request.urlopen(base + path, timeout=10) as response:
            payload[path] = json.loads(response.read().decode('utf-8'))
    except Exception as exc:
        payload[path] = {'error': str(exc)}

print(json.dumps(payload, ensure_ascii=False, indent=2))
"@

$tempPy = Join-Path $bundleDir "capture_bridge_state.py"
Set-Content -Path $tempPy -Value $code -Encoding UTF8
python $tempPy | Set-Content -Path (Join-Path $bundleDir "bridge_state.json") -Encoding UTF8

Write-Output $bundleDir
