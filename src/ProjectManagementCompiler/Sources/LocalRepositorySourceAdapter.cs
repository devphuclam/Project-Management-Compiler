using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
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
        var sourceId = SafeRepositoryId(request.Location);
        string root;
        try
        {
            root = SourcePathPolicy.NormalizeRoot(request.Location);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(FailedSnapshot(request.Ref, capturedAtUtc, "SOURCE_CAPTURE_FAILED", "The local source directory could not be normalized."));
        }
        catch (NotSupportedException)
        {
            return Task.FromResult(FailedSnapshot(request.Ref, capturedAtUtc, "SOURCE_CAPTURE_FAILED", "The local source directory could not be normalized."));
        }
        catch (IOException)
        {
            return Task.FromResult(FailedSnapshot(request.Ref, capturedAtUtc, "SOURCE_CAPTURE_FAILED", "The local source directory could not be normalized."));
        }

        if (!fileSystem.DirectoryExists(root))
        {
            return Task.FromResult(FailedSnapshot(request.Ref, capturedAtUtc, "SOURCE_CAPTURE_FAILED", "The local source directory is not readable."));
        }

        var blocked = false;
        IReadOnlyList<string> recognizedPaths = SourcePathPolicy.RecognizedPaths;
        if (request.ManagementEvidenceIncrementPath is not null)
        {
            try
            {
                recognizedPaths = SourcePathPolicy.RecognizedPaths
                    .Concat(SourcePathPolicy.GetManagementEvidencePaths(request.ManagementEvidenceIncrementPath))
                    .ToArray();
            }
            catch (ArgumentException)
            {
                blocked = true;
                diagnostics.Add(Diagnostic(
                    "SOURCE_PATH_ESCAPE",
                    request.ManagementEvidenceIncrementPath,
                    "The management evidence increment path is outside the declared specs boundary.",
                    request.Ref));
            }
        }

        long totalBytes = 0;
        foreach (var relativePath in recognizedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath;
            try
            {
                fullPath = SourcePathPolicy.ResolvePath(root, relativePath, fileSystem);
            }
            catch (ArgumentException)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_PATH_ESCAPE", relativePath, "A recognized source path is invalid.", request.Ref));
                continue;
            }

            if (!SourcePathPolicy.HasSafeSegments(root, fullPath, fileSystem))
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_PATH_ESCAPE", relativePath, "A source path traverses a reparse point.", request.Ref));
                continue;
            }

            if (!fileSystem.FileExists(fullPath))
            {
                if (request.ManagementEvidenceIncrementPath is null
                    || !SourcePathPolicy.IsOptionalManagementEvidencePath(request.ManagementEvidenceIncrementPath, relativePath))
                {
                    diagnostics.Add(Diagnostic("SOURCE_FILE_MISSING", relativePath, "A recognized source file is missing.", request.Ref));
                }

                continue;
            }

            RepositoryFileReadResult read;
            try
            {
                read = fileSystem.ReadFile(
                    root,
                    fullPath,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                    request.MaxDocumentBytes,
                    request.MaxTotalDocumentBytes - totalBytes);
            }
            catch (RepositoryFileReadException exception)
            {
                blocked = true;
                diagnostics.Add(exception.Failure switch
                {
                    RepositoryFileReadFailure.ReparsePoint => Diagnostic("SOURCE_PATH_ESCAPE", relativePath, "A source file became a reparse point at the read boundary.", request.Ref),
                    RepositoryFileReadFailure.FileTooLarge => Diagnostic("SOURCE_FILE_TOO_LARGE", relativePath, $"Source file exceeds the {request.MaxDocumentBytes} byte limit.", request.Ref),
                    RepositoryFileReadFailure.TotalTooLarge => Diagnostic("SOURCE_TOTAL_TOO_LARGE", relativePath, $"Selected source exceeds the {request.MaxTotalDocumentBytes} byte total limit.", request.Ref),
                    _ => Diagnostic("SOURCE_CAPTURE_FAILED", relativePath, "The selected source file could not be read.", request.Ref)
                });
                continue;
            }
            catch (FileNotFoundException)
            {
                diagnostics.Add(Diagnostic("SOURCE_FILE_MISSING", relativePath, "A recognized source file is missing.", request.Ref));
                continue;
            }
            catch (Win32Exception)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_CAPTURE_FAILED", relativePath, "The selected source file could not be read.", request.Ref));
                continue;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
            {
                blocked = true;
                diagnostics.Add(Diagnostic("SOURCE_CAPTURE_FAILED", relativePath, "The selected source file could not be read.", request.Ref));
                continue;
            }

            var format = GetFormat(relativePath);
            documents.Add(new SourceDocument
            {
                Id = $"source-document-{documents.Count + 1:D2}",
                RelativeFile = relativePath,
                Format = format,
                SizeBytes = read.BytesRead,
                Content = read.Content,
                SourceReference = new SourceReference
                {
                    SourceId = sourceId,
                    Repository = "local-repository",
                    ResolvedRef = request.Ref,
                    RelativeFile = relativePath,
                    ExtractionRule = "allow-listed-local-source-capture",
                    ConfidenceState = DataState.Known,
                    ValidationState = ValidationState.Known
                }
            });
            totalBytes += read.BytesRead;
        }

        var state = blocked ? CaptureState.Blocked : CaptureState.Known;
        var snapshot = new RepositorySnapshot
        {
            RepositoryId = sourceId,
            RepositoryLabel = "local-repository",
            LocationLabel = "local-repository",
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

    private static ImportWarning Diagnostic(string code, string relativePath, string message, string? resolvedRef) => new()
    {
        Id = $"{code}:{relativePath}",
        Severity = code == "SOURCE_FILE_MISSING" ? WarningSeverity.Warning : WarningSeverity.Error,
        Code = code,
        Message = message,
        SourceReferences = [new SourceReference
        {
            SourceId = SafeRepositoryId(resolvedRef ?? "local-source"),
            Repository = "local-repository",
            ResolvedRef = resolvedRef,
            RelativeFile = relativePath,
            ExtractionRule = "allow-listed-local-source-capture"
        }]
    };

    private static RepositorySnapshot FailedSnapshot(string? resolvedRef, DateTimeOffset capturedAtUtc, string code, string message) => new()
    {
        RepositoryId = SafeRepositoryId(resolvedRef ?? "local-source"),
        RepositoryLabel = "local-repository",
        LocationLabel = "local-repository",
        ResolvedRef = resolvedRef,
        CapturedAtUtc = capturedAtUtc,
        CaptureState = CaptureState.Blocked,
        Diagnostics = [Diagnostic(code, string.Empty, message, resolvedRef)]
    };

    private static string SafeRepositoryId(string location)
    {
        var normalized = location;
        try
        {
            normalized = SourcePathPolicy.NormalizeRoot(location);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or IOException)
        {
            normalized = location.Trim();
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized.ToUpperInvariant()));
        return $"local-{Convert.ToHexString(digest)[..16].ToLowerInvariant()}";
    }

    private sealed class PhysicalRepositoryFileSystem : IRepositoryFileSystem
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);
        public bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        public RepositoryFileReadResult ReadFile(string allowedRoot, string path, Encoding encoding, long maxFileBytes, long remainingTotalBytes)
        {
            if (maxFileBytes < 0 || remainingTotalBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
            }

            if (IsReparsePoint(path))
            {
                throw new RepositoryFileReadException(RepositoryFileReadFailure.ReparsePoint, "The source file is a reparse point.");
            }

            using var stream = OpenReadStream(path);

            if (OperatingSystem.IsWindows() && !IsHandleContainedUnderRoot(allowedRoot, stream.SafeFileHandle))
            {
                throw new RepositoryFileReadException(RepositoryFileReadFailure.ReparsePoint, "The opened source file is outside the allowed root.");
            }

            if (stream.Length > maxFileBytes)
            {
                throw new RepositoryFileReadException(RepositoryFileReadFailure.FileTooLarge, "The source file exceeds its limit.", stream.Length);
            }

            if (stream.Length > remainingTotalBytes)
            {
                throw new RepositoryFileReadException(RepositoryFileReadFailure.TotalTooLarge, "The source exceeds the total limit.", stream.Length);
            }

            var maxRead = Math.Min(maxFileBytes, remainingTotalBytes);
            using var content = new MemoryStream(capacity: checked((int)Math.Min(stream.Length, maxRead)));
            var buffer = new byte[64 * 1024];
            long bytesRead = 0;
            while (true)
            {
                if (IsReparsePoint(path, stream.SafeFileHandle))
                {
                    throw new RepositoryFileReadException(RepositoryFileReadFailure.ReparsePoint, "The source file became a reparse point at the read boundary.", bytesRead);
                }

                var requested = (int)Math.Min(buffer.Length, maxRead - bytesRead + 1);
                var read = stream.Read(buffer, 0, requested);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
                if (bytesRead > maxFileBytes)
                {
                    throw new RepositoryFileReadException(RepositoryFileReadFailure.FileTooLarge, "The source file exceeds its limit.", bytesRead);
                }

                if (bytesRead > remainingTotalBytes)
                {
                    throw new RepositoryFileReadException(RepositoryFileReadFailure.TotalTooLarge, "The source exceeds the total limit.", bytesRead);
                }

                content.Write(buffer, 0, read);
            }

            return new RepositoryFileReadResult(
                encoding.GetString(content.GetBuffer(), 0, checked((int)bytesRead)),
                bytesRead);
        }

        private static bool IsHandleContainedUnderRoot(string allowedRoot, SafeFileHandle handle)
        {
            var normalizedRoot = SourcePathPolicy.NormalizeRoot(allowedRoot);
            var handlePath = NormalizeHandlePath(GetFinalPathName(handle));
            var relative = Path.GetRelativePath(normalizedRoot, handlePath);

            return !Path.IsPathRooted(relative)
                && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeHandlePath(string path)
        {
            var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                normalized = @"\\" + normalized[8..];
            }
            else if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[4..];
            }

            return SourcePathPolicy.NormalizeRoot(normalized);
        }

        private static string GetFinalPathName(SafeFileHandle handle)
        {
            var capacity = 256;
            while (true)
            {
                var buffer = new StringBuilder(capacity);
                var length = GetFinalPathNameByHandle(handle, buffer, (uint)capacity, 0);
                if (length == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (length < capacity)
                {
                    return buffer.ToString();
                }

                capacity = checked((int)length + 1);
            }
        }

        private bool IsReparsePoint(string path, SafeFileHandle handle)
        {
            if (!OperatingSystem.IsWindows())
            {
                return IsReparsePoint(path);
            }

            if (!GetFileInformationByHandle(handle, out var information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return (information.FileAttributes & (uint)FileAttributes.ReparsePoint) != 0;
        }

        private static FileStream OpenReadStream(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                return new FileStream(path, new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.SequentialScan,
                    BufferSize = 64 * 1024
                });
            }

            var handle = CreateFile(
                path,
                GenericRead,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOpenReparsePoint | FileFlagSequentialScan,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return new FileStream(handle, FileAccess.Read, 64 * 1024, isAsync: false);
        }

        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint OpenExisting = 3;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileFlagSequentialScan = 0x08000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFileW", SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle handle,
            out ByHandleFileInformation information);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle handle,
            StringBuilder filePath,
            uint filePathLength,
            uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
            public uint VolumeSerialNumber;
        }
    }
}
