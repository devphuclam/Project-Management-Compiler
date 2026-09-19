# Data Model: IDEAEngineering Manifest Import

## Import request and result

`ManifestImportRequest`

- `RepositoryRoot`: local root used only for capture, never persisted.
- `ManifestPath`: repository-relative manifest path; must be exactly the declared
  manifest entry point and cannot be absolute or traverse outside the root.
- `Mode`: `GIT_COMMIT` or `UNCOMMITTED_PREVIEW`.
- `RequestedCommit`: required for `GIT_COMMIT`; resolved commit SHA is persisted.
- `AsOfOverride`: optional analysis context, labeled as an override and never source
  evidence.
- `MaxFileBytes` and `MaxTotalBytes`: source-boundary limits.

`ManifestImportResult`

- `Classification`: `OFFICIAL_COMMIT`, `CANDIDATE_PREVIEW`,
  `UNCOMMITTED_PREVIEW`, or `FAILED`.
- `Snapshot`: nullable valid `IdeaEngineeringSnapshot`.
- `Attempt`: immutable metadata and diagnostics for every attempt.
- `Diagnostics`: stable code, severity, typed identity, relative path, field,
  message, and recommended action.
- `ValidationResult`: `PASS`, `PASS_WITH_WARNINGS`, or `FAIL`.
- `SourceReadiness`: gate result from the source package.

## Snapshot metadata

`IdeaEngineeringSnapshotMetadata` contains safe repository identity, context, source
commit or deterministic preview identity, manifest path, contract version, baseline
ID/version, register revision, status date, imported timestamp, deterministic
snapshot ID, validation result, source readiness, warning/error counts, and source
calendar identities. It does not contain an absolute local path or source body.

Snapshot ID is deterministic from project ID, source commit or preview identity,
baseline ID, and register revision. The imported timestamp is display metadata and is
excluded from semantic digest calculation.

## Canonical planning model

The existing canonical project remains the view-independent planning model. Manifest
authority produces typed `WorkItemIdentity` values (`kind + id`) for project, phase,
work package, delivery card, gate, and milestone. Planning fields are owned by the
roadmap/work-package/delivery-card roles. Dependencies reference typed identities and
must resolve without cycles. The accepted source totals are retained as explicit
validation facts, not inferred from display rows.

## Source execution

`SourceExecutionSnapshot`

- source contract `ProjectId` and `BaselineId` identities, cross-checked against
  import metadata;
- source register revision and status date;
- `Records` keyed by typed delivery-card identity;
- source provenance for each record;
- `RecordingState`: `NOT_RECORDED` or `RECORDED`;
- independent `ExecutionState`: `NOT_STARTED`, `IN_PROGRESS`, `COMPLETED`,
  `SUSPENDED`, `CANCELLED`, nullable when not recorded;
- independent `ResultState`: `NOT_RUN`, `PASS`, `FAIL`, `BLOCKED`,
  `NOT_APPLICABLE`, nullable when not recorded;
- actual start/finish, actual effort, remaining effort, forecast override, blockers,
  evidence references, and source status date.

Unknown values remain null. `NOT_RECORDED` is not translated to `NOT_STARTED`.
Readiness evidence is a separate management-evidence collection and cannot populate
execution fields.

## Execution proposal

`ExecutionProposal`

- proposal ID and target typed identity;
- base snapshot ID and expected register revision;
- proposed field changes and evidence references;
- `Lifecycle`: `DRAFT`, `READY_FOR_REVIEW`, or `STALE_BASE`;
- proposal diagnostics, created/updated metadata, and optional scenario assumptions.

The proposal is local, non-authoritative, and absent from official analysis. It is
  automatically stale when the current official snapshot no longer matches either base
  snapshot ID or register revision. It is not rebased automatically.

### Controlled proposal evidence

Each proposal evidence record used for completion readiness must contain:

- non-empty `EvidenceId`;
- a supported source-contract `Type`: `SOURCE_RECORD`, `COMMIT`, `PULL_REQUEST`,
  `TEST_RESULT`, `REVIEW_RECORD`, `ARTIFACT`, or `EXTERNAL_RECORD`;
- non-empty `Description`;
- a valid `RecordedAt` timestamp;
- non-empty `RecordedBy`;
- an optional safe repository-relative `RepositoryPath` that is not `.git`;
- an optional hexadecimal commit token; and
- an optional safe external URI without embedded user information. If both the
  repository path and URI are absent, the record is invalid.

Invalid evidence remains attached and diagnosed. It is not silently discarded and
does not qualify a completion proposal.

## Application state

`CompilerApplicationState` holds three independent references:

1. `CurrentOfficialSnapshot`: last valid source-ready exact-commit snapshot;
2. `LatestImportAttempt`: latest success, candidate, preview, or failed attempt;
3. `ActivePreview`: latest valid candidate/working-tree preview, if any.

The state also retains local proposals as the single runtime owner. The
`CanonicalProject.ExecutionProposals` collection is the persistence projection of
that owner, not an independent mutable store. Reopen hydrates proposals from the
canonical project, while create/update operations update the retained state and
the current canonical projection together. Schema `1.0` migrated proposals enter
the same collection. Reopen/import/proposal operations never mutate
`SourceExecution`.

Legacy `ExecutionOverlay` is read only for schema `1.0` migration and is
neutralized after migration. It is not an effective execution source for a
manifest snapshot.

## Canonical schema 2.0 validation

After deserialization, validation must confirm:

- metadata fields are internally consistent, populated where required, safe, and
  free of local absolute paths/credentials;
- source execution records use typed identities, target existing Delivery Cards,
  unique `kind + id`, valid recording/state/result semantics, non-negative effort,
  safe source paths, and valid controlled evidence;
- proposals target existing Delivery Cards, use supported fields and lifecycle
  values, carry required base snapshot/revision metadata, and contain only valid
  typed/date/numeric changes and safe evidence.

Any error makes reopen fail closed; successful JSON deserialization alone is not a
valid canonical reopen.

## Capture invariants

Working-tree pre/post fingerprints use independent manifest reads and independent
declared-boundary reads. Git `HEAD`/status verification is required for a stable
preview. Exact Git reads inspect tree mode and blob size before reading a blob body;
mode `120000` is rejected and application ceilings cap caller-requested file and
total limits.

## State transition rules

| Event | Current official | Latest attempt | Active preview |
|---|---|---|---|
| valid ready exact commit | replace | set official result | unchanged unless explicitly cleared |
| valid unready exact commit | unchanged | set candidate | replace with candidate |
| valid working tree | unchanged | set preview | replace with preview |
| failed candidate/preview | unchanged | set failed attempt | unchanged |
| new official snapshot | replace | set official result | unchanged unless explicitly cleared |

Proposal preview is a computed read model and never changes any of the three cells.
