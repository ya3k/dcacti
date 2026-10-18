using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace GameServer.Application.Tests.Balance.Tests;

public sealed class ProtectedArtifactGuardTests
{
    [Fact]
    public void BalanceBaselineMatrix_CanonicalPath_IsProtectedFromWrite()
    {
        var canonicalPath = BalanceBaselineMatrix.CanonicalArtifactPath();
        Assert.Contains(Path.Combine("tasks", "artifacts"), canonicalPath);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BalanceBaselineMatrix.WriteArtifact("{}", canonicalPath));

        Assert.Contains("Refusing to overwrite protected historical artifact", ex.Message);
    }

    [Fact]
    public void BalanceControlledExperimentMatrix_CanonicalPath_IsProtectedFromWrite()
    {
        var canonicalPath = BalanceControlledExperimentMatrix.CanonicalArtifactPath();
        Assert.Contains(Path.Combine("tasks", "artifacts"), canonicalPath);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BalanceControlledExperimentMatrix.WriteArtifact("{}", canonicalPath));

        Assert.Contains("Refusing to overwrite protected historical artifact", ex.Message);
    }

    [Fact]
    public void ArtifactPath_DefaultsToIsolatedDirectory_NeverTasksArtifacts()
    {
        var baselinePath = BalanceBaselineMatrix.ArtifactPath();
        var canonicalBaseline = BalanceBaselineMatrix.CanonicalArtifactPath();
        Assert.NotEqual(canonicalBaseline, baselinePath);
        Assert.DoesNotContain(Path.Combine("tasks", "artifacts"), baselinePath);

        var expPath = BalanceControlledExperimentMatrix.ArtifactPath();
        var canonicalExp = BalanceControlledExperimentMatrix.CanonicalArtifactPath();
        Assert.NotEqual(canonicalExp, expPath);
        Assert.DoesNotContain(Path.Combine("tasks", "artifacts"), expPath);
    }

    [Fact]
    public void ProtectedArtifactGuard_RejectsAllProtectedFilenamesInArtifactsDirectory()
    {
        var repoRoot = BalanceBaselineMatrix.RepositoryRoot();
        var artifactsDir = Path.Combine(repoRoot, "tasks", "artifacts");

        foreach (var protectedName in ProtectedArtifactGuard.ProtectedFileNames)
        {
            var target = Path.Combine(artifactsDir, protectedName);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                ProtectedArtifactGuard.EnsureNotProtected(target));

            Assert.Contains("Refusing to overwrite protected historical artifact", ex.Message);
        }
    }

    [Fact]
    public void ProtectedArtifactGuard_AllowsIsolatedTemporaryDirectoryWrites()
    {
        var tempDir = Path.Combine(AppContext.BaseDirectory, "test-artifacts-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var safePath = Path.Combine(tempDir, "test-artifact.json");

            // Must not throw
            ProtectedArtifactGuard.EnsureNotProtected(safePath);
            File.WriteAllText(safePath, "{\"test\": true}");
            Assert.True(File.Exists(safePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
