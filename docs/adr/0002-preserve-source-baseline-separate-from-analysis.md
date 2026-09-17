---
status: accepted
---

# Preserve the source baseline separately from calculated management analysis

The compiler retains authoritative source values as an immutable baseline and stores CPM, variance, health, and future forecast results as calculated analysis alongside it. The management engine may report disagreement or insufficient data, but it never rewrites a source date, effort, dependency, phase rule, or reserve value to make the calculation appear consistent.
