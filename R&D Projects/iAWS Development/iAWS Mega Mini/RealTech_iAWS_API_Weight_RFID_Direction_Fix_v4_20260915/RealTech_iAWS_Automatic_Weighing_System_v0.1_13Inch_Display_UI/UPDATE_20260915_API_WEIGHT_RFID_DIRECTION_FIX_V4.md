# RealTech iAWS Update - API, Weight, FTPS Log, RFID Direction Fix v4

Date: 2026-09-15

This update is based on the FTPS certificate mismatch fix v3. Existing UI layout, barrier logic,
camera sequencing, database schema, server configuration screens, and retry timing are preserved.
Only the defects listed below were changed.

## 1. HTTP API application-level response validation

A successful HTTP status code is no longer enough to mark a transaction as synchronized.
After POST completes, the response body is checked for explicit application-level failure values,
including `error`, `success=false`, failure-like `status` values, and the text `invalid data`.

Example response that is now treated as failure:

```json
{"error":"invalid data","hash":"..."}
```

When the API rejects the transaction:
- the transaction is NOT marked synced;
- the normal retry schedule is retained;
- the API response remains visible in SERVER LOG.

The outgoing API JSON is also written to SERVER LOG as `API REQUEST PAYLOAD` so server-side
validation failures can be diagnosed from the same log.

## 2. Zero / below-target weight transaction prevention

The stable-weight capture can no longer complete with zero/reset weight if the vehicle leaves the
weighbridge after RFID lock. The 3-second stable window is restarted if live weight falls below the
configured target, and the weighing attempt is aborted if live weight falls to the reset threshold.

Server synchronization also has a defensive validation that refuses a transaction with:
- invalid lane direction;
- empty RFID;
- weight <= 0 kg;
- weight below its saved target.

This prevents invalid zero-weight payloads from being reported as successful transactions.

## 3. Do not re-upload already uploaded camera files on retry

Camera remote paths are persisted as before. On a later transaction retry, any camera with an
existing remote path is skipped and SERVER LOG now states:

`CAMx UPLOAD SKIPPED | already uploaded | remote=...`

This allows an API-only retry after the four images have already reached the image server.

## 4. FTPS WinSCP reply-log file lock

WinSCP session logging is now read only after the WinSCP session has been disposed/flushed.
The log reader also opens the file with shared-read/write/delete access and a short retry loop.
This removes the misleading successful-upload message:

`Unable to read WinSCP reply log ... because it is being used by another process`

The transfer itself and certificate validation behavior are unchanged from v3.

## 5. IN / OUT RFID direction

RFID lane direction is now resolved from the actual SerialPort instance and configured COM-port
mapping before an EPC is published. The event-handler name is only a fallback. This protects IN
reads from being published as OUT after reconnect/rebind scenarios.

STATUS LOG now includes:

`RFID PORT MAP | IN=COMx | OUT=COMy`

and every accepted raw reader EPC includes its resolved lane and source port:

`RFID READ | lane=IN | port=COMx | EPC=...`

If the physical readers are connected to different COM ports than the Hardware Settings mapping,
correct the IN/OUT COM assignments; software cannot infer the physical lane from an electrically
swapped cable. With the COM mapping correct, a read received from the configured IN SerialPort is
published and processed as `LaneDirection.In`.

## Validation performed

- Modified C# files were checked for balanced braces/parentheses/brackets while ignoring strings
  and comments.
- `appsettings.json` parses as valid JSON.
- The project `.csproj` parses as valid XML.
- Only these source services were changed from v3: `ServerSyncWorker`, `LaneProcessor`,
  `ImageUploadService`, and `HardwareGateway`, plus this update note.
- A Windows .NET SDK is not installed in the current build environment, so the final WPF
  `dotnet build` must be run on the target/development Windows machine.
