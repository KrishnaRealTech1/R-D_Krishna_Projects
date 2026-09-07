using System.Globalization;
using System.Text.RegularExpressions;

namespace RfidVehicleAccess.Services;

public sealed record PaymentPriceResolution(
    string RequestedCategory,
    string AppliedCategory,
    decimal Price,
    bool UsedDefaultCategory);

public sealed class PaymentPriceResolver(AppOptions options)
{
    public PaymentPriceResolution Resolve(string? vehicleCategory)
    {
        var requestedCategory = vehicleCategory?.Trim() ?? string.Empty;
        var payment = options.Payment;
        var categories = payment?.Categories?
            .Where(category =>
                !string.IsNullOrWhiteSpace(category.Name) &&
                category.Price >= 0m)
            .ToList() ?? [];

        if (categories.Count == 0)
        {
            return new PaymentPriceResolution(
                requestedCategory,
                string.IsNullOrWhiteSpace(requestedCategory) ? "Default" : requestedCategory,
                Math.Max(0m, options.Processing.EntryFee),
                true);
        }

        var matchedCategory = FindCategory(categories, requestedCategory);
        if (matchedCategory is not null)
        {
            return new PaymentPriceResolution(
                requestedCategory,
                matchedCategory.Name.Trim(),
                matchedCategory.Price,
                false);
        }

        var defaultCategory = categories.FirstOrDefault(category =>
                                  string.Equals(
                                      category.Name.Trim(),
                                      payment.DefaultCategory?.Trim(),
                                      StringComparison.OrdinalIgnoreCase))
                              ?? categories[0];

        return new PaymentPriceResolution(
            requestedCategory,
            defaultCategory.Name.Trim(),
            defaultCategory.Price,
            true);
    }

    private static VehiclePaymentCategoryOptions? FindCategory(
        IReadOnlyList<VehiclePaymentCategoryOptions> categories,
        string requestedCategory)
    {
        if (string.IsNullOrWhiteSpace(requestedCategory))
        {
            return null;
        }

        var exactMatch = categories.FirstOrDefault(category =>
            string.Equals(
                category.Name.Trim(),
                requestedCategory,
                StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var requestedKey = CreateCategoryKey(requestedCategory);
        var normalizedMatch = categories.FirstOrDefault(category =>
            string.Equals(
                CreateCategoryKey(category.Name),
                requestedKey,
                StringComparison.OrdinalIgnoreCase));
        if (normalizedMatch is not null)
        {
            return normalizedMatch;
        }

        var requestedCapacity = ExtractCapacityDigits(requestedCategory);
        if (string.IsNullOrWhiteSpace(requestedCapacity))
        {
            return null;
        }

        var capacityMatches = categories
            .Where(category => string.Equals(
                ExtractCapacityDigits(category.Name),
                requestedCapacity,
                StringComparison.Ordinal))
            .Take(2)
            .ToList();

        return capacityMatches.Count == 1 ? capacityMatches[0] : null;
    }

    private static string CreateCategoryKey(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray());

    private static string ExtractCapacityDigits(string value)
    {
        var match = Regex.Match(
            value,
            @"[-+]?\d[\d,]*(?:\.\d+)?",
            RegexOptions.CultureInvariant);
        if (match.Success &&
            decimal.TryParse(
                match.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var capacity))
        {
            return capacity.ToString("0.############################", CultureInfo.InvariantCulture);
        }

        var digits = new string(value.Where(char.IsDigit).ToArray()).TrimStart('0');
        return string.IsNullOrWhiteSpace(digits) && value.Any(char.IsDigit)
            ? "0"
            : digits;
    }
}
