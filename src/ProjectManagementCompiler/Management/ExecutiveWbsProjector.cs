using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ExecutiveWbsProjector
{
    public ExecutiveWbsProjection Build(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var project = result.Project;
        var cardsByWorkPackage = project.DeliveryCards
            .GroupBy(card => card.WorkPackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var workPackagesByPhase = project.WorkPackages
            .GroupBy(workPackage => workPackage.PhaseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var phasesById = project.Phases.ToDictionary(phase => phase.Id, StringComparer.OrdinalIgnoreCase);
        var workPackagesById = project.WorkPackages.ToDictionary(workPackage => workPackage.Id, StringComparer.OrdinalIgnoreCase);
        var attentionByTarget = BuildAttention(project, result.Analysis);
        var rows = new List<ExecutiveWbsRow>();

        rows.Add(BuildProjectRow(project, result, attentionByTarget));

        foreach (var (phase, phaseIndex) in project.Phases.Select((value, index) => (value, index)))
        {
            var phaseCards = project.DeliveryCards
                .Where(card => string.Equals(card.PhaseId, phase.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            rows.Add(BuildPhaseRow(phase, phaseIndex, phaseCards, project, result, attentionByTarget));

            if (workPackagesByPhase.TryGetValue(phase.Id, out var workPackages))
            {
                foreach (var (workPackage, workPackageIndex) in workPackages.Select((value, index) => (value, index)))
                {
                    cardsByWorkPackage.TryGetValue(workPackage.Id, out var cards);
                    cards ??= Array.Empty<DeliveryCard>();
                    rows.Add(BuildWorkPackageRow(
                        workPackage,
                        phase,
                        phaseIndex,
                        workPackageIndex,
                        cards,
                        project,
                        result,
                        attentionByTarget));
                    rows.AddRange(cards.Select((card, cardIndex) => BuildCardRow(
                        card,
                        phase,
                        workPackage,
                        phaseIndex,
                        workPackageIndex,
                        cardIndex,
                        project,
                        result,
                        attentionByTarget)));
                }
            }

            foreach (var card in phaseCards.Where(card => !workPackagesById.ContainsKey(card.WorkPackageId)))
            {
                var cardIndex = rows.Count(row => row.Kind == ExecutiveWbsRowKind.DeliveryCard
                    && string.Equals(row.ParentReferenceCode, phase.Id, StringComparison.OrdinalIgnoreCase));
                rows.Add(BuildCardRow(
                    card,
                    phase,
                    null,
                    phaseIndex,
                    -1,
                    cardIndex,
                    project,
                    result,
                    attentionByTarget));
            }
        }

        foreach (var (workPackage, workPackageIndex) in project.WorkPackages
                     .Where(workPackage => !phasesById.ContainsKey(workPackage.PhaseId))
                     .Select((value, index) => (value, index)))
        {
            cardsByWorkPackage.TryGetValue(workPackage.Id, out var cards);
            cards ??= Array.Empty<DeliveryCard>();
            rows.Add(BuildWorkPackageRow(workPackage, null, -1, workPackageIndex, cards, project, result, attentionByTarget));
            rows.AddRange(cards.Select((card, cardIndex) => BuildCardRow(
                card,
                null,
                workPackage,
                -1,
                workPackageIndex,
                cardIndex,
                project,
                result,
                attentionByTarget)));
        }

        foreach (var card in project.DeliveryCards.Where(card => !workPackagesById.ContainsKey(card.WorkPackageId)))
        {
            if (rows.Any(row => row.Kind == ExecutiveWbsRowKind.DeliveryCard
                && string.Equals(row.ReferenceCode, card.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            rows.Add(BuildCardRow(card, phasesById.GetValueOrDefault(card.PhaseId), null, -1, -1, rows.Count, project, result, attentionByTarget));
        }

        return new ExecutiveWbsProjection
        {
            Rows = rows,
            ProjectCount = rows.Count(row => row.Kind == ExecutiveWbsRowKind.Project),
            PhaseCount = rows.Count(row => row.Kind == ExecutiveWbsRowKind.Phase),
            WorkPackageCount = rows.Count(row => row.Kind == ExecutiveWbsRowKind.WorkPackage),
            DeliveryCardCount = rows.Count(row => row.Kind == ExecutiveWbsRowKind.DeliveryCard)
        };
    }

    private static ExecutiveWbsRow BuildProjectRow(
        CanonicalProject project,
        CompilationResult result,
        IReadOnlyDictionary<string, string> attentionByTarget) =>
        BuildRollupRow(
            ExecutiveWbsRowKind.Project,
            "1",
            project.Project.Id,
            project.Project.Name,
            null,
            0,
            project.Baseline.PlanningStart,
            project.Baseline.PlanningFinish,
            project.Baseline.PlannedEffortHours,
            project.DeliveryCards,
            project,
            result,
            attentionByTarget,
            0,
            project.Project.Id,
            project.Project.SourceIds.Select(id => new SourceReference { SourceId = id }).ToArray());

    private static ExecutiveWbsRow BuildPhaseRow(
        Phase phase,
        int phaseIndex,
        IReadOnlyList<DeliveryCard> cards,
        CanonicalProject project,
        CompilationResult result,
        IReadOnlyDictionary<string, string> attentionByTarget) =>
        BuildRollupRow(
            ExecutiveWbsRowKind.Phase,
            $"1.{phaseIndex + 1}",
            phase.Id,
            phase.Name,
            project.Project.Id,
            1,
            phase.PlannedStart,
            phase.PlannedFinish,
            phase.PlannedEffortHours,
            cards,
            project,
            result,
            attentionByTarget,
            phaseIndex,
            phase.Id,
            phase.SourceReferences);

    private static ExecutiveWbsRow BuildWorkPackageRow(
        WorkPackage workPackage,
        Phase? phase,
        int phaseIndex,
        int workPackageIndex,
        IReadOnlyList<DeliveryCard> cards,
        CanonicalProject project,
        CompilationResult result,
        IReadOnlyDictionary<string, string> attentionByTarget)
    {
        var phaseNumber = phaseIndex + 1;
        var wbsNumber = phaseIndex >= 0 ? $"1.{phaseNumber}.{workPackageIndex + 1}" : $"1.0.{workPackageIndex + 1}";
        return BuildRollupRow(
            ExecutiveWbsRowKind.WorkPackage,
            wbsNumber,
            workPackage.Id,
            workPackage.Name,
            phase?.Id,
            2,
            workPackage.PlannedStart,
            workPackage.PlannedFinish,
            workPackage.PlannedEffortHours,
            cards,
            project,
            result,
            attentionByTarget,
            workPackageIndex,
            workPackage.Id,
            workPackage.SourceReferences,
            workPackage.DependencyIds);
    }

    private static ExecutiveWbsRow BuildCardRow(
        DeliveryCard card,
        Phase? phase,
        WorkPackage? workPackage,
        int phaseIndex,
        int workPackageIndex,
        int cardIndex,
        CanonicalProject project,
        CompilationResult result,
        IReadOnlyDictionary<string, string> attentionByTarget)
    {
        var record = ExecutionTruthResolver.ForCard(project, card.Id);
        var isRecorded = record?.IsRecorded == true;
        var progress = CalculateProgress(record);
        var parentCode = workPackage?.Id ?? phase?.Id;
        var wbsNumber = phaseIndex >= 0 && workPackageIndex >= 0
            ? $"1.{phaseIndex + 1}.{workPackageIndex + 1}.{cardIndex + 1}"
            : phaseIndex >= 0
                ? $"1.{phaseIndex + 1}.0.{cardIndex + 1}"
                : $"1.0.0.{cardIndex + 1}";
        var attention = attentionByTarget.GetValueOrDefault(card.Id, "—");
        return new ExecutiveWbsRow
        {
            WbsNumber = wbsNumber,
            ReferenceCode = card.Id,
            DisplayName = ReaderFacingTextPolicy.CleanName(card.Name, card.Id),
            Kind = ExecutiveWbsRowKind.DeliveryCard,
            ParentReferenceCode = parentCode,
            Depth = 3,
            OwnerLabel = ResolveOwner(project, card.Id),
            StateLabel = isRecorded ? ExecutionLabel(record!.ExecutionState) : ReaderFacingTextPolicy.MissingEvidenceLabel,
            ProgressPercent = progress,
            ProgressLabel = progress is null ? ReaderFacingTextPolicy.MissingEvidenceLabel : $"{progress}%",
            AttentionLabel = attention,
            PlannedStart = card.PlannedStart,
            PlannedFinish = card.PlannedFinish,
            PlannedEffortHours = card.PlannedEffortHours,
            ActualStart = isRecorded ? record!.ActualStart : null,
            ActualFinish = isRecorded ? record!.ActualFinish : null,
            ActualEffortHours = isRecorded ? record!.ActualEffortHours : null,
            RemainingEffortHours = isRecorded ? record!.RemainingEffortHours : null,
            LastOfficialUpdate = isRecorded ? record!.LastUpdatedAt : null,
            PredecessorCodes = Predecessors(project, card.Id),
            DependencyLabel = DependencyLabel(project, card.Id),
            EvidenceSummary = isRecorded
                ? progress is null ? "Có ghi nhận · Chưa đủ dữ liệu tiến độ" : $"Có ghi nhận · {progress}%"
                : ReaderFacingTextPolicy.MissingEvidenceLabel,
            SourceReferenceLabel = SourceReferenceLabel(card.SourceReferences),
            SourceOrder = cardIndex
        };
    }

    private static ExecutiveWbsRow BuildRollupRow(
        ExecutiveWbsRowKind kind,
        string wbsNumber,
        string referenceCode,
        string name,
        string? parentReferenceCode,
        int depth,
        DateOnly? plannedStart,
        DateOnly? plannedFinish,
        decimal? plannedEffortHours,
        IReadOnlyList<DeliveryCard> cards,
        CanonicalProject project,
        CompilationResult result,
        IReadOnlyDictionary<string, string> attentionByTarget,
        int sourceOrder,
        string sourceId,
        IReadOnlyList<SourceReference> sourceReferences,
        IReadOnlyList<string>? explicitPredecessors = null)
    {
        var records = cards.Select(card => ExecutionTruthResolver.ForCard(project, card.Id)).ToArray();
        var recorded = records.Where(record => record?.IsRecorded == true).Select(record => record!).ToArray();
        var progress = records.Length > 0 && records.All(record => record?.IsRecorded == true && IsProgressEligible(record!))
            ? CalculateProgress(recorded)
            : null;
        var starts = recorded.Select(record => record.ActualStart).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var finishes = recorded.Select(record => record.ActualFinish).Where(value => value is not null).Select(value => value!.Value).ToArray();
        var actualEffort = Sum(recorded.Select(record => record.ActualEffortHours));
        var remaining = Sum(recorded.Select(record => record.RemainingEffortHours));
        var attention = attentionByTarget.GetValueOrDefault(referenceCode, attentionByTarget.Values.FirstOrDefault(value => value != "—") ?? "—");
        var predecessors = explicitPredecessors?.Count > 0
            ? explicitPredecessors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : Predecessors(project, referenceCode);
        var evidence = cards.Count == 0
            ? ReaderFacingTextPolicy.MissingEvidenceLabel
            : $"Độ phủ {recorded.Length}/{cards.Count}" + (progress is null ? " · Chưa đủ dữ liệu tiến độ" : $" · {progress}%");

        return new ExecutiveWbsRow
        {
            WbsNumber = wbsNumber,
            ReferenceCode = referenceCode,
            DisplayName = ReaderFacingTextPolicy.CleanName(name, referenceCode),
            Kind = kind,
            ParentReferenceCode = parentReferenceCode,
            Depth = depth,
            OwnerLabel = ResolveRollupOwner(project, cards),
            StateLabel = RollupState(records),
            ProgressPercent = progress,
            ProgressLabel = progress is null ? ReaderFacingTextPolicy.MissingEvidenceLabel : $"{progress}%",
            AttentionLabel = attention,
            PlannedStart = plannedStart,
            PlannedFinish = plannedFinish,
            PlannedEffortHours = plannedEffortHours,
            ActualStart = starts.Length == 0 ? null : starts.Min(),
            ActualFinish = finishes.Length == 0 ? null : finishes.Max(),
            ActualEffortHours = actualEffort,
            RemainingEffortHours = remaining,
            LastOfficialUpdate = recorded.Select(record => record.LastUpdatedAt).Where(value => value is not null).OrderByDescending(value => value).FirstOrDefault(),
            PredecessorCodes = predecessors,
            DependencyLabel = DependencyLabel(project, referenceCode),
            EvidenceSummary = evidence,
            SourceReferenceLabel = SourceReferenceLabel(sourceReferences),
            SourceOrder = sourceOrder
        };
    }

    private static IReadOnlyDictionary<string, string> BuildAttention(CanonicalProject project, ManagementAnalysis analysis)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alert in analysis.Alerts.OrderBy(alert => alert.WorkItemId, StringComparer.Ordinal))
        {
            var label = alert.AlertCode.Trim().ToUpperInvariant() switch
            {
                "BLOCKED" or "SUSPENDED" => "Bị chặn",
                "OVERDUE" or "START_DELAY" => "Quá hạn",
                "AT_RISK" => "Có nguy cơ",
                _ => "—"
            };
            if (label != "—")
            {
                result.TryAdd(alert.WorkItemId, label);
            }
        }

        return result;
    }

    private static IReadOnlyList<string> Predecessors(CanonicalProject project, string id) =>
        project.Dependencies
            .Where(dependency => string.Equals(dependency.SubjectId, id, StringComparison.OrdinalIgnoreCase))
            .Select(dependency => dependency.PredecessorId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string DependencyLabel(CanonicalProject project, string id)
    {
        var dependencies = project.Dependencies
            .Where(dependency => string.Equals(dependency.SubjectId, id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return dependencies.Length == 0
            ? "Không có tiền nhiệm"
            : dependencies.All(dependency => dependency.ValidationState == ValidationState.Known)
                ? "Đã xác định"
                : "Cần kiểm tra";
    }

    private static string ResolveOwner(CanonicalProject project, string id)
    {
        var assignments = project.Assignments
            .Where(assignment => string.Equals(assignment.WorkItemId, id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(assignment.CarioRoleCode, "A", StringComparison.OrdinalIgnoreCase))
            .Select(assignment => (assignment.ConcreteIdentity?.Trim(), assignment.LogicalRoleCode?.Trim()))
            .Distinct()
            .ToArray();
        if (assignments.Length != 1)
        {
            return ReaderFacingTextPolicy.MissingOwnerLabel;
        }

        return !string.IsNullOrWhiteSpace(assignments[0].Item1)
            ? assignments[0].Item1!
            : ReaderFacingTextPolicy.OwnerOrMissing(RoleLabel(assignments[0].Item2));
    }

    private static string ResolveRollupOwner(CanonicalProject project, IReadOnlyList<DeliveryCard> cards)
    {
        var owners = cards.Select(card => ResolveOwner(project, card.Id))
            .Where(owner => owner != ReaderFacingTextPolicy.MissingOwnerLabel)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return owners.Length == 1 && cards.Count > 0 && cards.All(card => ResolveOwner(project, card.Id) == owners[0])
            ? owners[0]
            : ReaderFacingTextPolicy.MissingOwnerLabel;
    }

    private static string RollupState(IReadOnlyList<EffectiveExecutionRecord?> records)
    {
        if (records.Count == 0 || records.Any(record => record?.IsRecorded != true))
        {
            return ReaderFacingTextPolicy.MissingEvidenceLabel;
        }

        if (records.Any(record => record!.ExecutionState == ExecutionState.InProgress))
        {
            return "Đang thực hiện";
        }

        var states = records.Select(record => record!.ExecutionState).Distinct().ToArray();
        return states.Length == 1 ? ExecutionLabel(states[0]) : "Đang thực hiện";
    }

    private static int? CalculateProgress(EffectiveExecutionRecord? record)
    {
        if (record?.IsRecorded != true || !IsProgressEligible(record))
        {
            return null;
        }

        return decimal.ToInt32(decimal.Round(
            record.ActualEffortHours!.Value / (record.ActualEffortHours.Value + record.RemainingEffortHours!.Value) * 100m,
            0,
            MidpointRounding.AwayFromZero));
    }

    private static int? CalculateProgress(IReadOnlyList<EffectiveExecutionRecord> records)
    {
        var actual = records.Sum(record => record.ActualEffortHours!.Value);
        var remaining = records.Sum(record => record.RemainingEffortHours!.Value);
        return actual + remaining > 0m
            ? decimal.ToInt32(decimal.Round(actual / (actual + remaining) * 100m, 0, MidpointRounding.AwayFromZero))
            : null;
    }

    private static bool IsProgressEligible(EffectiveExecutionRecord record) =>
        record.IsRecorded
        && record.ActualEffortHours is not null
        && record.RemainingEffortHours is not null
        && record.ActualEffortHours >= 0m
        && record.RemainingEffortHours >= 0m
        && record.ActualEffortHours + record.RemainingEffortHours > 0m;

    private static decimal? Sum(IEnumerable<decimal?> values)
    {
        var known = values.Where(value => value is not null).Select(value => value!.Value).ToArray();
        return known.Length == 0 ? null : known.Sum();
    }

    private static string SourceReferenceLabel(IReadOnlyList<SourceReference> references)
    {
        var reference = references.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value.RelativeFile));
        if (reference is null || Path.IsPathFullyQualified(reference.RelativeFile))
        {
            return ReaderFacingTextPolicy.MissingEvidenceLabel;
        }

        var label = reference.RelativeFile.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(reference.Section))
        {
            label += $" · {reference.Section}";
        }

        return label;
    }

    private static string? RoleLabel(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "PM" => "Quản lý dự án",
        "LEAD" => "Đầu mối dự án",
        "DEV" or "DEV2" => "Nhóm phát triển",
        "QA" => "Đảm bảo chất lượng",
        "OWNER" => "Đầu mối chịu trách nhiệm",
        "OPS" or "OPERATIONS" => "Vận hành",
        "SPEC" => "Chuyên gia chuyên môn",
        _ => null
    };

    private static string ExecutionLabel(ExecutionState? state) => state switch
    {
        ExecutionState.NotStarted => "Chưa bắt đầu",
        ExecutionState.InProgress => "Đang thực hiện",
        ExecutionState.Completed => "Hoàn thành",
        ExecutionState.Suspended => "Tạm dừng",
        ExecutionState.Cancelled => "Đã hủy",
        _ => ReaderFacingTextPolicy.MissingEvidenceLabel
    };
}
