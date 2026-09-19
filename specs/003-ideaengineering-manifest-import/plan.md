# Implementation Plan: IDEAEngineering Manifest Import

**Branch**: `codex/ideaengineering-manifest-import-implementation` | **Date**: 2026-09-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/003-ideaengineering-manifest-import/spec.md`

## Summary

Add a deep, dependency-free `IIdeaEngineeringManifestImporter` seam that reads the
IDEAEngineering manifest from either one exact Git commit or a stability-checked
working tree, validates the source contract in-process, resolves the seven declared
authority roles, and produces an immutable Project Management Compiler snapshot.
Official source execution remains separate from local execution proposals. The
legacy fixed-path compiler remains available for compatibility, but the manifest
workflow never falls back to it.

## Technical Context

**Language/Version**: C# / .NET 8, nullable enabled, implicit usings enabled.

**Primary Dependencies**: Existing ASP.NET Core minimal hosting, `System.Text.Json`,
ClosedXML already used by the repository, and BCL process/file APIs. No new package,
PowerShell runtime call, or source-repository validator dependency is added.

**Storage**: In-memory application state plus the existing canonical JSON and CARIO
exports. Imported source bodies are transient and are not persisted.

**Testing**: The repository's executable custom test runner via `scripts/test.ps1`,
focused importer tests through the public seam, API/loopback verifier scripts, and
the existing regression suite.

**Target Platform**: Windows developer workstation and loopback ASP.NET Core app;
the implementation uses portable .NET APIs where possible.

**Project Type**: Dependency-free modular monolith with an ASP.NET Core UI/API and
canonical project compiler library.

**Performance Goals**: Bounded source reads, deterministic results, and a normal
accepted source import completing within the existing local smoke-test budget; no
unbounded repository scan or schema evaluation.

**Constraints**: Manifest is the only discovery entry point. Every read is bounded,
repository-relative, and tied to one commit or one stable working-tree capture.
Official state cannot be mutated by previews or proposals. Public artifacts must not
contain local absolute paths, raw source bodies, credentials, or source checkout data.

**Scale/Scope**: One IDEAEngineering planning package per import, 6 phases, 35 work
packages, 53 executable cards, 7 gates/milestones, and the source contract's
expected 512/88/600 hour totals.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Pre-design: PASS. The design keeps the canonical model before views, keeps baseline,
source evidence, unknowns, diagnostics, and provenance explicit, performs deterministic
in-process extraction, uses no new dependency or external service, and keeps scope
and failure handling explicit. Tests target deep public interfaces rather than private
helpers.

Post-design: PASS. The importer is a separate application boundary; official facts,
local proposals, and UI projections remain separate. The bounded schema evaluator
fails closed for unsupported contract semantics. Legacy compatibility is an explicit
migration path and cannot become source authority. The only intentional complexity is
the importer/source-reader boundary required for exact Git capture and stable preview
capture; it is justified in the Complexity Tracking table.

## Project Structure

### Documentation (this feature)

```text
specs/003-ideaengineering-manifest-import/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
└── tasks.md
```

### Source Code (repository root)

```text
src/ProjectManagementCompiler/
├── Application/
│   ├── IProjectCompiler.cs
│   ├── ProjectCompiler.cs
│   ├── CompilerApplicationState.cs
│   └── ManifestImport/
├── Domain/
│   ├── CanonicalProject.cs
│   ├── ExecutionEntities.cs
│   ├── ManifestImportEntities.cs
│   ├── ProposalEntities.cs
│   └── Diagnostics.cs
├── Sources/
│   ├── LocalRepositorySourceAdapter.cs
│   ├── ManifestGitObjectReader.cs
│   └── ManifestWorkingTreeReader.cs
├── Extraction/
│   ├── ManifestAuthorityResolver.cs
│   ├── BoundedJsonSchemaValidator.cs
│   └── IdeaEngineeringManifestImporter.cs
├── Outputs/
│   ├── CanonicalJsonSerializer.cs
│   └── ExecutionProposalExporter.cs
└── wwwroot/
    ├── app.js
    └── styles.css

tests/ProjectManagementCompiler.Tests/
├── ManifestImportTests.cs
├── ManifestFixtureTests.cs
├── ProposalTests.cs
├── CanonicalMigrationTests.cs
└── Program.cs

tests/fixtures/ideaengineering-manifest/
├── valid/
├── invalid/
└── schemas/
```

**Structure Decision**: Keep the existing single .NET project and executable test
runner. Add a focused manifest-import module beneath the existing Application,
Domain, Sources, Extraction, and Outputs boundaries instead of making the HTTP layer
the importer. The fixture set is deliberately minimal and public-safe. The existing
Gantt projection and legacy compiler remain in place and consume canonical results.

## Architecture and State Transitions

`ManifestImportRequest` enters `IIdeaEngineeringManifestImporter.ImportAsync`.
The importer selects either `GIT_COMMIT` or `UNCOMMITTED_PREVIEW`, captures only
manifest-declared files, validates contract/schema/semantics, resolves role authority,
and atomically constructs a `ManifestImportResult`. A passing exact commit with
source readiness `PASS` becomes `OFFICIAL_COMMIT`; a valid but unready commit becomes
`CANDIDATE_PREVIEW`; a working tree is always `UNCOMMITTED_PREVIEW`. A failed result
is retained as `LatestImportAttempt`, but does not replace `CurrentOfficialSnapshot`.
An accepted preview may replace `ActivePreview` only; it never replaces official.

Official analysis reads `SourceExecutionSnapshot`. Proposal preview computes a
temporary effective projection and labels it non-authoritative; it never mutates the
official snapshot or digest. A new source commit/register revision marks retained
proposals `STALE_BASE` without rebase.

## Implementation Order

1. Public import/result types, bounded capture, diagnostics, and fake reader seam.
2. Manifest parser, contract allow-list, bounded schema subset, and authority roles.
3. Planning and execution adapters with exact accepted-source totals and unknowns.
4. Snapshot classification, application-state retention, and calendar semantics.
5. Proposal lifecycle, API compatibility alias, preview, and deterministic export.
6. Canonical schema 2.0, schema 1.0 migration, and official/preview export metadata.
7. UI, launcher, fixtures, oracle compatibility, regression, and hardening.

Each slice follows red/green/refactor through the public importer or application/API
operation. The test runner is kept dependency-free.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| Exact Git/working-tree reader boundary | One public importer must support deterministic commit reads and stable previews without coupling domain logic to Git or the file system. | Direct reads in the HTTP/compiler layer would make atomicity, path safety, and fake-source tests inseparable from UI behavior. |
