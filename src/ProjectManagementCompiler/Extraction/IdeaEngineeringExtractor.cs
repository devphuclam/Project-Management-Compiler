using System.Globalization;
using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public sealed class IdeaEngineeringExtractor
{
    private const string Doc07Path = "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md";
    private const string AppendixPath = "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md";
    private const string GanttPath = "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html";
    private const string KanbanPath = "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md";

    private static readonly string[] CarioCodes = ["A", "R+", "R", "C", "I", "O"];
    private static readonly WorkingCalendarNormalizer Calendar = new();

    public ExtractedPlan Extract(AuthorityResolution resolution)
    {
        var warnings = resolution.Diagnostics.ToList();
        var documents = resolution.Documents.Select(document => document.Source).ToArray();
        var capturedDocuments = resolution.CapturedDocuments.Count > 0
            ? resolution.CapturedDocuments
            : documents;
        var source = documents.FirstOrDefault();
        var sourceId = source?.SourceReference.SourceId ?? string.Empty;
        var repository = source?.SourceReference.Repository ?? string.Empty;
        var resolvedRef = source?.SourceReference.ResolvedRef;
        var values = new List<ExtractedValueProvenance>();

        var authorityRows = resolution.Rows
            .Where(row => PathEquals(row.SourceRelativePath, Doc07Path))
            .ToArray();
        AddAuthorityValueReferences(resolution, authorityRows, values);

        var baseline = new ProjectBaseline
        {
            Id = resolution.Baseline.BaselineId ?? string.Empty,
            Version = resolution.Baseline.BaselineVersion ?? string.Empty,
            Status = resolution.Baseline.Status ?? string.Empty,
            AuthorityDocumentId = resolution.Baseline.AuthorityDocumentId,
            PlanningStart = resolution.Baseline.PlanningStart,
            PlanningFinish = resolution.Baseline.PlanningFinish,
            TargetDate = resolution.Baseline.TargetDate,
            PlannedEffortHours = resolution.Baseline.PlannedEffortHours,
            ReserveHours = resolution.Baseline.ReserveHours,
            CapacityHours = resolution.Baseline.CapacityHours,
            ValidationState = resolution.HasCanonicalBaseline ? ValidationState.Known : ValidationState.Blocked
        };

        var project = new Project
        {
            Id = resolution.Baseline.ProjectId ?? string.Empty,
            Name = resolution.Baseline.ProjectName ?? resolution.Baseline.ProjectId ?? string.Empty,
            TargetDate = resolution.Baseline.TargetDate,
            SourceIds = sourceId.Length == 0 ? Array.Empty<string>() : [sourceId]
        };
        if (resolution.AuthorityDocument is not null)
        {
            values.Add(new ExtractedValueProvenance
            {
                Key = "Project name",
                RawValue = project.Name,
                SourceReference = resolution.AuthorityDocument.Source.SourceReference with
                {
                    Section = "title",
                    ExtractionRule = "idea-planning-project-name"
                }
            });
        }

        if (!resolution.HasCanonicalBaseline)
        {
            return new ExtractedPlan
            {
                HasCanonicalBaseline = false,
                SourceId = sourceId,
                Repository = repository,
                ResolvedRef = resolvedRef,
                SourceDocuments = capturedDocuments,
                Project = project,
                Baseline = baseline,
                ValueProvenance = values,
                CapturedAtUtc = resolution.CapturedAtUtc,
                Warnings = warnings
            };
        }

        var phases = ExtractPhases(resolution, warnings);
        var workPackages = ExtractWorkPackages(resolution, warnings);
        var cardEvidence = new List<ExtractedCardEvidence>();
        var cards = ExtractCards(resolution, warnings, cardEvidence);
        var milestones = ExtractMilestones(resolution, warnings);
        var dependencies = ExtractDependencies(workPackages, cards, milestones, cardEvidence, resolution, warnings);
        var roles = ExtractRoles(cardEvidence);
        var assignments = ExtractAssignments(cardEvidence);
        var resourcePolicy = ExtractResourcePolicy(resolution);
        var policies = ExtractPolicies(resolution, warnings, resourcePolicy.Constraint);
        var capacity = new CapacityPlan
        {
            CapacityHours = baseline.CapacityHours,
            SourceResourcePolicy = resourcePolicy.Description,
            ResourceLogicalRole = resourcePolicy.Constraint is null ? null : "single-coder",
            EffortAccountingLevel = "WORK_PACKAGE_APPENDIX_A",
            Calendar = new CalendarDefinition
            {
                WorkingWeekdays = WorkingCalendarNormalizer.DefaultWorkingWeekdays,
                HoursPerWorkingDay = 8m
            }
        };
        var reserve = new Reserve
        {
            InitialHours = baseline.ReserveHours,
            ConsumedHours = null,
            RemainingHours = null,
            ConsumptionState = DataState.NotRun,
            RemainingState = DataState.Unknown,
            SourceReferences = FindAuthorityReferences(authorityRows, "Initial reserve")
        };

        return new ExtractedPlan
        {
            HasCanonicalBaseline = true,
            SourceId = sourceId,
            Repository = repository,
            ResolvedRef = resolvedRef,
            CapturedAtUtc = resolution.CapturedAtUtc,
            SourceDocuments = capturedDocuments,
            Project = project,
            Baseline = baseline,
            Phases = phases,
            WorkPackages = workPackages,
            DeliveryCards = cards,
            Milestones = milestones,
            Dependencies = dependencies,
            ResponsibilityRoles = roles,
            Assignments = assignments,
            CardEvidence = cardEvidence,
            Capacity = capacity,
            Reserve = reserve,
            Policies = policies,
            ValueProvenance = values,
            Warnings = warnings.OrderBy(warning => warning.Id, StringComparer.Ordinal).ToArray()
        };
    }

    private static IReadOnlyList<Phase> ExtractPhases(AuthorityResolution resolution, ICollection<ImportWarning> warnings)
    {
        var phases = new List<Phase>();
        foreach (var row in resolution.PhaseRows.OrderBy(row => row.RowIndex))
        {
            var rawId = Cell(row, "Phase ID", "Phase");
            var id = NormalizePhaseId(rawId) ?? string.Empty;
            var name = Cell(row, "Name") ?? rawId ?? string.Empty;
            var start = ParseDate(row, "Start", warnings)
                ?? ParseDate(row, "Planned start", warnings);
            var finish = ParseDate(row, "Finish", warnings)
                ?? ParseDate(row, "Planned finish", warnings);
            var duration = SafeDuration(start, null, finish, null, row, warnings);
            var allocation = resolution.Rows.FirstOrDefault(candidate =>
                PathEquals(candidate.SourceRelativePath, Doc07Path)
                && NormalizePhaseId(Cell(candidate, "Phase")) == id
                && Cell(candidate, "Planned work") is not null);
            var plannedWork = ParseDecimal(allocation, "Planned work", warnings);
            var reserve = ParseDecimal(allocation, "Reserve", warnings);
            var sourceReference = row.SourceReference;
            if (id.Length == 0)
            {
                warnings.Add(MissingValue(row, "MISSING_PHASE_ID", "Phase ID"));
                continue;
            }

            phases.Add(new Phase
            {
                Id = id,
                Name = name,
                PhaseId = id,
                PlannedStart = start,
                PlannedFinish = finish,
                PlannedEffortHours = plannedWork,
                PlannedEffortState = plannedWork is null ? DataState.Unknown : DataState.Known,
                PlannedDurationWorkingMinutes = duration.WorkingMinutes,
                DurationState = duration.State,
                ReserveHours = reserve,
                MilestoneIds = Array.Empty<string>(),
                SourceReferences = [sourceReference]
            });
        }

        return phases;
    }

    private static IReadOnlyList<WorkPackage> ExtractWorkPackages(AuthorityResolution resolution, ICollection<ImportWarning> warnings)
    {
        var workPackages = new List<WorkPackage>();
        foreach (var row in RowsFor(resolution, AppendixPath).Where(IsWorkPackageRow).OrderBy(row => row.RowIndex))
        {
            var id = NormalizeId(Cell(row, "ID", "Work package ID", "Code", "Mã"));
            var phaseId = NormalizePhaseId(Cell(row, "Phase")) ?? PhaseFromSection(row.Section) ?? string.Empty;
            var start = ParseDate(row, "Start", warnings);
            var finish = ParseDate(row, "Finish", warnings);
            var effort = ParseDecimal(row, "Effort", warnings) ?? ParseDecimal(row, "Giờ", warnings);
            var duration = SafeDuration(start, null, finish, null, row, warnings);
            var predecessors = SplitIds(Cell(row, "Predecessor", "Cần trước"));
            if (id.Length == 0)
            {
                warnings.Add(MissingValue(row, "MISSING_WORK_PACKAGE_ID", "ID"));
                continue;
            }

            if (phaseId.Length == 0)
            {
                warnings.Add(MissingValue(row, "MISSING_WORK_PACKAGE_PHASE", "Phase"));
            }

            workPackages.Add(new WorkPackage
            {
                Id = id,
                Name = Cell(row, "Name", "Công việc") ?? string.Empty,
                ParentId = null,
                PhaseId = phaseId,
                PlannedStart = start,
                PlannedFinish = finish,
                PlannedEffortHours = effort,
                PlannedEffortState = effort is null ? DataState.Unknown : DataState.Known,
                PlannedDurationWorkingMinutes = duration.WorkingMinutes,
                DurationState = duration.State,
                DependencyIds = predecessors,
                CompletionCondition = Cell(row, "Completion condition", "Đầu ra và cách biết đã xong") ?? string.Empty,
                DeliveryCardIds = Array.Empty<string>(),
                SourceReferences = [row.SourceReference]
            });
        }

        return workPackages;
    }

    private static IReadOnlyList<DeliveryCard> ExtractCards(AuthorityResolution resolution, ICollection<ImportWarning> warnings, ICollection<ExtractedCardEvidence> evidence)
    {
        var carioRows = RowsFor(resolution, KanbanPath)
            .Where(IsCarioRow)
            .GroupBy(row => NormalizeId(Cell(row, "Card")), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var cards = new List<DeliveryCard>();
        foreach (var row in RowsFor(resolution, KanbanPath).Where(IsCardRow).OrderBy(row => row.RowIndex))
        {
            var id = NormalizeId(Cell(row, "ID", "Card"));
            var workPackageId = NormalizeId(Cell(row, "Work package"));
            if (workPackageId.Length == 0)
            {
                workPackageId = WorkPackageFromCardId(id);
            }

            var phaseId = NormalizePhaseId(Cell(row, "Phase")) ?? PhaseFromSection(row.Section) ?? string.Empty;
            var schedule = ParseDateRange(row, warnings);
            var effort = ParseDecimal(row, "Effort", warnings) ?? ParseDecimal(row, "Giờ", warnings);
            var duration = SafeDuration(schedule.Start, schedule.StartMarker, schedule.Finish, schedule.FinishMarker, row, warnings);
            DiagnoseEffortDurationMismatch(id, effort, duration, row, warnings);
            var stateValue = Cell(row, "State", "Trạng thái");
            var state = stateValue is null ? (ExecutionState?)null : ParseExecutionState(stateValue, row, warnings);
            var roleAssignments = new List<ExtractedRoleAssignmentEvidence>();
            var logical = Cell(row, "Logical role");
            var cario = Cell(row, "CARIO");
            if (!string.IsNullOrWhiteSpace(logical) || !string.IsNullOrWhiteSpace(cario))
            {
                roleAssignments.Add(new ExtractedRoleAssignmentEvidence
                {
                    CardId = id,
                    LogicalRoleCode = logical ?? string.Empty,
                    CarioRoleCode = cario ?? string.Empty,
                    SourceReference = row.SourceReference
                });
            }

            if (carioRows.TryGetValue(id, out var matrices))
            {
                foreach (var matrix in matrices)
                {
                    foreach (var code in CarioCodes)
                    {
                        foreach (var logicalRole in SplitRoleCodes(Cell(matrix, code)))
                        {
                            roleAssignments.Add(new ExtractedRoleAssignmentEvidence
                            {
                                CardId = id,
                                LogicalRoleCode = logicalRole,
                                CarioRoleCode = code,
                                SourceReference = matrix.SourceReference
                            });
                        }
                    }
                }
            }
            if (id.Length == 0)
            {
                warnings.Add(MissingValue(row, "MISSING_DELIVERY_CARD_ID", "ID"));
                continue;
            }

            if (workPackageId.Length == 0 || phaseId.Length == 0)
            {
                warnings.Add(MissingValue(row, "MISSING_DELIVERY_CARD_PARENT", "Work package / Phase"));
            }

            cards.Add(new DeliveryCard
            {
                Id = id,
                Name = Cell(row, "Name", "Tên task nhập Kanban") ?? string.Empty,
                ParentId = workPackageId,
                PhaseId = phaseId,
                PlannedStart = schedule.Start,
                PlannedFinish = schedule.Finish,
                PlannedEffortHours = effort,
                PlannedEffortState = effort is null ? DataState.Unknown : DataState.Known,
                PlannedDurationWorkingMinutes = duration.WorkingMinutes,
                DurationState = duration.State,
                WorkPackageId = workPackageId,
                State = state,
                RoleAssignmentIds = roleAssignments.Select((_, index) => $"{id}:assignment:{index + 1:D3}").ToArray(),
                SourceReferences = [row.SourceReference]
            });
            evidence.Add(new ExtractedCardEvidence
            {
                CardId = id,
                LogicalRoleCode = logical ?? string.Empty,
                CarioRoleCode = cario ?? string.Empty,
                PredecessorId = Cell(row, "Predecessor", "Cần trước"),
                SourceReference = row.SourceReference,
                RoleAssignments = roleAssignments
            });
        }

        return cards;
    }

    private static IReadOnlyList<MilestoneDecision> ExtractMilestones(AuthorityResolution resolution, ICollection<ImportWarning> warnings)
    {
        var authorityRows = RowsFor(resolution, Doc07Path)
            .Where(row => Cell(row, "ID / target date") is not null)
            .OrderBy(row => row.RowIndex)
            .ToArray();
        var rows = authorityRows.Length > 0
            ? authorityRows
            : RowsFor(resolution, GanttPath).Where(row => Cell(row, "Gate ID") is not null).OrderBy(row => row.RowIndex).ToArray();
        var parentByGate = BuildGateParents(resolution);
        var milestones = new List<MilestoneDecision>();
        foreach (var row in rows)
        {
            var identity = Cell(row, "ID / target date");
            var id = NormalizeMilestoneId(identity?.Split('/', 2)[0] ?? Cell(row, "Gate ID"));
            var date = identity is null
                ? ParseDate(row, "Date", warnings)
                : ParseDateText(identity.Split('/', 2).ElementAtOrDefault(1), row, warnings).Date;
            if (id.Length == 0 || date is null)
            {
                warnings.Add(MissingValue(row, "MISSING_MILESTONE_VALUE", "Gate ID / Date"));
                continue;
            }

            var kindText = (Cell(row, "Kind", "Type") ?? string.Empty).Trim().ToUpperInvariant();
            var kind = kindText.Contains("DECISION", StringComparison.Ordinal)
                || kindText.Contains("CHECKPOINT", StringComparison.Ordinal)
                ? MilestoneKind.Decision
                : MilestoneKind.Milestone;

            milestones.Add(new MilestoneDecision
            {
                Id = id,
                Kind = kind,
                ParentId = parentByGate.TryGetValue(id, out var parent) ? parent : null,
                Name = identity?.Split('/', 2)[0].Trim() ?? id,
                PlannedDate = date,
                State = null,
                DependencyIds = Array.Empty<string>(),
                PlannedEffortHours = 0m,
                PlannedDurationWorkingMinutes = 0,
                SourceReferences = [row.SourceReference]
            });
        }

        return milestones;
    }

    private static IReadOnlyList<Dependency> ExtractDependencies(
        IReadOnlyList<WorkPackage> workPackages,
        IReadOnlyList<DeliveryCard> cards,
        IReadOnlyList<MilestoneDecision> milestones,
        IReadOnlyList<ExtractedCardEvidence> evidence,
        AuthorityResolution resolution,
        ICollection<ImportWarning> warnings)
    {
        var dependencies = new List<Dependency>();
        var knownWorkPackageIds = workPackages.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownCardIds = cards.Select(card => card.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownMilestoneIds = milestones.Select(milestone => milestone.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var workPackage in workPackages)
        {
            foreach (var predecessor in workPackage.DependencyIds)
            {
                dependencies.Add(new Dependency
                {
                    SubjectId = workPackage.Id,
                    SubjectKind = "WorkPackage",
                    PredecessorId = predecessor,
                    PredecessorKind = knownWorkPackageIds.Contains(predecessor) ? "WorkPackage" : "Unknown",
                    DependencyType = DependencyType.FinishToStart,
                    AnalysisEligible = false,
                    ValidationState = knownWorkPackageIds.Contains(predecessor) ? ValidationState.Known : ValidationState.InvalidSourceEvidence,
                    SourceReferences = workPackage.SourceReferences
                });
            }
        }

        foreach (var card in cards)
        {
            var predecessorText = evidence.FirstOrDefault(item => item.CardId == card.Id)?.PredecessorId;
            foreach (var rawPredecessor in SplitIds(predecessorText))
            {
                var predecessor = rawPredecessor.StartsWith("G-", StringComparison.OrdinalIgnoreCase)
                    ? NormalizeMilestoneId(rawPredecessor)
                    : NormalizeId(rawPredecessor);
                var valid = knownCardIds.Contains(predecessor) || knownMilestoneIds.Contains(predecessor);
                var predecessorKind = knownMilestoneIds.Contains(predecessor)
                    ? "Milestone"
                    : knownCardIds.Contains(predecessor) ? "DeliveryCard" : "Unknown";
                dependencies.Add(new Dependency
                {
                    SubjectId = card.Id,
                    SubjectKind = "DeliveryCard",
                    PredecessorId = predecessor,
                    PredecessorKind = predecessorKind,
                    DependencyType = DependencyType.FinishToStart,
                    AnalysisEligible = valid,
                    ValidationState = valid ? ValidationState.Known : ValidationState.InvalidSourceEvidence,
                    SourceReferences = card.SourceReferences
                });

                if (!valid)
                {
                    warnings.Add(new ImportWarning
                    {
                        Id = $"MISSING_DEPENDENCY_TARGET:{card.Id}:{predecessor}",
                        Severity = WarningSeverity.Warning,
                        Code = "MISSING_DEPENDENCY_TARGET",
                        Message = $"Delivery card '{card.Id}' names missing predecessor '{predecessor}'; the source edge is retained but excluded from analysis.",
                        AffectedIds = [card.Id, predecessor],
                        SourceReferences = card.SourceReferences
                    });
                }
            }
        }

        foreach (var row in RowsFor(resolution, KanbanPath).Where(IsGateRow))
        {
            var subject = NormalizeMilestoneId(Cell(row, "Card"));
            if (!knownMilestoneIds.Contains(subject))
            {
                continue;
            }

            foreach (var rawPredecessor in SplitIds(Cell(row, "Cần trước", "Predecessor")))
            {
                var predecessor = rawPredecessor.StartsWith("G-", StringComparison.OrdinalIgnoreCase)
                    ? NormalizeMilestoneId(rawPredecessor)
                    : NormalizeId(rawPredecessor);
                var valid = knownMilestoneIds.Contains(predecessor) || knownCardIds.Contains(predecessor) || knownWorkPackageIds.Contains(predecessor);
                var predecessorKind = knownMilestoneIds.Contains(predecessor)
                    ? "Milestone"
                    : knownCardIds.Contains(predecessor) ? "DeliveryCard" : knownWorkPackageIds.Contains(predecessor) ? "WorkPackage" : "Unknown";
                dependencies.Add(new Dependency
                {
                    SubjectId = subject,
                    SubjectKind = "Milestone",
                    PredecessorId = predecessor,
                    PredecessorKind = predecessorKind,
                    DependencyType = DependencyType.FinishToStart,
                    AnalysisEligible = valid && predecessorKind is "Milestone" or "DeliveryCard",
                    ValidationState = valid ? ValidationState.Known : ValidationState.InvalidSourceEvidence,
                    SourceReferences = [row.SourceReference]
                });
            }
        }

        return dependencies
            .OrderBy(dependency => dependency.SubjectId, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.PredecessorId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ResponsibilityRole> ExtractRoles(IReadOnlyList<ExtractedCardEvidence> evidence)
    {
        var roleRows = evidence.SelectMany(item => item.RoleAssignments).ToArray();
        var roles = new List<ResponsibilityRole>();
        foreach (var cario in CarioCodes)
        {
            var reference = roleRows.FirstOrDefault(row => string.Equals(row.CarioRoleCode, cario, StringComparison.OrdinalIgnoreCase))?.SourceReference;
            roles.Add(new ResponsibilityRole
            {
                Code = $"CARIO:{cario}",
                CarioRoleCode = cario,
                SourceMeaning = $"CARIO responsibility code {cario}",
                SourceReferences = reference is null ? Array.Empty<SourceReference>() : [reference]
            });
        }

        foreach (var logical in roleRows.Select(row => row.LogicalRoleCode).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal))
        {
            var reference = roleRows.First(row => string.Equals(row.LogicalRoleCode, logical, StringComparison.OrdinalIgnoreCase)).SourceReference;
            roles.Add(new ResponsibilityRole
            {
                Code = logical!,
                LogicalRoleCode = logical,
                SourceMeaning = $"Logical project role {logical}",
                SourceReferences = [reference]
            });
        }

        return roles;
    }

    private static IReadOnlyList<Assignment> ExtractAssignments(IReadOnlyList<ExtractedCardEvidence> evidence) =>
        evidence.SelectMany(item => item.RoleAssignments.Select(assignment => new Assignment
        {
            WorkItemId = item.CardId,
            LogicalRoleCode = assignment.LogicalRoleCode,
            CarioRoleCode = assignment.CarioRoleCode,
            ConcreteIdentity = null,
            MappingStatus = "UNRESOLVED",
            SourceReferences = [assignment.SourceReference]
        })).ToArray();

    private static PolicySet ExtractPolicies(AuthorityResolution resolution, ICollection<ImportWarning> warnings, string? resourceConstraint)
    {
        var wip = ParsePolicyInteger(resolution, "WIP policy", warnings) ?? ParseWipFromKanban(resolution);
        return new PolicySet
        {
            WorkInProgressLimit = wip,
            ResourceConstraint = resourceConstraint,
            Rules = resolution.PolicyFacts
                .Where(pair => pair.Key is "Calendar" or "Actual progress")
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value.Value}")
                .ToArray()
        };
    }

    private static (string? Constraint, string? Description) ExtractResourcePolicy(AuthorityResolution resolution)
    {
        var planningContent = string.Join("\n", resolution.Documents
            .Where(document => PathEquals(document.Source.RelativeFile, Doc07Path) || PathEquals(document.Source.RelativeFile, KanbanPath))
            .Select(document => document.Source.Content));
        var normalized = Regex.Replace(planningContent, @"[*`]", string.Empty);
        if (!Regex.IsMatch(normalized, @"only assumed coder|single[- ]coder|one coder|một người viết code", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return (null, null);
        }

        var description = Regex.IsMatch(normalized, @"weekday[- ]only|weekday-only|ngày làm việc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? "single primary coder; weekday-only baseline"
            : "single primary coder";
        return ("single primary coder", description);
    }

    private static void AddAuthorityValueReferences(AuthorityResolution resolution, IReadOnlyList<PlanningTableRow> authorityRows, ICollection<ExtractedValueProvenance> values)
    {
        foreach (var key in new[] { "Project ID", "Stable Document ID", "Baseline ID", "Baseline version", "Status", "Document Status", "Planning start", "Planning finish", "Target date", "Authoritative effort", "Planned phase work", "Initial reserve", "Controlled reserve", "Capacity", "Weekday capacity" })
        {
            var row = authorityRows.FirstOrDefault(candidate => string.Equals(Cell(candidate, "Field", "Item"), key, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                values.Add(new ExtractedValueProvenance { Key = key, RawValue = Cell(row, "Value", "Recorded value", "Planned hours / condition"), SourceReference = row.SourceReference });
            }
        }
    }

    private static IReadOnlyList<SourceReference> FindAuthorityReferences(IEnumerable<PlanningTableRow> rows, string key) =>
        rows.Where(row => string.Equals(Cell(row, "Field", "Item"), key, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.SourceReference)
            .ToArray();

    private static IEnumerable<PlanningTableRow> RowsFor(AuthorityResolution resolution, string path) =>
        resolution.Rows.Where(row => PathEquals(row.SourceRelativePath, path));

    private static bool IsWorkPackageRow(PlanningTableRow row) =>
        RegexLikeId(Cell(row, "ID", "Work package ID", "Code", "Mã"))
        && (Cell(row, "Phase") is not null || PhaseFromSection(row.Section) is not null)
        && Cell(row, "Name", "Công việc") is not null;

    private static bool IsCardRow(PlanningTableRow row)
    {
        var id = Cell(row, "ID", "Card");
        return RegexLikeId(id)
            && Cell(row, "Name", "Tên task nhập Kanban") is not null
            && (Cell(row, "Start", "Finish", "Thời gian") is not null)
            && !IsGateRow(row);
    }

    private static bool IsCarioRow(PlanningTableRow row) =>
        RegexLikeId(Cell(row, "Card"))
        && CarioCodes.Any(code => row.Headers.Any(header => NormalizeHeader(header) == NormalizeHeader(code)));

    private static bool IsGateRow(PlanningTableRow row) =>
        RegexLikeId(Cell(row, "Card"))
        && (Cell(row, "Hạn") is not null || row.Section?.Contains("Bảy card quyết định", StringComparison.OrdinalIgnoreCase) == true);

    private static bool RegexLikeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim().Trim('`');
        return trimmed.Length >= 3
            && char.IsLetter(trimmed[0])
            && trimmed.Skip(1).All(character => char.IsLetterOrDigit(character) || character is '-' or '+');
    }

    private static string? Cell(PlanningTableRow? row, params string[] names)
    {
        if (row is null)
        {
            return null;
        }

        foreach (var name in names)
        {
            var header = row.Headers.FirstOrDefault(candidate => NormalizeHeader(candidate) == NormalizeHeader(name));
            if (header is not null && row.Cells.TryGetValue(header, out var value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string NormalizeHeader(string value) => string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string NormalizeId(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Trim('`').ToUpperInvariant();

    private static string? NormalizePhaseId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value, @"\b(?<id>PH\d+)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["id"].Value.ToUpperInvariant() : NormalizeId(value);
    }

    private static string NormalizeMilestoneId(string? value)
    {
        var id = NormalizeId(value);
        if (id.Length == 0)
        {
            return string.Empty;
        }

        return id.StartsWith("G-", StringComparison.OrdinalIgnoreCase) ? id : $"G-{id}";
    }

    private static string WorkPackageFromCardId(string id)
    {
        var match = Regex.Match(id, @"^(?<package>[A-Z]+\d+)(?:-[A-Z])?$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["package"].Value.ToUpperInvariant() : id;
    }

    private static string? PhaseFromSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return null;
        }

        var match = Regex.Match(section, @"\b(?<id>PH\d+)\b", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["id"].Value.ToUpperInvariant() : null;
    }

    private static IReadOnlyList<string> SplitIds(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || raw.Trim() is "-" or "—"
            ? Array.Empty<string>()
            : raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeId)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static IReadOnlyList<string> SplitRoleCodes(string? raw) =>
        string.IsNullOrWhiteSpace(raw) || raw.Trim() is "-" or "—"
            ? Array.Empty<string>()
            : raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => value.Length > 0 && value is not "-" and not "—")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static DateOnly? ParseDate(PlanningTableRow? row, string header, ICollection<ImportWarning> warnings)
    {
        var raw = Cell(row, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim().Trim('`');
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateOnly.TryParseExact(value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        var shortDate = Regex.Match(value, @"^(?<day>\d{1,2})/(?<month>\d{1,2})(?:/(?<year>\d{4}))?$", RegexOptions.IgnoreCase);
        if (shortDate.Success
            && int.TryParse(shortDate.Groups["year"].Success ? shortDate.Groups["year"].Value : "2026", out var year)
            && int.TryParse(shortDate.Groups["month"].Value, out var month)
            && int.TryParse(shortDate.Groups["day"].Value, out var day)
            && DateOnly.TryParse($"{year:D4}-{month:D2}-{day:D2}", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        if (DateOnly.TryParseExact($"{value} 2026", "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        if (row is null)
        {
            return null;
        }

        warnings.Add(new ImportWarning
        {
            Id = $"MALFORMED_DATE:{row.SourceRelativePath}:{row.SourceLine}:{header}",
            Severity = WarningSeverity.Warning,
            Code = "MALFORMED_DATE",
            Message = $"Authored date '{raw}' for '{header}' is not ISO yyyy-MM-dd and was not guessed.",
            SourceReferences = [row.SourceReference]
        });
        return null;
    }

    private static (DateOnly? Start, string? StartMarker, DateOnly? Finish, string? FinishMarker) ParseDateRange(PlanningTableRow row, ICollection<ImportWarning> warnings)
    {
        var raw = Cell(row, "Thời gian");
        if (string.IsNullOrWhiteSpace(raw))
        {
            var start = ParseDateToken(row, "Start", warnings);
            var finish = ParseDateToken(row, "Finish", warnings);
            return (start.Date, start.Marker, finish.Date, finish.Marker);
        }

        var normalized = raw.Trim().Replace('–', '-').Replace('—', '-');
        var parts = normalized.Split('-', 2, StringSplitOptions.TrimEntries);
        var first = ParseDateText(parts[0], row, warnings);
        if (parts.Length == 1)
        {
            return (first.Date, first.Marker, first.Date, first.Marker);
        }

        var second = ParseDateText(parts[1], row, warnings);
        return (first.Date, first.Marker, second.Date, second.Marker);
    }

    private static (DateOnly? Date, string? Marker) ParseDateToken(PlanningTableRow row, string header, ICollection<ImportWarning> warnings)
    {
        var raw = Cell(row, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2)
        {
            warnings.Add(new ImportWarning
            {
                Id = $"MALFORMED_DATE:{row.SourceRelativePath}:{row.SourceLine}:{header}",
                Severity = WarningSeverity.Warning,
                Code = "MALFORMED_DATE",
                Message = $"Authored date token '{raw}' for '{header}' is not a safe ISO date with optional AM/PM marker.",
                SourceReferences = [row.SourceReference]
            });
            return (null, null);
        }

        var marker = parts.Length == 2 ? parts[1].ToUpperInvariant() : null;
        if (marker is not null and not ("AM" or "PM"))
        {
            warnings.Add(new ImportWarning
            {
                Id = $"AMBIGUOUS_DATE_MARKER:{row.SourceRelativePath}:{row.SourceLine}:{header}",
                Severity = WarningSeverity.Warning,
                Code = "AMBIGUOUS_DATE_MARKER",
                Message = $"Authored date marker '{marker}' for '{header}' is not AM or PM.",
                SourceReferences = [row.SourceReference]
            });
            return (null, null);
        }

        var date = ParseDateText(parts[0], row, warnings);
        return (date.Date, marker);
    }

    private static (DateOnly? Date, string? Marker) ParseDateText(string? raw, PlanningTableRow row, ICollection<ImportWarning> warnings)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var value = raw.Trim().Trim('`');
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var marker = parts.LastOrDefault()?.ToUpperInvariant() is "AM" or "PM" ? parts[^1].ToUpperInvariant() : null;
        var dateText = marker is null ? value : string.Join(' ', parts[..^1]);
        var date = ParseDateValue(dateText, row, warnings);
        return (date, marker);
    }

    private static DateOnly? ParseDateValue(string value, PlanningTableRow? row, ICollection<ImportWarning> warnings)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || DateOnly.TryParseExact(value, "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            || DateOnly.TryParseExact($"{value} 2026", "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        var shortDate = Regex.Match(value, @"^(?<day>\d{1,2})/(?<month>\d{1,2})$", RegexOptions.IgnoreCase);
        if (shortDate.Success
            && int.TryParse(shortDate.Groups["month"].Value, out var month)
            && int.TryParse(shortDate.Groups["day"].Value, out var day)
            && DateOnly.TryParse($"2026-{month:D2}-{day:D2}", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return date;
        }

        var naturalDate = Regex.Match(value, @"(?<day>\d{1,2})\s+(?<month>January|February|March|April|May|June|July|August|September|October|November|December)(?:\s+(?<year>\d{4}))?", RegexOptions.IgnoreCase);
        if (naturalDate.Success
            && int.TryParse(naturalDate.Groups["year"].Success ? naturalDate.Groups["year"].Value : "2026", out var naturalYear)
            && int.TryParse(naturalDate.Groups["day"].Value, out var naturalDay)
            && DateTime.TryParseExact($"{naturalDay} {naturalDate.Groups["month"].Value} {naturalYear}", "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var naturalDateTime))
        {
            return DateOnly.FromDateTime(naturalDateTime);
        }

        if (row is null)
        {
            return null;
        }

        warnings.Add(new ImportWarning
        {
            Id = $"MALFORMED_DATE:{row.SourceRelativePath}:{row.SourceLine}:{value}",
            Severity = WarningSeverity.Warning,
            Code = "MALFORMED_DATE",
            Message = $"Authored date '{value}' was not a safe supported date and was not guessed.",
            SourceReferences = [row.SourceReference]
        });
        return null;
    }

    private static decimal? ParseDecimal(PlanningTableRow? row, string header, ICollection<ImportWarning> warnings)
    {
        var raw = Cell(row, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().Replace("hours", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        if (row is null)
        {
            return null;
        }

        warnings.Add(new ImportWarning
        {
            Id = $"MALFORMED_NUMERIC:{row.SourceRelativePath}:{row.SourceLine}:{header}",
            Severity = WarningSeverity.Warning,
            Code = "MALFORMED_NUMERIC",
            Message = $"Authored numeric value '{raw}' for '{header}' is not invariant decimal and was not guessed.",
            SourceReferences = [row.SourceReference]
        });
        return null;
    }

    private static int? ParsePolicyInteger(AuthorityResolution resolution, string key, ICollection<ImportWarning> warnings)
    {
        if (!resolution.PolicyFacts.TryGetValue(key, out var fact))
        {
            return null;
        }

        var match = Regex.Match(fact.Value, @"\d+", RegexOptions.CultureInvariant);
        if (match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        warnings.Add(new ImportWarning
        {
            Id = $"MALFORMED_POLICY:{key}",
            Severity = WarningSeverity.Warning,
            Code = "MALFORMED_POLICY",
            Message = $"Policy '{key}' contains '{fact.Value}', which was not guessed.",
            SourceReferences = [fact.Row.SourceReference]
        });
        return null;
    }

    private static int? ParseWipFromKanban(AuthorityResolution resolution)
    {
        var content = resolution.Documents
            .FirstOrDefault(document => PathEquals(document.Source.RelativeFile, KanbanPath))?
            .Source.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalized = Regex.Replace(content, @"[*`]", string.Empty);
        var match = Regex.Match(normalized, @"tối đa\s+(?<limit>\d+)\s+card\s+thực hiện", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            match = Regex.Match(normalized, @"(?:maximum|max)\s+(?<limit>\d+)\s+(?:active\s+)?implementation\s+(?:item|card)s?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return match.Success && int.TryParse(match.Groups["limit"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            ? limit
            : null;
    }

    private static ExecutionState? ParseExecutionState(string? value, PlanningTableRow row, ICollection<ImportWarning> warnings)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "NOT_STARTED" => ExecutionState.NotStarted,
            "IN_PROGRESS" => ExecutionState.InProgress,
            "COMPLETED" => ExecutionState.Completed,
            "SUSPENDED" => ExecutionState.Suspended,
            "CANCELLED" => ExecutionState.Cancelled,
            _ => UnknownState(row, warnings)
        };
    }

    private static ExecutionState? UnknownState(PlanningTableRow row, ICollection<ImportWarning> warnings)
    {
        warnings.Add(MissingValue(row, "UNKNOWN_EXECUTION_STATE", "State"));
        return null;
    }

    private static WorkingDuration SafeDuration(DateOnly? start, string? startMarker, DateOnly? finish, string? finishMarker, PlanningTableRow row, ICollection<ImportWarning> warnings)
    {
        if (start is null || finish is null)
        {
            return new WorkingDuration { State = DataState.Unknown };
        }

        var duration = Calendar.Normalize(start.Value, startMarker, finish.Value, finishMarker);
        if (duration.Diagnostic is not null)
        {
            warnings.Add(duration.Diagnostic with
            {
                Id = $"{duration.Diagnostic.Code}:{row.SourceRelativePath}:{row.SourceLine}",
                SourceReferences = [row.SourceReference]
            });
        }

        return duration;
    }

    private static void DiagnoseEffortDurationMismatch(
        string workItemId,
        decimal? effortHours,
        WorkingDuration duration,
        PlanningTableRow row,
        ICollection<ImportWarning> warnings)
    {
        if (effortHours is null || duration.WorkingMinutes is null || duration.State != DataState.Known)
        {
            return;
        }

        var authoredMinutes = effortHours.Value * 60m;
        if (authoredMinutes == duration.WorkingMinutes.Value)
        {
            return;
        }

        warnings.Add(new ImportWarning
        {
            Id = $"EFFORT_DURATION_MISMATCH:{row.SourceRelativePath}:{row.SourceLine}:{workItemId}",
            Severity = WarningSeverity.Warning,
            Code = "EFFORT_DURATION_MISMATCH",
            Message = $"Delivery card '{workItemId}' authors {effortHours.Value:0.##} effort hours but its authored schedule normalizes to {duration.WorkingMinutes.Value / 60m:0.##} working hours; both values are retained independently.",
            AffectedIds = [workItemId],
            SourceReferences = [row.SourceReference]
        });
    }

    private static IReadOnlyDictionary<string, string> BuildGateParents(AuthorityResolution resolution)
    {
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in resolution.PhaseRows)
        {
            var phase = NormalizePhaseId(Cell(row, "Phase ID", "Phase"));
            if (phase is null)
            {
                continue;
            }

            var explicitGate = Cell(row, "Gate");
            if (!string.IsNullOrWhiteSpace(explicitGate))
            {
                parents[NormalizeMilestoneId(explicitGate)] = phase;
            }

            var startsAfter = Cell(row, "Starts after");
            var match = startsAfter is null ? null : Regex.Match(startsAfter, @"\b(?<id>D0|MS\d+)\b", RegexOptions.IgnoreCase);
            if (match?.Success == true)
            {
                parents[NormalizeMilestoneId(match.Groups["id"].Value)] = phase;
            }
        }

        var phases = resolution.PhaseRows
            .Select(row => NormalizePhaseId(Cell(row, "Phase ID", "Phase")))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (phases.Length > 0)
        {
            parents.TryAdd("G-D0", phases[0]);
            parents.TryAdd("G-MS5", phases[^1]);
        }

        return parents;
    }

    private static ImportWarning MissingValue(PlanningTableRow row, string code, string field) => new()
    {
        Id = $"{code}:{row.SourceRelativePath}:{row.SourceLine}",
        Severity = WarningSeverity.Error,
        Code = code,
        Message = $"Required authored field '{field}' is missing; no value was guessed.",
        SourceReferences = [row.SourceReference]
    };

    private static bool PathEquals(string left, string right) => string.Equals(left.Replace('\\', '/'), right, StringComparison.OrdinalIgnoreCase);
}
