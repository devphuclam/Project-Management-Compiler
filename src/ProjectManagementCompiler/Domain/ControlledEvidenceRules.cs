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
}
