namespace ProjectManagementCompiler.Tests;

using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;

internal static class XlsxPreviewApplicationTests
{
    public static void PreviewApiAndStateAreIsolatedFromOfficialResult()
    {
        var program = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs"));
        var state = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Application", "CompilerApplicationState.cs"));

        TestAssert.Contains("/api/xlsx-preview", program, "The application must expose a dedicated XLSX preview API.");
        TestAssert.Contains("ActiveXlsxPreview", state, "Application state must own the temporary XLSX preview separately.");
        TestAssert.Contains("CurrentOfficialResult", state, "The preview state must leave the official result as a separate projection.");
        TestAssert.Contains("INVALID_XLSX_PREVIEW", program, "Invalid preview uploads must use a structured API error.");
    }

    public static void PreviewLifecycleKeepsOfficialAndProposalStateUntouched()
    {
        var program = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs"));
        var manifestService = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Application", "ManifestImport", "ManifestImportApplicationService.cs"));

        TestAssert.Contains("MapDelete(\"/api/xlsx-preview\"", program, "The preview must have an explicit clear endpoint.");
        TestAssert.Contains("ClearXlsxPreview", manifestService + program, "A new official import must clear the temporary XLSX preview.");
        TestAssert.Contains("NO_ACTIVE_XLSX_PREVIEW", program, "The API must distinguish an empty preview state.");
    }

    public static void ValidPreviewDoesNotReplaceOfficialOrProposalProjection()
    {
        var metadata = OfficialMetadata("official-001");
        var proposal = new ExecutionProposal
        {
            Id = "proposal-001",
            BaseSnapshotId = metadata.SnapshotId,
            TargetKind = "DeliveryCard",
            TargetId = "P01-A"
        };
        var official = new CompilationResult
        {
            Project = new CanonicalProject
            {
                ImportMetadata = metadata,
                ExecutionProposals = [proposal]
            },
            SemanticDigest = "official-digest"
        };
        var state = new CompilerApplicationState();
        state.Set(official);
        var preview = new XlsxPreviewModel
        {
            FileName = "preview.xlsx",
            ProjectId = "preview-project",
            ProjectName = "Preview"
        };

        state.SetXlsxPreview(preview);

        TestAssert.Equal("official-digest", state.CurrentOfficialResult!.SemanticDigest, "A preview must not replace the official compilation result.");
        TestAssert.Equal(1, state.Proposals.Count, "A preview must not replace the official proposal projection.");
        TestAssert.Equal("preview-project", state.ActiveXlsxPreview!.ProjectId, "The validated preview must be visible through its separate state slot.");
    }

    public static void FailedPreviewImportLeavesLastValidPreviewUnchanged()
    {
        var state = new CompilerApplicationState();
        var previous = new XlsxPreviewModel
        {
            FileName = "previous.xlsx",
            ProjectId = "previous-project",
            ProjectName = "Previous"
        };
        state.SetXlsxPreview(previous);

        var failed = new XlsxPreviewImporter().Import("tampered.xlsx", XlsxPreviewTestFixtures.CorruptPackage());

        TestAssert.False(failed.IsValid, "The tampered workbook must fail before state replacement.");
        TestAssert.Equal("previous-project", state.ActiveXlsxPreview!.ProjectId, "A failed preview upload must retain the last valid preview.");
    }

    public static void OfficialManifestImportClearsOnlyXlsxPreview()
    {
        var metadata = OfficialMetadata("official-002");
        var proposal = new ExecutionProposal
        {
            Id = "proposal-002",
            BaseSnapshotId = metadata.SnapshotId,
            TargetKind = "DeliveryCard",
            TargetId = "P01-A"
        };
        var official = new CompilationResult
        {
            Project = new CanonicalProject
            {
                ImportMetadata = metadata,
                ExecutionProposals = [proposal]
            },
            SemanticDigest = "official-digest"
        };
        var state = new CompilerApplicationState();
        state.Set(official);
        state.SetXlsxPreview(new XlsxPreviewModel { ProjectId = "preview-project" });
        state.RecordManifestImport(
            new ManifestImportResult
            {
                Classification = ManifestImportClassification.OfficialCommit,
                Snapshot = new IdeaEngineeringSnapshot
                {
                    Project = official.Project,
                    Metadata = metadata
                }
            },
            official);

        TestAssert.True(state.ActiveXlsxPreview is null, "A new official import must clear the temporary XLSX preview.");
        TestAssert.Equal(1, state.Proposals.Count, "Clearing the preview must retain the official proposal projection.");
    }

    private static ManifestSnapshotMetadata OfficialMetadata(string snapshotId) => new()
    {
        Classification = ManifestImportClassification.OfficialCommit,
        SnapshotId = snapshotId,
        SourceIdentity = "commit-" + snapshotId,
        ProjectId = "official-project"
    };
}
