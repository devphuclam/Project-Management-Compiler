# Source Adapter Contract

## Purpose

Capture source material into a repository snapshot without exposing provider-
specific concepts to the canonical project model.

## Interface

```csharp
public interface IProjectSourceAdapter
{
    Task<RepositorySnapshot> CaptureAsync(
        SourceRequest request,
        CancellationToken cancellationToken);
}
```

### SourceRequest

```text
location: required string
ref: optional string
allowGitHttps: bool
```

The local path mode is mandatory. HTTPS mode is optional and uses only the
existing Git executable; it never requires a GitHub API or package installation.

### RepositorySnapshot

```text
repositoryId: stable string
repositoryLabel: string
locationLabel: string
resolvedRef: string | null
captureState: KNOWN | BLOCKED | UNKNOWN
documents: SourceDocument[]
diagnostics: ImportWarning[]
```

`SourceDocument` contains a repository-relative path, format (`markdown`,
`html`, or `text`), content, and source-reference metadata. It does not contain
`GitHubIssue`, `GitHubMilestone`, or other provider-domain records.

## MVP1 discovery convention

The IDEAEngineering strategy recognizes these relative paths:

```text
README.md
docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md
docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md
docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html
docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md
```

The strategy may use equivalent fixture paths through an explicit fixture map,
but it must not scan arbitrary files and guess their meaning.

## Failure behavior

- unreadable local path: `SOURCE_CAPTURE_FAILED` with `BLOCKED` state;
- missing recognized files: `UNSUPPORTED_PLANNING_CONVENTION` or a precise
  missing-source warning;
- unavailable optional Git/network capability: explicit blocked diagnostic;
- unsupported repository structure: no invented plan and no normal export;
- source content that is readable but incomplete: safe partial records plus
  warnings only when canonical identity and authority remain safe.

## Authority resolver contract

```csharp
AuthorityResolution Resolve(RepositorySnapshot snapshot);
```

The resolver applies DOC-07 > Appendix A > Gantt > Kanban > README precedence,
records the selected baseline, and returns conflicts and stale subordinate
references as diagnostics. The resolver never modifies source documents.
