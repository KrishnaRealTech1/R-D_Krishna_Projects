namespace RfidVehicleAccess.Services;

public sealed class RfidValidator(AppOptions options)
{
    public string Normalize(string? rawRfid)
    {
        if (string.IsNullOrWhiteSpace(rawRfid))
        {
            return string.Empty;
        }

        return string.Concat(rawRfid.Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();
    }

    public bool MatchesAllowedPrefix(string rawRfid)
    {
        var rfid = Normalize(rawRfid);
        if (string.IsNullOrWhiteSpace(rfid))
        {
            return false;
        }

        var comparison = options.Processing.CaseSensitivePrefixes
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return options.Processing.AllowedRfidPrefixes
            .Where(prefix => !string.IsNullOrWhiteSpace(prefix))
            .Any(prefix => rfid.StartsWith(prefix.Trim(), comparison));
    }

    public bool IsAllowed(string rawRfid)
    {
        var rfid = Normalize(rawRfid);
        if (string.IsNullOrWhiteSpace(rfid))
        {
            return false;
        }

        return !options.Processing.RfidPrefixValidationEnabled || MatchesAllowedPrefix(rfid);
    }
}
