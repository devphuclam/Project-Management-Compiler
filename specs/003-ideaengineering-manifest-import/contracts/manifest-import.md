# Contract: Manifest Import

## Public seam

```csharp
public interface IIdeaEngineeringManifestImporter
{
    Task<ManifestImportResult> ImportAsync(
        ManifestImportRequest request,
        CancellationToken cancellationToken = default);
}
```

## Request

```csharp
public sealed record ManifestImportRequest(
    string RepositoryRoot,
    string ManifestPath,
    ManifestImportMode Mode,
    string? RequestedCommit = null,
    DateOnly? AnalysisAsOfOverride = null,
    long MaxFileBytes = 4_000_000,
    long MaxTotalBytes = 32_000_000);
```

Rules:

- `ManifestPath` must be repository-relative and equal to
  `planning/project-management-compiler-manifest.json` for the supported source
  contract.
- `GIT_COMMIT` requires a non-empty commit and reads all blobs from that commit.
- `UNCOMMITTED_PREVIEW` ignores a requested commit, captures the working tree, and
  always produces preview classification.
- Invalid request/path/commit returns a failed result with a stable diagnostic; it
  does not throw for expected source validation failures.

## Result

```csharp
public sealed record ManifestImportResult(
    ManifestImportClassification Classification,
    IdeaEngineeringSnapshot? Snapshot,
    ManifestImportAttempt Attempt,
    IReadOnlyList<CompilerDiagnostic> Diagnostics);
```

Valid results expose a canonical planning project, source execution snapshot,
metadata, readiness evidence, and validation facts. A failed result has no valid
snapshot and is safe to retain as the latest attempt.

## Diagnostics

Every diagnostic has `Code`, `Severity`, optional typed identity, optional relative
source path, optional field, human message, and recommended action. At minimum the
importer emits the stable codes `PMC-CONTRACT-002` for unsupported contract semantics,
`PMC-SNAPSHOT-002` for mixed working-tree capture, `PMC-PATH-001` for unsafe paths,
`PMC-IDENTITY-001` for duplicate typed identity, `PMC-EFFORT-001` for invalid effort,
`PMC-SCHEMA-001` for schema failure, and `PMC-CALENDAR-001` for the accepted
baseline/forecast calendar distinction.
