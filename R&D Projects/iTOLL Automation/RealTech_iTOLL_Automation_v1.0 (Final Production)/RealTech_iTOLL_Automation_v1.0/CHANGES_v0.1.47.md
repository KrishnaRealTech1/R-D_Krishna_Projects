# RealTech iTOLL Automation v0.1.47 Changes

## Windows publish build fix

- Added an explicit `using System.Net.Http;` directive to `VehicleApiImportService.cs`.
- Fixes WPF temporary-project compiler errors for `HttpClient`, `HttpContent`, `HttpResponseMessage`, `HttpRequestMessage`, `HttpMethod` and `HttpCompletionOption`.
- No NuGet package is required because `System.Net.Http` is part of the .NET 8 shared framework.
- Vehicle API behavior and configuration remain unchanged from v0.1.46.
- Updated application, assembly and file versions to `0.1.47`.
