$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\RfidVehicleAccess.App\RfidVehicleAccess.App.csproj"
$output = Join-Path $PSScriptRoot "Publish\StandaloneWinX64"
$executable = Join-Path $output "RealTechSTPAutomation.exe"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET 8 SDK is required to build the standalone EXE. Install the .NET 8 SDK and run this script again."
}

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

New-Item -ItemType Directory -Path $output -Force | Out-Null

Write-Host "Restoring NuGet packages..."
dotnet restore $project --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw "Package restore failed."
}

Write-Host "Publishing the self-contained single-file application..."
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    --output $output `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:CopyOutputSymbolsToPublishDirectory=false

if ($LASTEXITCODE -ne 0) {
    throw "Standalone publish failed."
}

if (-not (Test-Path $executable)) {
    throw "Publish completed, but RealTechSTPAutomation.exe was not created."
}

$publishedFiles = @(Get-ChildItem $output -File -Recurse)
if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].FullName -ne $executable) {
    $fileList = ($publishedFiles | ForEach-Object {
        $_.FullName.Substring($output.Length).TrimStart('\')
    }) -join [Environment]::NewLine

    throw @"
The publish output contains files other than the standalone EXE:
$fileList

Do not distribute only the EXE until this publish configuration is reviewed.
"@
}

$sizeMb = [Math]::Round((Get-Item $executable).Length / 1MB, 1)
Write-Host ""
Write-Host "Standalone application created successfully:"
Write-Host $executable
Write-Host "File size: $sizeMb MB"
