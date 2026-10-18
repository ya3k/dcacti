namespace GameServer.Application.Tests.Balance;

/// <summary>
/// Safety guard that prevents verification tests and runners from overwriting
/// pinned historical baseline artifacts and experiment archives.
/// </summary>
internal static class ProtectedArtifactGuard
{
    /// <summary>
    /// Filenames of protected historical artifacts that must never be overwritten
    /// by test execution or candidate generation.
    /// </summary>
    public static readonly IReadOnlyList<string> ProtectedFileNames =
    [
        "TASK-194-baseline-simulation-results.json",
        "TASK-197-controlled-balance-experiments.json",
        "TASK-296-canonical-75-candidate.json",
        "MVP-ACCEPTANCE-RELEASE-CLOSURE-AUDIT.md",
    ];

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if <paramref name="targetPath"/>
    /// resolves to a protected historical artifact.
    /// </summary>
    public static void EnsureNotProtected(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var fullPath = Path.GetFullPath(targetPath);
        var fileName = Path.GetFileName(fullPath);
        var repoRoot = BalanceBaselineMatrix.RepositoryRoot();

        // 1. Guard against direct overwrite of known protected filenames in tasks/artifacts/
        var canonicalArtifactsDir = Path.GetFullPath(Path.Combine(repoRoot, "tasks", "artifacts"));
        var targetDir = Path.GetDirectoryName(fullPath) ?? string.Empty;

        var isInsideCanonicalArtifactsDir = string.Equals(targetDir, canonicalArtifactsDir, StringComparison.OrdinalIgnoreCase);

        if (isInsideCanonicalArtifactsDir)
        {
            foreach (var protectedName in ProtectedFileNames)
            {
                if (string.Equals(fileName, protectedName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Refusing to overwrite protected historical artifact at '{fullPath}'. " +
                        "Pinned baselines and experiment archives must never be overwritten by test execution.");
                }
            }
        }

        // 2. Guard against overwriting the TASK-296 candidate artifact in test directories
        if (string.Equals(fileName, "TASK-296-canonical-75-candidate.json", StringComparison.OrdinalIgnoreCase))
        {
            var candidateInOutputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "TASK-296-canonical-75-candidate.json"));
            if (string.Equals(fullPath, candidateInOutputDir, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Refusing to overwrite protected TASK-296 candidate artifact at '{fullPath}'. " +
                    "Candidate artifacts are protected from mutation.");
            }
        }
    }
}
