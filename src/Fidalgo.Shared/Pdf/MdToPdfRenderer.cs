using System.Diagnostics;
using System.Net;
using Fidalgo.Shared.Configuration;
using Fidalgo.Shared.ErrorHandling;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Fidalgo.Shared.Pdf;

/// <summary>
/// <see cref="IPdfRenderer"/> implementation that posts Markdown and CSS to a <c>spawnia/md-to-pdf</c>
/// HTTP endpoint with Polly exponential-backoff retries, linked timeout enforcement,
/// and OpenTelemetry <see cref="Activity"/> tracing.
/// </summary>
public class MdToPdfRenderer : IPdfRenderer
{
    private static readonly ActivitySource ActivitySource = new("Fidalgo.ApplicationPacket");
    private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _httpClient;
    private readonly IOptions<ApplicationPacketOptions> _options;
    private readonly ILogger<MdToPdfRenderer> _logger;
    private readonly TimeSpan _retryBaseDelay;
    private readonly TimeSpan? _timeoutOverride;

    /// <summary>Initializes a new instance of <see cref="MdToPdfRenderer"/>.</summary>
    public MdToPdfRenderer(
        HttpClient httpClient,
        IOptions<ApplicationPacketOptions> options,
        ILogger<MdToPdfRenderer> logger)
        : this(httpClient, options, logger, DefaultRetryBaseDelay, null)
    {
    }

    internal MdToPdfRenderer(
        HttpClient httpClient,
        IOptions<ApplicationPacketOptions> options,
        ILogger<MdToPdfRenderer> logger,
        TimeSpan retryBaseDelay,
        TimeSpan? timeoutOverride = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _retryBaseDelay = retryBaseDelay;
        _timeoutOverride = timeoutOverride;
    }

    /// <inheritdoc/>
    public async Task<byte[]> RenderMarkdownToPdfAsync(
        string markdown,
        PdfRenderOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(markdown);
        ArgumentNullException.ThrowIfNull(options);

        var opts = _options.Value;
        if (!Uri.TryCreate(opts.MdToPdfEndpoint, UriKind.Absolute, out var endpointUri))
        {
            throw new AgentExecutionException($"Invalid md-to-pdf endpoint URL: '{opts.MdToPdfEndpoint}'.");
        }

        var engine = string.IsNullOrWhiteSpace(opts.MdToPdfEngine) ? "weasyprint" : opts.MdToPdfEngine;
        var css = PdfCssProvider.GetCss(options);
        var timeout = _timeoutOverride ?? TimeSpan.FromSeconds(Math.Max(1, opts.PdfTimeoutSeconds));

        using var activity = ActivitySource.StartActivity("pdf.render", ActivityKind.Client);
        activity?.SetTag("pdf.kind", options.Kind.ToString());
        activity?.SetTag("pdf.engine", engine);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var pipeline = BuildRetryPipeline(cancellationToken, timeoutCts.Token);

        byte[] pdfBytes;
        try
        {
            pdfBytes = await pipeline.ExecuteAsync(async ct =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
                {
                    Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("markdown", markdown),
                        new KeyValuePair<string, string>("css", css),
                        new KeyValuePair<string, string>("engine", engine)
                    ])
                };

                using var response = await _httpClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        throw new HttpRequestException(
                            $"md-to-pdf service returned transient HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                            inner: null,
                            statusCode: response.StatusCode);
                    }

                    throw new AgentExecutionException(
                        $"md-to-pdf service returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
                }

                return await response.Content.ReadAsByteArrayAsync(ct);
            }, timeoutCts.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(ex, "PDF rendering timed out after {TimeoutSeconds}s.", timeout.TotalSeconds);
            throw new AgentExecutionException($"PDF rendering timed out after {timeout.TotalSeconds:0.#}s.", ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AgentExecutionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF rendering failed for document kind {DocumentKind}.", options.Kind);
            throw new AgentExecutionException($"PDF rendering failed: {ex.Message}", ex);
        }

        if (!IsValidPdf(pdfBytes))
        {
            throw new AgentExecutionException("md-to-pdf service returned empty or invalid PDF content.");
        }

        activity?.SetTag("pdf.bytes", pdfBytes.Length);
        return pdfBytes;
    }

    private ResiliencePipeline BuildRetryPipeline(CancellationToken callerToken, CancellationToken linkedTimeoutToken)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = _retryBaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>(ex => ex.StatusCode is null || IsTransientStatusCode(ex.StatusCode.Value))
                    .Handle<TaskCanceledException>(_ => !callerToken.IsCancellationRequested && !linkedTimeoutToken.IsCancellationRequested),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient PDF rendering error on attempt {AttemptNumber}; retrying in {RetryDelayMs}ms.",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .Build();
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static bool IsValidPdf(byte[]? bytes) =>
        bytes is { Length: >= 4 } &&
        bytes[0] == (byte)'%' &&
        bytes[1] == (byte)'P' &&
        bytes[2] == (byte)'D' &&
        bytes[3] == (byte)'F';
}
