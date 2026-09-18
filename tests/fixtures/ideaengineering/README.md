# Controlled planning fixture

This directory is a synthetic, public-safe planning snapshot for offline tests.
It contains generic names and no private checkout, employee data, credentials,
tokens, or proprietary business material.

Fixture shape:

- Phases: PH0, PH1, PH2, PH3, PH4, PH5
- Work packages: 35
- Delivery cards: 53
- Gate/milestone records: 7
- Authoritative work-package effort: 512 hours
- Card effort: retained separately as planning detail
- Execution evidence: none; all cards are `NOT_STARTED`

The five recognized source paths are deliberately explicit:

1. `README.md`
2. `docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md`
3. `docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md`
4. `docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html`
5. `docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md`

The source policy is WIP `1`, initial reserve `88` hours, capacity `600`
hours, and a Monday-Friday calendar with eight working hours per day.
