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
        }

        var readme = GetDocument(documents, candidate.Root, "README.md");
        if (readme is not null)
        {
            observations.AddRange(ExtractGateObservations(ParseTables(readme)));
        }

        var tasks = GetDocument(documents, candidate.Root, "tasks.md");
        if (tasks is not null)
        {
            observations.AddRange(ExtractChecklistContext(tasks));
        }

        EnsureUniqueObservationIds(observations);
        ValidateConflictingObservations(observations, diagnostics);
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

    private static void ValidateConflictingObservations(
        IEnumerable<ManagementEvidenceObservation> observations,
        ICollection<ImportWarning> diagnostics)
    {
        foreach (var group in observations
                     .Where(observation => !string.IsNullOrWhiteSpace(observation.SourceRecordId))
                     .GroupBy(observation => (observation.EvidenceKind, SourceRecordId: observation.SourceRecordId)))
        {
            var meanings = group
                .Select(observation => $"{observation.StateCode ?? "-"}/{observation.ResultCode ?? "-"}")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (meanings.Length < 2)
            {
                continue;
            }

            diagnostics.Add(Diagnostic(
                "EVIDENCE_CONFLICT",
                $"Evidence record '{group.Key.SourceRecordId}' has conflicting attributable state/result values; all source observations were preserved.",
                group.SelectMany(observation => observation.SourceReferences)));
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

        var row = ParseTables(register)
            .FirstOrDefault(item => Normalize(Cell(item, "Field", "Control field", "Trường")) == "STABLERECORDID");
        var recordId = row is null ? $"{candidate.IncrementId}-CONTROL" : Cell(row, "Value", "Recorded value", "Giá trị") ?? $"{candidate.IncrementId}-CONTROL";
        return new ManagementEvidenceObservation
        {
            Id = $"control:{NormalizeRecordId(recordId)}",
            EvidenceKind = ManagementEvidenceKind.ControlEnvelope,
            SourceRecordId = NormalizeRecordId(recordId),
            SourceReferences = row is null ? [register.SourceReference] : [row.SourceReference],
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
                OwnerRole = NormalizeRole(Cell(row, "Owner", "Required role", "Accountable owner / authority")),
                DueCondition = NormalizeDueCondition(Cell(row, "Due condition", "Due condition / closure evidence")),
                GateEffect = NormalizeGateEffect(Cell(row, "Gate effect", "Affected work / gate effect")),
                BlockerOrDeviation = NormalizeBlocker(Cell(row, "Blocker / deviation", "Blocker", "Deviation")),
                EvidenceLinks = SafeEvidenceLinks(Cell(row, "Evidence link", "Evidence", "Evidence links")),
                SourceReferences = [row.SourceReference],
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
            observations.Add(new ManagementEvidenceObservation
            {
                Id = $"decision:{id}",
                EvidenceKind = ManagementEvidenceKind.DecisionRecord,
                SourceRecordId = id,
                StateCode = NormalizeCode(question),
                OwnerRole = NormalizeRole(Cell(row, "Accountable owner / authority", "Required authority")),
                DueCondition = NormalizeDueCondition(Cell(row, "Due condition / closure evidence")),
                GateEffect = NormalizeGateEffect(Cell(row, "Affected work / gate effect", "Effect on PH1")),
                SourceReferences = [row.SourceReference],
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

            observations.Add(new ManagementEvidenceObservation
            {
                Id = $"human-action:{id}",
                EvidenceKind = ManagementEvidenceKind.HumanAction,
                SourceRecordId = id,
                StateCode = NormalizeCode(Cell(row, "Status", "Current status", "Trạng thái hiện tại")),
                OwnerRole = NormalizeRole(Cell(row, "Required role", "Vai trò hoặc authority bắt buộc")),
                GateEffect = NormalizeGateEffect(Cell(row, "Effect", "Ảnh hưởng / kết quả sau khi hoàn tất")),
                SourceReferences = [row.SourceReference],
                AuthorityRank = 3,
                AuthorityKind = "human-action-board",
                ValidationState = ValidationState.Known
            });
        }

        return observations;
    }

    private static IReadOnlyList<ManagementEvidenceObservation> ExtractGateObservations(IReadOnlyList<ReadinessTableRow> rows)
    {
        var observations = new List<ManagementEvidenceObservation>();
        foreach (var row in rows)
        {
            var label = Normalize(Cell(row, "Field", "Control field", "Trường", "Nội dung", "Content", "Item"));
            var value = Cell(row, "Value", "Recorded value", "Giá trị", "Trạng thái", "Status", "State");
            if (value is null)
            {
                continue;
            }

            if (label.Contains("GATEEXECUTIONSTATE", StringComparison.Ordinal)
                || label.Equals("EXECUTIONSTATE", StringComparison.Ordinal))
            {
                observations.Add(new ManagementEvidenceObservation
                {
                    Id = "gate:execution",
                    EvidenceKind = ManagementEvidenceKind.GateExecution,
                    SourceRecordId = "PG4",
                    RawTargetKind = "Gate",
                    RawTargetId = "PG4",
                    ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = "PG4" },
                    StateCode = NormalizeCode(value),
                    SourceReferences = [row.SourceReference],
                    AuthorityRank = 2,
                    AuthorityKind = "pg4-gate-record",
                    ValidationState = ValidationState.Known
                });
            }
            else if (label.Contains("GATEOUTCOME", StringComparison.Ordinal)
                || label.Equals("OUTCOME", StringComparison.Ordinal))
            {
                observations.Add(new ManagementEvidenceObservation
                {
                    Id = "gate:outcome",
                    EvidenceKind = ManagementEvidenceKind.GateOutcome,
                    SourceRecordId = "PG4",
                    RawTargetKind = "Gate",
                    RawTargetId = "PG4",
                    ExplicitTarget = new EvidenceTarget { Kind = "Gate", Id = "PG4" },
                    ResultCode = NormalizeCode(value),
                    SourceReferences = [row.SourceReference],
                    AuthorityRank = 2,
                    AuthorityKind = "pg4-gate-record",
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
        var execution = observations.FirstOrDefault(observation => observation.EvidenceKind == ManagementEvidenceKind.GateExecution);
        var outcome = observations.FirstOrDefault(observation => observation.EvidenceKind == ManagementEvidenceKind.GateOutcome);
        if (execution is null || outcome is null)
        {
            return;
        }

        var executionNotComplete = execution.StateCode is "NOT-RUN" or "IN-PROGRESS";
        var legalBeforeComplete = outcome.ResultCode == "NOT-APPLICABLE";
        var legalAfterComplete = execution.StateCode == "COMPLETE"
            && outcome.ResultCode is "PASS" or "PASS-WITH-ACTIONS" or "FAIL" or "BLOCKED";
        if ((executionNotComplete && !legalBeforeComplete) || (!executionNotComplete && !legalAfterComplete))
        {
            diagnostics.Add(Diagnostic(
                "GATE_EXECUTION_OUTCOME_INVALID",
                $"PG4 execution '{execution.StateCode}' and outcome '{outcome.ResultCode}' are not a legal pair.",
                execution.SourceReferences.Concat(outcome.SourceReferences)));
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
            "/contracts/pg4-gate-record.md"
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
            @"\b(PASS-WITH-ACTIONS|NOT-APPLICABLE|IN-PROGRESS|NOT-RUN|DEFERRED_SCOPE|BLOCKED|COMPLETE|PASS|FAIL|OPEN|RESOLVED|CHECKED|UNCHECKED)\b");
        return match.Success ? match.Value : null;
    }

    private static string NormalizeRecordId(string? value) =>
        Regex.Replace(value?.Trim() ?? string.Empty, @"[^A-Za-z0-9_-]", string.Empty).ToUpperInvariant();

    private static string Normalize(string? value) =>
        Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", string.Empty).ToUpperInvariant();

    private static string NormalizeDisplay(string value) =>
        Regex.Replace(value.Trim(), @"[*]", string.Empty).Trim();

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
        var codes = new[] { "BLOCKS_PG4", "DEFERRED_SCOPE", "AUTHORIZATION", "PG4" }
            .Where(code => normalized.Contains(code, StringComparison.Ordinal))
            .ToArray();
        return codes.Length == 0 ? "GATE_EFFECT_RECORDED" : string.Join("|", codes);
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
