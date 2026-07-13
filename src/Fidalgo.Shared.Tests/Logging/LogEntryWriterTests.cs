using Fidalgo.Shared.Logging;
using Fidalgo.Shared.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace Fidalgo.Shared.Tests.Logging;

public class LogEntryWriterTests
{
    private readonly ILogger<LogEntryWriter> _logger;
    private readonly ITraceContextProvider _traceContextProvider;
    private readonly LogEntryWriter _writer;

    public LogEntryWriterTests()
    {
        _logger = Substitute.For<ILogger<LogEntryWriter>>();
        _traceContextProvider = Substitute.For<ITraceContextProvider>();
        _writer = new LogEntryWriter(_logger, _traceContextProvider);
    }

    [Fact]
    public async Task WriteAsync_WithNoTraceContext_GeneratesNewContextAndLogs()
    {
        _traceContextProvider.GetCurrentContext().Returns((TraceContext?)null);
        var logEntry = new LogEntry(
            DateTime.UtcNow,
            Fidalgo.Shared.Models.LogLevel.Information,
            "Test message",
            "trace1",
            "span1",
            "corr1",
            null,
            null);

        await _writer.WriteAsync(logEntry);

        _logger.ReceivedWithAnyArgs().Log(
            Microsoft.Extensions.Logging.LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task WriteErrorAsync_WithNestedExceptions_MapsRecursively()
    {
        _traceContextProvider.GetCurrentContext().Returns((TraceContext?)null);
        
        var innermost = new InvalidOperationException("Innermost error");
        var inner = new ArgumentException("Inner error", innermost);
        var outer = new Exception("Outer error", inner);

        await _writer.WriteErrorAsync(outer, "Error occurred", "trace", "span", "corr");

        _logger.ReceivedWithAnyArgs().Log(
            Microsoft.Extensions.Logging.LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}