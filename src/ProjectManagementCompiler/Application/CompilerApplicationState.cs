using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Application;

public sealed class CompilerApplicationState
{
    private readonly object gate = new();
    private CompilationResult? current;
    private CompilationResult? currentOfficialResult;
    private IdeaEngineeringSnapshot? currentOfficialSnapshot;
    private CompilationResult? activePreviewResult;
    private IdeaEngineeringSnapshot? activePreview;
    private XlsxPreviewModel? activeXlsxPreview;
    private ManifestImportAttempt? latestImportAttempt;
    private IReadOnlyList<ExecutionProposal> proposals = Array.Empty<ExecutionProposal>();

    public CompilationResult? Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public CompilationResult? CurrentOfficialResult
    {
        get
        {
            lock (gate)
            {
                return currentOfficialResult;
            }
        }
    }

    public IdeaEngineeringSnapshot? CurrentOfficialSnapshot
    {
        get
        {
            lock (gate)
            {
                return currentOfficialSnapshot;
            }
        }
    }

    public CompilationResult? ActivePreviewResult
    {
        get
        {
            lock (gate)
            {
                return activePreviewResult;
            }
        }
    }

    public IdeaEngineeringSnapshot? ActivePreview
    {
        get
        {
            lock (gate)
            {
                return activePreview;
            }
        }
    }

    public XlsxPreviewModel? ActiveXlsxPreview
    {
        get
        {
            lock (gate)
            {
                return activeXlsxPreview;
            }
        }
    }

    public ManifestImportAttempt? LatestImportAttempt
    {
        get
        {
            lock (gate)
            {
                return latestImportAttempt;
            }
        }
    }

    public IReadOnlyList<ExecutionProposal> Proposals
    {
        get
        {
            lock (gate)
            {
                return proposals.ToArray();
            }
        }
    }

    public void Set(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (gate)
        {
            current = result;
            if (result.Project.ImportMetadata is not null
                || result.Project.ExecutionProposals.Count > 0
                || currentOfficialResult is null)
            {
                proposals = OrderedProposals(result.Project.ExecutionProposals);
            }
            if (result.Project.ImportMetadata is not null)
            {
                currentOfficialResult = result;
                currentOfficialSnapshot = new IdeaEngineeringSnapshot
                {
                    Project = result.Project,
                    Metadata = result.Project.ImportMetadata,
                    SourceExecution = result.Project.SourceExecution,
                    Diagnostics = Array.Empty<ManifestDiagnostic>()
                };
            }
        }
    }

    public void RecordManifestImport(
        ManifestImportResult result,
        CompilationResult? compiledSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (gate)
        {
            latestImportAttempt = result.Attempt;
            if (result.Snapshot is null)
            {
                return;
            }

            switch (result.Classification)
            {
                case ManifestImportClassification.OfficialCommit:
                    activeXlsxPreview = null;
                    var importedProject = compiledSnapshot?.Project ?? result.Snapshot.Project;
                    var importedProposals = OrderedProposals(importedProject.ExecutionProposals);
                    var retained = proposals.Count == 0
                        ? importedProposals
                        : proposals.Select(proposal => MarkStaleIfNeeded(proposal, result.Snapshot.Metadata)).ToArray();
                    proposals = OrderedProposals(retained);
                    var projectedProject = importedProject with { ExecutionProposals = proposals };
                    currentOfficialSnapshot = result.Snapshot with
                    {
                        Project = projectedProject,
                        SourceExecution = projectedProject.SourceExecution
                    };
                    currentOfficialResult = compiledSnapshot is null
                        ? null
                        : ProjectWithProposals(compiledSnapshot, proposals);
                    current = currentOfficialResult ?? current;
                    break;
                case ManifestImportClassification.CandidatePreview:
                case ManifestImportClassification.UncommittedPreview:
                    activePreview = result.Snapshot;
                    activePreviewResult = compiledSnapshot;
                    break;
            }
        }
    }

    public void ClearActivePreview()
    {
        lock (gate)
        {
            activePreview = null;
            activePreviewResult = null;
        }
    }

    public void SetXlsxPreview(XlsxPreviewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        lock (gate)
        {
            activeXlsxPreview = preview;
        }
    }

    public void ClearXlsxPreview()
    {
        lock (gate)
        {
            activeXlsxPreview = null;
        }
    }

    public void ReplaceProposals(IEnumerable<ExecutionProposal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        lock (gate)
        {
            SetProposalProjectionLocked(values);
        }
    }

    public ExecutionProposal? FindProposal(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (gate)
        {
            return proposals.FirstOrDefault(proposal => string.Equals(proposal.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void UpsertProposal(ExecutionProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        lock (gate)
        {
            SetProposalProjectionLocked(proposals
                .Where(existing => !string.Equals(existing.Id, proposal.Id, StringComparison.OrdinalIgnoreCase))
                .Append(proposal));
        }
    }

    private void SetProposalProjectionLocked(IEnumerable<ExecutionProposal> values)
    {
        proposals = OrderedProposals(values);
        if (currentOfficialResult is null)
        {
            if (current is not null)
            {
                current = ProjectWithProposals(current, proposals);
            }

            return;
        }

        var previous = currentOfficialResult;
        var projected = ProjectWithProposals(previous, proposals);
        var projectedProject = projected.Project;
        currentOfficialResult = projected;
        currentOfficialSnapshot = currentOfficialSnapshot is null
            ? null
            : currentOfficialSnapshot with
            {
                Project = projectedProject,
                SourceExecution = projectedProject.SourceExecution
            };
        if (current is not null)
        {
            current = ProjectWithProposals(current, proposals);
        }
    }

    private static CompilationResult ProjectWithProposals(
        CompilationResult result,
        IReadOnlyList<ExecutionProposal> values)
    {
        var project = result.Project with { ExecutionProposals = values };
        return result with
        {
            Project = project,
            SemanticDigest = CanonicalJsonDigest.Compute(project)
        };
    }

    private static ExecutionProposal MarkStaleIfNeeded(
        ExecutionProposal proposal,
        ManifestSnapshotMetadata metadata) =>
        string.Equals(proposal.BaseSnapshotId, metadata.SnapshotId, StringComparison.OrdinalIgnoreCase)
            && proposal.ExpectedRegisterRevision == metadata.RegisterRevision
            ? proposal
            : proposal with { Lifecycle = ProposalLifecycle.StaleBase };

    private static IReadOnlyList<ExecutionProposal> OrderedProposals(IEnumerable<ExecutionProposal> values) =>
        values
            .Where(proposal => proposal is not null)
            .OrderBy(proposal => proposal.Id, StringComparer.Ordinal)
            .ToArray();
}
