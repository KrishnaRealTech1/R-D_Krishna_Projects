$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\RfidVehicleAccess.App\RfidVehicleAccess.App.csproj"

dotnet restore $project
dotnet run --project $project
