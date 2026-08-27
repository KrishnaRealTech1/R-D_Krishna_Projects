$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $root 'tools'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
$zip = Join-Path $env:TEMP 'gcam-ffmpeg.zip'
$tmp = Join-Path $env:TEMP 'gcam-ffmpeg'
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'Downloading FFmpeg essentials build...'
Invoke-WebRequest -Uri 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $zip
Expand-Archive -Path $zip -DestinationPath $tmp -Force
$ffmpeg = Get-ChildItem $tmp -Recurse -Filter ffmpeg.exe | Select-Object -First 1
$ffprobe = Get-ChildItem $tmp -Recurse -Filter ffprobe.exe | Select-Object -First 1
if (-not $ffmpeg) { throw 'ffmpeg.exe not found after extraction.' }
Copy-Item $ffmpeg.FullName (Join-Path $tools 'ffmpeg.exe') -Force
if ($ffprobe) { Copy-Item $ffprobe.FullName (Join-Path $tools 'ffprobe.exe') -Force }
Write-Host "Installed FFmpeg into $tools"
