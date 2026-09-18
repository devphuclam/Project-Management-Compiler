---
status: accepted
---

# Keep mutable execution evidence in a separate overlay

The Compiler stores manual execution updates in an `ExecutionOverlay` keyed by
canonical delivery-card IDs. The extracted project baseline remains immutable;
actual dates, actual/remaining effort, execution state, and user evidence are
not written into `DeliveryCard` or source provenance. Management analysis reads
both structures and derives variance, alerts, overdue, and dependency risk.

This separation is required because the original plan must remain historically
visible after work slips, and because actual effort is not an elapsed-duration
substitute. It also gives JSON reopen a deliberate representation for both the
baseline and user-maintained execution evidence without introducing a database,
provider synchronization, or a second task store.

The existing `schemaVersion: "1.0"` remains compatible by treating a missing
`executionOverlay` field as an empty overlay. New snapshots emit the field;
readers reject malformed records but do not require it in older 1.0 snapshots.
Derived alerts and variance are recalculated from the baseline, overlay, and an
explicit as-of date rather than persisted as source authority.
