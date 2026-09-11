# v0.1.38 Implementation Summary

This release updates only the lane logging and background synchronization core. RFID validation, balance rules, camera selection, vehicle matching, and barrier behavior remain functionally unchanged.

## Updated components

- `Services/AppLogger.cs`: active IN/OUT elapsed process timer correlation.
- `Services/LaneProcessor.cs`: detailed local process stage logging and final total time.
- `Services/SftpImageUploadService.cs`: reusable connection, one-time directory preparation, total timeout, and short outage cooldown.
- `Services/ServerSyncWorker.cs`: per-stage logs, retry scheduling, queue continuation, and image reuse.
- `Models/TripRecord.cs`: persisted upload/retry metadata.
- `Data/TripRepository.cs`: eligible-retry queries and upload/failure/success updates.
- `Data/DatabaseInitializer.cs`: backward-compatible SQLite migration.
- `Services/AppOptions.cs`, `ServerPanelWindow.xaml.cs`, and `appsettings.json`: retry and total-timeout settings.
- `Models/AppLogEntry.cs`: millisecond timestamps.
- `ViewModels/MainViewModel.cs`: increases visible log retention from 500 to 2,000 lines per channel.
