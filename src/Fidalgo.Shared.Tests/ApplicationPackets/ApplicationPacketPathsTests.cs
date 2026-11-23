using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.Storage;
using Fidalgo.Shared.UserProfile;

namespace Fidalgo.Shared.Tests.ApplicationPackets;

public class ApplicationPacketPathsTests
{
    [Fact]
    public void BuildJobFolderName_FormatsDeterministicSlugWithShortId()
    {
        var job = new JobEntity
        {
            InternalId = Guid.Parse("a1b2c3d4-1111-2222-3333-444455556666"),
            Employer = "Acme Corp.",
            Title = "Senior Software Architect (Remote)"
        };

        var folder = ApplicationPacketPaths.BuildJobFolderName(job);

        Assert.Equal("acme-corp_senior-software-architect-remote_a1b2c3d4", folder);
    }

    [Fact]
    public void BuildJobFolderName_DisambiguatesIdenticalEmployerAndTitleViaInternalId()
    {
        var job1 = new JobEntity
        {
            InternalId = Guid.Parse("11111111-aaaa-bbbb-cccc-000000000001"),
            Employer = "Acme",
            Title = "Staff Engineer"
        };
        var job2 = new JobEntity
        {
            InternalId = Guid.Parse("22222222-aaaa-bbbb-cccc-000000000002"),
            Employer = "Acme",
            Title = "Staff Engineer"
        };

        var folder1 = ApplicationPacketPaths.BuildJobFolderName(job1);
        var folder2 = ApplicationPacketPaths.BuildJobFolderName(job2);

        Assert.NotEqual(folder1, folder2);
        Assert.EndsWith("_11111111", folder1);
        Assert.EndsWith("_22222222", folder2);
    }

    [Fact]
    public void BuildJobFolderName_StripsInvalidWindowsCharsAndHandlesNullTitle()
    {
        var job = new JobEntity
        {
            InternalId = Guid.Parse("deadbeef-0000-0000-0000-000000000000"),
            Employer = "Bad\\/:*?\"<>|Corp",
            Title = null
        };

        var folder = ApplicationPacketPaths.BuildJobFolderName(job);

        Assert.Equal("bad-corp_untitled-role_deadbeef", folder);
        Assert.True(folder.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("NUL")]
    [InlineData("PRN")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public void Slugify_PrefixesWindowsReservedDeviceNames(string reserved)
    {
        var slug = ApplicationPacketPaths.Slugify(reserved, 32, "unknown");

        Assert.StartsWith("job-", slug);
    }

    [Fact]
    public void BuildJobFolderName_TruncatesOverlongEmployerAndTitle()
    {
        var job = new JobEntity
        {
            InternalId = Guid.Parse("12345678-0000-0000-0000-000000000000"),
            Employer = new string('a', 80),
            Title = new string('b', 80)
        };

        var folder = ApplicationPacketPaths.BuildJobFolderName(job);
        var parts = folder.Split('_');

        Assert.Equal(3, parts.Length);
        Assert.True(parts[0].Length <= 32);
        Assert.True(parts[1].Length <= 40);
        Assert.Equal("12345678", parts[2]);
    }

    [Fact]
    public void ResolveJobDirectory_AllowsDriveRootBaseDir()
    {
        var job = new JobEntity
        {
            InternalId = Guid.Parse("abcdef12-0000-0000-0000-000000000000"),
            Employer = "Acme",
            Title = "Architect"
        };

        var driveRoot = Path.GetPathRoot(Path.GetTempPath())!;
        var resolved = ApplicationPacketPaths.ResolveJobDirectory(driveRoot, job);

        Assert.StartsWith(driveRoot, resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("acme_architect_abcdef12", resolved);
    }

    [Fact]
    public void ResolveContainedDirectory_ThrowsOnTraversal()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "fidalgo-containment");

        Assert.Throws<InvalidOperationException>(() =>
            ApplicationPacketPaths.ResolveContainedDirectory(baseDir, Path.Combine("..", "outside")));
    }

    [Theory]
    [InlineData("resume.pdf", true)]
    [InlineData("COVER-LETTER.PDF", true)]
    [InlineData("job-posting.md", true)]
    [InlineData("resume.md", true)]
    [InlineData("cover-letter.md", true)]
    [InlineData("packet.json", false)]
    [InlineData("../appsettings.json", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedPacketFileName_EnforcesFiveFileAllowlist(string? fileName, bool expected)
    {
        Assert.Equal(expected, ApplicationPacketPaths.IsAllowedPacketFileName(fileName));
    }

    [Fact]
    public void ResolveBaseOutputDirectory_FollowsPrecedence()
    {
        var opts = new ApplicationPacketOptions
        {
            ProfileDirectory = @"C:\profile",
            OutputDirectory = @"C:\configured-out"
        };

        Assert.Equal(@"C:\override-out", ApplicationPacketPaths.ResolveBaseOutputDirectory(@"C:\override-out", opts));
        Assert.Equal(@"C:\configured-out", ApplicationPacketPaths.ResolveBaseOutputDirectory(null, opts));

        opts.OutputDirectory = null;
        Assert.Equal(Path.Combine(@"C:\profile", UserProfilePaths.ApplicationsDirectoryName), ApplicationPacketPaths.ResolveBaseOutputDirectory(null, opts));

        opts.ProfileDirectory = null;
        Assert.Equal(UserProfilePaths.GetDefaultApplicationsDir(), ApplicationPacketPaths.ResolveBaseOutputDirectory(null, opts));
    }

    [Fact]
    public void JobUrlFormatter_FormatsAdzunaHttpAndEmptyUrls()
    {
        var adzunaJob = new JobEntity { SourceWebsite = "adzuna", EmployerJobId = "98765" };
        var httpJob = new JobEntity { SourceWebsite = "https://example.com/jobs/1" };
        var unknownJob = new JobEntity { SourceWebsite = "internal" };

        Assert.Equal("https://www.adzuna.com/details/98765", JobUrlFormatter.FormatUrl(adzunaJob));
        Assert.Equal("https://example.com/jobs/1", JobUrlFormatter.FormatUrl(httpJob));
        Assert.Equal(string.Empty, JobUrlFormatter.FormatUrl(unknownJob));
    }
}
