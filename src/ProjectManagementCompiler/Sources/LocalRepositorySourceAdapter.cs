using System.Text;
using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Sources;

public sealed class LocalRepositorySourceAdapter : IProjectSourceAdapter
{
    private readonly IRepositoryFileSystem fileSystem;

    public LocalRepositorySourceAdapter()
        : this(new PhysicalRepositoryFileSystem())
    {
    }

    public LocalRepositorySourceAdapter(IRepositoryFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
    }

    public Task<RepositorySnapshot> CaptureAsync(SourceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Location))
        {
            throw new ArgumentException("A local source location is required.", nameof(request));
        }

        if (request.MaxDocumentBytes < 0 || request.MaxTotalDocumentBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Source size limits cannot be negative.");
        }

        var capturedAtUtc = DateTimeOffset.UtcNow;
        var diagnostics = new List<ImportWarning>();
        var documents = new List<SourceDocument>();
        string root;
        try
        {
            root = Path.GetFullPath(request.Location).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            return Task.FromResult(FailedSnapshot(request.Location, capturedAtUtc, "SOURCE_CAPTURE_FAILED", exception.Message));
        }

        if (!fileSystem.DirectoryExists(root))
        {
            return Task.FromResult(FailedSnapshot(root, capturedAtUtc, "SOURCE_CAPTURE_FAILED", "The local source directory is not readable."));
        }

        var blocked = false;
        long totalBytes = 0;
        foreach (var relativePath in SourcePathPolicy.RecognizedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            try
            {
                fullPath = SourcePathPolicy.ResolvePath(root, relativePath, fileSystem);
            }
            catch (ArgumentException exception)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_PATH_ESCAPE", relativePath, exception.Message));
                continue;
            }

            if (!SourcePathPolicy.HasSafeSegments(root, fullPath, fileSystem))
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_PATH_ESCAPE", relativePath, "A source path traverses a reparse point."));
                continue;
            }

            if (!fileSystem.FileExists(fullPath))
            {
                diagnostics.Add(Diagnostic("SOURCE_FILE_MISSING", relativePath, "A recognized source file is missing."));
                continue;
            }

            long length;
            try
            {
                length = fileSystem.GetFileLength(fullPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_CAPTURE_FAILED", relativePath, exception.Message));
                continue;
            }
            if (length > request.MaxDocumentBytes)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_FILE_TOO_LARGE", relativePath, $"Source file is {length} bytes; maximum is {request.MaxDocumentBytes} bytes."));
                continue;
            }

            if (length > request.MaxTotalDocumentBytes - totalBytes)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_TOTAL_TOO_LARGE", relativePath, $"Selected source exceeds the {request.MaxTotalDocumentBytes} byte total limit."));
                continue;
            }

            string content;
            try
            {
                content = fileSystem.ReadAllText(fullPath, new UTF8Encoding(false, true));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_CAPTURE_FAILED", relativePath, exception.Message));
                continue;
            }
            var format = GetFormat(relativePath);
            documents.Add(new SourceDocument
            {
                Id = $"source-document-{documents.Count + 1:D2}",
                RelativeFile = relativePath,
                Format = format,
                SizeBytes = length,
                Content = content,
                SourceReference = new SourceReference
                {
                    SourceId = "local-repository",
                    Repository = root,
                    RelativeFile = relativePath,
                    ExtractionRule = "allow-listed-local-source-capture",
                    ConfidenceState = DataState.Known,
                    ValidationState = ValidationState.Known
                }
            });
            totalBytes += length;
        }

        var state = blocked ? CaptureState.Blocked : CaptureState.Known;
        var snapshot = new RepositorySnapshot
        {
            RepositoryId = "local-repository",
            RepositoryLabel = Path.GetFileName(root) ?? root,
            LocationLabel = root,
            ResolvedRef = request.Ref,
            CapturedAtUtc = capturedAtUtc,
            CaptureState = state,
            Documents = documents,
            Diagnostics = diagnostics
        };
        return Task.FromResult(snapshot);
    }

    private static SourceDocumentFormat GetFormat(string path) =>
        Path.GetExtension(path).Equals(".html", StringComparison.OrdinalIgnoreCase)
            ? SourceDocumentFormat.Html
            : SourceDocumentFormat.Markdown;

    private static ImportWarning Diagnostic(string code, string relativePath, string message) => new()
    {
        Id = $"{code}:{relativePath}",
        Severity = WarningSeverity.Error,
        Code = code,
        Message = message,
        SourceReferences = [new SourceReference { SourceId = "local-repository", RelativeFile = relativePath, ExtractionRule = "allow-listed-local-source-capture" }]
    };

    private static RepositorySnapshot FailedSnapshot(string location, DateTimeOffset capturedAtUtc, string code, string message) => new()
    {
        RepositoryId = "local-repository",
        RepositoryLabel = Path.GetFileName(location) ?? location,
        LocationLabel = location,
        CapturedAtUtc = capturedAtUtc,
        CaptureState = CaptureState.Blocked,
        Diagnostics = [Diagnostic(code, string.Empty, message)]
    };

    private sealed class PhysicalRepositoryFileSystem : IRepositoryFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);
        public bool IsReparsePoint(string path) =>
            (File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        public long GetFileLength(string path) => new FileInfo(path).Length;
        public string ReadAllText(string path, Encoding encoding) => File.ReadAllText(path, encoding);
    }
}
