namespace ProjectManagementCompiler.Domain;

public static class ControlledEvidenceRules
{
    public static IReadOnlySet<string> SupportedTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SOURCE_RECORD",
        "COMMIT",
        "PULL_REQUEST",
        "TEST_RESULT",
        "REVIEW_RECORD",
        "ARTIFACT",
        "EXTERNAL_RECORD"
    };

    public static IReadOnlySet<string> SupportedResults { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "NOT_RUN",
        "PASS",
        "FAIL",
        "BLOCKED",
        "NOT_APPLICABLE"
    };

    public static bool IsValid(SourceExecutionEvidence? item) =>
        item is not null
        && !string.IsNullOrWhiteSpace(item.EvidenceId)
        && SupportedTypes.Contains(item.Type)
        && !string.IsNullOrWhiteSpace(item.Description)
        && SupportedResults.Contains(item.Result ?? string.Empty)
        && item.RecordedAt is not null
        && !string.IsNullOrWhiteSpace(item.RecordedBy)
        && IsValidCommit(item.Commit)
        && IsSafeRepositoryPath(item.RepositoryPath)
        && IsSafeOptionalExternalUri(item.ExternalUri);

    public static bool IsValidCommit(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || (value.Length is >= 7 and <= 40 && value.All(Uri.IsHexDigit));

    public static bool IsSafeExternalUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSafeRepositoryPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Path.IsPathRooted(value)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("\\", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = value.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0
            && !segments.Any(segment => segment is "." or ".."
                                        || segment.Contains(':', StringComparison.Ordinal)
                                        || segment.Any(char.IsControl))
            && !normalized.Equals(".git", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSafeOptionalExternalUri(string? value) =>
        string.IsNullOrWhiteSpace(value) || IsSafeExternalUri(value);
}
