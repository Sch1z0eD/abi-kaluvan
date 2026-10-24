# Апдейтер ABI Kaluvan: порівнює version.json з останнім релізом на GitHub і за згодою ставить новий поверх.
# Повертає $true, якщо файли оновлено (тоді server.ps1 перезапускає себе вже з новими файлами).
param([string]$Root = $PSScriptRoot)
$repo = 'neverh8me/abi-kaluvan'

try {
  [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
  $local = (Get-Content (Join-Path $Root 'version.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
  $rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest" -TimeoutSec 5 -UserAgent 'abi-kaluvan-updater'
  $remote = $rel.tag_name -replace '^v', ''
  if ([version]$remote -le [version]$local) { Write-Host "ABI Kaluvan v$local is up to date." -ForegroundColor DarkGray; return $false }
} catch {
  # немає інтернету чи GitHub недоступний — просто працюємо далі зі своєю версією
  Write-Host 'Could not check for updates (offline?), starting the current version.' -ForegroundColor DarkYellow
  return $false
}

Write-Host ''
Write-Host "A new version of ABI Kaluvan is out: v$remote (you have v$local)." -ForegroundColor Cyan
Write-Host "What's new: $($rel.html_url)"
$answer = Read-Host 'Update now? [Y/n]'
if ($answer -match '^(n|no|н|ні|нет)$') { return $false }

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('abi-kaluvan-update-' + [guid]::NewGuid().ToString('N'))
try {
  New-Item -ItemType Directory -Path $tmp | Out-Null
  $asset = $rel.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
  $zipUrl = if ($asset) { $asset.browser_download_url } else { $rel.zipball_url }
  $zip = Join-Path $tmp 'update.zip'
  Write-Host 'Downloading...'
  Invoke-WebRequest $zipUrl -OutFile $zip -UseBasicParsing -UserAgent 'abi-kaluvan-updater'
  Expand-Archive $zip -DestinationPath (Join-Path $tmp 'x') -Force
  # в архіві файли лежать у підпапці — шукаємо ту, де є start.bat
  $src = Get-ChildItem (Join-Path $tmp 'x') -Recurse -Filter 'start.bat' | Select-Object -First 1
  if (-not $src) { throw 'start.bat is missing in the archive' }
  Copy-Item (Join-Path $src.DirectoryName '*') -Destination $Root -Recurse -Force
  Write-Host "Updated to v$remote." -ForegroundColor Green
  return $true
} catch {
  Write-Host "Update failed: $($_.Exception.Message). Running the current version." -ForegroundColor Red
  return $false
} finally {
  Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
