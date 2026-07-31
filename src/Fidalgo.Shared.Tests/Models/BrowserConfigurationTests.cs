using Fidalgo.Shared.Models;
using Xunit;

namespace Fidalgo.Shared.Tests.Models;

public class BrowserConfigurationTests
{
    [Fact]
    public void Defaults_HeadlessIsTrue()
    {
        var config = new BrowserConfiguration();

        Assert.True(config.Headless);
    }

    [Fact]
    public void Defaults_ViewportIsFullHd()
    {
        var config = new BrowserConfiguration();

        Assert.Equal(1920, config.ViewportWidth);
        Assert.Equal(1080, config.ViewportHeight);
    }

    [Fact]
    public void Defaults_TimeoutIs30Seconds()
    {
        var config = new BrowserConfiguration();

        Assert.Equal(30000, config.TimeoutMilliseconds);
    }

    [Fact]
    public void Defaults_SignInPauseIs120Seconds()
    {
        var config = new BrowserConfiguration();

        Assert.Equal(120, config.SignInPauseSeconds);
    }

    [Fact]
    public void Defaults_InterceptWaitIsZero_OffByDefault()
    {
        var config = new BrowserConfiguration();

        Assert.Equal(0, config.MaxWaitForInterceptSeconds);
    }
}
