using System.Text.Json;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed record TripAuthorizationResult(bool Available, bool Approved, decimal? Balance, decimal? DebitAmount, string Reason, string RequestId);

public sealed class OnlineTripAuthorizationService(AppOptions options, MqttPublishService mqtt, AppLogger logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool Enabled => options.Server.Enabled &&
        string.Equals(options.Processing.ProcessingMode, "ONLINE_OFFLINE", StringComparison.OrdinalIgnoreCase);

    public async Task<TripAuthorizationResult> AuthorizeAsync(LaneDirection direction, VehicleRecord vehicle, decimal localBalance, decimal debitAmount, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid().ToString("N");
        if (!Enabled) return new(false, false, null, null, "Online authorization disabled", requestId);
        var requestTopic = options.Server.Mqtt.TripAuthorizationRequestTopic?.Trim() ?? string.Empty;
        var responseTopic = options.Server.Mqtt.TripAuthorizationResponseTopic?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requestTopic) || string.IsNullOrWhiteSpace(responseTopic))
            return new(false, false, null, null, "Online authorization topics are not configured", requestId);

        var payload = JsonSerializer.Serialize(new
        {
            schemaVersion = "2.0",
            requestId,
            siteId = options.Device.SiteId,
            deviceId = options.Device.DeviceId,
            direction = direction == LaneDirection.In ? "IN" : "OUT",
            vehicleNumber = vehicle.VehicleNumber,
            rfidNumber = vehicle.RfidNumber,
            vehicleCategory = vehicle.VehicleCategory,
            categoryDebitAmount = debitAmount,
            contractorId = vehicle.ContractorCode,
            contractorName = vehicle.ContractorName,
            balanceType = string.Equals(options.Processing.OfflineBalanceMode, "CONTRACTOR", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(options.Processing.OfflineBalanceMode, "BOTH", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(vehicle.ContractorCode))
                    ? "CONTRACTOR"
                    : "RFID",
            deviceLocalBalance = localBalance,
            deviceDateTime = DateTimeOffset.Now,
            device = new { options.Device.SiteId, options.Device.DeviceId, machineName = Environment.MachineName }
        }, JsonOptions);

        try
        {
            var responseText = await mqtt.PublishAndWaitForResponseAsync(requestTopic, responseTopic, payload,
                TimeSpan.FromSeconds(Math.Max(2, options.Processing.OnlineAuthorizationTimeoutSeconds)), cancellationToken);
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            var returnedId = GetString(root, "requestId");
            if (!string.Equals(returnedId, requestId, StringComparison.OrdinalIgnoreCase))
                return new(false, false, null, null, "Authorization response requestId mismatch", requestId);
            var status = GetString(root, "status");
            var approved = GetBool(root, "approved") || string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
            var reason = GetString(root, "reason");
            return new(true, approved, GetDecimal(root, "balance"), GetDecimal(root, "debitAmount"), reason, requestId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await logger.ServerAsync($"Trip authorization timeout for {vehicle.VehicleNumber}; offline fallback will be used.", cancellationToken);
            return new(false, false, null, null, "Authorization timeout", requestId);
        }
        catch (Exception ex)
        {
            await logger.ServerAsync($"Trip authorization unavailable for {vehicle.VehicleNumber}: {ex.Message}. Offline fallback will be used.", cancellationToken);
            return new(false, false, null, null, ex.Message, requestId);
        }
    }

    private static string GetString(JsonElement root, string name) => root.TryGetProperty(name, out var v) ? v.ToString() : string.Empty;
    private static bool GetBool(JsonElement root, string name) => root.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b));
    private static decimal? GetDecimal(JsonElement root, string name) => root.TryGetProperty(name, out var v) && decimal.TryParse(v.ToString(), out var d) ? d : null;
}
