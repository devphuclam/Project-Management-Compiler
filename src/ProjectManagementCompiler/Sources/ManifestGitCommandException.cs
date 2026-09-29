namespace ProjectManagementCompiler.Sources;

public sealed class ManifestGitCommandException : IOException
{
    public ManifestGitCommandException(int? exitCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ExitCode = exitCode;
    }

    public int? ExitCode { get; }
}
