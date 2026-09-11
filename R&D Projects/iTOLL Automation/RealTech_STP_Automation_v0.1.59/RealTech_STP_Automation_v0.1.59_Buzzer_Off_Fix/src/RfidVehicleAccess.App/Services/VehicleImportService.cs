using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class VehicleImportService(
    AppOptions options,
    RfidValidator rfidValidator,
    VehicleRepository vehicleRepository,
    ContractorRepository contractorRepository,
    ImportAuditRepository auditRepository,
    AppLogger logger)
{
    public async Task<ImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var importFormat = DetectImportFormat(filePath);
        var rows = importFormat switch
        {
            VehicleImportFormat.Csv => ReadCsv(filePath),
            VehicleImportFormat.Excel => ReadExcel(filePath),
            _ => throw new NotSupportedException(
                "Supported vehicle import formats are CSV, XLSX and XLSM.")
        };

        var result = new ImportResult();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.TotalRows++;

            try
            {
                var rfid = rfidValidator.Normalize(row.RfidNumber);
                var vehicleNumber = NormalizeVehicleNumber(row.VehicleNumber);

                if (string.IsNullOrWhiteSpace(rfid) || string.IsNullOrWhiteSpace(vehicleNumber))
                {
                    result.IncompleteRows++;
                    RecordInvalidRow(
                        result,
                        $"Row {row.RowNumber}: RFID or vehicle number is empty.");
                    continue;
                }

                if (!rfidValidator.MatchesAllowedPrefix(rfid))
                {
                    if (options.Import.EnforceRfidPrefixValidation)
                    {
                        RecordInvalidRow(
                            result,
                            $"Row {row.RowNumber}: RFID {rfid} does not match an allowed prefix.");
                        continue;
                    }

                    result.AcceptedOutsidePrefixRows++;
                }

                if (seen.TryGetValue(rfid, out var existingVehicleInFile))
                {
                    result.DuplicateRows++;

                    if (!AreEquivalentVehicleNumbers(existingVehicleInFile, vehicleNumber))
                    {
                        result.ConflictingRows++;
                        RecordInvalidRow(
                            result,
                            $"Row {row.RowNumber}: RFID {rfid} was already assigned to " +
                            $"{existingVehicleInFile}; {vehicleNumber} was skipped. " +
                            "The first valid row in the file is retained.");
                    }
                    else
                    {
                        result.Skipped++;
                    }

                    continue;
                }

                seen[rfid] = vehicleNumber;

                var existing = await vehicleRepository.GetByRfidAsync(
                    rfid,
                    cancellationToken);
                if (existing is not null &&
                    !AreEquivalentVehicleNumbers(existing.VehicleNumber, vehicleNumber))
                {
                    result.Failed++;
                    result.Errors.Add(
                        $"Row {row.RowNumber}: RFID {rfid} is already assigned to vehicle " +
                        $"{existing.VehicleNumber}; {vehicleNumber} was rejected.");
                    continue;
                }

                var record = CreateVehicleRecord(row, rfid, vehicleNumber, existing);
                if (!string.IsNullOrWhiteSpace(record.ContractorCode))
                {
                    var contractorBalance = ParseDecimal(row.ContractorBalance) ?? 0m;
                    await contractorRepository.UpsertAsync(new ContractorRecord
                    {
                        ContractorCode = record.ContractorCode,
                        ContractorName = string.IsNullOrWhiteSpace(record.ContractorName) ? record.ContractorCode : record.ContractorName,
                        Balance = contractorBalance,
                        IsActive = ParseBoolean(row.ContractorActive, true),
                        UpdatedAt = DateTimeOffset.Now,
                        LastServerSyncAt = DateTimeOffset.Now
                    }, cancellationToken);
                }
                if (record.CategoryDebitAmount.HasValue)
                {
                    await contractorRepository.UpsertCategoryPriceAsync(record.VehicleCategory, record.CategoryDebitAmount.Value, cancellationToken);
                }
                var operation = await vehicleRepository.UpsertAsync(
                    record,
                    existing,
                    options.Import.UpdateExistingRecords,
                    cancellationToken);

                if (!string.IsNullOrWhiteSpace(operation.Error))
                {
                    result.Failed++;
                    result.Errors.Add($"Row {row.RowNumber}: {operation.Error}");
                }
                else if (operation.Inserted)
                {
                    result.Inserted++;
                }
                else if (operation.Updated)
                {
                    result.Updated++;
                }
                else
                {
                    result.Skipped++;
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Row {row.RowNumber}: {ex.Message}");
            }
        }

        await auditRepository.AddAsync(Path.GetFileName(filePath), result, cancellationToken);
        await logger.StatusAsync(
            $"Vehicle import completed from {Path.GetFileName(filePath)}. {result.ToSummary()}",
            cancellationToken);
        return result;
    }


    private VehicleRecord CreateVehicleRecord(
        VehicleImportRow row,
        string rfid,
        string vehicleNumber,
        VehicleRecord? existing)
    {
        var isExisting = existing is not null;
        var sourceSite = string.IsNullOrWhiteSpace(row.SourceSite)
            ? existing?.SourceSite ?? string.Empty
            : row.SourceSite.Trim();
        var vehicleCategory = string.IsNullOrWhiteSpace(row.VehicleCategory)
            ? existing?.VehicleCategory ?? options.Payment.DefaultCategory
            : NormalizeVehicleCategory(row.VehicleCategory);
        var accessType = string.IsNullOrWhiteSpace(row.AccessType) && isExisting
            ? existing!.AccessType
            : ParseAccessType(row.AccessType);
        var balance = string.IsNullOrWhiteSpace(row.Balance) && isExisting
            ? existing!.Balance
            : ParseDecimal(row.Balance) ?? options.Import.DefaultOpeningBalance;
        var emptyWeight = string.IsNullOrWhiteSpace(row.EmptyWeight) && isExisting
            ? existing!.EmptyWeight
            : ParseDecimal(row.EmptyWeight);
        var isActive = string.IsNullOrWhiteSpace(row.Active) && isExisting
            ? existing!.IsActive
            : ParseBoolean(row.Active, true);

        return new VehicleRecord
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            RfidNumber = rfid,
            VehicleNumber = vehicleNumber,
            SourceSite = sourceSite,
            VehicleCategory = vehicleCategory,
            AccessType = accessType,
            Balance = balance,
            EmptyWeight = emptyWeight,
            IsActive = isActive,
            CreatedAt = existing?.CreatedAt ?? DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
            LastServerSyncAt = existing?.LastServerSyncAt,
            ContractorCode = string.IsNullOrWhiteSpace(row.ContractorCode) ? existing?.ContractorCode ?? string.Empty : row.ContractorCode.Trim(),
            ContractorName = string.IsNullOrWhiteSpace(row.ContractorName) ? existing?.ContractorName ?? string.Empty : row.ContractorName.Trim(),
            CategoryDebitAmount = string.IsNullOrWhiteSpace(row.DebitAmount) ? existing?.CategoryDebitAmount : ParseDecimal(row.DebitAmount)
        };
    }

    private IEnumerable<VehicleImportRow> ReadCsv(string filePath)
    {
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, configuration);
        if (!csv.Read())
        {
            yield break;
        }

        csv.ReadHeader();
        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            var row = new VehicleImportRow
            {
                RowNumber = rowNumber,
                RfidNumber = GetCsvValue(csv, "rf_id", "rfid", "rfid_number", "tag_number"),
                VehicleNumber = GetCsvValue(csv, "vehicle_no", "vehicle_number", "vehicleno"),
                SourceSite = GetCsvValue(csv, "username", "site", "source_site"),
                VehicleCategory = GetCsvValue(
                    csv,
                    "vehicle_category", "category", "vehicle_capacity",
                    "capacity", "capacity_liters", "capacity_litres"),
                AccessType = GetCsvValue(csv, "rfid_type", "access_type", "type"),
                Balance = GetCsvValue(csv, "balance", "wallet_balance", "amount"),
                EmptyWeight = GetCsvValue(csv, "empty_weight", "tare_weight"),
                Active = GetCsvValue(csv, "rf_status", "active", "is_active", "status"),
                ContractorCode = GetCsvValue(csv, "contractor_id", "contractor_code", "user_id", "user_code"),
                ContractorName = GetCsvValue(csv, "contractor_name", "user_name", "contractor"),
                ContractorBalance = GetCsvValue(csv, "contractor_balance", "user_balance", "account_balance"),
                ContractorActive = GetCsvValue(csv, "contractor_active", "user_active"),
                DebitAmount = GetCsvValue(csv, "debit_amount", "category_debit_amount", "trip_amount", "price")
            };

            if (!row.IsCompletelyEmpty())
            {
                yield return row;
            }
        }
    }

    private static IEnumerable<VehicleImportRow> ReadExcel(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();
        var firstRow = worksheet.FirstRowUsed();
        var lastRow = worksheet.LastRowUsed();
        if (firstRow is null || lastRow is null)
        {
            yield break;
        }

        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in firstRow.CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (!string.IsNullOrWhiteSpace(header))
            {
                headers[header] = cell.Address.ColumnNumber;
            }
        }

        for (var rowNumber = firstRow.RowNumber() + 1;
             rowNumber <= lastRow.RowNumber();
             rowNumber++)
        {
            var worksheetRow = worksheet.Row(rowNumber);
            var row = new VehicleImportRow
            {
                RowNumber = rowNumber,
                RfidNumber = GetExcelValue(worksheetRow, headers,
                    "rf_id", "rfid", "rfid_number", "tag_number"),
                VehicleNumber = GetExcelValue(worksheetRow, headers,
                    "vehicle_no", "vehicle_number", "vehicleno"),
                SourceSite = GetExcelValue(worksheetRow, headers,
                    "username", "site", "source_site"),
                VehicleCategory = GetExcelValue(worksheetRow, headers,
                    "vehicle_category", "category", "vehicle_capacity",
                    "capacity", "capacity_liters", "capacity_litres"),
                AccessType = GetExcelValue(worksheetRow, headers,
                    "rfid_type", "access_type", "type"),
                Balance = GetExcelValue(worksheetRow, headers,
                    "balance", "wallet_balance", "amount"),
                EmptyWeight = GetExcelValue(worksheetRow, headers,
                    "empty_weight", "tare_weight"),
                Active = GetExcelValue(worksheetRow, headers,
                    "rf_status", "active", "is_active", "status"),
                ContractorCode = GetExcelValue(worksheetRow, headers, "contractor_id", "contractor_code", "user_id", "user_code"),
                ContractorName = GetExcelValue(worksheetRow, headers, "contractor_name", "user_name", "contractor"),
                ContractorBalance = GetExcelValue(worksheetRow, headers, "contractor_balance", "user_balance", "account_balance"),
                ContractorActive = GetExcelValue(worksheetRow, headers, "contractor_active", "user_active"),
                DebitAmount = GetExcelValue(worksheetRow, headers, "debit_amount", "category_debit_amount", "trip_amount", "price")
            };

            if (!row.IsCompletelyEmpty())
            {
                yield return row;
            }
        }
    }

    private static VehicleImportFormat DetectImportFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        // XLSX/XLSM files are ZIP packages and start with a PK signature. Some operator files
        // are accidentally saved as Excel workbooks and then renamed with a .csv extension.
        // Detecting the actual file content keeps those files importable without weakening
        // normal plain-text CSV processing.
        if (IsExcelPackage(filePath))
        {
            return VehicleImportFormat.Excel;
        }

        return extension switch
        {
            ".csv" => VehicleImportFormat.Csv,
            ".xlsx" or ".xlsm" => VehicleImportFormat.Excel,
            _ => throw new NotSupportedException(
                "Supported vehicle import formats are CSV, XLSX and XLSM.")
        };
    }

    private static bool IsExcelPackage(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);

        Span<byte> signature = stackalloc byte[4];
        if (stream.Read(signature) < signature.Length)
        {
            return false;
        }

        return signature[0] == 0x50 &&
               signature[1] == 0x4B &&
               ((signature[2] == 0x03 && signature[3] == 0x04) ||
                (signature[2] == 0x05 && signature[3] == 0x06) ||
                (signature[2] == 0x07 && signature[3] == 0x08));
    }

    private void RecordInvalidRow(ImportResult result, string message)
    {
        if (options.Import.SkipInvalidRows)
        {
            result.Skipped++;
            result.Warnings.Add(message);
            return;
        }

        result.Failed++;
        result.Errors.Add(message);
    }

    private static string GetCsvValue(CsvReader csv, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (csv.HeaderRecord?.Any(header =>
                    NormalizeHeader(header).Equals(alias, StringComparison.OrdinalIgnoreCase)) != true)
            {
                continue;
            }

            var actualHeader = csv.HeaderRecord.First(header =>
                NormalizeHeader(header).Equals(alias, StringComparison.OrdinalIgnoreCase));
            return csv.GetField(actualHeader)?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string GetExcelValue(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (headers.TryGetValue(alias, out var columnNumber))
            {
                return row.Cell(columnNumber).GetFormattedString().Trim();
            }
        }

        return string.Empty;
    }

    private RfidAccessType ParseAccessType(string value)
    {
        if (Enum.TryParse<RfidAccessType>(value, true, out var parsed))
        {
            return parsed;
        }

        return Enum.TryParse<RfidAccessType>(
            options.Import.DefaultAccessType,
            true,
            out var configured)
            ? configured
            : RfidAccessType.Free;
    }

    private static bool ParseBoolean(string value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "y" or "active" => true,
            "0" or "false" or "no" or "n" or "inactive" => false,
            _ => defaultValue
        };
    }

    private static decimal? ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var invariant)
            ? invariant
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var local)
                ? local
                : null;
    }


    private string NormalizeVehicleCategory(string value)
    {
        var category = string.Join(' ', value.Trim().Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(category)
            ? options.Payment.DefaultCategory
            : category;
    }

    private static string NormalizeVehicleNumber(string value) =>
        string.Join(' ', value.Trim().Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static bool AreEquivalentVehicleNumbers(string left, string right) =>
        GetVehicleNumberComparisonKey(left).Equals(
            GetVehicleNumberComparisonKey(right),
            StringComparison.OrdinalIgnoreCase);

    private static string GetVehicleNumberComparisonKey(string value)
    {
        var normalized = NormalizeVehicleNumber(value);
        var alphanumeric = new string(normalized.Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(alphanumeric) ? normalized : alphanumeric;
    }

    private static string NormalizeHeader(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

    private enum VehicleImportFormat
    {
        Csv,
        Excel
    }

    private sealed class VehicleImportRow
    {
        public int RowNumber { get; init; }
        public string RfidNumber { get; init; } = string.Empty;
        public string VehicleNumber { get; init; } = string.Empty;
        public string SourceSite { get; init; } = string.Empty;
        public string VehicleCategory { get; init; } = string.Empty;
        public string AccessType { get; init; } = string.Empty;
        public string Balance { get; init; } = string.Empty;
        public string EmptyWeight { get; init; } = string.Empty;
        public string Active { get; init; } = string.Empty;
        public string ContractorCode { get; init; } = string.Empty;
        public string ContractorName { get; init; } = string.Empty;
        public string ContractorBalance { get; init; } = string.Empty;
        public string ContractorActive { get; init; } = string.Empty;
        public string DebitAmount { get; init; } = string.Empty;

        public bool IsCompletelyEmpty() =>
            string.IsNullOrWhiteSpace(RfidNumber) &&
            string.IsNullOrWhiteSpace(VehicleNumber) &&
            string.IsNullOrWhiteSpace(SourceSite) &&
            string.IsNullOrWhiteSpace(VehicleCategory) &&
            string.IsNullOrWhiteSpace(AccessType) &&
            string.IsNullOrWhiteSpace(Balance) &&
            string.IsNullOrWhiteSpace(EmptyWeight) &&
            string.IsNullOrWhiteSpace(Active) &&
            string.IsNullOrWhiteSpace(ContractorCode) &&
            string.IsNullOrWhiteSpace(ContractorName) &&
            string.IsNullOrWhiteSpace(ContractorBalance) &&
            string.IsNullOrWhiteSpace(DebitAmount);
    }
}
