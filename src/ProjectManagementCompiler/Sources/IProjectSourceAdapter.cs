namespace ProjectManagementCompiler.Sources;

public interface IProjectSourceAdapter
{
    Task<RepositorySnapshot> CaptureAsync(SourceRequest request, CancellationToken cancellationToken);
}
