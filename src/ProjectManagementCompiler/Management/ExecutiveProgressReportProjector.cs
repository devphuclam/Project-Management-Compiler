using System.Globalization;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class ExecutiveProgressReportProjector
{
    public ExecutiveProgressReport Build(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var project = result.Project;
        var metadata = project.ImportMetadata
            ?? throw new InvalidOperationException("An official manifest snapshot is required for an executive progress report.");
        var sourceReportingDate = metadata.RegisterStatusDate
            ?? throw new InvalidOperationException("The official manifest snapshot has no reporting date.");
        var analysisAsOfDate = result.Analysis.AsOfDate
            ?? throw new InvalidOperationException("The official analysis has no as-of date.");

        var phaseEntries = project.Phases
            .Select((phase, index) => new PhaseEntry(phase, index))
            .ToArray();
        var currentPhase = phaseEntries
            .Where(entry => IsWithin(sourceReportingDate, entry.Phase.PlannedStart, entry.Phase.PlannedFinish))
            .OrderByDescending(entry => entry.Phase.PlannedStart)
            .ThenBy(entry => entry.Phase.PlannedFinish ?? DateOnly.MaxValue)
            .ThenBy(entry => entry.SourceOrder)
            .FirstOrDefault();

        var milestoneEntries = project.Milestones
            .Select((milestone, index) => new MilestoneEntry(milestone, index))
            .Where(entry => entry.Milestone.Kind == MilestoneKind.Milestone && entry.Milestone.PlannedDate is not null)
            .OrderBy(entry => entry.Milestone.PlannedDate)
            .ThenBy(entry => entry.SourceOrder)
            .ToArray();
        var nextMilestone = milestoneEntries
            .FirstOrDefault(entry => entry.Milestone.PlannedDate >= sourceReportingDate);

        var overviewTimeline = phaseEntries
            .Select(entry => new ExecutiveScheduleRow
            {
                Kind = ExecutiveScheduleRowKind.Phase,
                DisplayName = CleanName(entry.Phase.Name),
                PlannedStart = entry.Phase.PlannedStart,
                PlannedFinish = entry.Phase.PlannedFinish,
                StateLabel = "Chưa cập nhật",
                OwnerLabel = "Chưa xác định đầu mối",
                IsCurrent = currentPhase is not null && currentPhase.SourceOrder == entry.SourceOrder,
                IsNextMilestone = false,
                SourceOrder = entry.SourceOrder
            })
            .Concat(milestoneEntries.Select(entry => new ExecutiveScheduleRow
            {
                Kind = ExecutiveScheduleRowKind.Milestone,
                DisplayName = CleanName(entry.Milestone.Name),
                PlannedStart = entry.Milestone.PlannedDate,
                PlannedFinish = entry.Milestone.PlannedDate,
                StateLabel = ExecutionLabel(entry.Milestone.State),
                OwnerLabel = "Chưa xác định đầu mối",
                IsCurrent = false,
                IsNextMilestone = nextMilestone is not null && nextMilestone.SourceOrder == entry.SourceOrder,
                SourceOrder = entry.SourceOrder
            }))
            .ToArray();

        var workPackageSchedule = ProjectWorkPackages(project, sourceReportingDate);
        var deliveryCardDetails = ProjectDeliveryCards(project);
        var allAttention = ProjectAttention(project, result.Analysis, sourceReportingDate, nextMilestone);

        return new ExecutiveProgressReport
        {
            ProjectName = CleanName(project.Project.Name),
            SourceReportingDate = sourceReportingDate,
            AnalysisAsOfDate = analysisAsOfDate,
            PlanningStart = project.Baseline.PlanningStart,
            PlanningFinish = project.Baseline.PlanningFinish,
            CurrentPhase = currentPhase is null
                ? "Chưa xác định giai đoạn hiện tại"
                : CleanName(currentPhase.Phase.Name),
            ScheduleCondition = ProjectScheduleCondition(project, result.Analysis),
            ReadinessCondition = ProjectReadinessCondition(project),
            NextMilestone = nextMilestone is null
                ? new ExecutiveMilestoneSummary
                {
                    DisplayName = "Chưa xác định mốc sắp tới",
                    IsMissing = true
                }
                : new ExecutiveMilestoneSummary
                {
                    DisplayName = CleanName(nextMilestone.Milestone.Name),
                    Kind = nextMilestone.Milestone.Kind,
                    PlannedDate = nextMilestone.Milestone.PlannedDate,
                    IsMissing = false
                },
            Progress = ProjectProgress(result.Analysis),
            OverviewAttention = allAttention.Where(item => item.OverviewEligible).Take(5).ToArray(),
            AllAttention = allAttention,
            OverviewTimeline = overviewTimeline,
            WorkPackageSchedule = workPackageSchedule,
            DeliveryCardDetails = deliveryCardDetails
        };
    }

    private static IReadOnlyList<ExecutiveScheduleRow> ProjectWorkPackages(CanonicalProject project, DateOnly sourceReportingDate)
    {
        var phases = project.Phases.ToDictionary(phase => phase.Id, StringComparer.OrdinalIgnoreCase);
        return project.WorkPackages
            .Select((workPackage, sourceOrder) =>
            {
                phases.TryGetValue(workPackage.PhaseId, out var phase);
                var children = project.DeliveryCards
                    .Where(card => string.Equals(card.WorkPackageId, workPackage.Id, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return new ExecutiveScheduleRow
                {
                    Kind = ExecutiveScheduleRowKind.WorkPackage,
                    DisplayName = CleanName(workPackage.Name, workPackage.Id),
                    PhaseDisplayName = phase is null ? "Chưa xác định" : CleanName(phase.Name, phase.Id),
                    PlannedStart = workPackage.PlannedStart,
                    PlannedFinish = workPackage.PlannedFinish,
                    StateLabel = WorkPackageStateLabel(project, children),
                    OwnerLabel = ResolveWorkPackageOwner(project, workPackage, children),
                    IsCurrent = IsWithin(sourceReportingDate, workPackage.PlannedStart, workPackage.PlannedFinish),
                    IsNextMilestone = false,
                    SourceOrder = sourceOrder
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<ExecutiveDeliveryCardDetail> ProjectDeliveryCards(CanonicalProject project)
    {
        var phases = project.Phases.ToDictionary(phase => phase.Id, StringComparer.OrdinalIgnoreCase);
        var workPackages = project.WorkPackages.ToDictionary(workPackage => workPackage.Id, StringComparer.OrdinalIgnoreCase);
        return project.DeliveryCards
            .Select((card, sourceOrder) =>
            {
                phases.TryGetValue(card.PhaseId, out var phase);
                workPackages.TryGetValue(card.WorkPackageId, out var workPackage);
                var record = ExecutionTruthResolver.ForCard(project, card.Id);
                return new ExecutiveDeliveryCardDetail
                {
                    Description = CleanCardDescription(card.Name, card.Id),
                    PhaseName = phase is null ? "Chưa xác định" : CleanName(phase.Name, phase.Id),
                    WorkPackageName = workPackage is null ? "Chưa xác định" : CleanName(workPackage.Name, workPackage.Id),
                    PlannedStart = card.PlannedStart,
                    PlannedFinish = card.PlannedFinish,
                    OwnerLabel = ResolveDeliveryCardOwner(project, card.Id),
                    StateLabel = record?.IsRecorded == true ? ExecutionLabel(record.ExecutionState) : "Chưa cập nhật",
                    ReferenceCode = card.Id,
                    SourceOrder = sourceOrder
                };
            })
            .ToArray();
    }

    private static string WorkPackageStateLabel(CanonicalProject project, IReadOnlyList<DeliveryCard> children)
    {
        if (children.Count == 0)
        {
            return "Chưa cập nhật";
        }

        var records = children
            .Select(card => ExecutionTruthResolver.ForCard(project, card.Id))
            .ToArray();
        if (records.Any(record => record?.IsRecorded == true && record.ExecutionState == ExecutionState.InProgress))
        {
            return "Đang thực hiện";
        }

        if (records.All(record => record?.IsRecorded == true && record.ExecutionState == ExecutionState.Completed))
        {
            return "Hoàn thành";
        }

        if (records.All(record => record?.IsRecorded == true && record.ExecutionState == ExecutionState.NotStarted))
        {
            return "Chưa bắt đầu";
        }

        if (records.All(record => record?.IsRecorded == true && record.ExecutionState == ExecutionState.Suspended))
        {
            return "Tạm dừng";
        }

        if (records.All(record => record?.IsRecorded == true && record.ExecutionState == ExecutionState.Cancelled))
        {
            return "Đã hủy";
        }

        return "Chưa cập nhật";
    }

    private static string ResolveDeliveryCardOwner(CanonicalProject project, string cardId)
    {
        var accountable = project.Assignments
            .Where(assignment => string.Equals(assignment.WorkItemId, cardId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(assignment.CarioRoleCode, "A", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return ResolveAssignments(accountable);
    }

    private static string ResolveWorkPackageOwner(CanonicalProject project, WorkPackage workPackage, IReadOnlyList<DeliveryCard> children)
    {
        var direct = ValidEvidence(project)
            .Where(observation => observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck
                && string.Equals(observation.ExplicitTarget?.Kind, "WorkPackage", StringComparison.OrdinalIgnoreCase)
                && string.Equals(observation.ExplicitTarget?.Id, workPackage.Id, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(observation.OwnerRole))
            .Select(observation => RoleLabel(observation.OwnerRole))
            .Where(label => label is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (direct.Length == 1)
        {
            return direct[0]!;
        }

        var childOwners = children
            .Select(card => ResolveDeliveryCardOwner(project, card.Id))
            .ToArray();
        return childOwners.Length > 0
            && childOwners.All(owner => owner != "Chưa xác định đầu mối")
            && childOwners.Distinct(StringComparer.Ordinal).Count() == 1
            ? childOwners[0]
            : "Chưa xác định đầu mối";
    }

    private static string ResolveAssignments(IReadOnlyList<Assignment> assignments)
    {
        var distinct = assignments
            .Select(assignment => (Identity: assignment.ConcreteIdentity?.Trim(), Role: assignment.LogicalRoleCode?.Trim()))
            .Distinct()
            .ToArray();
        if (distinct.Length != 1)
        {
            return "Chưa xác định đầu mối";
        }

        var identity = distinct[0].Identity;
        if (!string.IsNullOrWhiteSpace(identity))
        {
            return identity;
        }

        return RoleLabel(distinct[0].Role) ?? "Chưa xác định đầu mối";
    }

    private static string? RoleLabel(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return role.Trim().ToUpperInvariant() switch
        {
            "PM" => "Quản lý dự án",
            "LEAD" => "Đầu mối dự án",
            "DEV" or "DEV2" => "Nhóm phát triển",
            "QA" => "Đảm bảo chất lượng",
            "PDA" or "PRODUCT_DECISION_AUTHORITY" => "Thẩm quyền quyết định sản phẩm",
            "PROC" => "Phụ trách quy trình",
            "STAKE" => "Bên liên quan",
            "OWNER" => "Đầu mối chịu trách nhiệm",
            "QLHT" => "Quản lý hệ thống",
            "HTKT" => "Hỗ trợ kỹ thuật",
            "OPS" or "OPERATIONS" => "Vận hành",
            "DATA" or "DATA_CUSTODIAN" => "Đầu mối quản trị dữ liệu",
            "SPEC" => "Chuyên gia chuyên môn",
            "PILOT" => "Đầu mối thử nghiệm",
            "GATE_AUTHORITY" => "Thẩm quyền phê duyệt cổng",
            "ENGINEERING_AUTHORITY" => "Thẩm quyền kỹ thuật",
            "PROJECT_AUTHORITY" => "Thẩm quyền dự án",
            "PRODUCT_AUTHOR" => "Chủ trì sản phẩm",
            "PROJECT_REVIEWER" => "Người duyệt dự án",
            "VERIFICATION_REVIEWER" => "Người duyệt xác minh",
            "SECURITY_REVIEWER" => "Người duyệt an toàn",
            "AUTHORITY" => "Thẩm quyền phê duyệt",
            _ => null
        };
    }

    private static IReadOnlyList<ExecutiveAttentionItem> ProjectAttention(
        CanonicalProject project,
        ManagementAnalysis analysis,
        DateOnly sourceReportingDate,
        MilestoneEntry? nextMilestone)
    {
        var candidates = new List<AttentionCandidate>();
        var cardsById = project.DeliveryCards.ToDictionary(card => card.Id, StringComparer.OrdinalIgnoreCase);
        var validEvidence = ValidEvidence(project);
        var sourceOrder = 0;

        foreach (var alert in analysis.Alerts
                     .OrderBy(alert => alert.WorkItemId, StringComparer.Ordinal)
                     .ThenBy(alert => alert.AlertCode, StringComparer.Ordinal))
        {
            var category = alert.AlertCode.Trim().ToUpperInvariant() switch
            {
                "BLOCKED" => ExecutiveAttentionCategory.Blocked,
                "OVERDUE" or "START_DELAY" => ExecutiveAttentionCategory.Overdue,
                "AT_RISK" => ExecutiveAttentionCategory.AtRisk,
                _ => (ExecutiveAttentionCategory?)null
            };
            if (category is null)
            {
                continue;
            }

            cardsById.TryGetValue(alert.WorkItemId, out var card);
            var owner = card is null ? "Chưa xác định đầu mối" : ResolveDeliveryCardOwner(project, card.Id);
            var dueDate = card is null
                ? null
                : category == ExecutiveAttentionCategory.Overdue && string.Equals(alert.AlertCode, "START_DELAY", StringComparison.OrdinalIgnoreCase)
                    ? card.PlannedStart
                    : card.PlannedFinish;
            var cleanName = card is null ? "hạng mục" : CleanCardDescription(card.Name, card.Id);
            var impact = category switch
            {
                ExecutiveAttentionCategory.Blocked => "Hạng mục đang bị chặn.",
                ExecutiveAttentionCategory.Overdue => $"Hạng mục đã quá ngày kế hoạch {FormatDate(dueDate)}.",
                _ => "Hạng mục được ghi nhận có nguy cơ."
            };
            candidates.Add(new AttentionCandidate
            {
                Category = category.Value,
                Action = $"Xử lý hạng mục “{cleanName}”.",
                Impact = impact,
                OwnerLabel = owner,
                DueDate = dueDate,
                DueLabel = FormatDate(dueDate),
                OverviewEligible = true,
                DeduplicationKey = $"alert:{alert.AlertCode}:{alert.WorkItemId}",
                SourceOrder = sourceOrder++
            });

            if (owner == "Chưa xác định đầu mối")
            {
                candidates.Add(new AttentionCandidate
                {
                    Category = ExecutiveAttentionCategory.MissingOwner,
                    Action = $"Xử lý hạng mục “{cleanName}”.",
                    Impact = "Hạng mục quan trọng chưa xác định đầu mối.",
                    OwnerLabel = owner,
                    DueDate = dueDate,
                    DueLabel = FormatDate(dueDate),
                    OverviewEligible = true,
                    DeduplicationKey = $"missing-owner:{alert.WorkItemId}",
                    SourceOrder = sourceOrder++
                });
            }
        }

        foreach (var (observation, index) in validEvidence.Select((observation, index) => (observation, index)))
        {
            if (observation.EvidenceKind == ManagementEvidenceKind.DecisionRecord
                && string.Equals(observation.StateCode, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                var action = FirstMeaningful(observation.ActionSummary, observation.Summary);
                var impact = ConsequenceLabel(FirstMeaningful(observation.GateEffectSummary, observation.AffectedTargetSummary, observation.CompletionCondition));
                if (action is null || impact is null)
                {
                    continue;
                }

                var attribution = AttributeDecision(project, observation, nextMilestone);
                candidates.Add(new AttentionCandidate
                {
                    Category = attribution is null ? ExecutiveAttentionCategory.OtherDecision : ExecutiveAttentionCategory.DecisionBeforeNextMilestone,
                    Action = action,
                    Impact = impact,
                    OwnerLabel = RoleLabel(observation.RequiredAuthorityRole) ?? "Chưa xác định đầu mối",
                    DueDate = attribution?.DueDate,
                    DueLabel = attribution?.DueLabel ?? "Chưa xác định",
                    OverviewEligible = attribution is not null,
                    DeduplicationKey = $"evidence:{observation.Id}",
                    SourceOrder = 10_000 + index
                });
            }

            if (observation.EvidenceKind == ManagementEvidenceKind.HumanAction
                && observation.StateCode is not null
                && (string.Equals(observation.StateCode, "OPEN", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(observation.StateCode, "NOT-RUN", StringComparison.OrdinalIgnoreCase)))
            {
                var action = FirstMeaningful(observation.ActionSummary, observation.Summary);
                var impact = ConsequenceLabel(FirstMeaningful(observation.AffectedTargetSummary, observation.CompletionCondition, observation.GateEffectSummary));
                if (action is null || impact is null)
                {
                    continue;
                }

                candidates.Add(new AttentionCandidate
                {
                    Category = ExecutiveAttentionCategory.PendingAction,
                    Action = action,
                    Impact = impact,
                    OwnerLabel = RoleLabel(observation.WaitingForRole) ?? RoleLabel(observation.RequiredAuthorityRole) ?? "Chưa xác định đầu mối",
                    DueDate = null,
                    DueLabel = "Chưa xác định",
                    OverviewEligible = false,
                    DeduplicationKey = $"evidence:{observation.Id}",
                    SourceOrder = 10_000 + index
                });
            }
        }

        return candidates
            .GroupBy(candidate => candidate.DeduplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(candidate => CategoryOrder(candidate.Category)).ThenBy(candidate => candidate.SourceOrder).First())
            .OrderBy(candidate => CategoryOrder(candidate.Category))
            .ThenBy(candidate => candidate.DueDate ?? DateOnly.MaxValue)
            .ThenBy(candidate => candidate.SourceOrder)
            .ThenBy(candidate => candidate.DeduplicationKey, StringComparer.Ordinal)
            .Select(candidate => new ExecutiveAttentionItem
            {
                Category = candidate.Category,
                Action = candidate.Action,
                Impact = candidate.Impact,
                OwnerLabel = candidate.OwnerLabel,
                DueDate = candidate.DueDate,
                DueLabel = candidate.DueLabel,
                OverviewEligible = candidate.OverviewEligible,
                DeduplicationKey = candidate.DeduplicationKey,
                SourceOrder = candidate.SourceOrder
            })
            .ToArray();
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ValidEvidence(CanonicalProject project)
    {
        var reconciliations = project.ManagementEvidence.Reconciliations
            .GroupBy(item => item.ObservationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Status, StringComparer.OrdinalIgnoreCase);
        return project.ManagementEvidence.Observations
            .Where(observation => observation.ValidationState == ValidationState.Known)
            .Where(observation => !reconciliations.TryGetValue(observation.Id, out var status)
                || status is not (EvidenceReconciliationStatus.Invalid or EvidenceReconciliationStatus.Ambiguous))
            .ToArray();
    }

    private static int CategoryOrder(ExecutiveAttentionCategory category) => category switch
    {
        ExecutiveAttentionCategory.Blocked => 0,
        ExecutiveAttentionCategory.Overdue => 1,
        ExecutiveAttentionCategory.DecisionBeforeNextMilestone => 2,
        ExecutiveAttentionCategory.AtRisk => 3,
        ExecutiveAttentionCategory.MissingOwner => 4,
        ExecutiveAttentionCategory.OtherDecision => 5,
        ExecutiveAttentionCategory.PendingAction => 6,
        _ => int.MaxValue
    };

    private static string? FirstMeaningful(params string?[] values) =>
        values.Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? ConsequenceLabel(string? value)
    {
        var cleaned = FirstMeaningful(value);
        if (cleaned is null)
        {
            return null;
        }

        var token = cleaned.ToUpperInvariant();
        if (token == "NONE")
        {
            return null;
        }

        if (token == "AUTHORIZATION")
        {
            return "Cần phê duyệt trước khi tiếp tục.";
        }

        if (token == "DEFERRED_SCOPE")
        {
            return "Ảnh hưởng phạm vi đang được hoãn.";
        }

        if (token.StartsWith("BLOCKS_", StringComparison.Ordinal))
        {
            return $"Có thể chặn cổng {CleanName(cleaned["BLOCKS_".Length..])}.";
        }

        var looksTechnical = cleaned.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');
        return looksTechnical && cleaned == token ? null : CleanName(cleaned);
    }

    private static DecisionAttribution? AttributeDecision(CanonicalProject project, ManagementEvidenceObservation observation, MilestoneEntry? nextMilestone)
    {
        if (nextMilestone is null || nextMilestone.Milestone.PlannedDate is null)
        {
            return null;
        }

        var milestone = nextMilestone.Milestone;
        var milestoneId = milestone.Id;
        var strippedMilestoneId = StripLeadingGatePrefix(milestoneId);
        if (MatchesId(observation.SourceRecordId, milestoneId, strippedMilestoneId))
        {
            return DateAttribution(milestone.PlannedDate.Value);
        }

        if (observation.ExplicitTarget is { } target
            && string.Equals(target.Kind, "Milestone", StringComparison.OrdinalIgnoreCase)
            && MatchesId(target.Id, milestoneId, strippedMilestoneId))
        {
            return DateAttribution(milestone.PlannedDate.Value);
        }

        var reconciled = project.ManagementEvidence.Reconciliations
            .Where(item => string.Equals(item.ObservationId, observation.Id, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.ResolvedTarget)
            .FirstOrDefault(target => target is not null
                && string.Equals(target.Kind, "Milestone", StringComparison.OrdinalIgnoreCase));
        if (reconciled is not null && MatchesId(reconciled.Id, milestoneId, strippedMilestoneId))
        {
            return DateAttribution(milestone.PlannedDate.Value);
        }

        var dueDate = FindIsoDate(observation.DueCondition);
        if (dueDate is not null && dueDate <= milestone.PlannedDate)
        {
            return DateAttribution(dueDate.Value);
        }

        var beforePackages = project.WorkPackages
            .Where(workPackage => ContainsWholeToken(observation.DueCondition, workPackage.Id))
            .ToArray();
        if (observation.DueCondition?.TrimStart().StartsWith("Before", StringComparison.OrdinalIgnoreCase) == true
            && beforePackages.Length > 0
            && beforePackages.All(workPackage => workPackage.PlannedStart is not null && workPackage.PlannedStart <= milestone.PlannedDate))
        {
            var earliest = beforePackages.Min(workPackage => workPackage.PlannedStart!.Value);
            return new DecisionAttribution(earliest, $"Trước {CleanName(beforePackages.OrderBy(package => package.PlannedStart).First().Name, beforePackages.OrderBy(package => package.PlannedStart).First().Id)}");
        }

        foreach (var value in new[] { observation.GateId, observation.GateEffectCode, observation.DueCondition })
        {
            if (MatchesWholeToken(value, milestoneId, strippedMilestoneId))
            {
                return DateAttribution(milestone.PlannedDate.Value);
            }
        }

        return null;
    }

    private static bool MatchesId(string? value, string canonical, string stripped) =>
        string.Equals(value, canonical, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, stripped, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesWholeToken(string? value, string canonical, string stripped) =>
        ContainsWholeToken(value, canonical) || ContainsWholeToken(value, stripped);

    private static bool ContainsWholeToken(string? value, string token)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return value.Split(new[] { ' ', '\t', '\r', '\n', ',', ';', ':', '/', '(', ')', '[', ']', '`' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(candidate => string.Equals(candidate, token, StringComparison.OrdinalIgnoreCase));
    }

    private static DateOnly? FindIsoDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        foreach (var token in value.Split(new[] { ' ', '\t', '\r', '\n', ',', ';', ':', '/', '(', ')', '[', ']', '`' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (DateOnly.TryParseExact(token, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    private static DecisionAttribution DateAttribution(DateOnly date) =>
        new(date, FormatDate(date));

    private static string StripLeadingGatePrefix(string value) =>
        value.StartsWith("G-", StringComparison.Ordinal) ? value[2..] : value;

    private sealed record AttentionCandidate
    {
        public ExecutiveAttentionCategory Category { get; init; }
        public string Action { get; init; } = string.Empty;
        public string Impact { get; init; } = string.Empty;
        public string OwnerLabel { get; init; } = string.Empty;
        public DateOnly? DueDate { get; init; }
        public string DueLabel { get; init; } = string.Empty;
        public bool OverviewEligible { get; init; }
        public string DeduplicationKey { get; init; } = string.Empty;
        public int SourceOrder { get; init; }
    }

    private sealed record DecisionAttribution(DateOnly DueDate, string DueLabel);

    private static ExecutiveCondition ProjectScheduleCondition(CanonicalProject project, ManagementAnalysis analysis)
    {
        var blocked = DistinctAlertWorkItems(analysis, "BLOCKED");
        if (blocked.Count > 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ScheduleBlocked,
                Label = "Bị chặn",
                Detail = $"Có {blocked.Count} hạng mục lịch trình đang bị chặn.",
                Tone = ExecutiveConditionTone.Blocked
            };
        }

        var late = DistinctAlertWorkItems(analysis, "OVERDUE", "START_DELAY");
        if (late.Count > 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ScheduleLate,
                Label = "Trễ kế hoạch",
                Detail = $"Có {late.Count} hạng mục đã quá ngày kế hoạch.",
                Tone = ExecutiveConditionTone.Blocked
            };
        }

        var atRisk = DistinctAlertWorkItems(analysis, "AT_RISK");
        if (atRisk.Count > 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ScheduleAtRisk,
                Label = "Có nguy cơ",
                Detail = $"Có {atRisk.Count} hạng mục được ghi nhận có nguy cơ.",
                Tone = ExecutiveConditionTone.Attention
            };
        }

        var assessable = analysis.AsOfDate is not null
            && project.Phases.Any(phase => IsValidRange(phase.PlannedStart, phase.PlannedFinish))
            || project.WorkPackages.Any(workPackage => IsValidRange(workPackage.PlannedStart, workPackage.PlannedFinish))
            || project.DeliveryCards.Any(card => IsValidRange(card.PlannedStart, card.PlannedFinish));
        return assessable
            ? new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ScheduleAssessable,
                Label = "Chưa ghi nhận lệch kế hoạch",
                Detail = "Chưa ghi nhận hạng mục bị chặn, quá hạn hoặc có nguy cơ tại ngày báo cáo.",
                Tone = ExecutiveConditionTone.Plan
            }
            : new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ScheduleUnknown,
                Label = "Chưa đánh giá",
                Detail = "Chưa đủ dữ liệu để đánh giá tình trạng lịch trình.",
                Tone = ExecutiveConditionTone.Unknown
            };
    }

    private static ExecutiveCondition ProjectReadinessCondition(CanonicalProject project)
    {
        var evidence = project.ManagementEvidence;
        var reconciliations = evidence.Reconciliations
            .GroupBy(item => item.ObservationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Status, StringComparer.OrdinalIgnoreCase);
        var valid = evidence.Observations
            .Where(observation => observation.ValidationState == ValidationState.Known)
            .Where(observation => !reconciliations.TryGetValue(observation.Id, out var status)
                || status is not (EvidenceReconciliationStatus.Invalid or EvidenceReconciliationStatus.Ambiguous))
            .ToArray();

        if (evidence.DiscoveryState != ManagementEvidenceDiscoveryState.Known || valid.Length == 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ReadinessUnknown,
                Label = "Chưa đánh giá",
                Detail = "Chưa đủ bằng chứng để đánh giá mức sẵn sàng.",
                Tone = ExecutiveConditionTone.Unknown
            };
        }

        var blocked = valid.Count(observation =>
            string.Equals(observation.ResultCode, "BLOCKED", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(observation.BlockerOrDeviation));
        if (blocked > 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ReadinessBlocked,
                Label = "Bị chặn",
                Detail = $"Có {blocked} nội dung sẵn sàng đang bị chặn.",
                Tone = ExecutiveConditionTone.Blocked
            };
        }

        var decisions = valid.Count(observation =>
            observation.EvidenceKind == ManagementEvidenceKind.DecisionRecord
            && string.Equals(observation.StateCode, "OPEN", StringComparison.OrdinalIgnoreCase));
        if (decisions > 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ReadinessDecision,
                Label = "Cần quyết định",
                Detail = $"Có {decisions} quyết định đang chờ thẩm quyền.",
                Tone = ExecutiveConditionTone.Attention
            };
        }

        var actions = valid.Count(observation =>
            (observation.EvidenceKind == ManagementEvidenceKind.HumanAction
                && (string.Equals(observation.StateCode, "OPEN", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(observation.StateCode, "NOT-RUN", StringComparison.OrdinalIgnoreCase)))
            || (observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck
                && observation.StateCode is not null
                && new[] { "NOT-RUN", "FAIL", "PASS-WITH-ACTIONS", "IN-PROGRESS" }
                    .Contains(observation.StateCode, StringComparer.OrdinalIgnoreCase)));
        if (actions > 0)
        {
            return new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ReadinessAction,
                Label = "Cần xử lý",
                Detail = $"Có {actions} hành động hoặc kiểm tra chưa hoàn tất.",
                Tone = ExecutiveConditionTone.Attention
            };
        }

        var passed = valid.Any(observation =>
            observation.EvidenceKind == ManagementEvidenceKind.GateOutcome
            && string.Equals(observation.ResultCode, "PASS", StringComparison.OrdinalIgnoreCase));
        return passed
            ? new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ReadinessComplete,
                Label = "Đủ điều kiện theo bằng chứng đã ghi nhận",
                Detail = "Bằng chứng hiện có ghi nhận cổng sẵn sàng đã đạt.",
                Tone = ExecutiveConditionTone.Complete
            }
            : new ExecutiveCondition
            {
                Code = ExecutiveConditionCode.ReadinessUnknown,
                Label = "Chưa đánh giá",
                Detail = "Chưa đủ bằng chứng để đánh giá mức sẵn sàng.",
                Tone = ExecutiveConditionTone.Unknown
            };
    }

    private static ExecutiveProgressSummary ProjectProgress(ManagementAnalysis analysis)
    {
        var counts = analysis.ExecutionStatus;
        var completed = Math.Max(0, counts.Completed);
        var inProgress = Math.Max(0, counts.InProgress);
        var notStarted = Math.Max(0, counts.NotStarted);
        var unknown = Math.Max(0, counts.Total - completed - inProgress - notStarted);
        var actual = analysis.ExecutionEffort.ActualEffortHours;
        var remaining = analysis.ExecutionEffort.RemainingEffortHours;
        var eligible = actual is not null
            && remaining is not null
            && actual >= 0m
            && remaining >= 0m
            && actual + remaining > 0m;
        var percent = eligible
            ? (int?)decimal.ToInt32(decimal.Round(actual!.Value / (actual.Value + remaining!.Value) * 100m, 0, MidpointRounding.AwayFromZero))
            : null;

        return new ExecutiveProgressSummary
        {
            RecordedPercent = percent,
            Statement = percent is null
                ? "Chưa đủ dữ liệu để tính % hoàn thành"
                : $"Đã ghi nhận {percent}% theo nỗ lực thực tế và còn lại.",
            ActualEffortHours = actual,
            RemainingEffortHours = remaining,
            CompletedCount = completed,
            InProgressCount = inProgress,
            NotStartedCount = notStarted,
            UnknownCount = unknown
        };
    }

    private static HashSet<string> DistinctAlertWorkItems(ManagementAnalysis analysis, params string[] codes) =>
        analysis.Alerts
            .Where(alert => codes.Contains(alert.AlertCode.Trim(), StringComparer.OrdinalIgnoreCase))
            .Select(alert => alert.WorkItemId)
            .Where(workItemId => !string.IsNullOrWhiteSpace(workItemId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string ExecutionLabel(ExecutionState? state) => state switch
    {
        ExecutionState.NotStarted => "Chưa bắt đầu",
        ExecutionState.InProgress => "Đang thực hiện",
        ExecutionState.Completed => "Hoàn thành",
        ExecutionState.Suspended => "Tạm dừng",
        ExecutionState.Cancelled => "Đã hủy",
        _ => "Chưa cập nhật"
    };

    private static string CleanName(string? value, string? exactId = null)
    {
        var cleaned = value?.Trim() ?? string.Empty;
        while (cleaned.Length >= 2 && cleaned[0] == '`' && cleaned[^1] == '`')
        {
            cleaned = cleaned[1..^1].Trim();
        }

        while (cleaned.StartsWith("[", StringComparison.Ordinal))
        {
            var closing = cleaned.IndexOf(']');
            if (closing <= 0)
            {
                break;
            }

            cleaned = cleaned[(closing + 1)..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(exactId)
            && cleaned.StartsWith(exactId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var remainder = cleaned[exactId.Trim().Length..];
            if (remainder.Length == 0)
            {
                cleaned = string.Empty;
            }
            else if (char.IsWhiteSpace(remainder[0]) || remainder[0] is ':' or '-' or '–' or '—')
            {
                cleaned = remainder.TrimStart(' ', '\t', ':', '-', '–', '—');
            }
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "Chưa cập nhật" : cleaned;
    }

    private static string CleanCardDescription(string? value, string cardId)
    {
        var cleaned = CleanName(value, cardId);
        var suffix = cardId.Trim();
        if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            && cleaned.Length > suffix.Length
            && char.IsWhiteSpace(cleaned[cleaned.Length - suffix.Length - 1]))
        {
            cleaned = cleaned[..(cleaned.Length - suffix.Length)].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(cleaned) ? "Chưa cập nhật" : cleaned;
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa xác định";

    private static bool IsWithin(DateOnly date, DateOnly? start, DateOnly? finish) =>
        IsValidRange(start, finish) && date >= start!.Value && date <= finish!.Value;

    private static bool IsValidRange(DateOnly? start, DateOnly? finish) =>
        start is not null && finish is not null && start <= finish;

    private sealed record PhaseEntry(Phase Phase, int SourceOrder);
    private sealed record MilestoneEntry(MilestoneDecision Milestone, int SourceOrder);
}
