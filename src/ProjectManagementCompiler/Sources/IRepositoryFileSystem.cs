using System.Text;

namespace ProjectManagementCompiler.Sources;

public interface IRepositoryFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    bool IsReparsePoint(string path);
    long GetFileLength(string path);
    string ReadAllText(string path, Encoding encoding);
}
