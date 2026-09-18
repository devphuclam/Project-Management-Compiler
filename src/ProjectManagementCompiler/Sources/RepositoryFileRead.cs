namespace ProjectManagementCompiler.Sources;

public sealed record RepositoryFileReadResult(string Content, long BytesRead);

public enum RepositoryFileReadFailure
{
    ReparsePoint,
    FileTooLarge,
    TotalTooLarge
}

public sealed class RepositoryFileReadException : IOException
{
    public RepositoryFileReadException(RepositoryFileReadFailure failure, string message, long observedBytes = 0)
        : base(message)
    {
        Failure = failure;
        ObservedBytes = observedBytes;
    }

    public RepositoryFileReadFailure Failure { get; }
    public long ObservedBytes { get; }
}
