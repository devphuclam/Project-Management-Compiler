using System.Text;

namespace ProjectManagementCompiler.Tests;

internal sealed class FakeRepositoryFileSystem : ProjectManagementCompiler.Sources.IRepositoryFileSystem
{
    private readonly Dictionary<string, FakeEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    public List<string> ReadPaths { get; } = [];
    public List<string> ValidatedReadPaths { get; } = [];
    public List<string> LegacyLengthReadPaths { get; } = [];
    public List<string> LegacyTextReadPaths { get; } = [];
    public HashSet<string> ReparseAtValidatedRead { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> OversizeAtValidatedRead { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool ThrowOnLegacyRead { get; set; }
    public long TotalBytesRead { get; private set; }

    public void AddDirectory(string path, bool reparsePoint = false) =>
        entries[Normalize(path)] = new FakeEntry(true, string.Empty, 0, reparsePoint, []);

    public void AddFile(string path, string content, bool reparsePoint = false, long? length = null) =>
        entries[Normalize(path)] = new FakeEntry(false, content, length ?? Encoding.UTF8.GetByteCount(content), reparsePoint, new UTF8Encoding(false, true).GetBytes(content));

    public void AddFileBytes(string path, byte[] bytes, bool reparsePoint = false, long? length = null) =>
        entries[Normalize(path)] = new FakeEntry(false, Encoding.UTF8.GetString(bytes), length ?? bytes.LongLength, reparsePoint, bytes);

    public void Remove(string path) => entries.Remove(Normalize(path));

    public void MarkReparse(string path) => entries[Normalize(path)] = entries[Normalize(path)] with { IsReparsePoint = true };

    public void SetLength(string path, long length) => entries[Normalize(path)] = entries[Normalize(path)] with { Length = length };

    public bool DirectoryExists(string path) => entries.TryGetValue(Normalize(path), out var entry) && entry.IsDirectory;

    public bool FileExists(string path) => entries.TryGetValue(Normalize(path), out var entry) && !entry.IsDirectory;

    public bool IsReparsePoint(string path) => entries.TryGetValue(Normalize(path), out var entry) && entry.IsReparsePoint;

    public long GetFileLength(string path)
    {
        var normalized = Normalize(path);
        LegacyLengthReadPaths.Add(normalized);
        if (ThrowOnLegacyRead)
        {
            throw new InvalidOperationException("Legacy length reads are not allowed.");
        }

        return entries[normalized].Length;
    }

    public string ReadAllText(string path, Encoding encoding)
    {
        var normalized = Normalize(path);
        LegacyTextReadPaths.Add(normalized);
        ReadPaths.Add(normalized);
        if (ThrowOnLegacyRead)
        {
            throw new InvalidOperationException("Legacy text reads are not allowed.");
        }

        return encoding.GetString(entries[normalized].Bytes);
    }

    public ProjectManagementCompiler.Sources.RepositoryFileReadResult ReadFile(string path, Encoding encoding, long maxFileBytes, long remainingTotalBytes)
    {
        var normalized = Normalize(path);
        ValidatedReadPaths.Add(normalized);
        var entry = entries[normalized];
        var actualLength = Math.Max(entry.Length, entry.Bytes.LongLength);
        if (entry.IsReparsePoint || ReparseAtValidatedRead.Contains(normalized))
        {
            throw new ProjectManagementCompiler.Sources.RepositoryFileReadException(ProjectManagementCompiler.Sources.RepositoryFileReadFailure.ReparsePoint, "The source file is a reparse point.");
        }

        if (OversizeAtValidatedRead.Contains(normalized) || actualLength > maxFileBytes)
        {
            throw new ProjectManagementCompiler.Sources.RepositoryFileReadException(ProjectManagementCompiler.Sources.RepositoryFileReadFailure.FileTooLarge, "The source file exceeds its limit.", actualLength);
        }

        if (actualLength > remainingTotalBytes)
        {
            throw new ProjectManagementCompiler.Sources.RepositoryFileReadException(ProjectManagementCompiler.Sources.RepositoryFileReadFailure.TotalTooLarge, "The source exceeds the total limit.", actualLength);
        }

        var content = encoding.GetString(entry.Bytes);
        ReadPaths.Add(normalized);
        TotalBytesRead += entry.Bytes.LongLength;
        return new ProjectManagementCompiler.Sources.RepositoryFileReadResult(content, entry.Bytes.LongLength);
    }

    private static string Normalize(string path) => ProjectManagementCompiler.Sources.SourcePathPolicy.NormalizeRoot(path);

    private sealed record FakeEntry(bool IsDirectory, string Content, long Length, bool IsReparsePoint, byte[] Bytes);
}
