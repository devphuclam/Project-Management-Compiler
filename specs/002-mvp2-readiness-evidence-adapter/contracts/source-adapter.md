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
* `trace-matrix.md`.

Missing optional documents become safe diagnostics. No other file is read.

## Active increment resolution

1. A caller-supplied relative path is validated below `specs/`.
2. README and readiness-register identity markers must both be present and
   agree.
3. No mtime, current clock, lexical sorting, or arbitrary recursion can select
   an increment.
4. No resolvable identity emits `ACTIVE_INCREMENT_UNKNOWN`.
5. Multiple declared candidates emit `ACTIVE_INCREMENT_AMBIGUOUS`.

## Safe result boundary

The result may contain typed facts, diagnostic codes, repository-relative
source references, ref, line/table metadata, and normalized display values.
It must not contain raw Markdown, absolute local roots, credentials, or private
environment values.

## Failure behavior

Source capture errors are represented as structured diagnostics. The planning
baseline remains available when possible. Adapter failures cannot fabricate a
readiness PASS, close a decision, change a baseline, or update the execution
overlay.
