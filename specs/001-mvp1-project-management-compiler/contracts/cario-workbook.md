# CARIO Workbook Contract

The workbook is a human-assisted fill file. It is not a native CARIO import
contract because the source does not provide evidence of a supported import
format.

## File name

`<ProjectName>_CARIO.xlsx`

The project name is sanitized for a file name only. The workbook content retains
the source project identity.

## Worksheets and columns

### 01_TASKS

Required columns:

```text
Work Item Type
Task ID
Phase
Work Package
Nội dung công việc
Ngày bắt đầu dự kiến
Deadline
Mức độ ưu tiên
Đơn vị / Phòng ban
Ban
Ghi chú
Trạng thái ban đầu
Planned Effort (hours)
Baseline / Analysis State
Source Reference
```

Rows include 53 delivery cards and 7 decision/milestone records. Work-package
parents are represented by hierarchy columns and the children sheet, not as
additional executable effort rows.

### 02_ASSIGNMENTS

```text
Task ID
Project Logical Role
CARIO Person / Account
CARIO Role
Mapping Status
Source Reference
```

Blank concrete identities are valid output when configuration is absent.

### 03_CHILDREN_MILESTONES

```text
Parent ID
Child ID
Relationship Type
Child Type
Name
Planned Date / Deadline
Source Reference
```

### 04_DEPENDENCIES

```text
Task / Milestone ID
Depends On
Dependency Type
Analysis Eligibility
Validation State
Source Reference
```

### 05_PROJECT_INFO

```text
Field
Value
Data State
Source Reference
```

It includes project identity, baseline, target date, capacity, reserve, WIP
policy, source authority, CPM state, forecast state, and export note.

### 06_IMPORT_WARNINGS

```text
Warning ID
Severity
Code
Message
Affected Item IDs
Source Reference
```

## Required export behavior

- `A`, `R+`, `R`, `C`, `I`, and `O` meanings come from source configuration;
- person, department, team, and priority fields remain blank without explicit
  configuration;
- unresolved mappings create warning rows;
- `Ngày bắt đầu dự kiến` and `Deadline` always use baseline planned dates;
  manual actual dates, alerts, and variance never replace or rename those
  planning fields;
- the workbook remains plan-focused even when an execution overlay exists;
  execution data may appear only in clearly separate analysis/project-info
  fields and must not be presented as CARIO planned input;
- Vietnamese text is encoded in valid UTF-8 XML parts;
- workbook generation does not require Microsoft Excel or Office COM;
- simple readable header formatting is permitted but not required to change the
  meaning of a cell.
