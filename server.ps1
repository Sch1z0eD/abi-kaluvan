# Локальний сервер ABI Kaluvan (без встановлення нічого): роздає сайт і передає йому звук лише гри.
# Сам сервер — у helper.cs, тут лише компіляція й запуск.
param([string]$Exe = 'UAGame', [int]$Port = 8777, [switch]$NoUpdate)
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$port = $Port
$url  = "http://localhost:$port/"

try {
  $probe = [System.Net.Sockets.TcpClient]::new('127.0.0.1', $port)
  $probe.Close()
  Write-Host "ABI Kaluvan is already running, opening the page." -ForegroundColor Yellow
  Start-Process $url
  exit 0
} catch { }

# Новий реліз на GitHub — оновлюємось і запускаємо вже оновлений server.ps1
if (-not $NoUpdate -and (& (Join-Path $PSScriptRoot 'update.ps1') -Root $PSScriptRoot)) {
  & (Join-Path $PSScriptRoot 'server.ps1') -Exe $Exe -Port $Port -NoUpdate
  exit
}

# Спільна база покупок: злити свою з GitHub, свої нові — у спільну (див. sync.ps1)
& (Join-Path $PSScriptRoot 'sync.ps1') -Root $PSScriptRoot

# Ярлик «ABI Kaluvan» з іконкою (у .bat іконки бути не може): у папці програми — завжди актуальний
# (папку могли перенести), на робочому столі — лише один раз, щоб не повертати видалений
try {
  $sh = New-Object -ComObject WScript.Shell
  $bat = Join-Path $PSScriptRoot 'start.bat'
  $make = {
    param($path)
    $l = $sh.CreateShortcut($path)
    $l.TargetPath = $bat; $l.WorkingDirectory = $PSScriptRoot
    $l.IconLocation = (Join-Path $PSScriptRoot 'icon.ico') + ',0'
    $l.Description = 'ABI Kaluvan: Field Merchant cargo helper'
    $l.Save()
  }
  $local = Join-Path $PSScriptRoot 'ABI Kaluvan.lnk'
  if (-not (Test-Path $local) -or $sh.CreateShortcut($local).TargetPath -ne $bat) { & $make $local }
  $marker = Join-Path $PSScriptRoot '.shortcut-done'
  if (-not (Test-Path $marker)) {
    & $make (Join-Path ([Environment]::GetFolderPath('Desktop')) 'ABI Kaluvan.lnk')
    Set-Content $marker '' -Encoding ASCII
    Write-Host "Created an 'ABI Kaluvan' shortcut on your desktop. Use it to start the app next time." -ForegroundColor Cyan
  }
} catch { }

try {
  Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'helper.cs'), [Text.Encoding]::UTF8)) -ReferencedAssemblies System.Drawing -ErrorAction Stop
} catch {
  Write-Host "Could not build helper.cs: $($_.Exception.Message)" -ForegroundColor Red
  Read-Host 'Press Enter to exit'
  exit 1
}

Write-Host "ABI Kaluvan is running: $url" -ForegroundColor Green
Write-Host "Capturing sound and picture from the game only ($Exe.exe). Close this window to stop."
Start-Process $url
[AbiEar.Server]::Run($PSScriptRoot, $port, $Exe)
