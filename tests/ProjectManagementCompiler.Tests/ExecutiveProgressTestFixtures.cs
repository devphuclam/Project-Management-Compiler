using System.IO.Compression;
using System.Xml.Linq;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveProgressTestFixtures
{
    public static readonly DateOnly DailyGanttDefaultReportingDate = new(2026, 9, 30);
    public static readonly DateOnly DailyGanttBaselineStart = new(2026, 9, 18);
    public const string CompletedEarlyCardId = "P01-A";
    public const string CompletedLateCardId = "P01-B";
    public const string OpenInProgressCardId = "P02-A";
    public const string InProgressWithoutStartCardId = "P02-B";
    public const string ExplicitNotStartedCardId = "P03-A";
    public const string UnrecordedCardId = "P03-B";
    public const string SuspendedCardId = "P04-A";
    public const string CancelledCardId = "P04-B";
    public const string OneSidedEffortCardId = "P05-A";
    public const string ZeroSumEffortCardId = "P06-A";
    public const string MissingEffortCardId = "P07-A";
    public const string OneDayCompletedCardId = "F01-A";
    public const string RepeatedRawId = "P01";
    public const string FinishOnlyCardId = ExplicitNotStartedCardId;
    public const string EffortOnlyCardId = InProgressWithoutStartCardId;
    public const string NoExecutionEvidenceCardId = UnrecordedCardId;
    public const int ExpectedProjectCount = 1;
    public const int ExpectedPhaseCount = 6;
    public const int ExpectedWorkPackageCount = 35;
    public const int ExpectedDeliveryCardCount = 53;

    public static IReadOnlyList<(string SourceText, string? ExactId, string Expected)> ReaderFacingTitleCases { get; } =
    [
        ("[PH0][PLN01] Delivery Card → **Xác nhận bộ tài liệu được phép dùng để bắt đầu**", "PLN01", "Xác nhận bộ tài liệu được phép dùng để bắt đầu"),
        ("P01-A — P01-A — Chốt phạm vi phiên bản thử nghiệm cuối năm", "P01-A", "Chốt phạm vi phiên bản thử nghiệm cuối năm"),
        ("[Bắt buộc] Giữ nguyên nội dung có ý nghĩa", "P02-A", "[Bắt buộc] Giữ nguyên nội dung có ý nghĩa"),
        ("", null, "Chưa ghi nhận")
    ];

    public static CompilationResult BuildOfficialFixtureResult(DateOnly? asOfDate = null)
    {
        var project = CapturePlanningFixture();
        var sourceProject = SourceExecutionTestFixtures.Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 19),
            ActualEffortHours = 4m,
            RemainingEffortHours = 4m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero)
        });
        return SourceExecutionTestFixtures.BuildResult(sourceProject, asOfDate ?? new DateOnly(2026, 9, 19));
    }

    /// <summary>
    /// Builds an official, deterministic snapshot for daily-Gantt projections.
    /// The fixture intentionally contains complete, sparse, and unrecorded
    /// execution evidence without ever promoting a proposal or overlay to source truth.
    /// </summary>
    public static CompilationResult BuildDailyGanttFixtureResult(
        DateOnly? sourceReportingDate = null,
        DateOnly? analysisAsOfDate = null)
    {
        var reportingDate = sourceReportingDate ?? DailyGanttDefaultReportingDate;
        return SourceExecutionTestFixtures.BuildResult(
            BuildDailyGanttFixtureProject(reportingDate),
            analysisAsOfDate ?? reportingDate);
    }

    /// <summary>
    /// Feature 007 acceptance fixture. It preserves the established daily-Gantt
    /// fixture while adding a finish-only completion and deterministic
    /// reader-facing title noise. Together with the existing records it covers
    /// recorded interval, open interval, completion point, effort-only, and no
    /// execution evidence without changing legacy fixture expectations.
    /// </summary>
    public static CompilationResult BuildFeature007ReportFixtureResult(
        DateOnly? sourceReportingDate = null,
        DateOnly? analysisAsOfDate = null)
    {
        var reportingDate = sourceReportingDate ?? DailyGanttDefaultReportingDate;
        return SourceExecutionTestFixtures.BuildResult(
            BuildFeature007ReportFixtureProject(reportingDate),
            analysisAsOfDate ?? reportingDate);
    }

    public static CanonicalProject BuildFeature007ReportFixtureProject(
        DateOnly? sourceReportingDate = null)
    {
        var reportingDate = sourceReportingDate ?? DailyGanttDefaultReportingDate;
        var project = BuildDailyGanttFixtureProject(reportingDate);

        var cards = project.DeliveryCards
            .Select(card => card.Id switch
            {
                CompletedEarlyCardId => card with
                {
                    Name = "[PH0][P01][P01-A] Delivery Card → **Xác nhận bộ tài liệu được phép dùng để bắt đầu**"
                },
                CompletedLateCardId => card with
                {
                    Name = "P01-B — P01-B — Chốt phạm vi phiên bản thử nghiệm cuối năm"
                },
                OpenInProgressCardId => card with
                {
                    Name = "[Bắt buộc] Giữ nguyên nội dung có ý nghĩa"
                },
                _ => card
            })
            .ToArray();

        var records = project.SourceExecution.Records
            .Select(record => string.Equals(
                    record.Entity.Id,
                    FinishOnlyCardId,
                    StringComparison.OrdinalIgnoreCase)
                ? Recorded(
                    FinishOnlyCardId,
                    ExecutionState.Completed,
                    SourceResultState.Pass,
                    actualFinish: new DateOnly(2026, 9, 24),
                    actualEffortHours: 2m,
                    remainingEffortHours: 0m,
                    lastUpdatedAt: AtUtc(2026, 9, 24, 16),
                    evidence: [CompletionEvidence(FinishOnlyCardId, new DateOnly(2026, 9, 24))])
                : record)
            .ToArray();

        return project with
        {
            DeliveryCards = cards,
            SourceExecution = project.SourceExecution with { Records = records }
        };
    }

    public static CompilationResult BuildReportingDateBeforeBaselineFixtureResult() =>
        BuildDailyGanttFixtureResult(
            sourceReportingDate: new DateOnly(2026, 9, 1),
            analysisAsOfDate: new DateOnly(2026, 9, 1));

    public static CompilationResult BuildReportingDateAfterBaselineFixtureResult() =>
        BuildDailyGanttFixtureResult(
            sourceReportingDate: new DateOnly(2027, 1, 5),
            analysisAsOfDate: new DateOnly(2027, 1, 5));

    public static CanonicalProject BuildDailyGanttFixtureProject(DateOnly? sourceReportingDate = null)
    {
        var reportingDate = sourceReportingDate ?? DailyGanttDefaultReportingDate;
        var planning = CapturePlanningFixture();
        var metadata = OfficialMetadata(planning, reportingDate);
        var cards = planning.DeliveryCards
            .Select(card => card.Id == OneSidedEffortCardId
                ? card with
                {
                    Name = "[P05-A] **Chuẩn bị dữ liệu, tài khoản và hướng dẫn vận hành** — mô tả dài có dấu tiếng Việt để kiểm tra báo cáo quản trị"
                }
                : card)
            .ToArray();

        return planning with
        {
            SchemaVersion = "2.0",
            DeliveryCards = cards,
            Milestones = planning.Milestones
                .Concat(
                [
                    new MilestoneDecision
                    {
                        // This intentionally shares the raw ID of work package P01.
                        // Canonical identity remains unambiguous because the kind is Milestone.
                        Id = RepeatedRawId,
                        Kind = MilestoneKind.Milestone,
                        ParentId = RepeatedRawId,
                        Name = "Mốc kiểm tra có mã trùng P01",
                        PlannedDate = new DateOnly(2026, 9, 24),
                        State = ExecutionState.NotStarted,
                        PlannedEffortHours = 0m,
                        PlannedDurationWorkingMinutes = 0
                    }
                ])
                .ToArray(),
            ImportMetadata = metadata,
            SourceExecution = new SourceExecutionSnapshot
            {
                ProjectId = metadata.ProjectId,
                BaselineId = metadata.BaselineId,
                RegisterId = "daily-gantt-execution-register",
                RegisterRevision = metadata.RegisterRevision,
                StatusDate = reportingDate,
                TimeZone = "UTC",
                SourcePath = SourceExecutionPath,
                Records =
                [
                    Recorded(
                        CompletedEarlyCardId,
                        ExecutionState.Completed,
                        SourceResultState.Pass,
                        actualStart: new DateOnly(2026, 9, 16),
                        actualFinish: new DateOnly(2026, 9, 17),
                        actualEffortHours: 3m,
                        remainingEffortHours: 0m,
                        lastUpdatedAt: AtUtc(2026, 9, 17, 16),
                        evidence: [CompletionEvidence(CompletedEarlyCardId, new DateOnly(2026, 9, 17))]),
                    Recorded(
                        CompletedLateCardId,
                        ExecutionState.Completed,
                        SourceResultState.Pass,
                        actualStart: new DateOnly(2026, 9, 21),
                        actualFinish: new DateOnly(2026, 9, 23),
                        actualEffortHours: 5m,
                        remainingEffortHours: 0m,
                        lastUpdatedAt: AtUtc(2026, 9, 23, 16),
                        evidence: [CompletionEvidence(CompletedLateCardId, new DateOnly(2026, 9, 23))]),
                    Recorded(
                        OpenInProgressCardId,
                        ExecutionState.InProgress,
                        SourceResultState.NotApplicable,
                        actualStart: new DateOnly(2026, 9, 21),
                        actualEffortHours: 1m,
                        remainingEffortHours: 7m,
                        forecastFinish: AtUtc(2026, 10, 6, 17),
                        lastUpdatedAt: AtUtc(2026, 9, 30, 9)),
                    Recorded(
                        InProgressWithoutStartCardId,
                        ExecutionState.InProgress,
                        SourceResultState.NotApplicable,
                        actualEffortHours: 2m,
                        remainingEffortHours: 2m,
                        lastUpdatedAt: AtUtc(2026, 9, 30, 10)),
                    Recorded(
                        ExplicitNotStartedCardId,
                        ExecutionState.NotStarted,
                        SourceResultState.NotRun,
                        lastUpdatedAt: AtUtc(2026, 9, 30, 11)),
                    NotRecorded(UnrecordedCardId),
                    Recorded(
                        SuspendedCardId,
                        ExecutionState.Suspended,
                        SourceResultState.Blocked,
                        actualStart: new DateOnly(2026, 9, 25),
                        actualFinish: new DateOnly(2026, 9, 26),
                        actualEffortHours: 2m,
                        remainingEffortHours: 2m,
                        blocker: "Đang chờ quyết định kiểm thử tích hợp.",
                        lastUpdatedAt: AtUtc(2026, 9, 26, 15)),
                    Recorded(
                        CancelledCardId,
                        ExecutionState.Cancelled,
                        SourceResultState.NotApplicable,
                        actualStart: new DateOnly(2026, 9, 28),
                        actualFinish: new DateOnly(2026, 9, 29),
                        actualEffortHours: 1m,
                        remainingEffortHours: 0m,
                        lastUpdatedAt: AtUtc(2026, 9, 29, 15)),
                    Recorded(
                        OneSidedEffortCardId,
                        ExecutionState.InProgress,
                        SourceResultState.NotApplicable,
                        actualStart: new DateOnly(2026, 9, 29),
                        actualEffortHours: 3m,
                        forecastFinish: AtUtc(2026, 10, 7, 17),
                        lastUpdatedAt: AtUtc(2026, 9, 30, 12)),
                    Recorded(
                        ZeroSumEffortCardId,
                        ExecutionState.InProgress,
                        SourceResultState.NotApplicable,
                        actualStart: DailyGanttDefaultReportingDate,
                        actualEffortHours: 0m,
                        remainingEffortHours: 0m,
                        lastUpdatedAt: AtUtc(2026, 9, 30, 13)),
                    Recorded(
                        MissingEffortCardId,
                        ExecutionState.InProgress,
                        SourceResultState.NotApplicable,
                        actualStart: DailyGanttDefaultReportingDate,
                        lastUpdatedAt: AtUtc(2026, 9, 30, 14)),
                    Recorded(
                        OneDayCompletedCardId,
                        ExecutionState.Completed,
                        SourceResultState.Pass,
                        actualStart: new DateOnly(2026, 9, 29),
                        actualFinish: new DateOnly(2026, 9, 29),
                        actualEffortHours: 4m,
                        remainingEffortHours: 0m,
                        lastUpdatedAt: AtUtc(2026, 9, 29, 16),
                        evidence: [CompletionEvidence(OneDayCompletedCardId, new DateOnly(2026, 9, 29))])
                ]
            },
            ExecutionOverlay = new ExecutionOverlay(),
            ExecutionProposals = Array.Empty<ExecutionProposal>(),
            Analysis = null
        };
    }

    /// <summary>
    /// An invalid source-evidence fixture retained separately from the valid sparse fixture.
    /// It must be rejected rather than made presentable by the daily-Gantt export.
    /// </summary>
    public static CanonicalProject BuildContradictoryActualFixtureProject() =>
        ReplaceSourceExecutionRecord(
            BuildDailyGanttFixtureProject(),
            CompletedEarlyCardId,
            record => record with
            {
                ActualStart = new DateOnly(2026, 9, 18),
                ActualFinish = new DateOnly(2026, 9, 17)
            });

    /// <summary>
    /// An invalid source-evidence fixture retained separately from the valid sparse fixture.
    /// It represents negative effort and must fail closed at validation/export boundaries.
    /// </summary>
    public static CanonicalProject BuildNegativeEffortFixtureProject() =>
        ReplaceSourceExecutionRecord(
            BuildDailyGanttFixtureProject(),
            OpenInProgressCardId,
            record => record with { ActualEffortHours = -0.5m });

    public static CanonicalProject CapturePlanningFixture()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }

    private const string SourceExecutionPath = "tests/fixtures/execution/daily-gantt-register.json";

    private static ManifestSnapshotMetadata OfficialMetadata(CanonicalProject project, DateOnly reportingDate) => new()
    {
        RepositoryIdentity = "test/Project-Management-Compiler",
        ImportMode = ManifestImportMode.GitCommit,
        Classification = ManifestImportClassification.OfficialCommit,
        SourceIdentity = "0123456789abcdef0123456789abcdef01234567",
        ManifestPath = "planning/project-management-compiler-manifest.json",
        ContractVersion = "0.1.0",
        ProjectId = project.Project.Id,
        BaselineId = project.Baseline.Id,
        BaselineVersion = project.Baseline.Version,
        RegisterRevision = 7,
        RegisterStatusDate = reportingDate,
        ValidationResult = ManifestValidationResult.Pass,
        SourceReadiness = SourceReadinessState.Pass,
        SnapshotId = $"daily-gantt-{reportingDate:yyyyMMdd}",
        ImportedAtUtc = AtUtc(reportingDate.Year, reportingDate.Month, reportingDate.Day, 18),
        Calendars = new ProjectCalendarSet
        {
            TimeZone = "UTC",
            BaselineCalendarId = "daily-gantt-baseline",
            ForecastCalendarId = "daily-gantt-forecast",
            DifferenceTreatment = "CalendarDays"
        }
    };

    private static SourceExecutionRecord Recorded(
        string cardId,
        ExecutionState executionState,
        SourceResultState resultState,
        DateOnly? actualStart = null,
        DateOnly? actualFinish = null,
        decimal? actualEffortHours = null,
        decimal? remainingEffortHours = null,
        DateTimeOffset? forecastFinish = null,
        string? blocker = null,
        DateTimeOffset? lastUpdatedAt = null,
        IReadOnlyList<SourceExecutionEvidence>? evidence = null) => new()
        {
            Entity = CanonicalWorkItemKey.DeliveryCard(cardId),
            RecordingState = SourceRecordingState.Recorded,
            ExecutionState = executionState,
            ResultState = resultState,
            ActualStart = actualStart,
            ActualFinish = actualFinish,
            ActualEffortHours = actualEffortHours,
            RemainingEffortHours = remainingEffortHours,
            ForecastFinish = forecastFinish,
            Blocker = blocker,
            Evidence = evidence ?? Array.Empty<SourceExecutionEvidence>(),
            LastUpdatedAt = lastUpdatedAt ?? AtUtc(2026, 9, 30, 8),
            RecordedBy = "TEST",
            SourcePath = SourceExecutionPath
        };

    private static SourceExecutionRecord NotRecorded(string cardId) => new()
    {
        Entity = CanonicalWorkItemKey.DeliveryCard(cardId),
        RecordingState = SourceRecordingState.NotRecorded,
        SourcePath = SourceExecutionPath
    };

    private static SourceExecutionEvidence CompletionEvidence(string cardId, DateOnly recordedDate) => new()
    {
        EvidenceId = $"{cardId}-completion-evidence",
        Type = "SOURCE_RECORD",
        RepositoryPath = SourceExecutionPath,
        Description = "Controlled completion evidence for the daily-Gantt acceptance fixture.",
        Result = "PASS",
        RecordedAt = AtUtc(recordedDate.Year, recordedDate.Month, recordedDate.Day, 16),
        RecordedBy = "TEST"
    };

    private static CanonicalProject ReplaceSourceExecutionRecord(
        CanonicalProject project,
        string cardId,
        Func<SourceExecutionRecord, SourceExecutionRecord> replace) =>
        project with
        {
            SourceExecution = project.SourceExecution with
            {
                Records = project.SourceExecution.Records
                    .Select(record => string.Equals(record.Entity.Id, cardId, StringComparison.OrdinalIgnoreCase)
                        ? replace(record)
                        : record)
                    .ToArray()
            }
        };

    private static DateTimeOffset AtUtc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    public static string ReadEntry(byte[] package, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing XLSX entry '{entryName}'.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    public static XDocument ReadXml(byte[] package, string entryName) => XDocument.Parse(ReadEntry(package, entryName));

    public static IReadOnlyList<string> SheetNames(byte[] package) =>
        ReadXml(package, "xl/workbook.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "sheet")
            .Select(element => (string?)element.Attribute("name") ?? string.Empty)
            .ToArray();

    public static string WorksheetText(byte[] package, int sheetNumber) =>
        string.Join("|", ReadXml(package, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName is "t" or "v")
            .Select(element => element.Value));

    public static IReadOnlyList<string> WorksheetHeaders(byte[] package, int sheetNumber) =>
        ReadXml(package, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Select(row => row.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value).ToArray())
            .FirstOrDefault() ?? Array.Empty<string>();

    public static IReadOnlyList<string> WorksheetRow(byte[] package, int sheetNumber, string firstCellText) =>
        ReadXml(package, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Select(row => row.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value).ToArray())
            .FirstOrDefault(cells => cells.FirstOrDefault() == firstCellText) ?? Array.Empty<string>();

    public static void AssertHasSheetNames(byte[] package, params string[] expected)
    {
        TestAssert.Equal(string.Join('|', expected), string.Join('|', SheetNames(package)), "The workbook sheet names must remain in the approved order.");
    }
}
