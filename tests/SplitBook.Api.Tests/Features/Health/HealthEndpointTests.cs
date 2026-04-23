using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SplitBook.Api.Tests.Features.Health;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_Returns200Ok()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealth_ReturnsStatusOkAndVersion()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Assert
        root.TryGetProperty("status", out var status).Should().BeTrue();
        status.GetString().Should().Be("ok");

        root.TryGetProperty("version", out var version).Should().BeTrue();
        version.GetString().Should().NotBeNullOrEmpty();
    }
}
