using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Fidalgo.Shared.Tests.UserProfile;

public class UserProfileResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogger<UserProfileResolver> _logger;
    private readonly UserProfileResolver _sut;

    public UserProfileResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fidalgo-usrprof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _logger = Substitute.For<ILogger<UserProfileResolver>>();
        _sut = new UserProfileResolver(_logger, _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveCareerStoryPath_ReturnsCliPath_WhenProvidedAndExists()
    {
        var cliPath = Path.Combine(_tempDir, "my-story.md");
        File.WriteAllText(cliPath, "My story");

        var result = _sut.ResolveCareerStoryPath(cliPath);

        Assert.Equal(cliPath, result);
    }

    [Fact]
    public void ResolveResumePath_ReturnsCliPath_WhenProvidedAndExists()
    {
        var cliPath = Path.Combine(_tempDir, "my-resume.md");
        File.WriteAllText(cliPath, "My resume");

        var result = _sut.ResolveResumePath(cliPath);

        Assert.Equal(cliPath, result);
    }

    [Fact]
    public void ResolveResumePath_FallsBackToDefault_WhenCliOmittedAndDefaultExists()
    {
        var defaultPath = Path.Combine(_tempDir, UserProfilePaths.ResumeFileName);
        File.WriteAllText(defaultPath, "Default resume");

        var result = _sut.ResolveResumePath(null);

        Assert.Equal(defaultPath, result);
    }

    [Fact]
    public void ResolveResumePath_FallsBackToDefault_WhenCliMissingButDefaultExists()
    {
        var missingCli = Path.Combine(_tempDir, "missing.md");
        var defaultPath = Path.Combine(_tempDir, UserProfilePaths.ResumeFileName);
        File.WriteAllText(defaultPath, "Default resume");

        var result = _sut.ResolveResumePath(missingCli);

        Assert.Equal(defaultPath, result);
    }

    [Fact]
    public void ResolveResumePath_LogsWarning_WhenCliProvidedButMissing()
    {
        var missingCli = Path.Combine(_tempDir, "missing.md");

        _sut.ResolveResumePath(missingCli);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    [Fact]
    public void ResolveResumePath_ReturnsNull_WhenNeitherExists()
    {
        var result = _sut.ResolveResumePath(null);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveResumePath_DoesNotLog_WhenCliOmitted()
    {
        _sut.ResolveResumePath(null);

        _logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    [Fact]
    public void ResolveCareerStoryPath_FallsBackToDefault_WhenCliOmittedAndDefaultExists()
    {
        var defaultPath = Path.Combine(_tempDir, UserProfilePaths.CareerStoryFileName);
        File.WriteAllText(defaultPath, "Default story");

        var result = _sut.ResolveCareerStoryPath(null);

        Assert.Equal(defaultPath, result);
    }

    [Fact]
    public void ResolveCareerStoryPath_FallsBackToDefault_WhenCliMissingButDefaultExists()
    {
        var missingCli = Path.Combine(_tempDir, "missing.md");
        var defaultPath = Path.Combine(_tempDir, UserProfilePaths.CareerStoryFileName);
        File.WriteAllText(defaultPath, "Default story");

        var result = _sut.ResolveCareerStoryPath(missingCli);

        Assert.Equal(defaultPath, result);
    }

    [Fact]
    public void ResolveCareerStoryPath_LogsWarning_WhenCliProvidedButMissing()
    {
        var missingCli = Path.Combine(_tempDir, "missing.md");

        _sut.ResolveCareerStoryPath(missingCli);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    [Fact]
    public void ResolveCareerStoryPath_ReturnsNull_WhenNeitherExists()
    {
        var result = _sut.ResolveCareerStoryPath(null);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveCareerStoryPath_DoesNotLog_WhenCliOmitted()
    {
        _sut.ResolveCareerStoryPath(null);

        _logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    [Fact]
    public void ResolveCareerGoalsPath_UsesGoalsFileNameNotStoryFileName()
    {
        var defaultGoalsPath = Path.Combine(_tempDir, UserProfilePaths.CareerGoalsFileName);
        File.WriteAllText(defaultGoalsPath, "Default goals");

        var result = _sut.ResolveCareerGoalsPath(null);

        Assert.Equal(defaultGoalsPath, result);
        Assert.EndsWith(UserProfilePaths.CareerGoalsFileName, result);
    }

    [Fact]
    public void ResolveCareerGoalsPath_ReturnsCliPath_WhenProvidedAndExists()
    {
        var cliPath = Path.Combine(_tempDir, "my-goals.md");
        File.WriteAllText(cliPath, "My goals");

        var result = _sut.ResolveCareerGoalsPath(cliPath);

        Assert.Equal(cliPath, result);
    }

    [Fact]
    public void ResolveCareerGoalsPath_ReturnsNull_WhenNeitherExists()
    {
        var result = _sut.ResolveCareerGoalsPath(null);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveResumePath_PrefersPrimaryDocumentsDir_OverLegacyDirWithoutInfoLog()
    {
        var primaryDir = Path.Combine(_tempDir, "primary");
        var legacyDir = Path.Combine(_tempDir, "legacy");
        Directory.CreateDirectory(primaryDir);
        Directory.CreateDirectory(legacyDir);

        var primaryResume = Path.Combine(primaryDir, UserProfilePaths.ResumeFileName);
        var legacyResume = Path.Combine(legacyDir, UserProfilePaths.ResumeFileName);
        File.WriteAllText(primaryResume, "Primary resume");
        File.WriteAllText(legacyResume, "Legacy resume");

        var resolver = new UserProfileResolver(_logger, primaryDir, legacyDir);
        var result = resolver.ResolveResumePath(null);

        Assert.Equal(primaryResume, result);
        _logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    [Fact]
    public void ResolveResumePath_FallsBackToLegacyDirAndLogsInformation_WhenMissingInPrimary()
    {
        var primaryDir = Path.Combine(_tempDir, "primary-empty");
        var legacyDir = Path.Combine(_tempDir, "legacy-populated");
        Directory.CreateDirectory(primaryDir);
        Directory.CreateDirectory(legacyDir);

        var legacyResume = Path.Combine(legacyDir, UserProfilePaths.ResumeFileName);
        File.WriteAllText(legacyResume, "Legacy resume");

        var resolver = new UserProfileResolver(_logger, primaryDir, legacyDir);
        var result = resolver.ResolveResumePath(null);

        Assert.Equal(legacyResume, result);
        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }

    [Fact]
    public void ResolveResumePath_ExplicitBaseDir_DoesNotFallBackToLegacyDir()
    {
        var explicitDir = Path.Combine(_tempDir, "explicit");
        Directory.CreateDirectory(explicitDir);

        var resolver = new UserProfileResolver(_logger, explicitDir);
        var result = resolver.ResolveResumePath(null);

        Assert.Null(result);
        _logger.DidNotReceive().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object?>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object?, Exception?, string>>());
    }
}
