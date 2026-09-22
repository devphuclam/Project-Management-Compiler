using System.Text.RegularExpressions;

namespace ProjectManagementCompiler.Management;

public static partial class ReaderFacingTextPolicy
{
    public const string MissingEvidenceLabel = "Chưa ghi nhận";
    public const string MissingOwnerLabel = "Chưa phân công";

    public static string CleanName(string? sourceText, string? exactId = null)
    {
        var cleaned = sourceText?.Trim() ?? string.Empty;
        var id = exactId?.Trim();

        if (cleaned.Length == 0)
        {
            return MissingEvidenceLabel;
        }

        cleaned = StripMarkdownDecoration(cleaned);

        var changed = true;
        while (changed && cleaned.Length > 0)
        {
            changed = false;

            while (TryStripLeadingBracketIdentity(cleaned, id, out var withoutBracket))
            {
                cleaned = withoutBracket;
                changed = true;
            }

            if (TryStripLeadingExactId(cleaned, id, out var withoutLeadingId))
            {
                cleaned = withoutLeadingId;
                changed = true;
            }

            var withoutKind = KindPrefixRegex().Replace(cleaned, string.Empty, 1).Trim();
            if (!string.Equals(withoutKind, cleaned, StringComparison.Ordinal))
            {
                cleaned = withoutKind;
                changed = true;
            }

            var withoutArrow = cleaned.TrimStart(' ', '\t', '→', '›', '>');
            if (!string.Equals(withoutArrow, cleaned, StringComparison.Ordinal))
            {
                cleaned = withoutArrow;
                changed = true;
            }

            var withoutMarkdown = StripMarkdownDecoration(cleaned);
            if (!string.Equals(withoutMarkdown, cleaned, StringComparison.Ordinal))
            {
                cleaned = withoutMarkdown;
                changed = true;
            }
        }

        while (TryStripTrailingExactId(cleaned, id, out var withoutTrailingId))
        {
            cleaned = withoutTrailingId;
        }

        cleaned = WhitespaceRegex().Replace(cleaned, " ").Trim();
        return cleaned.Length == 0 ? MissingEvidenceLabel : cleaned;
    }

    public static string OwnerOrMissing(string? owner) =>
        string.IsNullOrWhiteSpace(owner) ? MissingOwnerLabel : owner.Trim();

    private static bool TryStripLeadingBracketIdentity(
        string value,
        string? exactId,
        out string result)
    {
        result = value;
        if (value.Length == 0 || value[0] != '[')
        {
            return false;
        }

        var closing = value.IndexOf(']');
        if (closing <= 1)
        {
            return false;
        }

        var token = value[1..closing].Trim();
        if (!LooksLikeIdentityPrefix(token, exactId))
        {
            return false;
        }

        result = value[(closing + 1)..].Trim();
        return true;
    }

    private static bool TryStripLeadingExactId(
        string value,
        string? exactId,
        out string result)
    {
        result = value;
        if (string.IsNullOrWhiteSpace(exactId)
            || !value.StartsWith(exactId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = value[exactId.Length..];
        if (remainder.Length == 0)
        {
            result = string.Empty;
            return true;
        }

        if (!char.IsWhiteSpace(remainder[0]) && !IsSeparator(remainder[0]))
        {
            return false;
        }

        result = remainder.TrimStart(' ', '\t', ':', '-', '–', '—', '|', '/', '→', '›', '>');
        return true;
    }

    private static bool TryStripTrailingExactId(
        string value,
        string? exactId,
        out string result)
    {
        result = value;
        if (string.IsNullOrWhiteSpace(exactId)
            || !value.EndsWith(exactId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefixWithBoundary = value[..^exactId.Length];
        if (prefixWithBoundary.Length == 0)
        {
            result = string.Empty;
            return true;
        }

        if (!char.IsWhiteSpace(prefixWithBoundary[^1]) && !IsSeparator(prefixWithBoundary[^1]))
        {
            return false;
        }

        var prefix = prefixWithBoundary.TrimEnd();
        result = prefix.TrimEnd(' ', '\t', ':', '-', '–', '—', '|', '/', '→', '›', '>');
        return true;
    }

    private static bool LooksLikeIdentityPrefix(string value, string? exactId)
    {
        if (!string.IsNullOrWhiteSpace(exactId)
            && string.Equals(value, exactId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IdentityPrefixRegex().IsMatch(value);
    }

    private static string StripMarkdownDecoration(string value)
    {
        var cleaned = value.Trim();
        var previous = string.Empty;
        while (!string.Equals(previous, cleaned, StringComparison.Ordinal))
        {
            previous = cleaned;
            cleaned = TrimPairedMarker(cleaned, "`");
            cleaned = TrimPairedMarker(cleaned, "**");
            cleaned = TrimPairedMarker(cleaned, "__");
            cleaned = TrimPairedMarker(cleaned, "~~");
        }

        return cleaned
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string TrimPairedMarker(string value, string marker) =>
        value.Length >= marker.Length * 2
        && value.StartsWith(marker, StringComparison.Ordinal)
        && value.EndsWith(marker, StringComparison.Ordinal)
            ? value[marker.Length..^marker.Length].Trim()
            : value;

    private static bool IsSeparator(char value) =>
        value is ':' or '-' or '–' or '—' or '|' or '/' or '→' or '›' or '>';

    [GeneratedRegex(
        @"^(?=.{2,64}$)(?=.*[A-Za-z])(?=.*\d)[A-Za-z0-9_.-]+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IdentityPrefixRegex();

    [GeneratedRegex(
        @"^(?:(?:Project|Phase|Work\s*Package|Delivery\s*Card|Planning\s*Package|Planning\s*Card|Milestone|Decision\s*Gate)\s*(?:[:\-–—]|→|›|>)\s*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex KindPrefixRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
