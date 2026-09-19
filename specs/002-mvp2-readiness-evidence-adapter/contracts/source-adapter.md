# IDEAEngineering Readiness Source Adapter Contract

## Interface

```csharp
ManagementEvidenceAdapterResult Adapt(
    RepositorySnapshot snapshot,
    CanonicalProject planningProject);
```

The adapter is deterministic for the same captured documents and canonical
planning project. It does not access the network, enumerate directories, or
mutate the planning project.

## Allow-list

For an explicit increment path `specs/<safe-increment>/`, the capture profile
may request only:

* `README.md`;
* `readiness-register.md`;
* `contracts/decision-and-evidence-register.md`;
* `contracts/pg4-gate-record.md`;
* `tasks.md`;
* `trace-matrix.md`;
* optional expected-later `<increment>/pg4-gate-record.md`.

The first six files are the required bounded profile. The root gate record is
optional until the gate-record task executes; its absence is not a
`SOURCE_FILE_MISSING` or `EVIDENCE_SOURCE_UNAVAILABLE` diagnostic. No other
file is read, and no directory is recursively enumerated.

## Active increment resolution

1. A caller-supplied relative path is validated below `specs/`.
2. README and readiness-register identity markers must both be present and
   agree.
3. No mtime, current clock, lexical sorting, or arbitrary recursion can select
   an increment.
4. No resolvable identity emits `ACTIVE_INCREMENT_UNKNOWN`.
5. Multiple declared candidates emit `ACTIVE_INCREMENT_AMBIGUOUS`.

If management evidence is requested without a non-empty increment path, the
compile contract fails before capture with `MANAGEMENT_EVIDENCE_PATH_REQUIRED`
and instructs the operator to select the readiness increment path. This is a
configuration error, not an unknown active increment.

`includeManagementEvidence` is the authoritative enable/disable switch for a
fresh source compilation. When it is `false`,
`managementEvidenceIncrementPath` is ignored, even when a path value is
present, and capture remains planning-only with discovery state
`NOT_REQUESTED`. When it is `true`, the path is required and the bounded
readiness profile is captured and adapted. A saved canonical JSON reopen uses
the evidence already persisted in that JSON; the fresh-compilation switch does
not strip stored evidence.

## Safe result boundary

The result may contain typed facts, diagnostic codes, repository-relative
source references, ref, line/table metadata, and normalized display values.
It must not contain raw Markdown, absolute local roots, credentials, or private
environment values.

Safe semantic summaries are bounded and may retain management meaning such as
`T006 requires project reviewer disposition`. Owner, waiting-for, required
authority, blocker, pending action, due condition, gate effect, decision, and
human-action meaning remain separate fields.

## Failure behavior

Source capture errors are represented as structured diagnostics. The planning
baseline remains available when possible. Adapter failures cannot fabricate a
readiness PASS, close a decision, change a baseline, or update the execution
overlay.
