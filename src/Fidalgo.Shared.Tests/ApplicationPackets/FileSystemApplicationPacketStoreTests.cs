using System.Text;
using Fidalgo.Shared.ApplicationPackets;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fidalgo.Shared.Tests.ApplicationPackets;

public class FileSystemApplicationPacketStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemApplicationPacketStore _sut;
    private readonly JobEntity _job;

    public FileSystemApplicationPacketStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fidalgo-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _sut = new FileSystemApplicationPacketStore(NullLogger<FileSystemApplicationPacketStore>.Instance);
        _job = new JobEntity
        {
            InternalId = Guid.Parse("a1b2c3d4-e5f6-4789-abcd-0123456789ef"),
            Employer = "Acme Corp",
            Title = "Principal Engineer"
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsExistsFalse_WhenDirectoryMissing()
    {
        var status = await _sut.GetStatusAsync(_tempDir, _job);

        Assert.False(status.Exists);
        Assert.Null(status.GeneratedAtUtc);
        Assert.Null(status.ResumeMarkdown);
        Assert.Null(status.CoverLetterMarkdown);
        Assert.Equal("acme-corp_principal-engineer_a1b2c3d4", status.FolderName);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsExistsFalse_WhenRequiredFileMissingOrZeroBytes()
    {
        var files = ApplicationPacketPaths.GetPacketFiles(_tempDir, _job);
        Directory.CreateDirectory(files.DirectoryPath);
        await File.WriteAllBytesAsync(files.ResumePdfPath, "%PDF-1.4\n"u8.ToArray());
        await File.WriteAllBytesAsync(files.CoverLetterPdfPath, []); // 0 bytes!
        await File.WriteAllTextAsync(files.JobPostingPath, "# Job Posting");

        var status = await _sut.GetStatusAsync(_tempDir, _job);

        Assert.False(status.Exists);
    }

    [Fact]
    public async Task SavePacketAsync_WritesAllArtifactsAtomicallyWithNoLeftoverTempFiles()
    {
        var request = new ApplicationPacketWriteRequest(
            ResumePdfBytes: "%PDF-1.4\nresume"u8.ToArray(),
            CoverLetterPdfBytes: "%PDF-1.4\ncover"u8.ToArray(),
            JobPostingMarkdown: "# Posting",
            ResumeMarkdown: "# Resume",
            CoverLetterMarkdown: "# Cover Letter",
            ModelName: "test-model",
            ForceRegenerated: false);

        var files = await _sut.SavePacketAsync(_tempDir, _job, request);

        Assert.True(File.Exists(files.ResumePdfPath));
        Assert.True(File.Exists(files.CoverLetterPdfPath));
        Assert.True(File.Exists(files.JobPostingPath));
        Assert.True(File.Exists(files.ResumeMarkdownPath));
        Assert.True(File.Exists(files.CoverLetterMarkdownPath));
        Assert.True(File.Exists(files.ManifestPath));

        var leftoverTempFiles = Directory.GetFiles(files.DirectoryPath, "*.tmp-*");
        Assert.Empty(leftoverTempFiles);

        var status = await _sut.GetStatusAsync(_tempDir, _job);
        Assert.True(status.Exists);
        Assert.NotNull(status.GeneratedAtUtc);
        Assert.Equal("# Resume", status.ResumeMarkdown);
        Assert.Equal("# Cover Letter", status.CoverLetterMarkdown);
    }

    [Theory]
    [InlineData("resume.pdf")]
    [InlineData("cover-letter.pdf")]
    [InlineData("job-posting.md")]
    [InlineData("resume.md")]
    [InlineData("cover-letter.md")]
    public async Task OpenPacketFileAsync_AllowsAllFiveAllowlistedFiles_AndAllowsSharedReads(string fileName)
    {
        var request = new ApplicationPacketWriteRequest(
            ResumePdfBytes: "%PDF-1.4\nresume"u8.ToArray(),
            CoverLetterPdfBytes: "%PDF-1.4\ncover"u8.ToArray(),
            JobPostingMarkdown: "# Posting",
            ResumeMarkdown: "# Resume",
            CoverLetterMarkdown: "# Cover Letter",
            ModelName: "test-model",
            ForceRegenerated: false);

        await _sut.SavePacketAsync(_tempDir, _job, request);

        await using var stream1 = await _sut.OpenPacketFileAsync(_tempDir, _job, fileName);
        await using var stream2 = await _sut.OpenPacketFileAsync(_tempDir, _job, fileName.ToUpperInvariant());

        Assert.NotNull(stream1);
        Assert.NotNull(stream2);
        using var reader = new StreamReader(stream1!, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.NotEmpty(content);
    }

    [Theory]
    [InlineData("packet.json")]
    [InlineData("../appsettings.json")]
    [InlineData("..\\jobs.db")]
    [InlineData("unknown.txt")]
    public async Task OpenPacketFileAsync_ThrowsValidationException_ForDisallowedFileNames(string disallowedFileName)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.OpenPacketFileAsync(_tempDir, _job, disallowedFileName));

        Assert.True(ex.Errors.ContainsKey("fileName"));
    }

    [Fact]
    public async Task OpenPacketFileAsync_ReturnsNull_WhenAllowedFileDoesNotExistOnDisk()
    {
        var stream = await _sut.OpenPacketFileAsync(_tempDir, _job, ApplicationPacketPaths.ResumePdfFileName);

        Assert.Null(stream);
    }
}
