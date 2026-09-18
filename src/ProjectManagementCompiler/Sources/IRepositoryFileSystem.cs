using System.Text;

namespace ProjectManagementCompiler.Sources;

public interface IRepositoryFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    bool IsReparsePoint(string path);
    RepositoryFileReadResult ReadFile(string allowedRoot, string path, Encoding encoding, long maxFileBytes, long remainingTotalBytes);
}
