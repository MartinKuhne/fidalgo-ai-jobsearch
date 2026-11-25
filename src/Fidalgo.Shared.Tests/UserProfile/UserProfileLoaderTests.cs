using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.UserProfile;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.Tests.UserProfile;

public class UserProfileLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _profileDir;

    public UserProfileLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fidalgo-loader-" + Guid.NewGuid().ToString("N"));
        _profileDir = Path.Combine(_tempDir, "profile");
        Directory.CreateDirectory(_profileDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_LoadsAllProfileFilesFromProfileDirectory()
    {
        var resumePath = Path.Combine(_profileDir, UserProfilePaths.ResumeFileName);
        var storyPath = Path.Combine(_profileDir, UserProfilePaths.CareerStoryFileName);
        var goalsPath = Path.Combine(_profileDir, UserProfilePaths.CareerGoalsFileName);

        await File.WriteAllTextAsync(resumePath, "# Jane Doe\nStaff Engineer");
        await File.WriteAllTextAsync(storyPath, "Led platform migration.");
        await File.WriteAllTextAsync(goalsPath, "Principal Architect role.");

        var loader = CreateLoader(new ApplicationPacketOptions { ProfileDirectory = _profileDir }, email: "jane@example.com");

        var content = await loader.LoadAsync();

        Assert.Equal("# Jane Doe\nStaff Engineer", content.ResumeMarkdown);
        Assert.Equal("Led platform migration.", content.CareerStoryMarkdown);
        Assert.Equal("Principal Architect role.", content.CareerGoalsMarkdown);
        Assert.Equal("jane@example.com", content.DefaultEmail);
        Assert.Equal(Path.Combine(_profileDir, UserProfilePaths.ApplicationsDirectoryName), content.ApplicationsOutputDir);
        Assert.False(content.Sources.ResumeFromOverride);
        Assert.Equal(resumePath, content.Sources.ResumePath);
        Assert.True(content.Sources.HasCareerStory);
        Assert.Equal(storyPath, content.Sources.CareerStoryPath);
        Assert.True(content.Sources.HasCareerGoals);
        Assert.Equal(goalsPath, content.Sources.CareerGoalsPath);
    }

    [Fact]
    public async Task LoadAsync_ThrowsValidationExceptionWithBaseResumeKey_WhenResumeMissing()
    {
        var loader = CreateLoader(new ApplicationPacketOptions { ProfileDirectory = _profileDir });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => loader.LoadAsync());

        Assert.True(ex.Errors.ContainsKey("baseResume"));
        Assert.NotEmpty(ex.Errors["baseResume"]);
    }

    [Fact]
    public async Task LoadAsync_InlineOverridesWinOverFilesAndPaths()
    {
        var resumePath = Path.Combine(_profileDir, UserProfilePaths.ResumeFileName);
        await File.WriteAllTextAsync(resumePath, "Disk resume");

        var customResumePath = Path.Combine(_tempDir, "custom-resume.md");
        await File.WriteAllTextAsync(customResumePath, "Custom path resume");

        var loader = CreateLoader(new ApplicationPacketOptions { ProfileDirectory = _profileDir });

        var content = await loader.LoadAsync(new UserProfileLoadOverrides(
            ResumePath: customResumePath,
            ResumeContentOverride: "Inline override resume",
            CareerStoryContentOverride: "Inline career story",
            CareerGoalsContentOverride: "Inline career goals",
            OutputDirectoryOverride: Path.Combine(_tempDir, "explicit-out")));

        Assert.Equal("Inline override resume", content.ResumeMarkdown);
        Assert.Equal("Inline career story", content.CareerStoryMarkdown);
        Assert.Equal("Inline career goals", content.CareerGoalsMarkdown);
        Assert.True(content.Sources.ResumeFromOverride);
        Assert.Null(content.Sources.ResumePath);
        Assert.True(content.Sources.HasCareerStory);
        Assert.True(content.Sources.HasCareerGoals);
        Assert.Equal(Path.Combine(_tempDir, "explicit-out"), content.ApplicationsOutputDir);
    }

    [Fact]
    public async Task LoadAsync_AllowsMissingOptionalDocuments()
    {
        var resumePath = Path.Combine(_profileDir, UserProfilePaths.ResumeFileName);
        await File.WriteAllTextAsync(resumePath, "# Resume");

        var loader = CreateLoader(new ApplicationPacketOptions
        {
            ProfileDirectory = _profileDir,
            OutputDirectory = Path.Combine(_tempDir, "configured-out")
        });

        var content = await loader.LoadAsync();

        Assert.Equal("# Resume", content.ResumeMarkdown);
        Assert.Null(content.CareerStoryMarkdown);
        Assert.Null(content.CareerGoalsMarkdown);
        Assert.False(content.Sources.HasCareerStory);
        Assert.False(content.Sources.HasCareerGoals);
        Assert.Equal(Path.Combine(_tempDir, "configured-out"), content.ApplicationsOutputDir);
    }

    private UserProfileLoader CreateLoader(ApplicationPacketOptions options, string? email = null)
    {
        var resolver = new UserProfileResolver(NullLogger<UserProfileResolver>.Instance, options.ProfileDirectory);
        var configValues = new Dictionary<string, string?>();
        if (email is not null)
        {
            configValues["Email"] = email;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new UserProfileLoader(
            resolver,
            Options.Create(options),
            config,
            NullLogger<UserProfileLoader>.Instance);
    }
}
