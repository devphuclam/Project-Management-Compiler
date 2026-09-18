using System.Text;

namespace ProjectManagementCompiler.Tests;

internal sealed class FakeRepositoryFileSystem : ProjectManagementCompiler.Sources.IRepositoryFileSystem
{
    private readonly Dictionary<string, FakeEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    public List<string> ReadPaths { get; } = [];

    public void AddDirectory(string path, bool reparsePoint = false) =>
        entries[Normalize(path)] = new FakeEntry(true, string.Empty, 0, reparsePoint);

    public void AddFile(string path, string content, bool reparsePoint = false, long? length = null) =>
        entries[Normalize(path)] = new FakeEntry(false, content, length ?? Encoding.UTF8.GetByteCount(content), reparsePoint);

    public void Remove(string path) => entries.Remove(Normalize(path));

    public void MarkReparse(string path) => entries[Normalize(path)] = entries[Normalize(path)] with { IsReparsePoint = true };

    public void SetLength(string path, long length) => entries[Normalize(path)] = entries[Normalize(path)] with { Length = length };

    public bool DirectoryExists(string path) => entries.TryGetValue(Normalize(path), out var entry) && entry.IsDirectory;

    public bool FileExists(string path) => entries.TryGetValue(Normalize(path), out var entry) && !entry.IsDirectory;

    public bool IsReparsePoint(string path) => entries.TryGetValue(Normalize(path), out var entry) && entry.IsReparsePoint;

    public long GetFileLength(string path) => entries[Normalize(path)].Length;

    public string ReadAllText(string path, Encoding encoding)
    {
        var normalized = Normalize(path);
        ReadPaths.Add(normalized);
        return entries[normalized].Content;
    }

    private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed record FakeEntry(bool IsDirectory, string Content, long Length, bool IsReparsePoint);
}
