# IDEAEngineering readiness compatibility review

## Reviewed source

- Repository: `devphuclam/IDEAEngineering` local authorized checkout
- Current `main` HEAD reviewed: `109c766e369793b0caa2c4cc3a576df528eddb92`
- Review date: 2026-09-19
- Scope: `specs/004-technical-pilot-readiness` identity, readiness states,
  gate-record contract/path, decision/action meaning, and recent approval-policy
  changes

No IDEAEngineering files or private source content are copied into the public
Project Management Compiler repository. This note records compatibility
decisions only.

## Findings

1. `IE-INC-READY-001` remains a draft PH0 readiness package. P01 is
   `IN-PROGRESS` / `NOT-RUN`; P02–P07 remain `NOT-RUN`; open decisions remain
   unresolved; PG4 execution is `NOT-RUN` and outcome is `NOT-APPLICABLE` until
   an attributable decision exists.
2. The readiness register explicitly distinguishes task state from gate state
   and result. It records a proposed successor increment as proposal-only,
   with no successor feature directory. The adapter therefore extracts the
   successor ID/summary but never treats it as authorization.
3. The current task sequence identifies `pg4-review-package.md` as a
   pre-decision review package and T026's increment-root `pg4-gate-record.md`
   as the attributable actual gate record. The latter is expected only after
   the gate task executes, so it remains an optional bounded capture path.
4. The current PG4 contract preserves the semantics required by this compiler:
   execution is separate from outcome, `NOT-RUN`/`IN-PROGRESS` require
   `NOT-APPLICABLE`, and only a completed attributable decision can produce a
   permitted outcome. No production implementation authorization is inferred.
5. The 19-09-2026 approval-policy self-approval records clarify a separate
   successor policy concern: RBAC eligibility, versioned approval policy,
   independent approval, and Release permission remain distinct. The records
   explicitly leave PG3/PG4 and runtime verification unchanged and `NOT-RUN`.
   They do not alter `specs/004` readiness source paths, state/result meaning,
   gate outcome semantics, or the T026 authority boundary.

## Decision

MVP2.1 intake remains limited to the existing six required readiness files plus
the optional increment-root actual gate record. The compiler does not capture
the new approval-policy registers automatically, does not broaden recursive
discovery, and does not interpret policy self-approval as readiness or PG4
evidence. Any future change to `specs/004` authority or gate semantics requires
a new compatibility review and contract update.

## Compatibility cases

The Project Management Compiler regression/compatibility matrix covers:

- baseline-only compilation;
- explicit readiness capture;
- missing readiness path;
- typed `WorkPackage:P04` collision with `DeliveryCard:P04`;
- standalone gate/decision/human-action evidence;
- absent actual gate record;
- higher-authority synthetic actual gate record;
- equal-authority conflict;
- save/reopen preservation.
