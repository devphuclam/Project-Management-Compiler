using System.Reflection;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveProgressExportApplicationTests
{
    public static void ExecutiveExportExposesAnIndependentCompilerSeam()
    {
        var interfaceMethod = typeof(IProjectCompiler).GetMethod("ExportExecutiveProgressXlsx");
        var implementationMethod = typeof(ProjectCompiler).GetMethod("ExportExecutiveProgressXlsx");

        TestAssert.True(interfaceMethod is not null, "The application contract must expose the executive progress export seam.");
        TestAssert.True(implementationMethod is not null, "ProjectCompiler must implement the executive progress export seam.");
        TestAssert.Equal(typeof(byte[]), interfaceMethod!.ReturnType, "The executive export seam must return workbook bytes.");
    }

    public static void ExecutiveExportUsesOfficialStateWithoutMutatingIt()
    {
        var official = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult();
        var proposal = new ExecutionProposal
        {
            Id = "proposal-executive-export",
            BaseSnapshotId = official.Project.ImportMetadata!.SnapshotId,
            ExpectedRegisterRevision = official.Project.ImportMetadata.RegisterRevision,
            TargetKind = "DeliveryCard",
            TargetId = "P01-A"
        };
        official = official with
        {
            Project = official.Project with { ExecutionProposals = [proposal] },
            SemanticDigest = CanonicalJsonDigest.Compute(official.Project with { ExecutionProposals = [proposal] })
        };

        var state = new CompilerApplicationState();
        state.Set(official);
        state.SetXlsxPreview(new Outputs.XlsxPreviewModel
        {
            FileName = "preview.xlsx",
            ProjectId = "preview-project",
            ProjectName = "Preview"
        });

        var digestBefore = state.CurrentOfficialResult!.SemanticDigest;
        var executionBefore = state.CurrentOfficialResult.Project.SourceExecution;
        var proposalsBefore = state.Proposals.ToArray();
        var previewBefore = state.ActiveXlsxPreview;
        var bytes = InvokeExport(new ProjectCompiler(), official);

        TestAssert.True(bytes.Length > 0, "The official executive export must produce a non-empty workbook.");
        TestAssert.Equal(digestBefore, state.CurrentOfficialResult!.SemanticDigest, "Export must not mutate the official semantic digest.");
        TestAssert.Equal(executionBefore, state.CurrentOfficialResult.Project.SourceExecution, "Export must not mutate official source execution.");
        TestAssert.Equal(string.Join('|', proposalsBefore.Select(item => item.Id)), string.Join('|', state.Proposals.Select(item => item.Id)), "Export must not mutate proposals.");
        TestAssert.Equal(previewBefore!.ProjectId, state.ActiveXlsxPreview!.ProjectId, "Export must not mutate the active XLSX preview.");
    }

    public static void ExecutiveExportRejectsIncompleteOrNonOfficialResults()
    {
        var official = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult();
        var cases = new Dictionary<string, CompilationResult>
        {
            ["missing metadata"] = official with { Project = official.Project with { ImportMetadata = null } },
            ["candidate classification"] = official with
            {
                Project = official.Project with
                {
                    ImportMetadata = official.Project.ImportMetadata! with
                    {
                        Classification = ManifestImportClassification.CandidatePreview
                    }
                }
            },
            ["missing source reporting date"] = official with
            {
                Project = official.Project with
                {
                    ImportMetadata = official.Project.ImportMetadata! with { RegisterStatusDate = null }
                }
            },
            ["invalid source reporting date"] = official with
            {
                Project = official.Project with
                {
                    ImportMetadata = official.Project.ImportMetadata! with { RegisterStatusDate = DateOnly.MinValue }
                }
            },
            ["missing analysis date"] = official with
            {
                Analysis = official.Analysis with { AsOfDate = null }
            },
            ["missing compiled views"] = official with { Views = null! }
        };

        foreach (var (name, result) in cases)
        {
            try
            {
                InvokeExport(new ProjectCompiler(), result);
                throw new InvalidOperationException($"Executive export case '{name}' should fail closed.");
            }
            catch (ProjectCompilationException exception)
            {
                TestAssert.Equal("executive-export", exception.Phase, $"Incomplete executive export case '{name}' must identify its phase.");
                TestAssert.True(exception.Diagnostics.Any(diagnostic => diagnostic.Code == "EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL"), $"Incomplete executive export case '{name}' must use the stable diagnostic code.");
            }
        }
    }

    public static void ExecutiveExportFailsClosedForInvalidEvidenceAndMissingPlanningAxis()
    {
        var official = ExecutiveProgressTestFixtures.BuildDailyGanttFixtureResult();
        var contradictoryActual = official with
        {
            Project = official.Project with
            {
                SourceExecution = official.Project.SourceExecution with
                {
                    Records = official.Project.SourceExecution.Records
                        .Select(record => string.Equals(record.Entity.Id, ExecutiveProgressTestFixtures.CompletedEarlyCardId, StringComparison.OrdinalIgnoreCase)
                            ? record with { ActualFinish = new DateOnly(2026, 9, 15) }
                            : record)
                        .ToArray()
                }
            }
        };
        var negativeEffort = official with
        {
            Project = official.Project with
            {
                SourceExecution = official.Project.SourceExecution with
                {
                    Records = official.Project.SourceExecution.Records
                        .Select(record => string.Equals(record.Entity.Id, ExecutiveProgressTestFixtures.OpenInProgressCardId, StringComparison.OrdinalIgnoreCase)
                            ? record with { ActualEffortHours = -1m }
                            : record)
                        .ToArray()
                }
            }
        };
        var noPlanningAxis = official with
        {
            Project = official.Project with
            {
                Baseline = official.Project.Baseline with { PlanningStart = null, PlanningFinish = null },
                Phases = official.Project.Phases.Select(phase => phase with { PlannedStart = null, PlannedFinish = null }).ToArray(),
                WorkPackages = official.Project.WorkPackages.Select(workPackage => workPackage with { PlannedStart = null, PlannedFinish = null }).ToArray(),
                DeliveryCards = official.Project.DeliveryCards.Select(card => card with { PlannedStart = null, PlannedFinish = null }).ToArray(),
                Milestones = official.Project.Milestones.Select(milestone => milestone with { PlannedDate = null }).ToArray()
            }
        };

        AssertExportRejected(contradictoryActual, "EXECUTIVE_EXPORT_INVALID_OFFICIAL_EVIDENCE", "Actual finish before actual start");
        AssertExportRejected(negativeEffort, "EXECUTIVE_EXPORT_INVALID_OFFICIAL_EVIDENCE", "negative official effort");
        AssertExportRejected(noPlanningAxis, "EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL", "missing truthful planning axis");
    }

    public static void ExecutiveExportEndpointUsesOfficialSelectionAndDatedDownloadContract()
    {
        var program = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs"));

        TestAssert.Contains("MapGet(\"/api/exports/executive-progress.xlsx\"", program, "The application must expose the executive progress download endpoint.");
        TestAssert.Contains("CurrentOfficialResult", program, "The executive endpoint must select the official result explicitly.");
        TestAssert.Contains("ActivePreview", program, "A loaded preview without an official snapshot must return NO_OFFICIAL_SNAPSHOT.");
        TestAssert.Contains("NO_OFFICIAL_SNAPSHOT", program, "The executive endpoint must distinguish an absent official snapshot.");
        TestAssert.Contains("EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL", program, "The executive endpoint must expose the stable incomplete-official diagnostic.");
        TestAssert.Contains("BaoCaoTienDo", program, "The executive endpoint must use the dated Vietnamese download suffix.");
        TestAssert.Contains("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", program, "The executive endpoint must return XLSX content type.");
    }

    public static void ExecutiveWorkbookIsPresentationOnlyAndCannotBecomePreviewOrSource()
    {
        var official = ExecutiveProgressTestFixtures.BuildOfficialFixtureResult();
        var bytes = InvokeExport(new ProjectCompiler(), official);
        var workbookText = string.Join('|', Enumerable.Range(1, 5).Select(sheet => ExecutiveProgressTestFixtures.WorksheetText(bytes, sheet)));

        TestAssert.False(workbookText.Contains("CARIO_EXPORT", StringComparison.OrdinalIgnoreCase), "The executive workbook must not expose technical CARIO export markers.");
        TestAssert.False(workbookText.Contains("PMC_EXPORT_KIND", StringComparison.OrdinalIgnoreCase), "The executive workbook must not expose technical preview markers.");

        var previewImport = new Outputs.XlsxPreviewImporter().Import("executive.xlsx", bytes);
        TestAssert.False(previewImport.IsValid, "The executive workbook must be rejected by the technical XLSX preview importer.");

        var state = new CompilerApplicationState();
        state.Set(official);
        state.SetXlsxPreview(new Outputs.XlsxPreviewModel
        {
            FileName = "existing-preview.xlsx",
            ProjectId = "existing-preview",
            ProjectName = "Existing preview"
        });
        var digest = state.CurrentOfficialResult!.SemanticDigest;
        var previewId = state.ActiveXlsxPreview!.ProjectId;
        _ = InvokeExport(new ProjectCompiler(), state.CurrentOfficialResult!);

        TestAssert.Equal(digest, state.CurrentOfficialResult!.SemanticDigest, "The executive workbook must not replace official canonical state.");
        TestAssert.Equal(previewId, state.ActiveXlsxPreview!.ProjectId, "The executive workbook must not replace an existing technical preview.");
    }

    private static byte[] InvokeExport(ProjectCompiler compiler, CompilationResult result)
    {
        var method = typeof(ProjectCompiler).GetMethod("ExportExecutiveProgressXlsx")
            ?? throw new InvalidOperationException("The executive export seam is not implemented yet.");
        try
        {
            return (byte[])(method.Invoke(compiler, [result])
                ?? throw new InvalidOperationException("The executive export seam returned no workbook."));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static void AssertExportRejected(CompilationResult result, string expectedDiagnostic, string scenario)
    {
        try
        {
            _ = InvokeExport(new ProjectCompiler(), result);
            throw new InvalidOperationException($"Executive export must fail closed for {scenario}.");
        }
        catch (ProjectCompilationException exception)
        {
            TestAssert.Equal("executive-export", exception.Phase, $"Executive export must identify the export boundary for {scenario}.");
            TestAssert.True(
                exception.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, expectedDiagnostic, StringComparison.Ordinal)),
                $"Executive export must provide stable diagnostic '{expectedDiagnostic}' for {scenario}.");
        }
    }
}
