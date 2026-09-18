using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

/// <summary>
/// Routes local paths to the mandatory local adapter and defines the optional
/// HTTPS capability boundary. Enabled HTTPS capture is a future capability;
/// this MVP deliberately does not execute Git or access the network.
/// </summary>
public sealed class ExistingGitHttpsSourceAdapter : IProjectSourceAdapter
{
    private readonly IProjectSourceAdapter localAdapter;

    public ExistingGitHttpsSourceAdapter()
        : this(new LocalRepositorySourceAdapter())
    {
    }

    public ExistingGitHttpsSourceAdapter(IProjectSourceAdapter localAdapter)
    {
        this.localAdapter = localAdapter;
    }

    public Task<RepositorySnapshot> CaptureAsync(SourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsHttpsLocation(request.Location))
        {
            return localAdapter.CaptureAsync(request, cancellationToken);
        }

        return Task.FromResult(UnavailableSnapshot(request));
    }

    private static bool IsHttpsLocation(string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static RepositorySnapshot UnavailableSnapshot(SourceRequest request)
    {
        const string repositoryId = "https-repository";
        const string extractionRule = "optional-git-https-capability";

        return new RepositorySnapshot
        {
            RepositoryId = repositoryId,
            RepositoryLabel = repositoryId,
            LocationLabel = "https-source",
            ResolvedRef = request.Ref,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            CaptureState = CaptureState.Blocked,
            Documents = Array.Empty<SourceDocument>(),
            Diagnostics =
            [
                new ImportWarning
                {
                    Id = "CAPABILITY_GIT_HTTPS_UNAVAILABLE",
                    Severity = WarningSeverity.Error,
                    Code = "CAPABILITY_GIT_HTTPS_UNAVAILABLE",
                    Message = "Optional Git HTTPS capture is unavailable in the offline MVP capability set.",
                    SourceReferences =
                    [
                        new SourceReference
                        {
                            SourceId = repositoryId,
                            Repository = repositoryId,
                            ResolvedRef = request.Ref,
                            ExtractionRule = extractionRule,
                            ConfidenceState = DataState.Unknown,
                            ValidationState = ValidationState.Unknown
                        }
                    ]
                }
            ]
        };
    }
}
