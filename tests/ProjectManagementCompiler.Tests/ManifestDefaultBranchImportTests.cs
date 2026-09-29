using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Application.ManifestImport;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;
using System.Reflection;

namespace ProjectManagementCompiler.Tests;

internal static class ManifestDefaultBranchImportTests
{
    private const string FullCommit = "0123456789abcdef0123456789abcdef01234567";

    public static void DefaultBranchImportResolvesOnceAndForcesExactOfficialCommit()
    {
        var resolver = new FixedResolver(ManifestDefaultBranchResolution.Success("refs/remotes/origin/main", FullCommit));
        var importer = new RecordingImporter(OfficialImport());
        var compiler = new RecordingCompiler();
        var state = new CompilerApplicationState();
        var service = new ManifestImportApplicationService(importer, compiler, state, resolver);
        var request = new ManifestDefaultBranchImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory(),
            ManifestPath = "planning/project-management-compiler-manifest.json",
            AnalysisAsOfOverride = new DateOnly(2026, 9, 29),
            MaxFileBytes = 1024,
            MaxTotalBytes = 2048
        };
        var mapping = new CarioMappingConfiguration();

        _ = service.ImportDefaultBranchAsync(request, mapping, CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal(1, resolver.CallCount, "The default ref must be resolved once per import request.");
        TestAssert.Equal(1, importer.Requests.Count, "A successful resolution must invoke the existing importer exactly once.");
        var imported = importer.Requests.Single();
        TestAssert.Equal(ManifestImportMode.GitCommit, imported.Mode, "The application must force official Git-commit mode.");
        TestAssert.Equal(FullCommit, imported.RequestedCommit, "The importer must receive the exact resolved commit ID.");
        TestAssert.Equal(request.RepositoryRoot, imported.RepositoryRoot, "The repository root must be forwarded unchanged.");
        TestAssert.Equal(request.ManifestPath, imported.ManifestPath, "The manifest path must be forwarded unchanged.");
        TestAssert.Equal(request.AnalysisAsOfOverride, imported.AnalysisAsOfOverride, "The analysis-date override must be forwarded unchanged.");
        TestAssert.Equal(request.MaxFileBytes, imported.MaxFileBytes, "The per-file bound must be forwarded unchanged.");
        TestAssert.Equal(request.MaxTotalBytes, imported.MaxTotalBytes, "The aggregate bound must be forwarded unchanged.");
        TestAssert.True(ReferenceEquals(mapping, compiler.LastMapping), "The selected mapping must be passed to the existing compiler path.");
    }

    public static void DefaultBranchResolutionFailureRetainsOfficialStateAndSkipsImporter()
    {
        var state = StateWithOfficialSnapshot();
        var originalOfficial = state.CurrentOfficialResult;
        var resolver = new FixedResolver(ManifestDefaultBranchResolution.Failure(
            ManifestDefaultBranchFailureCode.DefaultRefUnavailable,
            "Chưa cấu hình nhánh mặc định."));
        var importer = new RecordingImporter(FailedImport());
        var service = new ManifestImportApplicationService(importer, new RecordingCompiler(), state, resolver);

        TestAssert.Throws<ManifestDefaultBranchImportException>(
            () => service.ImportDefaultBranchAsync(new ManifestDefaultBranchImportRequest
            {
                RepositoryRoot = Directory.GetCurrentDirectory()
            }).GetAwaiter().GetResult(),
            "A failed ref lookup must surface a typed recovery error.");

        TestAssert.Equal(0, importer.Requests.Count, "A failed ref lookup must not attempt a guessed or fallback import.");
        TestAssert.True(ReferenceEquals(originalOfficial, state.CurrentOfficialResult), "A ref-resolution failure must preserve the last official result.");
    }

    public static void CandidateImportAfterDefaultResolutionDoesNotReplaceOfficialSnapshot()
    {
        var state = StateWithOfficialSnapshot();
        var originalOfficial = state.CurrentOfficialResult;
        var candidate = CandidateImport();
        var resolver = new FixedResolver(ManifestDefaultBranchResolution.Success("refs/remotes/origin/main", FullCommit));
        var importer = new RecordingImporter(candidate);
        var service = new ManifestImportApplicationService(importer, new RecordingCompiler(), state, resolver);

        _ = service.ImportDefaultBranchAsync(new ManifestDefaultBranchImportRequest
        {
            RepositoryRoot = Directory.GetCurrentDirectory()
        }).GetAwaiter().GetResult();

        TestAssert.True(ReferenceEquals(originalOfficial, state.CurrentOfficialResult), "Candidate classification must not replace the official result.");
        TestAssert.True(state.ActivePreview is not null, "The existing state boundary must retain a candidate as preview data.");
    }

    public static void DefaultBranchApiContractCannotAcceptCallerModeOrCommit()
    {
        var propertyNames = typeof(ManifestDefaultBranchApiRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "RepositoryRoot", "ManifestPath", "AnalysisAsOfOverride", "MaxFileBytes", "MaxTotalBytes", "Mapping" })
        {
            TestAssert.True(propertyNames.Contains(required), $"The default-ref request must preserve supported option '{required}'.");
        }

        TestAssert.False(propertyNames.Contains("Mode"), "The normal import action must not accept a caller-selected authority mode.");
        TestAssert.False(propertyNames.Contains("RequestedCommit"), "The normal import action must not accept a caller-selected source commit.");

        var program = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs"));
        TestAssert.Contains("app.MapPost(\"/api/manifest-import/default-branch\"", program, "The app must expose the approved default-branch import route.");
        TestAssert.Contains("service.ImportDefaultBranchAsync", program, "The route must delegate to the application service boundary.");
        TestAssert.Contains("catch (ManifestDefaultBranchImportException", program, "Resolver failures must become typed, recoverable HTTP errors.");
    }

    private static ManifestImportResult FailedImport() => new()
    {
        Classification = ManifestImportClassification.Failed,
        Attempt = new ManifestImportAttempt
        {
            Classification = ManifestImportClassification.Failed,
            Mode = ManifestImportMode.GitCommit
        }
    };

    private static ManifestImportResult OfficialImport()
    {
        var metadata = new ManifestSnapshotMetadata
        {
            Classification = ManifestImportClassification.OfficialCommit,
            ImportMode = ManifestImportMode.GitCommit,
            SourceIdentity = FullCommit
        };
        var project = new CanonicalProject
        {
            Project = new Project { Id = "resolved", Name = "Resolved" },
            ImportMetadata = metadata
        };
        var snapshot = new IdeaEngineeringSnapshot { Project = project, Metadata = metadata };
        return new ManifestImportResult
        {
            Classification = ManifestImportClassification.OfficialCommit,
            Snapshot = snapshot,
            Attempt = new ManifestImportAttempt
            {
                Classification = ManifestImportClassification.OfficialCommit,
                Mode = ManifestImportMode.GitCommit,
                SourceIdentity = FullCommit
            }
        };
    }

    private static ManifestImportResult CandidateImport()
    {
        var metadata = new ManifestSnapshotMetadata
        {
            Classification = ManifestImportClassification.CandidatePreview,
            ImportMode = ManifestImportMode.GitCommit,
            SourceIdentity = FullCommit
        };
        var project = new CanonicalProject
        {
            Project = new Project { Id = "candidate", Name = "Candidate" },
            ImportMetadata = metadata
        };
        var snapshot = new IdeaEngineeringSnapshot { Project = project, Metadata = metadata };
        return new ManifestImportResult
        {
            Classification = ManifestImportClassification.CandidatePreview,
            Snapshot = snapshot,
            Attempt = new ManifestImportAttempt
            {
                Classification = ManifestImportClassification.CandidatePreview,
                Mode = ManifestImportMode.GitCommit,
                SourceIdentity = FullCommit
            }
        };
    }

    private static CompilerApplicationState StateWithOfficialSnapshot()
    {
        var state = new CompilerApplicationState();
        state.Set(new CompilationResult
        {
            Project = new CanonicalProject
            {
                Project = new Project { Id = "official", Name = "Official" },
                ImportMetadata = new ManifestSnapshotMetadata
                {
                    Classification = ManifestImportClassification.OfficialCommit,
                    ImportMode = ManifestImportMode.GitCommit,
                    SourceIdentity = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                }
            }
        });
        return state;
    }

    private sealed class FixedResolver(ManifestDefaultBranchResolution result) : IManifestDefaultBranchResolver
    {
        public int CallCount { get; private set; }

        public Task<ManifestDefaultBranchResolution> ResolveAsync(string repositoryRoot, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingImporter(ManifestImportResult result) : IIdeaEngineeringManifestImporter
    {
        public List<ManifestImportRequest> Requests { get; } = [];

        public Task<ManifestImportResult> ImportAsync(ManifestImportRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingCompiler : IProjectCompiler
    {
        public CarioMappingConfiguration? LastMapping { get; private set; }

        public CompilationResult BuildImportedResult(IdeaEngineeringSnapshot snapshot, DateOnly? asOfDate = null, CarioMappingConfiguration? mapping = null)
        {
            LastMapping = mapping;
            return new CompilationResult { Project = snapshot.Project, Mapping = mapping ?? new CarioMappingConfiguration() };
        }

        public Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public CompilationResult Reopen(string json, DateOnly? asOfDate = null, CarioMappingConfiguration? mapping = null) => throw new NotSupportedException();
        public ExecutionApplicationResult ApplyExecutionUpdate(CompilationResult current, ExecutionUpdate update, DateOnly? asOfDate = null) => throw new NotSupportedException();
        public string SaveJson(CompilationResult result) => throw new NotSupportedException();
        public byte[] ExportCarioXlsx(CompilationResult result) => throw new NotSupportedException();
        public byte[] ExportExecutiveProgressXlsx(CompilationResult result) => throw new NotSupportedException();
    }
}
