using Fidalgo.Shared.Retry;
using Xunit;

namespace Fidalgo.Shared.Tests.Retry;

public class RetryPolicyTests
{
    private readonly RetryPolicy _sut = new();

    [Fact]
    public async Task ExecuteAsync_ReturnsResult_WhenOperationSucceedsFirstTime()
    {
        var calls = 0;
        var result = await _sut.ExecuteAsync(() =>
        {
            calls++;
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesTransientFailure_ThenSucceeds()
    {
        var calls = 0;
        var result = await _sut.ExecuteAsync<int>(() =>
        {
            calls++;
            if (calls < 3) throw new HttpRequestException("transient");
            return Task.FromResult(7);
        });

        Assert.Equal(7, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsAfterMaxRetries_OnPersistentTransientFailure()
    {
        var calls = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.ExecuteAsync<int>(() =>
        {
            calls++;
            throw new HttpRequestException("persistent");
        }));

        // 1 initial + 3 retries = 4 attempts
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryNonTransientException()
    {
        var calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ExecuteAsync<int>(() =>
        {
            calls++;
            throw new InvalidOperationException("nope");
        }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void IsTransient_ReturnsTrue_ForHttpRequestException()
    {
        Assert.True(_sut.IsTransient(new HttpRequestException()));
    }

    [Fact]
    public void IsTransient_ReturnsTrue_ForTimeoutException()
    {
        Assert.True(_sut.IsTransient(new TimeoutException()));
    }

    [Fact]
    public void IsTransient_ReturnsTrue_ForIOException()
    {
        Assert.True(_sut.IsTransient(new IOException()));
    }

    [Fact]
    public void IsTransient_ReturnsFalse_ForOtherExceptions()
    {
        Assert.False(_sut.IsTransient(new InvalidOperationException()));
        Assert.False(_sut.IsTransient(new ArgumentException()));
        Assert.False(_sut.IsTransient(new NotSupportedException()));
    }

    [Fact]
    public async Task ExecuteAsync_Void_RetriesAndSucceeds()
    {
        var calls = 0;
        await _sut.ExecuteAsync(() =>
        {
            calls++;
            if (calls < 2) throw new HttpRequestException("once");
            return Task.CompletedTask;
        });

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_Void_ThrowsAfterMaxRetries()
    {
        var calls = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.ExecuteAsync(() =>
        {
            calls++;
            throw new HttpRequestException("always");
        }));

        Assert.Equal(4, calls);
    }
}
