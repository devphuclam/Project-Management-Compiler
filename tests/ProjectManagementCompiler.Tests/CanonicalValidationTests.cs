using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class CanonicalValidationTests
{
    public static void CanonicalValidatorRejectsMissingProjectAndSourceRelationships()
    {
        var project = CaptureCanonicalProject();

        AssertHasCode(project with
        {
            Project = project.Project with { Id = string.Empty }
        }, "INVALID_PROJECT_ID");

        AssertHasCode(project with
        {
            Project = project.Project with { SourceIds = ["missing-source"] }
        }, "INVALID_PROJECT_SOURCE");

        AssertHasCode(project with
        {
            Sources = [project.Sources[0], project.Sources[0] with { Id = project.Sources[0].Id }]
        }, "DUPLICATE_SOURCE_ID");

        var invalidReference = project.Provenance[0] with { SourceId = "missing-source" };
        AssertHasCode(project with { Provenance = [invalidReference] }, "INVALID_SOURCE_REFERENCE");
    }

    public static void CanonicalValidatorRejectsBaselineAndHierarchyViolations()
    {
        var project = CaptureCanonicalProject();
        var baseline = project.Baseline;

        AssertHasCode(project with
        {
            Baseline = baseline with { Id = string.Empty }
        }, "INVALID_BASELINE_ID");

        AssertHasCode(project with
        {
            Baseline = baseline with { AuthorityDocumentId = "missing-document" }
        }, "INVALID_BASELINE_AUTHORITY");

        AssertHasCode(project with
        {
            Baseline = baseline with { PlanningStart = DateOnly.MinValue }
        }, "INVALID_BASELINE_DATE");

        AssertHasCode(project with
        {
            Baseline = baseline with
            {
                PlanningStart = new DateOnly(2026, 12, 31),
                PlanningFinish = new DateOnly(2026, 9, 18)
            }
        }, "INVALID_BASELINE_DATE_RANGE");

        AssertHasCode(project with
        {
            Phases = [project.Phases[0], project.Phases[0]]
        }, "DUPLICATE_PHASE_ID");

        AssertHasCode(project with
        {
            WorkPackages = [project.WorkPackages[0] with { PhaseId = "missing-phase" }]
        }, "INVALID_WORK_PACKAGE_PHASE");

        AssertHasCode(project with
        {
            DeliveryCards = [project.DeliveryCards[0] with { WorkPackageId = "missing-work-package" }]
        }, "INVALID_CARD_WORK_PACKAGE");

        AssertHasCode(project with
        {
            DeliveryCards = [project.DeliveryCards[0] with { PhaseId = "missing-phase" }]
        }, "INVALID_CARD_PHASE");

        AssertHasCode(project with
        {
            Milestones = [project.Milestones[0] with { ParentId = "missing-parent" }]
        }, "INVALID_MILESTONE_PARENT");
    }

    public static void CanonicalValidatorRejectsRolesAssignmentsAndDependencyContracts()
    {
        var project = CaptureCanonicalProject();

        AssertHasCode(project with
        {
            Assignments = [project.Assignments[0] with { WorkItemId = "missing-card" }]
        }, "INVALID_ASSIGNMENT_TARGET");

        AssertHasCode(project with
        {
            Assignments = [project.Assignments[0] with { LogicalRoleCode = "missing-role" }]
        }, "INVALID_ASSIGNMENT_ROLE");

        AssertHasCode(project with
        {
            Assignments = [project.Assignments[0] with { CarioRoleCode = "BAD" }]
        }, "INVALID_CARIO_ROLE_CODE");

        AssertHasCode(project with
        {
            Dependencies = [project.Dependencies[0] with { SubjectId = "missing-subject" }]
        }, "INVALID_DEPENDENCY_SUBJECT");

        AssertHasCode(project with
        {
            Dependencies = [project.Dependencies[0] with
            {
                PredecessorId = "missing-predecessor",
                ValidationState = ValidationState.Warning,
                AnalysisEligible = true
            }]
        }, "INVALID_DEPENDENCY_PREDECESSOR");

        AssertHasCode(project with
        {
            Dependencies = [project.Dependencies[0] with
            {
                ValidationState = ValidationState.InvalidSourceEvidence,
                AnalysisEligible = true
            }]
        }, "INVALID_DEPENDENCY_ANALYSIS_STATE");

        AssertHasCode(project with
        {
            Dependencies = [project.Dependencies[0] with { ValidationState = (ValidationState)999 }]
        }, "INVALID_DEPENDENCY_VALIDATION_STATE");

        AssertHasCode(project with
        {
            DeliveryCards = [project.DeliveryCards[0] with { State = (ExecutionState)999 }]
        }, "INVALID_CARD_STATE");

        AssertHasCode(project with
        {
            Milestones = [project.Milestones[0] with { Kind = (MilestoneKind)999 }]
        }, "INVALID_MILESTONE_KIND");
    }

    public static void CanonicalValidatorRejectsDuplicateExecutableAndInvalidOverlayRecords()
    {
        var project = CaptureCanonicalProject();

        AssertHasCode(project with
        {
            DeliveryCards = [project.DeliveryCards[0], project.DeliveryCards[0]]
        }, "DUPLICATE_CARD_ID");

        var record = new ExecutionRecord
        {
            WorkItemId = project.DeliveryCards[0].Id,
            ExecutionState = ExecutionState.NotStarted,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
        AssertHasCode(project with
        {
            ExecutionOverlay = new ExecutionOverlay { Records = [record, record] }
        }, "DUPLICATE_EXECUTION_RECORD");
    }

    public static void CanonicalValidatorUsesTypedIdsForRealShapedP04AndDependencies()
    {
        var project = CaptureCanonicalProject("ideaengineering-real-shaped");
        var diagnostics = CanonicalProjectValidator.Validate(project);

        TestAssert.False(diagnostics.Any(diagnostic => diagnostic.Code is "INVALID_DEPENDENCY_SUBJECT_KIND" or "INVALID_DEPENDENCY_PREDECESSOR_KIND" or "DUPLICATE_DEPENDENCY"), "Cross-kind P04 dependencies must remain valid and distinct.");
        TestAssert.True(project.DeliveryCards.Any(card => card.Id == "P04"), "The real-shaped fixture must retain DeliveryCard P04.");
        TestAssert.True(project.WorkPackages.Any(workPackage => workPackage.Id == "P04"), "The real-shaped fixture must retain WorkPackage P04.");
        TestAssert.True(project.Assignments.Any(assignment => assignment.WorkItemId == "P04"), "The responsibility matrix must contain an assignment for DeliveryCard P04.");

        var cardDependency = project.Dependencies.Single(dependency =>
            dependency.SubjectKind == "DeliveryCard" && dependency.SubjectId == "P04");
        var duplicateDiagnostics = CanonicalProjectValidator.Validate(project with
        {
            Dependencies = project.Dependencies.Append(cardDependency).ToArray()
        });
        TestAssert.True(duplicateDiagnostics.Any(diagnostic => diagnostic.Code == "DUPLICATE_DEPENDENCY"), "An identical typed dependency must still be diagnosed as a duplicate.");
    }

    private static void AssertHasCode(CanonicalProject project, string code)
    {
        var diagnostics = CanonicalProjectValidator.Validate(project);
        TestAssert.True(
            diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Canonical validation must emit '{code}'. Actual codes: {string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Code).Distinct(StringComparer.Ordinal))}");
    }

    private static CanonicalProject CaptureCanonicalProject(string fixtureName = "ideaengineering")
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", fixtureName);
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }
}
