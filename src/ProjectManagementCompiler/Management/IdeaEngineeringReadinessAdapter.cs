using System.Text.RegularExpressions;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Management;

public interface IManagementEvidenceSourceAdapter
{
    ManagementEvidenceAdapterResult Adapt(RepositorySnapshot snapshot, CanonicalProject planningProject);
}

public sealed record ManagementEvidenceAdapterResult
{
    public ManagementEvidence Evidence { get; init; } = new();
}

public sealed class IdeaEngineeringReadinessAdapter : IManagementEvidenceSourceAdapter
{
    private static readonly Regex IncrementPattern = new(
        @"^\s*\*{0,2}Increment\*{0,2}\s*:\s*(?<id>[A-Za-z0-9][A-Za-z0-9_-]*)(?:[^A-Za-z0-9]+[—-]\s*(?<name>.+?))?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VersionStatusPattern = new(
        @"^\s*\*{0,2}Version\s*/\s*status\*{0,2}\s*:\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InlineIncrementPattern = new(
        @"`(?<id>IE-[A-Za-z0-9][A-Za-z0-9_-]*)`",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HeadingIncrementNamePattern = new(
        @"^\s*#\s*(?:PH\d+\s*[—-]\s*)?(?<name>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HeadingPhasePattern = new(
        @"^\s*#\s*(?<phase>PH\d+)(?:\s*[—-]|\s+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex StatusTablePattern = new(
        @"^\s*\|\s*PH\d+\s+source\s+package\s*\|\s*`?(?<value>[^|`]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly Regex ChecklistPattern = new(
        @"^\s*-\s*\[(?<mark>[ xX])\]\s*(?<id>T\d+)\b",
        RegexOptions.Compiled);

    private static readonly Regex EvidenceLinkPattern = new(
        @"\]\((?<link>[^)]+)\)",
        RegexOptions.Compiled);

    private static readonly Regex GateIdPattern = new(
        @"\b(?<id>PG\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SuccessorIncrementPattern = new(
        @"\b(?<id>IE-[A-Za-z0-9][A-Za-z0-9_-]*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ManagementEvidenceAdapterResult Adapt(RepositorySnapshot snapshot, CanonicalProject planningProject)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(planningProject);

        var diagnostics = new List<ImportWarning>();
        var candidates = FindCandidates(snapshot.Documents);
        if (candidates.Count == 0)
        {
            diagnostics.Add(Diagnostic(
                "ACTIVE_INCREMENT_UNKNOWN",
                "No readiness package with agreeing README and readiness-register identity was captured.",
                snapshot.Documents.Where(document => IsReadinessPath(document.RelativeFile)).Select(document => document.SourceReference)));
            return new ManagementEvidenceAdapterResult
            {
                Evidence = new ManagementEvidence
                {
                    DiscoveryState = ManagementEvidenceDiscoveryState.Unknown,
                    CapturedAtUtc = snapshot.CapturedAtUtc,
                    Diagnostics = diagnostics
                }
            };
        }

        if (candidates.Count > 1)
        {
            diagnostics.Add(Diagnostic(
                "ACTIVE_INCREMENT_AMBIGUOUS",
                "More than one readiness increment has agreeing identity records.",
                candidates.SelectMany(candidate => candidate.Documents.Values.Select(document => document.SourceReference))));
            return new ManagementEvidenceAdapterResult
            {
                Evidence = new ManagementEvidence
                {
                    DiscoveryState = ManagementEvidenceDiscoveryState.Ambiguous,
                    CapturedAtUtc = snapshot.CapturedAtUtc,
                    Diagnostics = diagnostics
                }
            };
        }

        var candidate = candidates[0];
        var documents = candidate.Documents;
        foreach (var relativePath in SourcePathPolicy.GetManagementEvidencePaths(candidate.Root))
        {
            if (!documents.ContainsKey(relativePath))
            {
                if (SourcePathPolicy.IsOptionalManagementEvidencePath(candidate.Root, relativePath))
                {
                    continue;
                }

                diagnostics.Add(Diagnostic(
                    "EVIDENCE_SOURCE_UNAVAILABLE",
                    $"The declared readiness source '{relativePath}' was not captured.",
                    [new SourceReference
                    {
                        SourceId = snapshot.RepositoryId,
                        Repository = snapshot.RepositoryLabel,
                        ResolvedRef = snapshot.ResolvedRef,
                        RelativeFile = relativePath,
                        ExtractionRule = "ideaengineering-readiness-allow-list",
                        ValidationState = ValidationState.Unknown
                    }]));
            }
        }

        var observations = new List<ManagementEvidenceObservation>();
        var controlEnvelope = ExtractControlEnvelope(documents, candidate);
        if (controlEnvelope is not null)
        {
            observations.Add(controlEnvelope);
        }

        var readinessRegister = GetDocument(documents, candidate.Root, "readiness-register.md");
        if (readinessRegister is not null)
        {
            var rows = ParseTables(readinessRegister);
            observations.AddRange(ExtractReadinessChecks(rows));
            observations.AddRange(ExtractDecisions(rows));
            observations.AddRange(ExtractHumanActions(rows));
            observations.AddRange(ExtractGateObservations(rows, authorityRank: 2, authorityKind: "readiness-register-state-result"));
        }

        var readme = GetDocument(documents, candidate.Root, "README.md");
        if (readme is not null)
        {
            observations.AddRange(ExtractGateObservations(ParseTables(readme), authorityRank: 3, authorityKind: "readme-summary"));
        }

        var actualGateRecord = GetDocument(documents, candidate.Root, "pg4-gate-record.md");
        if (actualGateRecord is not null)
        {
            observations.AddRange(ExtractGateObservations(ParseTables(actualGateRecord), authorityRank: 1, authorityKind: "actual-gate-record"));
        }

        var tasks = GetDocument(documents, candidate.Root, "tasks.md");
        if (tasks is not null)
        {
            observations.AddRange(ExtractChecklistContext(tasks));
        }

        EnsureUniqueObservationIds(observations);
        ValidateEffectiveConflicts(observations, diagnostics);
        ValidateStateResultPairs(observations, diagnostics);
        ValidateGatePair(observations, diagnostics);

        return new ManagementEvidenceAdapterResult
        {
            Evidence = new ManagementEvidence
            {
                DiscoveryState = ManagementEvidenceDiscoveryState.Known,
                IncrementPath = candidate.Root,
                IncrementId = candidate.IncrementId,
                IncrementPhaseId = candidate.IncrementPhaseId,
                IncrementName = candidate.IncrementName,
                IncrementStatus = candidate.IncrementStatus,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                Observations = observations,
                Diagnostics = diagnostics
            }
        };
    }

    /// <summary>
    /// Adapts the readiness document explicitly declared by the manifest. The
    /// manifest boundary may intentionally capture only the register, so this
    /// path does not invent a README or scan for an alternate package.
    /// </summary>
    public ManagementEvidenceAdapterResult AdaptDeclaredReadiness(
        RepositorySnapshot snapshot,
        CanonicalProject planningProject,
        string declaredPath)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(planningProject);

        var normalizedPath = declaredPath.Replace('\\', '/');
        var register = snapshot.Documents.FirstOrDefault(document =>
            string.Equals(document.RelativeFile.Replace('\\', '/'), normalizedPath, StringComparison.OrdinalIgnoreCase));
        if (register is null)
        {
            return new ManagementEvidenceAdapterResult
            {
                Evidence = new ManagementEvidence
                {
                    DiscoveryState = ManagementEvidenceDiscoveryState.Unknown,
                    CapturedAtUtc = snapshot.CapturedAtUtc,
                    Diagnostics =
                    [Diagnostic(
                        "EVIDENCE_SOURCE_UNAVAILABLE",
                        $"The manifest-declared readiness source '{normalizedPath}' was not captured.",
                        [new SourceReference
                        {
                            SourceId = snapshot.RepositoryId,
                            Repository = snapshot.RepositoryLabel,
                            ResolvedRef = snapshot.ResolvedRef,
                            RelativeFile = normalizedPath,
                            ExtractionRule = "ideaengineering-manifest-readiness-authority",
                            ValidationState = ValidationState.Unknown
                        }])]
                }
            };
        }

        var identity = ParseIdentity(register.Content);
        if (identity is null)
        {
            return new ManagementEvidenceAdapterResult
            {
                Evidence = new ManagementEvidence
                {
                    DiscoveryState = ManagementEvidenceDiscoveryState.Unknown,
                    CapturedAtUtc = snapshot.CapturedAtUtc,
                    Diagnostics =
                    [Diagnostic(
                        "ACTIVE_INCREMENT_UNKNOWN",
                        $"The manifest-declared readiness source '{normalizedPath}' has no parseable increment identity.",
                        [register.SourceReference])]
                }
            };
        }

        var root = GetIncrementRoot(normalizedPath);
        var documents = snapshot.Documents
            .Where(document => string.Equals(document.RelativeFile.Replace('\\', '/'), root, StringComparison.OrdinalIgnoreCase)
                || document.RelativeFile.Replace('\\', '/').StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(document => document.RelativeFile.Replace('\\', '/'), StringComparer.OrdinalIgnoreCase);
        var candidate = new IncrementCandidate(
            root,
            identity.Value.Id,
            identity.Value.Name,
            ParseStatus(register.Content),
            ParsePhaseId(register.Content),
            documents);
        var diagnostics = new List<ImportWarning>();
        var observations = new List<ManagementEvidenceObservation>();
        var controlEnvelope = ExtractControlEnvelope(documents, candidate);
        if (controlEnvelope is not null)
        {
            observations.Add(controlEnvelope);
        }

        var rows = ParseTables(register);
        observations.AddRange(ExtractReadinessChecks(rows));
        observations.AddRange(ExtractDecisions(rows));
        observations.AddRange(ExtractHumanActions(rows));
        observations.AddRange(ExtractGateObservations(rows, authorityRank: 2, authorityKind: "readiness-register-state-result"));
        EnsureUniqueObservationIds(observations);
        ValidateEffectiveConflicts(observations, diagnostics);
        ValidateStateResultPairs(observations, diagnostics);
        ValidateGatePair(observations, diagnostics);

        return new ManagementEvidenceAdapterResult
        {
            Evidence = new ManagementEvidence
            {
                DiscoveryState = ManagementEvidenceDiscoveryState.Known,
                IncrementPath = root,
                IncrementId = candidate.IncrementId,
                IncrementPhaseId = candidate.IncrementPhaseId,
                IncrementName = candidate.IncrementName,
                IncrementStatus = candidate.IncrementStatus,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                Observations = observations,
                Diagnostics = diagnostics
            }
        };
    }

    private static void EnsureUniqueObservationIds(IList<ManagementEvidenceObservation> observations)
    {
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < observations.Count; index++)
        {
            var observation = observations[index];
            var baseId = string.IsNullOrWhiteSpace(observation.Id)
                ? $"observation:{index + 1:D4}"
                : observation.Id;
            var candidate = baseId;
            var suffix = 1;
            while (!usedIds.Add(candidate))
            {
                suffix++;
                candidate = $"{baseId}:{suffix}";
            }

            if (!string.Equals(candidate, observation.Id, StringComparison.Ordinal))
            {
                observations[index] = observation with { Id = candidate };
            }
        }
    }

    private static void ValidateEffectiveConflicts(
        IEnumerable<ManagementEvidenceObservation> observations,
        ICollection<ImportWarning> diagnostics)
    {
        var evidence = new ManagementEvidence { Observations = observations.ToArray() };
        foreach (var selection in new EffectiveEvidenceResolver().Resolve(evidence)
                     .Where(selection => selection.Status == EffectiveEvidenceSelectionStatus.Conflict))
        {
            diagnostics.Add(Diagnostic(
                "EVIDENCE_CONFLICT",
                $"Evidence object '{selection.ObjectKind}:{selection.ObjectId}' has conflicting equal-authority values for '{selection.FieldName}'; all source observations were preserved.",
                selection.SourceReferences));
        }
    }

    private static IReadOnlyList<IncrementCandidate> FindCandidates(IReadOnlyList<SourceDocument> documents)
    {
        var grouped = documents
            .Where(document => IsReadinessPath(document.RelativeFile))
            .GroupBy(document => GetIncrementRoot(document.RelativeFile), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0)
            .ToArray();

        var candidates = new List<IncrementCandidate>();
        foreach (var group in grouped)
        {
            var byName = group.ToDictionary(
                document => document.RelativeFile.Replace('\\', '/'),
                StringComparer.OrdinalIgnoreCase);
            var readme = byName.GetValueOrDefault($"{group.Key}/README.md");
            var register = byName.GetValueOrDefault($"{group.Key}/readiness-register.md");
            if (readme is null || register is null)
            {
                continue;
            }

            var readmeIdentity = ParseIdentity(readme.Content);
            var registerIdentity = ParseIdentity(register.Content);
            if (readmeIdentity is null
                || registerIdentity is null
                || !string.Equals(readmeIdentity.Value.Id, registerIdentity.Value.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(new IncrementCandidate(
                group.Key,
                readmeIdentity.Value.Id,
                readmeIdentity.Value.Name ?? registerIdentity.Value.Name,
                ParseStatus(readme.Content),
                ParsePhaseId(readme.Content),
                byName));
        }

        return candidates;
    }

    private static ManagementEvidenceObservation? ExtractControlEnvelope(
        IReadOnlyDictionary<string, SourceDocument> documents,
        IncrementCandidate candidate)
    {
        var register = GetDocument(documents, candidate.Root, "readiness-register.md");
        if (register is null)
        {
            return null;
        }

        var rows = ParseTables(register);
        var row = rows.FirstOrDefault(item => Normalize(Cell(item, "Field", "Control field", "Trường")) == "STABLERECORDID");
        var recordId = row is null ? $"{candidate.IncrementId}-CONTROL" : Cell(row, "Value", "Recorded value", "Giá trị") ?? $"{candidate.IncrementId}-CONTROL";
        var successorRow = rows.FirstOrDefault(item =>
            Normalize(Cell(item, "Field", "Control field", "Trường")).Contains("PROPOSEDPG4SUCCESSOR", StringComparison.Ordinal));
        var successor = ParseSuccessor(successorRow is null ? null : Cell(successorRow, "Value", "Recorded value", "Giá trị"));
        var sourceReference = successorRow?.SourceReference ?? row?.SourceReference ?? register.SourceReference;
        return new ManagementEvidenceObservation
        {
            Id = $"control:{NormalizeRecordId(recordId)}",
            EvidenceKind = ManagementEvidenceKind.ControlEnvelope,
            SourceRecordId = NormalizeRecordId(recordId),
            Summary = successorRow is null ? null : SafeSummary(Cell(successorRow, "Value", "Recorded value", "Giá trị")),
            ProposedSuccessorIncrementId = successor.Id,
            ProposedSuccessorSummary = successor.Summary,
            SourceReferences = [WithAuthority(sourceReference, 1, "ideaengineering-readiness-control-envelope")],
            AuthorityRank = 1,
            AuthorityKind = "control-envelope",
            ValidationState = ValidationState.Known
        };
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ExtractReadinessChecks(
        IReadOnlyList<ReadinessTableRow> rows)
    {
        var observations = new List<ManagementEvidenceObservation>();
        foreach (var row in rows)
        {
            var id = NormalizeRecordId(Cell(row, "Work package", "Work-package", "Package"));
            if (!Regex.IsMatch(id, @"^P\d{2}$", RegexOptions.IgnoreCase)
                || Cell(row, "Task state", "State", "Task status") is null
                || Cell(row, "Result", "Readiness result") is null)
            {
                continue;
            }

            var ownerText = Cell(row, "Owner", "Required role", "Accountable owner / authority");
            var blockerText = Cell(row, "Blocker / deviation", "Blocker", "Deviation");
            var roles = ParseRoles(ownerText, blockerText);
            var dueText = Cell(row, "Due condition", "Due condition / closure evidence");
            var gateText = Cell(row, "Gate effect", "Affected work / gate effect");
            observations.Add(new ManagementEvidenceObservation
            {
                Id = $"readiness:{id}",
                EvidenceKind = ManagementEvidenceKind.ReadinessCheck,
                SourceRecordId = id,
                RawTargetKind = "WorkPackage",
                RawTargetId = id,
                ExplicitTarget = new EvidenceTarget { Kind = "WorkPackage", Id = id },
                StateCode = NormalizeCode(Cell(row, "Task state", "State", "Task status")),
                ResultCode = NormalizeCode(Cell(row, "Result", "Readiness result")),
                OwnerRole = roles.Owner?.Code,
                OwnerRoleLabel = roles.Owner?.DisplayLabel,
                OwnerRoleSourceMeaning = roles.Owner?.SourceMeaning,
                OwnerRoleReference = roles.Owner,
                WaitingForRole = roles.WaitingFor?.Code,
                WaitingForRoleLabel = roles.WaitingFor?.DisplayLabel,
                WaitingForRoleSourceMeaning = roles.WaitingFor?.SourceMeaning,
                WaitingForRoleReference = roles.WaitingFor,
                DueCondition = NormalizeDueCondition(dueText),
                DueConditionCode = NormalizeDueCondition(dueText),
                DueConditionSummary = SafeSummary(dueText),
                GateEffect = NormalizeGateEffect(gateText),
                GateEffectCode = NormalizeGateEffect(gateText),
                GateEffectSummary = SafeSummary(gateText),
                BlockerOrDeviation = NormalizeBlocker(blockerText),
                BlockerSummary = SafeSummary(blockerText),
                PendingActionSummary = BuildPendingActionSummary(id, blockerText, roles.WaitingFor),
                EvidenceLinks = SafeEvidenceLinks(Cell(row, "Evidence link", "Evidence", "Evidence links")),
                SourceReferences = [WithAuthority(row.SourceReference, 3, "ideaengineering-readiness-work-package")],
                AuthorityRank = 3,
                AuthorityKind = "readiness-register-state-result",
                ValidationState = ValidationState.Known
            });
        }

        return observations;
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ExtractDecisions(IReadOnlyList<ReadinessTableRow> rows)
    {
        var observations = new List<ManagementEvidenceObservation>();
        foreach (var row in rows)
        {
            var id = NormalizeRecordId(Cell(row, "ID", "Decision ID"));
            if (!Regex.IsMatch(id, @"^D\d+$", RegexOptions.IgnoreCase)
                || Cell(row, "Question / current state", "Exact question") is null)
            {
                continue;
            }

            var question = Cell(row, "Question / current state", "Exact question") ?? string.Empty;
            var authorityText = Cell(row, "Accountable owner / authority", "Required authority");
            var roles = ParseRoles(authorityText);
            var dueText = Cell(row, "Due condition / closure evidence");
            var gateText = Cell(row, "Affected work / gate effect", "Effect on PH1");
            observations.Add(new ManagementEvidenceObservation
            {
                Id = $"decision:{id}",
                EvidenceKind = ManagementEvidenceKind.DecisionRecord,
                SourceRecordId = id,
                StateCode = NormalizeCode(question),
                StateMeaning = SafeSummary(question),
                Summary = SafeSummary(question),
                OwnerRole = roles.Owner?.Code,
                OwnerRoleLabel = roles.Owner?.DisplayLabel,
                OwnerRoleSourceMeaning = roles.Owner?.SourceMeaning,
                OwnerRoleReference = roles.Owner,
                RequiredAuthorityRole = roles.Owner?.Code,
                RequiredAuthorityRoleLabel = roles.Owner?.DisplayLabel,
                RequiredAuthorityRoleSourceMeaning = roles.Owner?.SourceMeaning,
                RequiredAuthorityRoleReference = roles.Owner,
                DueCondition = NormalizeDueCondition(dueText),
                DueConditionCode = NormalizeDueCondition(dueText),
                DueConditionSummary = SafeSummary(dueText),
                GateEffect = NormalizeGateEffect(gateText),
                GateEffectCode = NormalizeGateEffect(gateText),
                GateEffectSummary = SafeSummary(gateText),
                AffectedTargetSummary = SafeSummary(gateText),
                SourceReferences = [WithAuthority(row.SourceReference, 2, "ideaengineering-readiness-decision")],
                AuthorityRank = 2,
                AuthorityKind = "decision-record",
                ValidationState = ValidationState.Known
            });
        }

        return observations;
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ExtractHumanActions(IReadOnlyList<ReadinessTableRow> rows)
    {
        var observations = new List<ManagementEvidenceObservation>();
        foreach (var row in rows)
        {
            var id = NormalizeRecordId(Cell(row, "Action ID", "Human action ID"));
            if (!Regex.IsMatch(id, @"^HA-\d+$", RegexOptions.IgnoreCase)
                || Cell(row, "Status", "Current status", "Trạng thái hiện tại") is null)
            {
                continue;
            }

            var actionSummary = Cell(row, "Task / package", "Action", "Action summary");
            var requiredRoleText = Cell(row, "Required role", "Vai trò hoặc authority bắt buộc");
            var roles = ParseRoles(requiredRoleText);
            var effectText = Cell(row, "Effect", "Ảnh hưởng / kết quả sau khi hoàn tất");
            observations.Add(new ManagementEvidenceObservation
            {
                Id = $"human-action:{id}",
                EvidenceKind = ManagementEvidenceKind.HumanAction,
                SourceRecordId = id,
                StateCode = NormalizeCode(Cell(row, "Status", "Current status", "Trạng thái hiện tại")),
                Summary = SafeSummary(actionSummary),
                ActionSummary = SafeSummary(actionSummary),
                AffectedTargetSummary = SafeSummary(actionSummary),
                CompletionCondition = SafeSummary(effectText),
                WaitingForRole = roles.Owner?.Code,
                WaitingForRoleLabel = roles.Owner?.DisplayLabel,
                WaitingForRoleSourceMeaning = roles.Owner?.SourceMeaning,
                WaitingForRoleReference = roles.Owner,
                RequiredAuthorityRole = roles.Owner?.Code,
                RequiredAuthorityRoleLabel = roles.Owner?.DisplayLabel,
                RequiredAuthorityRoleSourceMeaning = roles.Owner?.SourceMeaning,
                RequiredAuthorityRoleReference = roles.Owner,
                GateEffect = NormalizeGateEffect(effectText),
                GateEffectCode = NormalizeGateEffect(effectText),
                GateEffectSummary = SafeSummary(effectText),
                SourceReferences = [WithAuthority(row.SourceReference, 3, "ideaengineering-readiness-human-action")],
                AuthorityRank = 3,
                AuthorityKind = "human-action-board",
                ValidationState = ValidationState.Known
            });
        }

        return observations;
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ExtractGateObservations(
        IReadOnlyList<ReadinessTableRow> rows,
        int authorityRank,
        string authorityKind)
    {
        var observations = new List<ManagementEvidenceObservation>();
        var contextGateId = rows
            .Select(row =>
            {
                var label = Compact(Cell(row, "Field", "Control field", "Trường", "Nội dung", "Content", "Item"));
                var value = Cell(row, "Value", "Recorded value", "Giá trị", "Trạng thái", "Status", "State");
                return label is "GATEID" or "GATE" ? ParseGateId(value) : null;
            })
            .FirstOrDefault(value => value is not null);

        foreach (var row in rows)
        {
            var labelText = Cell(row, "Field", "Control field", "Trường", "Nội dung", "Content", "Item");
            var label = Compact(labelText);
            var value = Cell(row, "Value", "Recorded value", "Giá trị", "Trạng thái", "Status", "State");
            if (value is null)
            {
                continue;
            }

            var gateId = ParseGateId(labelText) ?? contextGateId;
            if (gateId is null)
            {
                continue;
            }

            if (label.Contains("GATEEXECUTIONSTATE", StringComparison.Ordinal)
                || label.Equals("EXECUTIONSTATE", StringComparison.Ordinal))
            {
                observations.Add(new ManagementEvidenceObservation
                {
                    Id = $"gate:{gateId}:execution:{authorityKind}",
                    EvidenceKind = ManagementEvidenceKind.GateExecution,
                    SourceRecordId = gateId,
                    RawTargetKind = "Gate",
                    RawTargetId = gateId,
                    ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = gateId },
                    GateId = gateId,
                    StateCode = NormalizeCode(value),
                    StateMeaning = SafeSummary(value),
                    SourceReferences = [WithAuthority(row.SourceReference, authorityRank, $"ideaengineering-{authorityKind}")],
                    AuthorityRank = authorityRank,
                    AuthorityKind = authorityKind,
                    ValidationState = ValidationState.Known
                });
            }
            else if (label.Contains("GATEOUTCOME", StringComparison.Ordinal)
                || label.Equals("OUTCOME", StringComparison.Ordinal))
            {
                observations.Add(new ManagementEvidenceObservation
                {
                    Id = $"gate:{gateId}:outcome:{authorityKind}",
                    EvidenceKind = ManagementEvidenceKind.GateOutcome,
                    SourceRecordId = gateId,
                    RawTargetKind = "Gate",
                    RawTargetId = gateId,
                    ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = gateId },
                    GateId = gateId,
                    ResultCode = NormalizeCode(value),
                    ResultMeaning = SafeSummary(value),
                    SourceReferences = [WithAuthority(row.SourceReference, authorityRank, $"ideaengineering-{authorityKind}")],
                    AuthorityRank = authorityRank,
                    AuthorityKind = authorityKind,
                    ValidationState = ValidationState.Known
                });
            }
        }

        return observations;
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ExtractChecklistContext(SourceDocument document)
    {
        var observations = new List<ManagementEvidenceObservation>();
        var lines = document.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var match = ChecklistPattern.Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            var id = match.Groups["id"].Value.ToUpperInvariant();
            observations.Add(new ManagementEvidenceObservation
            {
                Id = $"checklist:{id}",
                EvidenceKind = ManagementEvidenceKind.ChecklistContext,
                SourceRecordId = id,
                StateCode = match.Groups["mark"].Value.Equals("x", StringComparison.OrdinalIgnoreCase)
                    ? "CHECKED"
                    : "UNCHECKED",
                SourceReferences =
                [
                    document.SourceReference with
                    {
                        RelativeFile = document.RelativeFile,
                        Item = $"line-{index + 1:D4}",
                        ExtractionRule = "ideaengineering-readiness-checklist-context",
                        ValidationState = ValidationState.Known
                    }
                ],
                AuthorityRank = 5,
                AuthorityKind = "checklist-context",
                ValidationState = ValidationState.Known
            });
        }

        return observations;
    }

    private static void ValidateStateResultPairs(
        IEnumerable<ManagementEvidenceObservation> observations,
        ICollection<ImportWarning> diagnostics)
    {
        foreach (var observation in observations.Where(observation =>
                     observation.EvidenceKind == ManagementEvidenceKind.ReadinessCheck
                     && observation.ResultCode is "PASS" or "PASS-WITH-ACTIONS"
                     && observation.StateCode is "NOT-RUN" or "IN-PROGRESS"))
        {
            diagnostics.Add(Diagnostic(
                "READINESS_STATE_RESULT_CONFLICT",
                $"Readiness record '{observation.SourceRecordId}' cannot have result '{observation.ResultCode}' while state is '{observation.StateCode}'.",
                observation.SourceReferences));
        }
    }

    private static void ValidateGatePair(
        IEnumerable<ManagementEvidenceObservation> observations,
        ICollection<ImportWarning> diagnostics)
    {
        var materialized = observations.ToArray();
        var evidence = new ManagementEvidence { Observations = materialized };
        var resolver = new EffectiveEvidenceResolver();
        var gateIds = materialized
            .Where(observation => observation.EvidenceKind is ManagementEvidenceKind.GateExecution or ManagementEvidenceKind.GateOutcome)
            .Select(observation => observation.GateId ?? observation.SourceRecordId)
            .Select(ParseGateId)
            .Where(gateId => gateId is not null)
            .Select(gateId => gateId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(gateId => gateId, StringComparer.Ordinal)
            .ToArray();

        foreach (var gateId in gateIds)
        {
            var execution = resolver.Select(evidence, "Gate", gateId, ManagementEvidenceKind.GateExecution, "StateCode");
            var outcome = resolver.Select(evidence, "Gate", gateId, ManagementEvidenceKind.GateOutcome, "ResultCode");
            if (execution.Status != EffectiveEvidenceSelectionStatus.Resolved
                || outcome.Status != EffectiveEvidenceSelectionStatus.Resolved
                || execution.Value is null
                || outcome.Value is null)
            {
                continue;
            }

            var executionNotComplete = execution.Value is "NOT-RUN" or "IN-PROGRESS";
            var legalBeforeComplete = outcome.Value == "NOT-APPLICABLE";
            var legalAfterComplete = execution.Value == "COMPLETE"
                && outcome.Value is "PASS" or "PASS-WITH-ACTIONS" or "FAIL" or "BLOCKED";
            if ((executionNotComplete && !legalBeforeComplete) || (!executionNotComplete && !legalAfterComplete))
            {
                diagnostics.Add(Diagnostic(
                    "GATE_EXECUTION_OUTCOME_INVALID",
                    $"{gateId} execution '{execution.Value}' and outcome '{outcome.Value}' are not a legal pair.",
                    execution.SourceReferences.Concat(outcome.SourceReferences)));
            }
        }
    }

    private static IReadOnlyList<ReadinessTableRow> ParseTables(SourceDocument document)
    {
        var lines = document.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var rows = new List<ReadinessTableRow>();
        var tableIndex = 0;
        for (var index = 0; index + 1 < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith('|')
                || !IsSeparator(lines[index + 1]))
            {
                continue;
            }

            var headers = PlanningParserSupport.SplitPipeRow(lines[index]);
            tableIndex++;
            var rowIndex = 0;
            index += 2;
            for (; index < lines.Length && lines[index].TrimStart().StartsWith('|'); index++)
            {
                if (IsSeparator(lines[index]))
                {
                    continue;
                }

                var values = PlanningParserSupport.SplitPipeRow(lines[index]);
                if (values.Count == headers.Count)
                {
                    var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var cellIndex = 0; cellIndex < headers.Count; cellIndex++)
                    {
                        cells[headers[cellIndex]] = values[cellIndex];
                    }

                    rowIndex++;
                    rows.Add(new ReadinessTableRow
                    {
                        SourceLine = index + 1,
                        TableIndex = tableIndex,
                        RowIndex = rowIndex,
                        Cells = cells,
                        SourceReference = document.SourceReference with
                        {
                            RelativeFile = document.RelativeFile,
                            Table = $"table-{tableIndex:D2}",
                            Item = $"row-{rowIndex:D3}",
                            ExtractionRule = "ideaengineering-readiness-markdown-table",
                            ValidationState = ValidationState.Known
                        }
                    });
                }
            }

            index--;
        }

        return rows;
    }

    private static bool IsSeparator(string line) =>
        PlanningParserSupport.SplitPipeRow(line).Count > 0
        && PlanningParserSupport.SplitPipeRow(line).All(cell => Regex.IsMatch(cell, @"^:?-{3,}:?$"));

    private static SourceDocument? GetDocument(
        IReadOnlyDictionary<string, SourceDocument> documents,
        string root,
        string fileName) =>
        documents.GetValueOrDefault($"{root}/{fileName}");

    private static bool IsReadinessPath(string path) =>
        path.Replace('\\', '/').StartsWith("specs/", StringComparison.OrdinalIgnoreCase);

    private static string GetIncrementRoot(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var fileName in new[]
        {
            "/README.md",
            "/readiness-register.md",
            "/tasks.md",
            "/trace-matrix.md",
            "/contracts/decision-and-evidence-register.md",
            "/contracts/pg4-gate-record.md",
            "/pg4-gate-record.md"
        })

        {
            var marker = normalized.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
            {
                return normalized[..marker];
            }
        }

        return string.Empty;
    }

    private static (string Id, string? Name)? ParseIdentity(string content)
    {
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var match = IncrementPattern.Match(line);
            if (match.Success)
            {
                return (match.Groups["id"].Value.Trim(), NullIfBlank(match.Groups["name"].Value));
            }
        }

        var inlineMatch = InlineIncrementPattern.Match(content);
        if (inlineMatch.Success)
        {
            var name = content
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => HeadingIncrementNamePattern.Match(line))
                .Where(match => match.Success)
                .Select(match => NullIfBlank(match.Groups["name"].Value))
                .FirstOrDefault(value => value is not null);
            return (inlineMatch.Groups["id"].Value.Trim(), name);
        }

        return null;
    }

    private static string? ParseStatus(string content)
    {
        foreach (var line in content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var match = VersionStatusPattern.Match(line);
            if (match.Success)
            {
                return NormalizeDisplay(match.Groups["value"].Value);
            }
        }

        var tableMatch = StatusTablePattern.Match(content);
        if (tableMatch.Success)
        {
            return NormalizeDisplay(tableMatch.Groups["value"].Value);
        }

        return null;
    }

    private static string? ParsePhaseId(string content)
    {
        var match = HeadingPhasePattern.Match(content);
        return match.Success ? match.Groups["phase"].Value.ToUpperInvariant() : null;
    }

    private static string? NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(
            value.ToUpperInvariant(),
            @"\b(PASS-WITH-ACTIONS|NOT-APPLICABLE|IN-PROGRESS|NOT-RUN|DEFERRED_SCOPE|BLOCKED|COMPLETE|PASS|FAIL|OPEN|RESOLVED|CHECKED|UNCHECKED|DEFERRED)\b");
        return match.Success ? match.Value : null;
    }

    private static string NormalizeRecordId(string? value) =>
        Regex.Replace(value?.Trim() ?? string.Empty, @"[^A-Za-z0-9_-]", string.Empty).ToUpperInvariant();

    private static string Normalize(string? value) =>
        Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", string.Empty).ToUpperInvariant();

    private static string NormalizeDisplay(string value) =>
        Regex.Replace(value.Trim(), @"[*]", string.Empty).Trim();

    private static string Compact(string? value) =>
        Normalize(value).Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

    private static string? ParseGateId(string? value)
    {
        var match = GateIdPattern.Match(value ?? string.Empty);
        return match.Success ? match.Groups["id"].Value.ToUpperInvariant() : null;
    }

    private static (string? Id, string? Summary) ParseSuccessor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var normalized = SafeSummary(value);
        var match = SuccessorIncrementPattern.Match(value);
        if (!match.Success)
        {
            return (null, normalized);
        }

        var summary = value[(match.Index + match.Length)..]
            .Trim()
            .TrimStart(' ', ':', '-', '—', ';', '·');
        return (match.Groups["id"].Value.ToUpperInvariant(), SafeSummary(summary));
    }

    private static string? SafeSummary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var summary = Regex.Replace(value.Replace('`', ' '), @"\s+", " ").Trim();
        if (Path.IsPathRooted(summary)
            || Regex.IsMatch(summary, @"\b[A-Za-z]:[\\/]"))
        {
            return "Source detail omitted";
        }

        summary = summary.Replace("|", " / ", StringComparison.Ordinal);
        return summary.Length <= 240 ? summary : summary[..237] + "...";
    }

    private static string? BuildPendingActionSummary(
        string recordId,
        string? blockerText,
        EvidenceRoleReference? waitingFor)
    {
        var summary = SafeSummary(blockerText);
        if (summary is not null)
        {
            return summary;
        }

        return waitingFor is null
            ? null
            : $"{recordId} requires {waitingFor.DisplayLabel.ToLowerInvariant()} disposition";
    }

    private static RoleParseResult ParseRoles(string? value, string? context = null)
    {
        var roles = FindRoles(value);
        var owner = roles.FirstOrDefault();
        var reviewer = roles.FirstOrDefault(role => role.Code.EndsWith("_REVIEWER", StringComparison.Ordinal));
        EvidenceRoleReference? waitingFor = null;
        var contextText = $"{value} {context}".ToUpperInvariant();
        if (reviewer is not null
            && !ReferenceEquals(owner, reviewer)
            && (roles.Count > 1
                || contextText.Contains("PENDING", StringComparison.Ordinal)
                || contextText.Contains("REVIEW", StringComparison.Ordinal)
                || contextText.Contains("DISPOSITION", StringComparison.Ordinal)))
        {
            waitingFor = reviewer;
        }

        return new RoleParseResult(owner, waitingFor);
    }

    private static IReadOnlyList<EvidenceRoleReference> FindRoles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<EvidenceRoleReference>();
        }

        var sourceMeaning = SafeSummary(value);
        var normalized = value.ToUpperInvariant();
        var matches = new List<(int Position, EvidenceRoleReference Role)>();

        void Add(string code, string displayLabel, params string[] markers)
        {
            var position = markers
                .Select(marker => normalized.IndexOf(marker, StringComparison.Ordinal))
                .Where(index => index >= 0)
                .DefaultIfEmpty(int.MaxValue)
                .Min();
            if (position < int.MaxValue)
            {
                matches.Add((position, new EvidenceRoleReference
                {
                    Code = code,
                    DisplayLabel = displayLabel,
                    SourceMeaning = sourceMeaning
                }));
            }
        }

        Add("PRODUCT_DECISION_AUTHORITY", "Product Decision Authority", "PRODUCT DECISION AUTHORITY");
        Add("GATE_AUTHORITY", "Gate Authority", "GATE AUTHORITY");
        Add("ENGINEERING_AUTHORITY", "Engineering Authority", "ENGINEERING AUTHORITY");
        Add("PROJECT_AUTHORITY", "Project Authority", "PROJECT AUTHORITY");
        Add("PRODUCT_AUTHOR", "Principal Product Author", "PRINCIPAL PRODUCT AUTHOR");
        if (!normalized.Contains("PRINCIPAL PRODUCT AUTHOR", StringComparison.Ordinal))
        {
            Add("PRODUCT_AUTHOR", "Product Author", "PRODUCT AUTHOR");
        }

        Add("PROJECT_REVIEWER", "Project Reviewer", "PROJECT REVIEWER");
        Add("VERIFICATION_REVIEWER", "Verification Reviewer", "VERIFICATION REVIEWER");
        Add("SECURITY_REVIEWER", "Security Reviewer", "SECURITY REVIEWER");
        Add("DATA_CUSTODIAN", "Data Custodian", "DATA CUSTODIAN");
        Add("OPERATIONS", normalized.Contains("QLHT", StringComparison.Ordinal) ? "QLHT / Operations" : "Operations", "OPERATIONS", "QLHT");
        Add("AUTHORITY", "Authority", "AUTHORITY");

        return matches
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Role.Code, StringComparer.Ordinal)
            .Select(item => item.Role)
            .GroupBy(role => role.Code, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static SourceReference WithAuthority(SourceReference reference, int authorityRank, string extractionRule) =>
        reference with
        {
            RelativeFile = reference.RelativeFile.Replace('\\', '/'),
            AuthorityRank = authorityRank,
            ExtractionRule = extractionRule,
            ValidationState = ValidationState.Known
        };

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : NormalizeDisplay(value);

    private static string? NormalizeRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.ToUpperInvariant();
        if (normalized.Contains("REVIEW", StringComparison.Ordinal))
        {
            return "PROJECT_REVIEWER";
        }

        if (normalized.Contains("OPERATION", StringComparison.Ordinal))
        {
            return "OPERATIONS";
        }

        if (normalized.Contains("AUTHOR", StringComparison.Ordinal))
        {
            return "PRODUCT_AUTHOR";
        }

        if (normalized.Contains("CUSTODIAN", StringComparison.Ordinal))
        {
            return "DATA_CUSTODIAN";
        }

        if (normalized.Contains("AUTHORITY", StringComparison.Ordinal))
        {
            return "AUTHORITY";
        }

        return "ROLE_RECORDED";
    }

    private static string? NormalizeDueCondition(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var packageIds = Regex.Matches(value.ToUpperInvariant(), @"P\d{2}")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return packageIds.Length == 0
            ? "CONDITION_RECORDED"
            : $"CHECKPOINT_{string.Join("_", packageIds)}";
    }

    private static string? NormalizeGateEffect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.ToUpperInvariant();
        var codes = new List<string>();
        codes.AddRange(Regex.Matches(normalized, @"\bBLOCKS_PG\d+\b")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal));
        codes.AddRange(new[] { "DEFERRED_SCOPE", "AUTHORIZATION", "NONE" }
            .Where(code => normalized.Contains(code, StringComparison.Ordinal)));
        if (codes.Count == 0)
        {
            var gateId = ParseGateId(value);
            if (gateId is not null)
            {
                codes.Add(gateId);
            }
        }

        return codes.Count == 0 ? "GATE_EFFECT_RECORDED" : string.Join("|", codes.Distinct(StringComparer.Ordinal));
    }

    private static string? NormalizeBlocker(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.ToUpperInvariant();
        if (normalized.Contains("BLOCK", StringComparison.Ordinal))
        {
            return "BLOCKED";
        }

        if (normalized.Contains("PENDING", StringComparison.Ordinal)
            || normalized.Contains("NOT-RUN", StringComparison.Ordinal)
            || normalized.Contains("REVIEW", StringComparison.Ordinal)
            || normalized.Contains("REQUIRES", StringComparison.Ordinal)
            || normalized.Contains("CHỜ", StringComparison.Ordinal))
        {
            return "EVIDENCE_PENDING";
        }

        return "BLOCKER_RECORDED";
    }

    private static IReadOnlyList<string> SafeEvidenceLinks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return EvidenceLinkPattern.Matches(value)
            .Select(match => match.Groups["link"].Value.Trim().Replace('\\', '/'))
            .Where(link => !Path.IsPathRooted(link) && !link.Split('/').Any(segment => segment == ".."))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? Cell(ReadinessTableRow row, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var normalizedAlias = Normalize(alias);
            var pair = row.Cells.FirstOrDefault(item => Normalize(item.Key) == normalizedAlias);
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                return pair.Value.Trim();
            }
        }

        return null;
    }

    private static ImportWarning Diagnostic(
        string code,
        string message,
        IEnumerable<SourceReference> sourceReferences)
    {
        var references = sourceReferences
            .Where(reference => !Path.IsPathRooted(reference.RelativeFile))
            .Select(reference => reference with { RelativeFile = reference.RelativeFile.Replace('\\', '/') })
            .ToArray();
        return new ImportWarning
        {
            Id = $"{code}:{references.FirstOrDefault()?.RelativeFile ?? "management-evidence"}",
            Severity = WarningSeverity.Warning,
            Code = code,
            Message = message,
            SourceReferences = references
        };
    }

    private sealed record IncrementCandidate(
        string Root,
        string IncrementId,
        string? IncrementName,
        string? IncrementStatus,
        string? IncrementPhaseId,
        IReadOnlyDictionary<string, SourceDocument> Documents);

    private sealed record RoleParseResult(
        EvidenceRoleReference? Owner,
        EvidenceRoleReference? WaitingFor);

    private sealed record ReadinessTableRow
    {
        public int SourceLine { get; init; }
        public int TableIndex { get; init; }
        public int RowIndex { get; init; }
        public IReadOnlyDictionary<string, string> Cells { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public SourceReference SourceReference { get; init; } = new();
    }
}
