namespace RfidVehicleAccess.Models;

public sealed class ImportResult
{
    public int TotalRows { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int DuplicateRows { get; set; }
    public int IncompleteRows { get; set; }
    public int ConflictingRows { get; set; }
    public int AcceptedOutsidePrefixRows { get; set; }
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];

    public string ToSummary()
    {
        return $"Rows: {TotalRows}, Inserted: {Inserted}, Updated: {Updated}, " +
               $"Skipped: {Skipped}, Failed: {Failed}";
    }

    public string ToDisplayText(int maxIssueLines = 10)
    {
        var sections = new List<string> { ToSummary() };
        var notes = new List<string>();

        if (AcceptedOutsidePrefixRows > 0)
        {
            notes.Add(
                $"{AcceptedOutsidePrefixRows} row(s) contained an RFID outside the configured " +
                "runtime prefix and were allowed by the import settings.");
        }

        if (DuplicateRows > 0)
        {
            notes.Add(
                $"{DuplicateRows} repeated RFID row(s) were not imported again; " +
                "the first valid row for each RFID was kept.");
        }

        if (IncompleteRows > 0)
        {
            notes.Add($"{IncompleteRows} incomplete row(s) were not imported.");
        }

        if (ConflictingRows > 0)
        {
            notes.Add(
                $"{ConflictingRows} repeated RFID row(s) had a different vehicle number " +
                "and were not imported pending review.");
        }

        if (notes.Count > 0)
        {
            sections.Add("Import notes:" + Environment.NewLine +
                         string.Join(Environment.NewLine, notes.Select(note => $"- {note}")));
        }

        if (Errors.Count > 0)
        {
            sections.Add(FormatIssues("Errors", Errors, maxIssueLines));
        }
        else if (Warnings.Count > 0)
        {
            sections.Add(FormatIssues("Review notes", Warnings, maxIssueLines));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }

    private static string FormatIssues(
        string heading,
        IReadOnlyList<string> issues,
        int maxIssueLines)
    {
        var visibleCount = Math.Max(0, Math.Min(maxIssueLines, issues.Count));
        var lines = issues.Take(visibleCount).ToList();

        if (issues.Count > visibleCount)
        {
            lines.Add($"...and {issues.Count - visibleCount} more {heading.ToLowerInvariant()}.");
        }

        return heading + ":" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}
