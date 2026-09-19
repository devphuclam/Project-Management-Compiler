using System.Text.Json.Serialization;

namespace ProjectManagementCompiler.Domain;

/// <summary>
/// Stable identity for a canonical work item. IDs are unique within a kind;
/// references that can target more than one kind must therefore carry both
/// the kind and the source ID.
/// </summary>
public readonly struct CanonicalWorkItemKey : IEquatable<CanonicalWorkItemKey>
{
    [JsonConstructor]
    public CanonicalWorkItemKey(string kind, string id)
    {
        Kind = NormalizeKind(kind);
        Id = id?.Trim() ?? string.Empty;
    }

    public string Kind { get; }

    public string Id { get; }

    public bool Equals(CanonicalWorkItemKey other) =>
        StringComparer.OrdinalIgnoreCase.Equals(Kind, other.Kind)
        && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);

    public override bool Equals(object? obj) => obj is CanonicalWorkItemKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        StringComparer.OrdinalIgnoreCase.GetHashCode(Kind ?? string.Empty),
        StringComparer.OrdinalIgnoreCase.GetHashCode(Id ?? string.Empty));

    public override string ToString() => $"{Kind}:{Id}";

    public static bool operator ==(CanonicalWorkItemKey left, CanonicalWorkItemKey right) => left.Equals(right);

    public static bool operator !=(CanonicalWorkItemKey left, CanonicalWorkItemKey right) => !left.Equals(right);

    public static string NormalizeKind(string? kind)
    {
        var normalized = kind?.Trim() ?? string.Empty;
        return string.Equals(normalized, "MilestoneDecision", StringComparison.OrdinalIgnoreCase)
            ? "Milestone"
            : normalized;
    }

    public static CanonicalWorkItemKey DeliveryCard(string id) => new("DeliveryCard", id);

    public static CanonicalWorkItemKey WorkPackage(string id) => new("WorkPackage", id);

    public static CanonicalWorkItemKey Milestone(string id) => new("Milestone", id);
}
