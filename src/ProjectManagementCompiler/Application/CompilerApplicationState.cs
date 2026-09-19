using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Application;

public sealed class CompilerApplicationState
{
    private readonly object gate = new();
    private CompilationResult? current;
    private CompilationResult? currentOfficialResult;
    private IdeaEngineeringSnapshot? currentOfficialSnapshot;
    private CompilationResult? activePreviewResult;
    private IdeaEngineeringSnapshot? activePreview;
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
                    currentOfficialSnapshot = result.Snapshot;
                    currentOfficialResult = compiledSnapshot;
                    current = compiledSnapshot ?? current;
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

    public void ReplaceProposals(IEnumerable<ExecutionProposal> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        lock (gate)
        {
            proposals = values.ToArray();
        }
    }
}
