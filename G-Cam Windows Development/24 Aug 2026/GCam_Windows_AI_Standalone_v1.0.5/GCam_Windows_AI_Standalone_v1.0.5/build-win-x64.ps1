$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 8 SDK is required on the build PC.' }

    $ffmpegSource = Join-Path $root 'tools\ffmpeg.exe'
    if (-not (Test-Path $ffmpegSource)) { & '.\scripts\Get-Ffmpeg.ps1' }
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) { throw "FFmpeg download script failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $ffmpegSource)) { throw "FFmpeg download completed but file is missing: $ffmpegSource" }

    $cloudflaredSource = Join-Path $root 'tools\cloudflared.exe'
    if (-not (Test-Path $cloudflaredSource)) { & '.\scripts\Get-Cloudflared.ps1' }
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) { throw "cloudflared download script failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path $cloudflaredSource)) { throw "cloudflared download completed but file is missing: $cloudflaredSource" }

    & '.\scripts\Get-AiModels.ps1'
    if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) { throw "AI model download script failed with exit code $LASTEXITCODE." }
    $pvFallback = Join-Path $root 'models\MobileNetSSD_deploy.caffemodel'
    if (-not (Test-Path $pvFallback)) { throw "Person/vehicle AI fallback model is missing: $pvFallback" }

    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

    $publishDir = Join-Path $root 'Publish\win-x64'
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    # FFmpeg, cloudflared and the MobileNetSSD fallback exist before publish so MSBuild can embed them.
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

    $exe = Join-Path $publishDir 'GCam.Windows.exe'
    if (-not (Test-Path $exe)) { throw "Publish completed without expected executable: $exe" }

    $publishTools = Join-Path $publishDir 'tools'
    New-Item -ItemType Directory -Path $publishTools -Force | Out-Null
    Copy-Item $ffmpegSource (Join-Path $publishTools 'ffmpeg.exe') -Force
    Copy-Item $cloudflaredSource (Join-Path $publishTools 'cloudflared.exe') -Force

    $portableZip = Join-Path (Split-Path $publishDir -Parent) 'GCam-Windows-win-x64-v1.0.5.zip'
    if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $portableZip -CompressionLevel Optimal

    Write-Host ''
    Write-Host 'Build complete:' -ForegroundColor Green
    Write-Host $exe
    Write-Host ''
    Write-Host 'Portable deployment ZIP:' -ForegroundColor Green
    Write-Host $portableZip
    Write-Host ''
    Write-Host 'Included runtime fallbacks:'
    Write-Host '  - FFmpeg embedded + external tools\ffmpeg.exe'
    Write-Host '  - cloudflared embedded + external tools\cloudflared.exe'
    Write-Host '  - MobileNetSSD Person/Vehicle model embedded + external models copy'
    Write-Host ''
    Write-Host 'Local web dashboard default: http://127.0.0.1:8080'
    Write-Host 'For permanent Cloudflare access, route your Cloudflare Tunnel hostname to http://localhost:8080.'
} finally { Pop-Location }
