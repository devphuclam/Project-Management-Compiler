# IDEAEngineering public-safe compatibility fixture

This fixture is a deliberately reduced, synthetic representation of the
current IDEAEngineering planning contract. It contains only planning shape and
non-sensitive example values needed by the Project Management Compiler tests.

The source roles are intentionally split:

- DOC-07 owns control, baseline, phase schedule, and milestone dates.
- Appendix A owns the 35 work packages and predecessor evidence.
- Kanban/CARIO owns the 53 cards and many-to-many responsibility matrix.
- The HTML Gantt is a subordinate rendition used for cross-checking only.
