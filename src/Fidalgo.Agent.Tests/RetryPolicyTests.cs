using Fidalgo.Agent.Retry;

namespace Fidalgo.Agent.Tests;

public class RetryPolicyTests
{
    private readonly RetryPolicy _policy;

    public RetryPolicyTests()
    {
        _policy = new RetryPolicy();
    }

    [Fact]
    public void IsTransient_ShouldReturnTrue_ForHttpRequestException()
    {
        var exception = new HttpRequestException("test");
        Assert.True(_policy.IsTransient(exception));
    }

    [Fact]
    public void IsTransient_ShouldReturnTrue_ForTimeoutException()
    {
        var exception = new TimeoutException("test");
        Assert.True(_policy.IsTransient(exception));
    }

    [Fact]
    public void IsTransient_ShouldReturnTrue_ForIOException()
    {
        var exception = new IOException("test");
        Assert.True(_policy.IsTransient(exception));
    }

    [Fact]
    public void IsTransient_ShouldReturnFalse_ForArgumentException()
    {
        var exception = new ArgumentException("test");
        Assert.False(_policy.IsTransient(exception));
    }

    [Fact]
    public void IsTransient_ShouldReturnFalse_ForInvalidOperationException()
    {
        var exception = new InvalidOperationException("test");
        Assert.False(_policy.IsTransient(exception));
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ShouldReturnOnFirstSuccess()
    {
        var callCount = 0;
        var result = await _policy.ExecuteAsync(async () =>
        {
            callCount++;
            return "success";
        });

        Assert.Equal("success", result);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ShouldRetryOnTransientException()
    {
        var callCount = 0;
        var result = await _policy.ExecuteAsync(async () =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new HttpRequestException("transient error");
            }
            return "recovered";
        });

        Assert.Equal("recovered", result);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ShouldThrowAfterMaxRetries()
    {
        var callCount = 0;
        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await _policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new HttpRequestException("transient error");
            });
        });

        Assert.Equal("transient error", exception.Message);
        Assert.Equal(4, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ShouldNotRetryOnNonTransientException()
    {
        var callCount = 0;
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new ArgumentException("not transient");
            });
        });

        Assert.Equal("not transient", exception.Message);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutResult_ShouldSucceedOnFirstTry()
    {
        var callCount = 0;
        await _policy.ExecuteAsync(async () =>
        {
            callCount++;
        });

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutResult_ShouldRetryOnTransientException()
    {
        var callCount = 0;
        await _policy.ExecuteAsync(async () =>
        {
            callCount++;
            if (callCount < 2)
            {
                throw new TimeoutException("timeout");
            }
        });

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutResult_ShouldThrowAfterMaxRetries()
    {
        var callCount = 0;
        var exception = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await _policy.ExecuteAsync(async () =>
            {
                callCount++;
                throw new TimeoutException("timeout");
            });
        });

        Assert.Equal("timeout", exception.Message);
        Assert.Equal(4, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ShouldRespectCancellation()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await _policy.ExecuteAsync(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                return "should not reach here";
            }, cts.Token);
        });

        Assert.NotNull(exception);
    }
}
