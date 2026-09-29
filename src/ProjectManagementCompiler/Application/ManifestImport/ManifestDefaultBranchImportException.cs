using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Application.ManifestImport;

public sealed class ManifestDefaultBranchImportException : Exception
{
    public ManifestDefaultBranchImportException(ManifestDefaultBranchResolution resolution)
        : base(resolution.Message ?? "Không thể đọc phiên bản mặc định trong repository cục bộ.")
    {
        FailureCode = resolution.FailureCode ?? ManifestDefaultBranchFailureCode.GitCommandFailed;
        Code = FailureCode switch
        {
            ManifestDefaultBranchFailureCode.RepositoryRootRequired => "REPOSITORY_ROOT_REQUIRED",
            ManifestDefaultBranchFailureCode.RepositoryNotFound => "REPOSITORY_NOT_FOUND",
            ManifestDefaultBranchFailureCode.DefaultRefUnavailable => "DEFAULT_REF_UNAVAILABLE",
            ManifestDefaultBranchFailureCode.InvalidDefaultRef => "INVALID_DEFAULT_REF",
            ManifestDefaultBranchFailureCode.CommitUnresolvable => "COMMIT_UNRESOLVABLE",
            _ => "GIT_COMMAND_FAILED"
        };
    }

    public ManifestDefaultBranchFailureCode FailureCode { get; }
    public string Code { get; }
}
