namespace JobPlatform.SharedKernel.Application.Concurrency;

/// <summary>
/// Strong entity tags derived from an aggregate's RowVersion (foundation section 11: ETag/If-Match on updates).
/// A missing If-Match means "no precondition"; "*" matches any existing resource.
/// </summary>
public static class ETag
{
    public static string From(byte[] rowVersion) => "\"" + Convert.ToBase64String(rowVersion) + "\"";

    /// <summary>True when the precondition holds: no header, "*", or one of the listed tags equals the current one.</summary>
    public static bool Matches(string? ifMatch, byte[] currentRowVersion)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return true;
        }

        var current = From(currentRowVersion);
        foreach (var candidate in ifMatch.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (candidate == "*" || string.Equals(candidate, current, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
