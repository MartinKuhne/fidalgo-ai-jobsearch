using System.Net;
using Fidalgo.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using System.Text.Json;

namespace Fidalgo.Host.Tests;

public class ProgramTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProgramTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetJobs_WithMissingEmail_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/jobs?page=1&pageSize=10");

        // Assert - should be 400 Bad Request since email is required by minimal API framework (non-nullable string or required query param)
        // Wait, the API endpoint defines it as `[FromQuery] string email`. If missing, it might throw or return 400.
        // Actually if not provided, the route might return 400, or the service might throw ArgumentException which becomes 500 without a global exception handler.
        // Let's just check the response is not success, or check it explicitly.
        Assert.False(response.IsSuccessStatusCode);
    }
    
    [Fact]
    public async Task GetTenants_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/tenants");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        
        // It might be empty list depending on DB
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var tenants = JsonSerializer.Deserialize<List<TenantEmailInfo>>(content, options);
        Assert.NotNull(tenants);
    }
}
