using System.Text;

namespace ProjectManagementCompiler.Sources;

public interface IManifestDefaultBranchResolver
{
    Task<ManifestDefaultBranchResolution> ResolveAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default);
}

public enum ManifestDefaultBranchFailureCode
{
    RepositoryRootRequired,
    RepositoryNotFound,
    DefaultRefUnavailable,
    InvalidDefaultRef,
    CommitUnresolvable,
    GitCommandFailed
}

public sealed record ManifestDefaultBranchResolution
{
    public bool Succeeded => CommitSha is not null && FailureCode is null;
    public string? TargetRef { get; init; }
    public string? CommitSha { get; init; }
    public ManifestDefaultBranchFailureCode? FailureCode { get; init; }
    public string? Message { get; init; }

    public static ManifestDefaultBranchResolution Success(string targetRef, string commitSha) => new()
    {
        TargetRef = targetRef,
        CommitSha = commitSha
    };

    public static ManifestDefaultBranchResolution Failure(
        ManifestDefaultBranchFailureCode code,
        string message,
        string? targetRef = null) => new()
    {
        TargetRef = targetRef,
        FailureCode = code,
        Message = message
    };
}

public sealed class ManifestDefaultBranchResolver : IManifestDefaultBranchResolver
{
    private const string AllowedRefPrefix = "refs/remotes/origin/";
    private const int MaxRefOutputBytes = 1024;
    private const int MaxCommandOutputBytes = 128;
    private readonly IManifestGitCommandRunner commandRunner;

    public ManifestDefaultBranchResolver(IManifestGitCommandRunner? commandRunner = null)
    {
        this.commandRunner = commandRunner ?? new ProcessManifestGitCommandRunner();
    }

    public async Task<ManifestDefaultBranchResolution> ResolveAsync(
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.RepositoryRootRequired,
                "Chưa chọn thư mục repository nguồn.");
        }

        if (!Directory.Exists(repositoryRoot))
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.RepositoryNotFound,
                "Không tìm thấy thư mục repository nguồn.");
        }

        string targetRef;
        try
        {
            var symbolicRef = await commandRunner.RunAsync(
                repositoryRoot,
                ["symbolic-ref", "--quiet", "refs/remotes/origin/HEAD"],
                MaxRefOutputBytes,
                cancellationToken);
            if (!WithinLimit(symbolicRef.Stdout, MaxRefOutputBytes))
            {
                return ManifestDefaultBranchResolution.Failure(
                    ManifestDefaultBranchFailureCode.InvalidDefaultRef,
                    "Cấu hình nhánh mặc định vượt giới hạn an toàn.");
            }

            targetRef = symbolicRef.Stdout.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ManifestGitCommandException exception) when (exception.ExitCode == 1)
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.DefaultRefUnavailable,
                "Chưa cấu hình nhánh mặc định cho origin trong bản repository cục bộ.");
        }
        catch (Exception exception) when (IsGitBoundaryFailure(exception))
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.GitCommandFailed,
                "Không thể chạy lệnh Git để đọc nhánh mặc định cục bộ.");
        }

        if (!IsAllowedRefTarget(targetRef))
        {
            return ManifestDefaultBranchResolution.Failure(
                string.IsNullOrWhiteSpace(targetRef)
                    ? ManifestDefaultBranchFailureCode.DefaultRefUnavailable
                    : ManifestDefaultBranchFailureCode.InvalidDefaultRef,
                "Không có nhánh mặc định an toàn trong origin của repository cục bộ.");
        }

        try
        {
            await commandRunner.RunAsync(
                repositoryRoot,
                ["check-ref-format", targetRef],
                MaxCommandOutputBytes,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ManifestGitCommandException exception) when (exception.ExitCode is not null)
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.InvalidDefaultRef,
                "Tên nhánh mặc định trong repository không hợp lệ.",
                targetRef);
        }
        catch (Exception exception) when (IsGitBoundaryFailure(exception))
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.GitCommandFailed,
                "Không thể kiểm tra tên nhánh mặc định bằng Git.",
                targetRef);
        }

        string commitSha;
        try
        {
            var commit = await commandRunner.RunAsync(
                repositoryRoot,
                ["rev-parse", "--verify", "--end-of-options", $"{targetRef}^{{commit}}"],
                MaxCommandOutputBytes,
                cancellationToken);
            if (!WithinLimit(commit.Stdout, MaxCommandOutputBytes))
            {
                return ManifestDefaultBranchResolution.Failure(
                    ManifestDefaultBranchFailureCode.CommitUnresolvable,
                    "Git trả về dữ liệu commit vượt giới hạn an toàn.",
                    targetRef);
            }

            commitSha = commit.Stdout.Trim();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ManifestGitCommandException exception) when (exception.ExitCode is not null)
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.CommitUnresolvable,
                "Nhánh mặc định không trỏ tới một commit có thể đọc trong bản repository cục bộ.",
                targetRef);
        }
        catch (Exception exception) when (IsGitBoundaryFailure(exception))
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.GitCommandFailed,
                "Không thể chạy lệnh Git để phân giải commit cục bộ.",
                targetRef);
        }

        if (!IsFullCommitId(commitSha))
        {
            return ManifestDefaultBranchResolution.Failure(
                ManifestDefaultBranchFailureCode.CommitUnresolvable,
                "Nhánh mặc định không phân giải thành một commit đầy đủ.",
                targetRef);
        }

        return ManifestDefaultBranchResolution.Success(targetRef, commitSha);
    }

    private static bool IsAllowedRefTarget(string targetRef) =>
        targetRef.StartsWith(AllowedRefPrefix, StringComparison.Ordinal)
        && targetRef.Length > AllowedRefPrefix.Length
        && !string.Equals(targetRef, $"{AllowedRefPrefix}HEAD", StringComparison.Ordinal);

    private static bool IsFullCommitId(string value) =>
        value.Length == 40 && value.All(Uri.IsHexDigit);

    private static bool WithinLimit(string value, int maxBytes) =>
        Encoding.UTF8.GetByteCount(value) <= maxBytes;

    private static bool IsGitBoundaryFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException;
}
