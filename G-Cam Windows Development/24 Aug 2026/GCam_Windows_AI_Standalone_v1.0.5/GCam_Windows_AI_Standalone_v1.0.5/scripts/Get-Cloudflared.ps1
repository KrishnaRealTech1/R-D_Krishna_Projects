$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$tools = Join-Path $root 'tools'
New-Item -ItemType Directory -Path $tools -Force | Out-Null
$dest = Join-Path $tools 'cloudflared.exe'

if ((Test-Path $dest) -and (Get-Item $dest).Length -gt 1000000) {
    Write-Host "cloudflared already ready: $dest"
    exit 0
}

Write-Host 'Downloading latest Cloudflare cloudflared for Windows x64...'
$uri = 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe'
Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile $dest
if (-not (Test-Path $dest) -or (Get-Item $dest).Length -lt 1000000) {
    throw 'cloudflared download failed or produced an invalid file.'
}
Write-Host "cloudflared ready: $dest"
