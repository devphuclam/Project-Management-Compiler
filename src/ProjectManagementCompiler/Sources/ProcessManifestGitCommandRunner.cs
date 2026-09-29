using System.ComponentModel;

namespace ProjectManagementCompiler.Sources;

public sealed class ProcessManifestGitCommandRunner : IManifestGitCommandRunner
{
    public async Task<(string Stdout, string Stderr)> RunAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        long maxOutputBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ManifestGitObjectReader.RunGitAsync(repositoryRoot, arguments, maxOutputBytes, cancellationToken);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ManifestGitCommandException(null, "Git could not be started.", exception);
        }
    }
}
