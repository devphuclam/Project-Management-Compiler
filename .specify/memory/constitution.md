# Project Management Compiler Constitution

## Core Principles

### I. Canonical model before views

Every management view and output adapter MUST consume the same neutral canonical project model. Source-specific objects stay inside source adapters; CARIO-specific mappings stay inside output/configuration modules.

### II. Baseline and evidence are first-class

Authoritative source values MUST remain distinguishable from calculated analysis, and important extracted values MUST carry provenance. Unknown, blocked, not-run, and unresolved states MUST remain visible.

### III. Deterministic extraction

MVP1 extraction MUST be deterministic, offline-testable, and based on recognized IDEAEngineering planning conventions. It MUST NOT use AI/LLM inference or invent missing business data.

### IV. Test through deep interfaces

Source adapters, planning extraction, management calculations, and output adapters MUST expose small interfaces with observable contracts. Tests should cross those seams rather than depend on internal implementation details.

### V. Restricted-environment delivery

No Docker, package installation, runtime installation, public registry access, external API dependency, or secret inclusion is allowed. The application MUST use already installed platform capabilities.

### VI. Explicit scope and safe failure

MVP1 supports the IDEAEngineering repository conventions and local repository paths. Unsupported repositories, ambiguous authority, cycles, contradictory dates, and missing mappings MUST produce structured diagnostics instead of silent guesses.

## Development workflow

Design/specification approval precedes implementation. Implementation follows the approved plan, uses red-green-refactor testing at agreed seams, and must pass verification and review before completion is claimed.

## Governance

This constitution governs the MVP1 feature artifacts and implementation. A change to canonical terminology, baseline authority, source/output seams, or the restricted-environment rules requires a documented design update and ADR when the decision is hard to reverse or surprising to future maintainers.

**Version**: 1.0.0 | **Ratified**: 2026-09-17 | **Last Amended**: 2026-09-17
