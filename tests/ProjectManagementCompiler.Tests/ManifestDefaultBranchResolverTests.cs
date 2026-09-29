using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ManifestDefaultBranchResolverTests
{
    private const string ExpectedCommit = "0123456789abcdef0123456789abcdef01234567";

    public static void DefaultBranchResolvesConfiguredOfficialRefToFullCommit()
    {
        var runner = new RecordingGitCommandRunner();
        var resolver = new ManifestDefaultBranchResolver(runner);

        var result = resolver.ResolveAsync(Directory.GetCurrentDirectory(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        TestAssert.True(result.Succeeded, "A valid configured origin default ref must resolve successfully.");
        TestAssert.Equal("refs/remotes/origin/main", result.TargetRef, "Resolution must retain the validated local target ref for diagnostics.");
        TestAssert.Equal(ExpectedCommit, result.CommitSha, "The result must expose the full commit object ID, not the symbolic ref.");
        TestAssert.Equal(
            "symbolic-ref --quiet refs/remotes/origin/HEAD|check-ref-format refs/remotes/origin/main|rev-parse --verify --end-of-options refs/remotes/origin/main^{commit}",
            string.Join('|', runner.Commands),
            "Resolution must read the local symbolic ref, validate its target, and resolve that target as a commit.");
    }

    public static void ResolverDistinguishesGitProcessFailureFromUnavailableDefaultRef()
    {
        var runner = new ConfigurableGitCommandRunner
        {
            SymbolicRefFailure = new IOException("private diagnostic that must not escape")
        };
        var result = new ManifestDefaultBranchResolver(runner)
            .ResolveAsync(Directory.GetCurrentDirectory(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        TestAssert.True(!result.Succeeded, "A Git process failure must not produce a source identity.");
        TestAssert.True(result.FailureCode == ManifestDefaultBranchFailureCode.GitCommandFailed, "Git process failures must not be mislabeled as an absent default ref.");
        TestAssert.False(result.Message!.Contains("private diagnostic", StringComparison.Ordinal), "Raw Git diagnostics must not be returned to the UI.");
    }

    public static void ResolverUsesTypedSafeFailuresForUnavailableAndUntrustedRefs()
    {
        var missing = new ManifestDefaultBranchResolver(new ConfigurableGitCommandRunner { SymbolicRefOutput = string.Empty })
            .ResolveAsync(Directory.GetCurrentDirectory(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        TestAssert.True(missing.FailureCode == ManifestDefaultBranchFailureCode.DefaultRefUnavailable, "An empty local default-ref result must be reported as unavailable.");

        foreach (var target in new[] { "refs/heads/main", "refs/remotes/upstream/main", "refs/remotes/origin/HEAD", "refs/remotes/origin/../main" })
        {
            var runner = new ConfigurableGitCommandRunner { SymbolicRefOutput = target };
            var result = new ManifestDefaultBranchResolver(runner)
                .ResolveAsync(Directory.GetCurrentDirectory(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            TestAssert.True(result.FailureCode == ManifestDefaultBranchFailureCode.InvalidDefaultRef, $"The unapproved ref '{target}' must fail closed.");
            TestAssert.False(runner.Commands.Any(command => command.StartsWith("rev-parse ", StringComparison.Ordinal)), "An untrusted ref must not be resolved as an object.");
        }
    }

    public static void ResolverRejectsNonCommitAndOversizedOutputAndPropagatesCancellation()
    {
        var nonCommit = new ManifestDefaultBranchResolver(new ConfigurableGitCommandRunner { CommitOutput = "not-a-full-object-id" })
            .ResolveAsync(Directory.GetCurrentDirectory(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        TestAssert.True(nonCommit.FailureCode == ManifestDefaultBranchFailureCode.CommitUnresolvable, "A non-commit result must not be accepted as a source identity.");

        var oversized = new ManifestDefaultBranchResolver(new ConfigurableGitCommandRunner { SymbolicRefOutput = new string('x', 1025) })
            .ResolveAsync(Directory.GetCurrentDirectory(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        TestAssert.True(oversized.FailureCode == ManifestDefaultBranchFailureCode.InvalidDefaultRef, "An oversized symbolic-ref result must be rejected before further Git calls.");

        using var cancellation = new CancellationTokenSource();
        var cancellingRunner = new ConfigurableGitCommandRunner
        {
            BeforeSymbolicRef = cancellation.Cancel
        };
        TestAssert.Throws<OperationCanceledException>(
            () => new ManifestDefaultBranchResolver(cancellingRunner)
                .ResolveAsync(Directory.GetCurrentDirectory(), cancellation.Token)
                .GetAwaiter()
                .GetResult(),
            "Cancellation during local-ref resolution must propagate to the caller.");
    }

    private sealed class RecordingGitCommandRunner : IManifestGitCommandRunner
    {
        public List<string> Commands { get; } = [];

        public Task<(string Stdout, string Stderr)> RunAsync(
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            long maxOutputBytes,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(string.Join(' ', arguments));
            return arguments[0] switch
            {
                "symbolic-ref" => Task.FromResult(("refs/remotes/origin/main\n", string.Empty)),
                "check-ref-format" => Task.FromResult((string.Empty, string.Empty)),
                "rev-parse" => Task.FromResult(($"{ExpectedCommit}\n", string.Empty)),
                _ => throw new InvalidOperationException($"Unexpected Git command: {string.Join(' ', arguments)}")
            };
        }
    }

    private sealed class ConfigurableGitCommandRunner : IManifestGitCommandRunner
    {
        public string SymbolicRefOutput { get; init; } = "refs/remotes/origin/main\n";
        public string CommitOutput { get; init; } = $"{ExpectedCommit}\n";
        public Exception? SymbolicRefFailure { get; init; }
        public Action? BeforeSymbolicRef { get; init; }
        public List<string> Commands { get; } = [];

        public Task<(string Stdout, string Stderr)> RunAsync(
            string repositoryRoot,
            IReadOnlyList<string> arguments,
            long maxOutputBytes,
            CancellationToken cancellationToken)
        {
            Commands.Add(string.Join(' ', arguments));
            if (arguments[0] == "symbolic-ref")
            {
                BeforeSymbolicRef?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                if (SymbolicRefFailure is not null)
                {
                    throw SymbolicRefFailure;
                }

                return Task.FromResult((SymbolicRefOutput, string.Empty));
            }

            if (arguments[0] == "check-ref-format")
            {
                if (arguments[1].Contains("..", StringComparison.Ordinal))
                {
                    throw new ManifestGitCommandException(1, "The test double simulates Git rejecting an invalid ref.");
                }

                return Task.FromResult((string.Empty, string.Empty));
            }

            if (arguments[0] == "rev-parse")
            {
                return Task.FromResult((CommitOutput, string.Empty));
            }

            throw new InvalidOperationException($"Unexpected Git command: {string.Join(' ', arguments)}");
        }
    }
}
