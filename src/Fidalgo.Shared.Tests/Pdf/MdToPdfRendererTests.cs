using System.Diagnostics;
using System.Net;
using System.Web;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Fidalgo.Shared.Pdf;
using Fidalgo.Shared.Tests.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fidalgo.Shared.Tests.Pdf;

public class MdToPdfRendererTests
{
    private static readonly byte[] ValidPdfBytes = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n"u8.ToArray();

    [Fact]
    public async Task RenderMarkdownToPdfAsync_PostsFormEncodedFieldsAndReturnsPdfBytes()
    {
        var stub = new HttpMessageHandlerStub();
        Uri? capturedUri = null;
        HttpMethod? capturedMethod = null;
        string? capturedMediaType = null;
        string? capturedBody = null;

        stub.QueueResponse(req =>
        {
            capturedUri = req.RequestUri;
            capturedMethod = req.Method;
            capturedMediaType = req.Content?.Headers.ContentType?.MediaType;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidPdfBytes)
            };
        });

        var sut = CreateSut(stub, new ApplicationPacketOptions
        {
            MdToPdfEndpoint = "http://localhost:8000",
            MdToPdfEngine = "weasyprint"
        });

        var observedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Fidalgo.ApplicationPacket",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => observedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var bytes = await sut.RenderMarkdownToPdfAsync(
            "# Jane Doe\n- Engineer",
            new PdfRenderOptions(PdfDocumentKind.Resume, "A4"));

        Assert.Equal(ValidPdfBytes, bytes);
        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("http://localhost:8000/", capturedUri?.ToString());
        Assert.Equal("application/x-www-form-urlencoded", capturedMediaType);

        var parsed = HttpUtility.ParseQueryString(capturedBody!);
        Assert.Equal("# Jane Doe\n- Engineer", parsed["markdown"]);
        Assert.Equal("weasyprint", parsed["engine"]);
        Assert.Contains("size: A4;", parsed["css"]);
        Assert.Contains(observedActivities, a => a.OperationName == "pdf.render");
    }

    [Fact]
    public void PdfCssProvider_ProducesDistinctCssForResumeAndCoverLetter_AndSanitizesPageSize()
    {
        var resumeCss = PdfCssProvider.GetCss(new PdfRenderOptions(PdfDocumentKind.Resume, "Letter"));
        var coverCss = PdfCssProvider.GetCss(new PdfRenderOptions(PdfDocumentKind.CoverLetter, "Letter"));
        var sanitizedCss = PdfCssProvider.GetCss(new PdfRenderOptions(PdfDocumentKind.Resume, "A4; } body { display:none"));

        Assert.NotEqual(resumeCss, coverCss);
        Assert.Contains("Liberation Sans", resumeCss);
        Assert.Contains("Liberation Serif", coverCss);
        Assert.Contains("size: Letter;", sanitizedCss);
        Assert.DoesNotContain("display:none", sanitizedCss);
    }

    [Fact]
    public async Task RenderMarkdownToPdfAsync_RetriesTransient503_ThenSucceeds()
    {
        var stub = new HttpMessageHandlerStub();
        stub.QueueStringResponse("Busy", HttpStatusCode.ServiceUnavailable);
        stub.QueueStringResponse("Rate limited", HttpStatusCode.TooManyRequests);
        stub.QueueResponse(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(ValidPdfBytes)
        });

        var sut = CreateSut(stub);
        var bytes = await sut.RenderMarkdownToPdfAsync(
            "# Cover Letter",
            new PdfRenderOptions(PdfDocumentKind.CoverLetter));

        Assert.Equal(ValidPdfBytes, bytes);
        Assert.Equal(3, stub.CallCount);
    }

    [Fact]
    public async Task RenderMarkdownToPdfAsync_DoesNotRetry400_AndThrowsAgentExecutionException()
    {
        var stub = new HttpMessageHandlerStub();
        stub.QueueStringResponse("Bad Request", HttpStatusCode.BadRequest);

        var sut = CreateSut(stub);

        await Assert.ThrowsAsync<AgentExecutionException>(() =>
            sut.RenderMarkdownToPdfAsync("# Resume", new PdfRenderOptions(PdfDocumentKind.Resume)));

        Assert.Equal(1, stub.CallCount);
    }

    [Fact]
    public async Task RenderMarkdownToPdfAsync_ThrowsAgentExecutionException_AfterExhausting500Retries()
    {
        var stub = new HttpMessageHandlerStub();
        for (var i = 0; i < 4; i++)
        {
            stub.QueueStringResponse("Server error", HttpStatusCode.InternalServerError);
        }

        var sut = CreateSut(stub);

        await Assert.ThrowsAsync<AgentExecutionException>(() =>
            sut.RenderMarkdownToPdfAsync("# Resume", new PdfRenderOptions(PdfDocumentKind.Resume)));

        Assert.Equal(4, stub.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>not a pdf</html>")]
    public async Task RenderMarkdownToPdfAsync_ThrowsAgentExecutionException_OnEmptyOrInvalidPdfBytes(string invalidPayload)
    {
        var stub = new HttpMessageHandlerStub();
        stub.QueueStringResponse(invalidPayload, HttpStatusCode.OK);

        var sut = CreateSut(stub);

        await Assert.ThrowsAsync<AgentExecutionException>(() =>
            sut.RenderMarkdownToPdfAsync("# Resume", new PdfRenderOptions(PdfDocumentKind.Resume)));
    }

    [Fact]
    public async Task RenderMarkdownToPdfAsync_ThrowsAgentExecutionException_OnTimeout()
    {
        using var httpClient = new HttpClient(new HangingHttpMessageHandler());
        var sut = new MdToPdfRenderer(
            httpClient,
            Options.Create(new ApplicationPacketOptions()),
            NullLogger<MdToPdfRenderer>.Instance,
            TimeSpan.FromMilliseconds(5),
            timeoutOverride: TimeSpan.FromMilliseconds(25));

        var ex = await Assert.ThrowsAsync<AgentExecutionException>(() =>
            sut.RenderMarkdownToPdfAsync("# Resume", new PdfRenderOptions(PdfDocumentKind.Resume)));

        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HangingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static MdToPdfRenderer CreateSut(HttpMessageHandlerStub stub, ApplicationPacketOptions? options = null)
    {
        var httpClient = new HttpClient(stub);
        return new MdToPdfRenderer(
            httpClient,
            Options.Create(options ?? new ApplicationPacketOptions()),
            NullLogger<MdToPdfRenderer>.Instance,
            TimeSpan.FromMilliseconds(5));
    }
}
