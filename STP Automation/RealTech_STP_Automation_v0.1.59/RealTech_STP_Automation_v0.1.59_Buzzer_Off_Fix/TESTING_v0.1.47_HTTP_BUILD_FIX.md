# RealTech STP Automation v0.1.47 HTTP Build Fix Testing

## Reproduce the original failure

The v0.1.46 WPF markup temporary project could compile `System.Net.Http.Headers` and `System.Net.Http.Json` namespace imports while failing to resolve base HTTP types that relied on implicit global usings. Typical errors were CS0246 for `HttpClient`, `HttpContent` and `HttpResponseMessage`.

## Fix verification

1. Open `src\RfidVehicleAccess.App\Services\VehicleApiImportService.cs`.
2. Confirm the file contains `using System.Net.Http;`.
3. Run `build-standalone-exe.bat` on Windows with the .NET 8 SDK.
4. Confirm `Publish\StandaloneWinX64\RealTechSTPAutomation.exe` is created.
5. Open **Data > Sync Registered Vehicles from API...** and verify the existing API workflow still opens and runs.

## Expected result

The CS0246 errors for the base HTTP types no longer occur.
