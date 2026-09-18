using System.Globalization;
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
                SourceDocuments = documents,
                Project = project,
                Baseline = baseline,
                ValueProvenance = values,
                Warnings = warnings
            };
        }

        var phases = ExtractPhases(resolution, warnings);
        var workPackages = ExtractWorkPackages(resolution, warnings);
        var cardEvidence = new List<ExtractedCardEvidence>();
        var cards = ExtractCards(resolution, warnings, cardEvidence);
        var milestones = ExtractMilestones(resolution, warnings);
        var dependencies = ExtractDependencies(cards, cardEvidence, warnings);
        var roles = ExtractRoles(cards, cardEvidence);
        var assignments = ExtractAssignments(cards, cardEvidence);
        var policies = ExtractPolicies(resolution, warnings);
        var capacity = new CapacityPlan
        {
            CapacityHours = baseline.CapacityHours,
            SourceResourcePolicy = "WIP=1",
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
            CapturedAtUtc = null,
            SourceDocuments = documents,
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
            var id = NormalizeId(Cell(row, "Phase ID", "Phase"));
            var name = Cell(row, "Name") ?? string.Empty;
            var start = ParseDate(row, "Start", warnings);
            var finish = ParseDate(row, "Finish", warnings);
            var duration = SafeDuration(start, null, finish, null, row, warnings);
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
                PlannedStart = start ?? DateOnly.MinValue,
                PlannedFinish = finish ?? DateOnly.MinValue,
                PlannedEffortHours = null,
                PlannedEffortState = DataState.Unknown,
                PlannedDurationWorkingMinutes = duration.WorkingMinutes,
                DurationState = duration.State,
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
            var id = NormalizeId(Cell(row, "ID", "Work package ID", "Code"));
            var phaseId = NormalizeId(Cell(row, "Phase"));
            var start = ParseDate(row, "Start", warnings);
            var finish = ParseDate(row, "Finish", warnings);
            var effort = ParseDecimal(row, "Effort", warnings);
            var duration = SafeDuration(start, null, finish, null, row, warnings);
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
                Name = Cell(row, "Name") ?? string.Empty,
                ParentId = null,
                PhaseId = phaseId,
                PlannedStart = start ?? DateOnly.MinValue,
                PlannedFinish = finish ?? DateOnly.MinValue,
                PlannedEffortHours = effort,
                PlannedEffortState = effort is null ? DataState.Unknown : DataState.Known,
                PlannedDurationWorkingMinutes = duration.WorkingMinutes,
                DurationState = duration.State,
                DependencyIds = Array.Empty<string>(),
                CompletionCondition = Cell(row, "Completion condition") ?? string.Empty,
                DeliveryCardIds = Array.Empty<string>(),
                SourceReferences = [row.SourceReference]
            });
        }

        return workPackages;
    }

    private static IReadOnlyList<DeliveryCard> ExtractCards(AuthorityResolution resolution, ICollection<ImportWarning> warnings, ICollection<ExtractedCardEvidence> evidence)
    {
        var cards = new List<DeliveryCard>();
        foreach (var row in RowsFor(resolution, KanbanPath).Where(IsCardRow).OrderBy(row => row.RowIndex))
        {
            var id = NormalizeId(Cell(row, "ID"));
            var workPackageId = NormalizeId(Cell(row, "Work package"));
            var phaseId = NormalizeId(Cell(row, "Phase"));
            var start = ParseDateToken(row, "Start", warnings);
            var finish = ParseDateToken(row, "Finish", warnings);
            var effort = ParseDecimal(row, "Effort", warnings);
            var duration = SafeDuration(start.Date, start.Marker, finish.Date, finish.Marker, row, warnings);
            var state = ParseExecutionState(Cell(row, "State"), row, warnings);
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
                Name = Cell(row, "Name") ?? string.Empty,
                ParentId = workPackageId,
                PhaseId = phaseId,
                PlannedStart = start.Date ?? DateOnly.MinValue,
                PlannedFinish = finish.Date ?? DateOnly.MinValue,
                PlannedEffortHours = effort,
                PlannedEffortState = effort is null ? DataState.Unknown : DataState.Known,
                PlannedDurationWorkingMinutes = duration.WorkingMinutes,
                DurationState = duration.State,
                WorkPackageId = workPackageId,
                State = state,
                RoleAssignmentIds = [$"{id}:assignment"],
                SourceReferences = [row.SourceReference]
            });
            evidence.Add(new ExtractedCardEvidence
            {
                CardId = id,
                LogicalRoleCode = Cell(row, "Logical role") ?? string.Empty,
                CarioRoleCode = Cell(row, "CARIO") ?? string.Empty,
                PredecessorId = Cell(row, "Predecessor"),
                SourceReference = row.SourceReference
            });
        }

        return cards;
    }

    private static IReadOnlyList<MilestoneDecision> ExtractMilestones(AuthorityResolution resolution, ICollection<ImportWarning> warnings)
    {
        var milestones = new List<MilestoneDecision>();
        foreach (var row in RowsFor(resolution, GanttPath).Where(row => Cell(row, "Gate ID") is not null).OrderBy(row => row.RowIndex))
        {
            var id = NormalizeId(Cell(row, "Gate ID"));
            var date = ParseDate(row, "Date", warnings);
            if (id.Length == 0 || date is null)
            {
                warnings.Add(MissingValue(row, "MISSING_MILESTONE_VALUE", "Gate ID / Date"));
                continue;
            }

            var kindText = (Cell(row, "Kind") ?? string.Empty).Trim().ToUpperInvariant();
            var kind = kindText == "DECISION" ? MilestoneKind.Decision : MilestoneKind.Milestone;
            var parent = id switch
            {
                "G-D0" => "PH0",
                "G-MS0" => "PH1",
                "G-MS1" => "PH2",
                "G-MS2" => "PH3",
                "G-MS3" => "PH4",
                "G-MS4" or "G-MS5" => "PH5",
                _ => null
            };

            milestones.Add(new MilestoneDecision
            {
                Id = id,
                Kind = kind,
                ParentId = parent,
                Name = id,
                PlannedDate = date.Value,
                State = ExecutionState.NotStarted,
                DependencyIds = Array.Empty<string>(),
                PlannedEffortHours = 0m,
                PlannedDurationWorkingMinutes = 0,
                SourceReferences = [row.SourceReference]
            });
        }

        return milestones;
    }

    private static IReadOnlyList<Dependency> ExtractDependencies(IReadOnlyList<DeliveryCard> cards, IReadOnlyList<ExtractedCardEvidence> evidence, ICollection<ImportWarning> warnings)
    {
        var dependencies = new List<Dependency>();
        var knownCardIds = cards.Select(card => card.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            var predecessor = evidence.FirstOrDefault(item => item.CardId == card.Id)?.PredecessorId;
            if (string.IsNullOrWhiteSpace(predecessor) || predecessor is "-" or "—")
            {
                continue;
            }

            var normalizedPredecessor = NormalizeId(predecessor);
            var valid = knownCardIds.Contains(normalizedPredecessor);
            var source = card.SourceReferences;
            dependencies.Add(new Dependency
            {
                SubjectId = card.Id,
                SubjectKind = "DeliveryCard",
                PredecessorId = normalizedPredecessor,
                PredecessorKind = valid ? "DeliveryCard" : "Unknown",
                DependencyType = DependencyType.FinishToStart,
                AnalysisEligible = valid,
                ValidationState = valid ? ValidationState.Known : ValidationState.InvalidSourceEvidence,
                SourceReferences = source
            });

            if (!valid)
            {
                warnings.Add(new ImportWarning
                {
                    Id = $"MISSING_DEPENDENCY_TARGET:{card.Id}:{normalizedPredecessor}",
                    Severity = WarningSeverity.Warning,
                    Code = "MISSING_DEPENDENCY_TARGET",
                    Message = $"Delivery card '{card.Id}' names missing predecessor '{normalizedPredecessor}'; the source edge is retained but excluded from analysis.",
                    AffectedIds = [card.Id, normalizedPredecessor],
                    SourceReferences = source
                });
            }
        }

        return dependencies;
    }

    private static IReadOnlyList<ResponsibilityRole> ExtractRoles(IReadOnlyList<DeliveryCard> cards, IReadOnlyList<ExtractedCardEvidence> evidence)
    {
        var roleRows = evidence
            .Select(item => (Logical: item.LogicalRoleCode, Cario: item.CarioRoleCode, Reference: item.SourceReference))
            .ToArray();
        var roles = new List<ResponsibilityRole>();
        foreach (var cario in CarioCodes)
        {
            var reference = roleRows.FirstOrDefault(row => string.Equals(row.Cario, cario, StringComparison.OrdinalIgnoreCase)).Reference ?? new SourceReference();
            roles.Add(new ResponsibilityRole
            {
                Code = $"CARIO:{cario}",
                CarioRoleCode = cario,
                SourceMeaning = $"CARIO responsibility code {cario}",
                SourceReferences = reference.RelativeFile.Length == 0 ? Array.Empty<SourceReference>() : [reference]
            });
        }

        foreach (var logical in roleRows.Select(row => row.Logical).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal))
        {
            var reference = roleRows.First(row => string.Equals(row.Logical, logical, StringComparison.OrdinalIgnoreCase)).Reference;
            roles.Add(new ResponsibilityRole
            {
                Code = logical!,
                LogicalRoleCode = logical,
                SourceMeaning = $"Logical project role {logical}",
                SourceReferences = reference is null ? Array.Empty<SourceReference>() : [reference]
            });
        }

        return roles;
    }

    private static IReadOnlyList<Assignment> ExtractAssignments(IReadOnlyList<DeliveryCard> cards, IReadOnlyList<ExtractedCardEvidence> evidence) =>
        cards.Select(card =>
        {
            var cardEvidence = evidence.FirstOrDefault(item => item.CardId == card.Id);
            var logical = cardEvidence?.LogicalRoleCode ?? string.Empty;
            var cario = cardEvidence?.CarioRoleCode;
            return new Assignment
            {
                WorkItemId = card.Id,
                LogicalRoleCode = logical,
                CarioRoleCode = cario,
                ConcreteIdentity = null,
                MappingStatus = "UNRESOLVED",
                SourceReferences = card.SourceReferences
            };
        }).ToArray();

    private static PolicySet ExtractPolicies(AuthorityResolution resolution, ICollection<ImportWarning> warnings)
    {
        var wip = ParsePolicyInteger(resolution, "WIP policy", warnings);
        return new PolicySet
        {
            WorkInProgressLimit = wip,
            ResourceConstraint = "WIP=1",
            Rules = resolution.PolicyFacts
                .Where(pair => pair.Key is "Calendar" or "Actual progress")
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value.Value}")
                .ToArray()
        };
    }

    private static void AddAuthorityValueReferences(AuthorityResolution resolution, IReadOnlyList<PlanningTableRow> authorityRows, ICollection<ExtractedValueProvenance> values)
    {
        foreach (var key in new[] { "Project ID", "Baseline ID", "Baseline version", "Status", "Planning start", "Planning finish", "Target date", "Authoritative effort", "Initial reserve", "Capacity" })
        {
            var row = authorityRows.FirstOrDefault(candidate => string.Equals(Cell(candidate, "Field"), key, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                values.Add(new ExtractedValueProvenance { Key = key, RawValue = Cell(row, "Value"), SourceReference = row.SourceReference });
            }
        }
    }

    private static IReadOnlyList<SourceReference> FindAuthorityReferences(IEnumerable<PlanningTableRow> rows, string key) =>
        rows.Where(row => string.Equals(Cell(row, "Field"), key, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.SourceReference)
            .ToArray();

    private static IEnumerable<PlanningTableRow> RowsFor(AuthorityResolution resolution, string path) =>
        resolution.Rows.Where(row => PathEquals(row.SourceRelativePath, path));

    private static bool IsWorkPackageRow(PlanningTableRow row) =>
        RegexLikeId(Cell(row, "ID", "Work package ID", "Code")) && Cell(row, "Phase") is not null;

    private static bool IsCardRow(PlanningTableRow row) =>
        RegexLikeId(Cell(row, "ID")) && Cell(row, "Work package") is not null && Cell(row, "Logical role") is not null;

    private static bool RegexLikeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length >= 3
            && char.IsLetter(trimmed[0])
            && trimmed.Skip(1).All(character => char.IsLetterOrDigit(character) || character is '-' or '+');
    }

    private static string? Cell(PlanningTableRow row, params string[] names)
    {
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

    private static string NormalizeId(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static DateOnly? ParseDate(PlanningTableRow row, string header, ICollection<ImportWarning> warnings)
    {
        var raw = Cell(row, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
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

    private static (DateOnly? Date, string? Marker) ParseDateToken(PlanningTableRow row, string header, ICollection<ImportWarning> warnings)
    {
        var raw = Cell(row, header);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 2 || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
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

        return (date, marker);
    }

    private static decimal? ParseDecimal(PlanningTableRow row, string header, ICollection<ImportWarning> warnings)
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

        if (int.TryParse(fact.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
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

    private static ExecutionState ParseExecutionState(string? value, PlanningTableRow row, ICollection<ImportWarning> warnings)
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

    private static ExecutionState UnknownState(PlanningTableRow row, ICollection<ImportWarning> warnings)
    {
        warnings.Add(MissingValue(row, "UNKNOWN_EXECUTION_STATE", "State"));
        return ExecutionState.NotStarted;
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
