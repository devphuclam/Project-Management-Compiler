# MVP2.1 Requirements Checklist

## Specification

- [ ] Scope separates planning baseline, execution overlay, and management evidence.
- [ ] Inputs, outputs, non-goals, and review gate are explicit.
- [ ] IDEAEngineering semantics are represented without copying its repository.

## Source safety

- [ ] Readiness capture is opt-in or explicitly selected.
- [ ] Increment path is relative, safe, and below `specs/`.
- [ ] Package files are allow-listed and byte-bounded.
- [ ] No arbitrary recursion, latest guessing, or mtime selection exists.
- [ ] Source references cannot expose absolute paths or raw content.

## Domain correctness

- [ ] State and result remain independent.
- [ ] Gate execution and gate outcome remain independent.
- [ ] Decisions and human actions are not executable work items.
- [ ] Authority precedence and conflict diagnostics are deterministic.
- [ ] Typed identity is used for every reconciliation.
- [ ] Missing, ambiguous, unmatched, and invalid evidence are distinct.

## Compatibility

- [ ] MVP1 baseline-only compile remains unchanged.
- [ ] Schema 1.0 JSON reopens with empty evidence.
- [ ] Execution updates preserve evidence.
- [ ] Evidence round-trip preserves semantic digest.
- [ ] Existing UI layout and MVP1 security checks remain intact.

## Verification

- [ ] Named regression categories 1–30 have tests.
- [ ] Synthetic fixture contains only minimum safe facts.
- [ ] Build, test, launcher, and web verification pass fresh.
- [ ] `git diff --check` and `git status` are clean except intentional work.
- [ ] Branch/commit/push state and remaining approval are reported.
