# MVP1 Spec Kit Planning Baseline

This file records the planning context for the manually maintained Spec Kit
package. The `speckit` CLI is unavailable in the workspace, so these artifacts
follow the discovered reference templates and workflow. The detailed executable
implementation plan is created after the written-spec review gate by the
`writing-plans` workflow.

## Summary

Build a dependency-free .NET 10 modular monolith that captures an IDEAEngineering
repository snapshot, resolves DOC-07 authority, extracts its deterministic
planning convention into a neutral canonical model, calculates management
analysis, serves local browser views, and exports canonical JSON plus a
human-assisted CARIO workbook.

## Technical context

| Concern | Decision |
|---|---|
| Language/runtime | C# targeting installed `net10.0` |
| Web host | ASP.NET Core minimal local HTTP server, loopback-only |
| Dependencies | Platform libraries only; no package restore/install |
| Storage | In-memory compilation plus deliberate canonical JSON persistence |
| XLSX | Narrow Open XML writer using `System.IO.Compression` |
| UI | Plain HTML/CSS/browser JavaScript served as static files |
| Tests | Offline dependency-free executable test harness |
| Source | Local repository path mandatory; optional existing Git HTTPS capture |
| Project type | Local modular-monolith web application/compiler |
| Primary scope | IDEAEngineering planning conventions only |

## Constitution check

- Canonical model before views: pass by design.
- Baseline and evidence first: pass; source and calculated analysis are separate.
- Deterministic extraction: pass; no AI/LLM or live API requirement.
- Test through deep interfaces: pass; source, extraction, calculation, and output seams are explicit.
- Restricted environment: pass; platform libraries only, no Docker or installs.
- Safe failure: pass; unsupported authority, cycles, conflicts, and mappings produce diagnostics.

## Project structure decision

```text
src/ProjectManagementCompiler/
  Domain/
  Sources/
  Extraction/
  Management/
  Outputs/
  Application/
  Web/
tests/ProjectManagementCompiler.Tests/
tests/fixtures/ideaengineering/
specs/001-mvp1-project-management-compiler/
docs/superpowers/specs/
docs/superpowers/plans/
```

The domain and management modules have no dependency on ASP.NET Core or workbook
formatting. Web code depends on the application interface, not on source parser
internals.

## Implementation sequence

1. Project skeleton, constitution, canonical types, diagnostics, and fixture.
2. Local repository snapshot and IDEA authority/discovery/extraction.
3. Normalization, hierarchy, provenance, and canonical JSON export/reopen.
4. Dependency validation, CPM, baseline variance, capacity, reserve, and health.
5. CARIO mapping configuration and narrow XLSX writer.
6. Local browser UI with shared view models and review flow.
7. End-to-end regression, deterministic comparison, and verification.

Each slice is test-first and independently reviewable. No implementation starts
until the written design/specification gate is approved.
