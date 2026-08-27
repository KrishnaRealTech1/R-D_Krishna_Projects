$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$models = Join-Path $root 'models'
New-Item -ItemType Directory -Path $models -Force | Out-Null

$proto = Join-Path $models 'MobileNetSSD_deploy.prototxt'
$model = Join-Path $models 'MobileNetSSD_deploy.caffemodel'

if (-not (Test-Path $proto)) {
    Write-Host 'Downloading MobileNetSSD prototxt...'
    Invoke-WebRequest -UseBasicParsing `
        -Uri 'https://raw.githubusercontent.com/chuanqi305/MobileNet-SSD/master/deploy.prototxt' `
        -OutFile $proto
}

if (-not (Test-Path $model) -or (Get-Item $model).Length -lt 1000000) {
    Write-Host 'Downloading MobileNetSSD person/vehicle model (~23 MB)...'
    $urls = @(
        'https://github.com/chuanqi305/MobileNet-SSD/raw/master/mobilenet_iter_73000.caffemodel',
        'https://raw.githubusercontent.com/chuanqi305/MobileNet-SSD/master/mobilenet_iter_73000.caffemodel'
    )
    $ok = $false
    foreach ($url in $urls) {
        try {
            Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $model
            if ((Test-Path $model) -and (Get-Item $model).Length -gt 1000000) { $ok = $true; break }
        } catch {
            Write-Warning "Model download failed from $url : $($_.Exception.Message)"
            Remove-Item $model -Force -ErrorAction SilentlyContinue
        }
    }
    if (-not $ok) { throw 'Unable to download MobileNetSSD person/vehicle model.' }
}

Write-Host "AI person/vehicle fallback model ready: $model"
