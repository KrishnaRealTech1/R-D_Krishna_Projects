$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\RfidVehicleAccess.App\iAWS.App.csproj"

dotnet restore $project
dotnet run --project $project
