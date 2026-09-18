# Synthetic technical pilot kanban and CARIO plan

## Planning policy

| Policy | Value |
|---|---|
| WIP policy | 1 |
| Initial reserve | 88 |
| Capacity hours | 600 |
| Calendar | Monday-Friday, 8h per working day |
| State | NOT_STARTED |
| Actual progress | NONE |
| Missing predecessor | X99 |

Logical project roles are `LEAD`, `PDA`, `PROC`, `DEV2`, `QLHT`, `HTKT`,
`SPEC`, and `PILOT`. CARIO codes are `A`, `R+`, `R`, `C`, `I`, and `O`.
No concrete identity is authored.

## Delivery cards

| ID | Work package | Phase | Name | Start | Finish | Effort | Duration | State | Logical role | CARIO | Predecessor |
|---|---|---|---|---|---|---:|---:|---|---|---|---|
| P01-A | P01 | PH0 | Planning card P01-A | 2026-09-18 AM | 2026-09-18 PM | 4 | 240 | NOT_STARTED | LEAD | A | — |
| P01-B | P01 | PH0 | Planning card P01-B | 2026-09-21 AM | 2026-09-21 PM | 4 | 240 | NOT_STARTED | PDA | R+ | P01-A |
| P02-A | P02 | PH0 | Planning card P02-A | 2026-09-21 AM | 2026-09-21 PM | 4 | 240 | NOT_STARTED | PROC | R | P01-B |
| P02-B | P02 | PH0 | Planning card P02-B | 2026-09-22 AM | 2026-09-22 PM | 4 | 240 | NOT_STARTED | DEV2 | C | P02-A |
| P03-A | P03 | PH0 | Planning card P03-A | 2026-09-23 AM | 2026-09-23 PM | 4 | 240 | NOT_STARTED | QLHT | I | P02-B |
| P03-B | P03 | PH0 | Planning card P03-B | 2026-09-24 AM | 2026-09-24 PM | 4 | 240 | NOT_STARTED | HTKT | O | P03-A |
| P04-A | P04 | PH0 | Planning card P04-A | 2026-09-25 AM | 2026-09-25 PM | 4 | 240 | NOT_STARTED | SPEC | A | P03-B |
| P04-B | P04 | PH0 | Planning card P04-B | 2026-09-28 AM | 2026-09-28 PM | 4 | 240 | NOT_STARTED | PILOT | R+ | P04-A |
| P05-A | P05 | PH0 | Planning card P05-A | 2026-09-29 AM | 2026-09-29 PM | 4 | 240 | NOT_STARTED | LEAD | R | P04-B |
| P06-A | P06 | PH0 | Planning card P06-A | 2026-10-01 AM | 2026-10-01 PM | 4 | 240 | NOT_STARTED | PDA | C | P05-A |
| P07-A | P07 | PH0 | Planning card P07-A | 2026-10-05 AM | 2026-10-05 PM | 4 | 240 | NOT_STARTED | PROC | I | P06-A |
| F01-A | F01 | PH1 | Planning card F01-A | 2026-10-07 AM | 2026-10-07 PM | 4 | 240 | NOT_STARTED | DEV2 | O | P07-A |
| F01-B | F01 | PH1 | Planning card F01-B | 2026-10-08 AM | 2026-10-08 PM | 4 | 240 | NOT_STARTED | QLHT | A | F01-A |
| F02-A | F02 | PH1 | Planning card F02-A | 2026-10-09 AM | 2026-10-09 PM | 4 | 240 | NOT_STARTED | HTKT | R+ | F01-B |
| F02-B | F02 | PH1 | Planning card F02-B | 2026-10-12 AM | 2026-10-12 PM | 4 | 240 | NOT_STARTED | SPEC | R | F02-A |
| F03-A | F03 | PH1 | Planning card F03-A | 2026-10-13 AM | 2026-10-13 PM | 4 | 240 | NOT_STARTED | PILOT | C | F02-B |
| F03-B | F03 | PH1 | Planning card F03-B | 2026-10-14 AM | 2026-10-14 PM | 4 | 240 | NOT_STARTED | LEAD | I | F03-A |
| F04-A | F04 | PH1 | Planning card F04-A | 2026-10-15 AM | 2026-10-15 PM | 4 | 240 | NOT_STARTED | PDA | O | F03-B |
| F05-A | F05 | PH1 | Planning card F05-A | 2026-10-19 AM | 2026-10-19 PM | 4 | 240 | NOT_STARTED | PROC | A | F04-A |
| C01-A | C01 | PH2 | Planning card C01-A | 2026-10-21 AM | 2026-10-21 PM | 4 | 240 | NOT_STARTED | DEV2 | R+ | F05-A |
| C01-B | C01 | PH2 | Planning card C01-B | 2026-10-22 AM | 2026-10-22 PM | 4 | 240 | NOT_STARTED | QLHT | R | C01-A |
| C02-A | C02 | PH2 | Planning card C02-A | 2026-10-23 AM | 2026-10-23 PM | 4 | 240 | NOT_STARTED | HTKT | C | C01-B |
| C02-B | C02 | PH2 | Planning card C02-B | 2026-10-26 AM | 2026-10-26 PM | 4 | 240 | NOT_STARTED | SPEC | I | C02-A |
| C03-A | C03 | PH2 | Planning card C03-A | 2026-10-27 AM | 2026-10-27 PM | 4 | 240 | NOT_STARTED | PILOT | O | C02-B |
| C03-B | C03 | PH2 | Planning card C03-B | 2026-10-28 AM | 2026-10-28 PM | 4 | 240 | NOT_STARTED | LEAD | A | C03-A |
| C04-A | C04 | PH2 | Planning card C04-A | 2026-10-29 AM | 2026-10-29 PM | 4 | 240 | NOT_STARTED | PDA | R+ | C03-B |
| C05-A | C05 | PH2 | Planning card C05-A | 2026-11-02 AM | 2026-11-02 PM | 4 | 240 | NOT_STARTED | PROC | R | C04-A |
| W01-A | W01 | PH3 | Planning card W01-A | 2026-11-04 AM | 2026-11-04 PM | 4 | 240 | NOT_STARTED | DEV2 | C | C05-A |
| W01-B | W01 | PH3 | Planning card W01-B | 2026-11-05 AM | 2026-11-05 PM | 4 | 240 | NOT_STARTED | QLHT | I | W01-A |
| W02-A | W02 | PH3 | Planning card W02-A | 2026-11-06 AM | 2026-11-06 PM | 4 | 240 | NOT_STARTED | HTKT | O | W01-B |
| W02-B | W02 | PH3 | Planning card W02-B | 2026-11-09 AM | 2026-11-09 PM | 4 | 240 | NOT_STARTED | SPEC | A | W02-A |
| W03-A | W03 | PH3 | Planning card W03-A | 2026-11-10 AM | 2026-11-10 PM | 4 | 240 | NOT_STARTED | PILOT | R+ | W02-B |
| W03-B | W03 | PH3 | Planning card W03-B | 2026-11-11 AM | 2026-11-11 PM | 4 | 240 | NOT_STARTED | LEAD | R | W03-A |
| W04-A | W04 | PH3 | Planning card W04-A | 2026-11-12 AM | 2026-11-12 PM | 4 | 240 | NOT_STARTED | PDA | C | W03-B |
| W05-A | W05 | PH3 | Planning card W05-A | 2026-11-16 AM | 2026-11-16 PM | 4 | 240 | NOT_STARTED | PROC | I | W04-A |
| W06-A | W06 | PH3 | Planning card W06-A | 2026-11-18 AM | 2026-11-18 PM | 4 | 240 | NOT_STARTED | DEV2 | O | W05-A |
| W07-A | W07 | PH3 | Planning card W07-A | 2026-11-20 AM | 2026-11-20 PM | 4 | 240 | NOT_STARTED | QLHT | A | W06-A |
| L01-A | L01 | PH4 | Planning card L01-A | 2026-11-24 AM | 2026-11-24 PM | 4 | 240 | NOT_STARTED | HTKT | R+ | W07-A |
| L01-B | L01 | PH4 | Planning card L01-B | 2026-11-25 AM | 2026-11-25 PM | 4 | 240 | NOT_STARTED | SPEC | R | L01-A |
| L02-A | L02 | PH4 | Planning card L02-A | 2026-11-26 AM | 2026-11-26 PM | 4 | 240 | NOT_STARTED | PILOT | C | L01-B |
| L02-B | L02 | PH4 | Planning card L02-B | 2026-11-27 AM | 2026-11-27 PM | 4 | 240 | NOT_STARTED | LEAD | I | L02-A |
| L03-A | L03 | PH4 | Planning card L03-A | 2026-11-30 AM | 2026-11-30 PM | 4 | 240 | NOT_STARTED | PDA | O | L02-B |
| L03-B | L03 | PH4 | Planning card L03-B | 2026-12-01 AM | 2026-12-01 PM | 4 | 240 | NOT_STARTED | PROC | A | L03-A |
| L04-A | L04 | PH4 | Planning card L04-A | 2026-12-02 AM | 2026-12-02 PM | 4 | 240 | NOT_STARTED | DEV2 | R+ | L03-B |
| L05-A | L05 | PH4 | Planning card L05-A | 2026-12-04 AM | 2026-12-04 PM | 4 | 240 | NOT_STARTED | QLHT | R | L04-A |
| Q01-A | Q01 | PH5 | Planning card Q01-A | 2026-12-08 AM | 2026-12-08 PM | 4 | 240 | NOT_STARTED | HTKT | C | L05-A |
| Q01-B | Q01 | PH5 | Planning card Q01-B | 2026-12-09 AM | 2026-12-09 PM | 4 | 240 | NOT_STARTED | SPEC | I | Q01-A |
| Q02-A | Q02 | PH5 | Planning card Q02-A | 2026-12-10 AM | 2026-12-10 PM | 4 | 240 | NOT_STARTED | PILOT | O | Q01-B |
| Q02-B | Q02 | PH5 | Planning card Q02-B | 2026-12-11 AM | 2026-12-11 PM | 4 | 240 | NOT_STARTED | LEAD | A | Q02-A |
| Q03-A | Q03 | PH5 | Planning card Q03-A | 2026-12-14 AM | 2026-12-14 PM | 4 | 240 | NOT_STARTED | PDA | R+ | Q02-B |
| Q04-A | Q04 | PH5 | Planning card Q04-A | 2026-12-16 AM | 2026-12-16 PM | 4 | 240 | NOT_STARTED | PROC | R | Q03-A |
| Q05-A | Q05 | PH5 | Planning card Q05-A | 2026-12-18 AM | 2026-12-18 PM | 4 | 240 | NOT_STARTED | DEV2 | C | Q04-A |
| Q06-A | Q06 | PH5 | Planning card Q06-A | 2026-12-22 AM | 2026-12-22 PM | 4 | 240 | NOT_STARTED | QLHT | I | X99 |

All dates and half-day markers in this table are authored source values. The
`X99` predecessor is intentionally absent from this fixture and is retained as
source evidence for a later `INVALID_SOURCE_EVIDENCE` extraction diagnostic.
