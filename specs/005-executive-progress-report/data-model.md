# Data Model: Executive Progress Report Export

## Executive progress report

Immutable presentation projection for one official project snapshot. It is not
canonical state and cannot be imported or applied as execution evidence.

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `projectName` | string | yes | Canonical project display name; non-empty. |
| `sourceReportingDate` | ISO date | yes | Official manifest register status date; drives file name and report marker. |
| `analysisAsOfDate` | ISO date | yes | Existing analysis date; disclosed when different from source date. |
| `planningStart` | ISO date | no | Official baseline start; missing remains explicit. |
| `planningFinish` | ISO date | no | Official baseline finish; missing remains explicit. |
| `currentPhase` | display value | yes | Deterministic phase at source reporting date or `Chưa xác định`. |
| `scheduleCondition` | condition | yes | Schedule-only condition with text and color semantic. |
| `readinessCondition` | condition | yes | Evidence/gate readiness condition, independent of schedule. |
| `nextMilestone` | milestone summary | yes | Nearest `MilestoneKind.Milestone` on/after source date or explicit missing value; a `MilestoneKind.Decision` never qualifies. |
| `progress` | progress summary | yes | Evidence-backed percentage or explicit insufficient-data text plus counts. |
| `overviewAttention` | attention list | yes | First zero-to-five ranked items. |
| `allAttention` | attention list | yes | Complete actionable list in the same stable order. |
| `overviewTimeline` | schedule row list | yes | Phase and milestone rows only. |
| `workPackageSchedule` | schedule row list | yes | Work-package rows only. |
| `deliveryCardDetails` | detail row list | yes | Delivery-card rows only. |

### Invariants

- The source metadata classification is `OfficialCommit`.
- `overviewAttention` equals the first five `allAttention` items whose
  `overviewEligible` value is true.
- No collection stores raw diagnostics as reader-facing text.
- Projecting or exporting does not mutate the input project, analysis, views,
  proposals, or application state.
- Output row order is deterministic for the same semantic input.

## Executive condition

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `code` | internal enum | yes | Stable projector code; never printed verbatim. |
| `label` | Vietnamese string | yes | Reader-facing deterministic label. |
| `detail` | Vietnamese string | yes | Fixed factual template populated only with supported values. |
| `tone` | enum | yes | `Plan`, `Complete`, `Attention`, `Blocked`, or `Unknown`; color supplements text. |

Schedule and readiness conditions use separate instances. An unknown condition
cannot use `Complete` tone.

## Progress summary

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `recordedPercent` | whole percent | no | Present only when actual and remaining effort pass the evidence rule. |
| `statement` | string | yes | Percent statement or exact insufficient-data sentence. |
| `actualEffortHours` | decimal | no | Existing recorded aggregate; never inferred. |
| `remainingEffortHours` | decimal | no | Existing recorded aggregate; never inferred. |
| `completedCount` | integer | yes | Supported analysis card count, non-negative. |
| `inProgressCount` | integer | yes | Supported analysis card count, non-negative. |
| `notStartedCount` | integer | yes | Supported analysis card count, non-negative. |
| `unknownCount` | integer | yes | Total minus supported named states, never negative. |

Percentage eligibility is:

```text
actual is present and finite and actual >= 0
AND remaining is present and finite and remaining >= 0
AND actual + remaining > 0
```

When eligible, calculate `actual / (actual + remaining) * 100` and round once
to a whole percent with `.NET MidpointRounding.AwayFromZero`. When ineligible,
`recordedPercent` is null and `statement` is exactly
`Chưa đủ dữ liệu để tính % hoàn thành`.

## Executive schedule row

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `kind` | enum | yes | `Phase`, `Milestone`, or `WorkPackage`. |
| `displayName` | string | yes | Mechanically cleaned reader-facing source name. |
| `phaseDisplayName` | string | no | Parent phase name for grouping; no raw phase ID. |
| `plannedStart` | date | no | Official baseline date. |
| `plannedFinish` | date | no | Official baseline date; milestone uses its planned date for both bounds. |
| `stateLabel` | string | yes | Approved Vietnamese status or explicit unknown. |
| `ownerLabel` | string | yes | Concrete identity, approved role, or `Chưa xác định đầu mối`. |
| `isCurrent` | boolean | yes | True only when the source reporting date falls in the supported phase range. |
| `isNextMilestone` | boolean | yes | True only for the selected nearest milestone. |
| `sourceOrder` | integer | yes | Stable canonical order used after semantic sort keys. |

Phases and canonical entries whose kind is exactly `MilestoneKind.Milestone`
feed only `overviewTimeline`; `MilestoneKind.Decision` entries are excluded from
the timeline and next-milestone selection. Work packages feed only
`workPackageSchedule`. Raw identifiers are not exposed by this row.

The current phase is selected from phases whose valid inclusive range contains
`sourceReportingDate`, ordered by latest planned start, earliest planned finish,
then source order. The next milestone is selected only from canonical entries
whose kind is exactly `MilestoneKind.Milestone` and whose valid planned date is
on or after that date, ordered by earliest planned date then source order.
`MilestoneKind.Decision` entries remain eligible only for the applicable
attention rules. Empty candidate sets use the exact missing-value templates
below.

### Schedule-row state derivation

State presentation never uses a local proposal, elapsed plan time, readiness
state, or baseline position as execution evidence.

- **Delivery card**: use the existing official effective execution represented
  by the matching `GanttProjection` item. If `HasExecutionEvidence` is false or
  the effective execution state is null, use `Chưa cập nhật` even when an
  authored initial card state exists.
- **Milestone**: use `MilestoneDecision.State` only when it is non-null;
  otherwise use `Chưa cập nhật`.
- **Phase**: the canonical phase has no direct execution state, so always use
  `Chưa cập nhật`. `isCurrent` remains a separate baseline-date fact and must
  not change the state label.
- **Work package**: inspect only matching child delivery cards' official
  effective execution. Apply the first matching rule: any explicit
  `InProgress` → `Đang thực hiện`; all children have evidence and are
  `Completed` → `Hoàn thành`; all have evidence and are `NotStarted` →
  `Chưa bắt đầu`; all have evidence and are `Suspended` → `Tạm dừng`; all have
  evidence and are `Cancelled` → `Đã hủy`; otherwise → `Chưa cập nhật`.

Mixed completed/not-started/suspended/cancelled children without an explicit
in-progress record remain `Chưa cập nhật`; the projector does not invent an
aggregate execution state.

## Delivery-card detail row

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `description` | string | yes | Mechanically cleaned card name. |
| `phaseName` | string | yes | Cleaned parent phase name or explicit unknown. |
| `workPackageName` | string | yes | Cleaned parent work-package name or explicit unknown. |
| `plannedStart` / `plannedFinish` | date | no | Official baseline dates. |
| `ownerLabel` | string | yes | Reader-facing owner rule. |
| `stateLabel` | string | yes | Reader-facing execution state. |
| `referenceCode` | string | yes | Short card identity; emitted only after all reader-facing columns. |
| `sourceOrder` | integer | yes | Stable canonical order. |

## Management attention item

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `category` | enum | yes | `Blocked`, `Overdue`, `DecisionBeforeNextMilestone`, `AtRisk`, `MissingOwner`, `OtherDecision`, or `PendingAction`. |
| `action` | string | yes | Source-backed action/summary or fixed factual template. |
| `impact` | string | yes | Supported management consequence; no invented cause. |
| `ownerLabel` | string | yes | Reader-facing owner or explicit missing owner. |
| `dueDate` | date | no | Attributable official date when one can be resolved. |
| `dueLabel` | string | yes | Source due condition, formatted date, or `Chưa xác định`. |
| `overviewEligible` | boolean | yes | True only for the five approved overview-priority categories. |
| `deduplicationKey` | internal string | yes | Evidence observation identity or alert/work-item identity; never printed. |
| `sourceOrder` | integer | yes | Stable official source order. |

### Candidate rules

1. `Blocked`: valid evidence explicitly reports blocked/deviation with a
   management consequence, or approved analysis identifies blocked work.
2. `Overdue`: approved analysis contains an overdue schedule alert.
3. `DecisionBeforeNextMilestone`: a valid actionable decision remains open,
   has source action/summary plus management consequence, and satisfies the
   exact nearest-milestone attribution rules below.
4. `AtRisk`: approved analysis contains an at-risk schedule alert.
5. `MissingOwner`: an otherwise important item (critical, blocked, overdue, or
   at-risk) has no resolvable owner.
6. `OtherDecision`: a valid actionable open decision that does not satisfy the
   nearest-milestone attribution rule; retained on the full action sheet only.
7. `PendingAction`: a valid human action in `OPEN` or `NOT-RUN`; retained on the
   full action sheet unless the same evidence already qualifies as blocked.

Invalid or ambiguous evidence and diagnostics without management consequence
are not candidates. Duplicate candidates keep their highest-priority category.
Sort by the category order above, attributable due date (known earliest, then
unknown), then source order and stable deduplication key. Only categories 1–5
set `overviewEligible = true`.

### Decision attribution to the nearest milestone

An open decision qualifies as `DecisionBeforeNextMilestone` only when a next
milestone with a valid planned date exists and at least one of these bounded
rules succeeds:

1. the decision `SourceRecordId` equals the milestone ID after removing at most
   one exact leading `G-` from the milestone ID;
2. the decision's explicit target or non-ambiguous reconciled target equals the
   milestone kind and ID;
3. a strict ISO `yyyy-MM-dd` token in the source due condition is on or before
   the milestone date;
4. a source due condition beginning with `Before` names one or more exact work-
   package IDs, every named package resolves, and every package planned start
   is on or before the milestone date;
5. a whole-token gate or milestone ID in `GateId`, `GateEffectCode`, or the due
   condition equals the milestone ID or its single-`G-`-stripped form.

No fuzzy title matching, substring-only ID matching, inferred date, or generic
`OPEN` state qualifies. Rule 4 uses the earliest named package planned start as
the attributable due date. A strict ISO date is the due date for rule 3; the
milestone date is the due date for rules 1, 2, and 5. If no rule succeeds, the
decision remains `OtherDecision` in the full action list.

## Normative display rules

These mappings, precedence rules, and sentence templates are part of the
contract. Implementation must not add synonyms or generated prose.

### Execution and management state labels

| Source condition | Reader label |
| --- | --- |
| Missing/unrecorded execution | `Chưa cập nhật` |
| `NotStarted` / `NOT_STARTED` | `Chưa bắt đầu` |
| `InProgress` / `IN_PROGRESS` / `IN-PROGRESS` | `Đang thực hiện` |
| `Completed` / `COMPLETED` / `COMPLETE` | `Hoàn thành` |
| `Suspended` / `SUSPENDED` | `Tạm dừng` |
| `Cancelled` / `CANCELLED` | `Đã hủy` |
| Open decision | `Chưa chốt` |
| Missing/not-run assessment | `Chưa đánh giá` |
| Explicit blocked result | `Bị chặn` |

### Schedule condition precedence

Count distinct work-item IDs. Apply the first matching rule:

| Precedence | Condition | Label | Tone | Detail template |
| --- | --- | --- | --- | --- |
| 1 | Supported analysis alert code `BLOCKED` exists | `Bị chặn` | `Blocked` | `Có {count} hạng mục lịch trình đang bị chặn.` |
| 2 | `OVERDUE` or `START_DELAY` exists | `Trễ kế hoạch` | `Blocked` | `Có {count} hạng mục đã quá ngày kế hoạch.` |
| 3 | `AT_RISK` exists | `Có nguy cơ` | `Attention` | `Có {count} hạng mục được ghi nhận có nguy cơ.` |
| 4 | Analysis date exists and at least one planned row has a valid date range | `Chưa ghi nhận lệch kế hoạch` | `Plan` | `Chưa ghi nhận hạng mục bị chặn, quá hạn hoặc có nguy cơ tại ngày báo cáo.` |
| 5 | Otherwise | `Chưa đánh giá` | `Unknown` | `Chưa đủ dữ liệu để đánh giá tình trạng lịch trình.` |

The absence of alerts reaches rule 4 only when schedule data is assessable; it
never produces the label `Hoàn thành` or a green tone.

### Readiness condition precedence

Only observations with `ValidationState.Known` and without an `Invalid` or
`Ambiguous` reconciliation qualify. Apply the first matching rule:

| Precedence | Condition | Label | Tone | Detail template |
| --- | --- | --- | --- | --- |
| 1 | Discovery is not `Known`, or no valid observation exists | `Chưa đánh giá` | `Unknown` | `Chưa đủ bằng chứng để đánh giá mức sẵn sàng.` |
| 2 | Valid result is `BLOCKED` or a valid blocker/deviation is present | `Bị chặn` | `Blocked` | `Có {count} nội dung sẵn sàng đang bị chặn.` |
| 3 | A valid decision remains `OPEN` | `Cần quyết định` | `Attention` | `Có {count} quyết định đang chờ thẩm quyền.` |
| 4 | A valid human action is `OPEN`/`NOT-RUN`, or a readiness check is `NOT-RUN`, `FAIL`, `PASS-WITH-ACTIONS`, or `IN-PROGRESS` | `Cần xử lý` | `Attention` | `Có {count} hành động hoặc kiểm tra chưa hoàn tất.` |
| 5 | A resolved gate outcome is exactly `PASS` and none of rules 2–4 matches | `Đủ điều kiện theo bằng chứng đã ghi nhận` | `Complete` | `Bằng chứng hiện có ghi nhận cổng sẵn sàng đã đạt.` |
| 6 | Otherwise | `Chưa đánh giá` | `Unknown` | `Chưa đủ bằng chứng để đánh giá mức sẵn sàng.` |

Schedule condition never changes readiness condition, and readiness condition
never changes schedule condition.

### Summary and attention templates

- Missing current phase: `Chưa xác định giai đoạn hiện tại`.
- Missing next milestone: `Chưa xác định mốc sắp tới`.
- Valid progress: `Đã ghi nhận {percent}% theo nỗ lực thực tế và còn lại.`
- Invalid progress: `Chưa đủ dữ liệu để tính % hoàn thành`.
- State counts: `Hoàn thành: {completed} · Đang thực hiện: {inProgress} · Chưa bắt đầu: {notStarted} · Chưa cập nhật: {unknown}`.
- Empty attention: `Hiện chưa có nội dung cần xin ý kiến`.
- Analysis-alert action: `Xử lý hạng mục “{cleanName}”.`
- Blocked impact: `Hạng mục đang bị chặn.`
- Overdue impact: `Hạng mục đã quá ngày kế hoạch {date}.`
- At-risk impact: `Hạng mục được ghi nhận có nguy cơ.`
- Missing-owner impact: `Hạng mục quan trọng chưa xác định đầu mối.`

For decision and human-action rows, `action` is the first non-empty value from
the mechanically cleaned source `ActionSummary` and `Summary`; `impact` is the
first non-empty human-readable value from `GateEffectSummary`,
`AffectedTargetSummary`, and `CompletionCondition`. If no source-backed action
or impact exists, the item does not qualify. Dates display as `dd/MM/yyyy`.

Recognized consequence tokens use these exact translations before display:

| Source token | Reader text |
| --- | --- |
| `BLOCKS_<gate>` | `Có thể chặn cổng <gate>.` |
| `AUTHORIZATION` | `Cần phê duyệt trước khi tiếp tục.` |
| `DEFERRED_SCOPE` | `Ảnh hưởng phạm vi đang được hoãn.` |
| `NONE` | No management consequence; do not qualify from this value alone. |

Other consequence prose is preserved after mechanical cleanup only when it is
not an all-caps technical token. An unknown technical token never appears in
the workbook and does not qualify an item by itself. A due condition beginning
with `Before` displays `Trước {clean resolved target name}` when the target is a
known work package or milestone, and `Trước cổng {gate}` for a recognized gate
token. An unresolved/`After` condition displays `Chưa xác định` rather than raw
technical text.

### Approved owner-role table

Owner selection occurs in two stages.

First select the authoritative responsibility:

1. **Delivery card**: consider only assignments whose `CarioRoleCode` equals
   `A` case-insensitively. Exactly one distinct accountable assignment may
   qualify; no `R+`, `R`, `C`, `I`, or `O` assignment is substituted.
2. **Work package**: prefer exactly one valid `ReadinessCheck` observation
   explicitly targeting the work package with a recognized `OwnerRole`. If no
   such direct owner exists, resolve every child card's accountable owner and
   use it only when every child resolves and all labels are identical.
3. **Decision attention**: use `RequiredAuthorityRole`.
4. **Pending human action**: use `WaitingForRole`, then
   `RequiredAuthorityRole` when waiting-for is absent.
5. **Other evidence attention**: use the explicit `OwnerRole` attached to the
   qualifying observation.
6. **Phase or milestone row**: no assignment is inferred; use
   `Chưa xác định đầu mối` unless the canonical entity later gains a separately
   approved direct-owner field.

Then present the selected responsibility: use exactly one non-empty concrete
identity when present; otherwise translate exactly one recognized logical role
code through the table below; otherwise use `Chưa xác định đầu mối`. Conflicts,
multiple distinct accountable assignments, partially unresolved child-card
roll-ups, and unknown codes use the fallback. Raw codes never concatenate.

| Role code | Reader label |
| --- | --- |
| `PM` | `Quản lý dự án` |
| `LEAD` | `Đầu mối dự án` |
| `DEV`, `DEV2` | `Nhóm phát triển` |
| `QA` | `Đảm bảo chất lượng` |
| `PDA`, `PRODUCT_DECISION_AUTHORITY` | `Thẩm quyền quyết định sản phẩm` |
| `PROC` | `Phụ trách quy trình` |
| `STAKE` | `Bên liên quan` |
| `OWNER` | `Đầu mối chịu trách nhiệm` |
| `QLHT` | `Quản lý hệ thống` |
| `HTKT` | `Hỗ trợ kỹ thuật` |
| `OPS` | `Vận hành` |
| `DATA` | `Quản trị dữ liệu` |
| `SPEC` | `Chuyên gia chuyên môn` |
| `PILOT` | `Đầu mối thử nghiệm` |
| `GATE_AUTHORITY` | `Thẩm quyền phê duyệt cổng` |
| `ENGINEERING_AUTHORITY` | `Thẩm quyền kỹ thuật` |
| `PROJECT_AUTHORITY` | `Thẩm quyền dự án` |
| `PRODUCT_AUTHOR` | `Chủ trì sản phẩm` |
| `PROJECT_REVIEWER` | `Người duyệt dự án` |
| `VERIFICATION_REVIEWER` | `Người duyệt xác minh` |
| `SECURITY_REVIEWER` | `Người duyệt an toàn` |
| `DATA_CUSTODIAN` | `Đầu mối quản trị dữ liệu` |
| `OPERATIONS` | `Vận hành` |
| `AUTHORITY` | `Thẩm quyền phê duyệt` |

## Display terminology

A stateless deterministic component applies the normative presentation rules:

- execution/state mapping to approved Vietnamese labels;
- role code/meaning mapping to one approved role label;
- explicit unknown/missing labels;
- source-name mechanical cleanup;
- fixed schedule/readiness/progress/attention sentence templates;
- fixed date and number formatting under a Vietnamese display culture.

It does not infer semantics, translate arbitrary prose, inspect Git, or mutate
source text beyond the approved mechanical cleanup.

## Projection flow

```text
Official CompilationResult
  ├── CanonicalProject (baseline, hierarchy, source execution, evidence)
  ├── ManagementAnalysis (alerts, counts, effort, schedule state)
  └── ManagementViewSet (approved management projections)
          │
          ▼
ExecutiveProgressReportProjector
          │ immutable ExecutiveProgressReport
          ▼
ExecutiveProgressXlsxExporter
          │ four-sheet presentation-only XLSX bytes
          ▼
Official-only download endpoint
```

There is no reverse transition. The executive workbook is never converted to
canonical state, execution evidence, a proposal, or an XLSX preview.
