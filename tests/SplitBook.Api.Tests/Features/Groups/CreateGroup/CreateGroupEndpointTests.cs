using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Groups.CreateGroup;

public class CreateGroupEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public CreateGroupEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Helper: register a user and log in, returning the bearer token.
    /// </summary>
    private async Task<string> GetAuthTokenAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        // Register
        var registerRequest = new RegisterRequest(email, "TestUser", password);
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        // Login
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.ReadJsonAsync<LoginResponse>();

        return loginResult!.AccessToken;
    }

    /// <summary>
    /// Helper: create an HttpClient configured with a bearer token.
    /// </summary>
    private async Task<HttpClient> CreateAuthClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task CreateGroup_Returns201_WithGroupDto()
    {
        // Arrange
        var client = await CreateAuthClientAsync("creategroup@example.com", "GroupPass123!");
        var request = new CreateGroupRequest("Lisbon Trip", "EUR");

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);
        var result = await response.ReadJsonAsync<GroupDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Lisbon Trip");
        result.Currency.Should().Be("EUR");
        result.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task CreateGroup_CreatesGroupAndMembershipRows()
    {
        // Arrange
        var client = await CreateAuthClientAsync("membership@example.com", "MemberPass123!");
        var request = new CreateGroupRequest("Roommates", "GBP");

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);
        var result = await response.ReadJsonAsync<GroupDto>();
        var groupId = result!.Id;

        // Assert — response succeeded
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — Group row exists with correct data
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = await context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        group.Should().NotBeNull();
        group!.Name.Should().Be("Roommates");
        group.Currency.Should().Be("GBP");
        group.CreatedByUserId.Should().NotBe(Guid.Empty);
        group.CreatedAt.Should().NotBe(default);
        group.ArchivedAt.Should().BeNull();

        // Assert — Membership row exists linking creator to group
        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == group.CreatedByUserId);
        membership.Should().NotBeNull();
        membership!.JoinedAt.Should().NotBe(default);
        membership.RemovedAt.Should().BeNull();

        // Assert — Group.Id in response matches Membership.GroupId
        membership.GroupId.Should().Be(groupId);
    }

    [Fact]
    public async Task CreateGroup_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { name = "NoAuth Group", currency = "USD" };

        // Act — no Authorization header
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateGroup_MissingName_Returns400()
    {
        // Arrange
        var client = await CreateAuthClientAsync("missingname@example.com", "NamePass123!");
        var request = new { currency = "USD" };

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGroup_EmptyName_Returns400()
    {
        // Arrange
        var client = await CreateAuthClientAsync("emptyname@example.com", "EmptyPass123!");
        var request = new { name = "", currency = "USD" };

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGroup_WhitespaceName_Returns400()
    {
        // Arrange
        var client = await CreateAuthClientAsync("whitename@example.com", "WhitePass123!");
        var request = new { name = "   ", currency = "USD" };

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGroup_MissingCurrency_Returns400()
    {
        // Arrange
        var client = await CreateAuthClientAsync("missingcurr@example.com", "CurrPass123!");
        var request = new { name = "No Currency Group" };

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGroup_InvalidCurrency_Returns400()
    {
        // Arrange
        var client = await CreateAuthClientAsync("invalidcurr@example.com", "InvalidPass123!");
        var request = new { name = "Bad Currency Group", currency = "USD1" };

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateGroup_NumericCurrency_Returns400()
    {
        // Arrange
        var client = await CreateAuthClientAsync("numcurr@example.com", "NumPass123!");
        var request = new { name = "Numeric Currency Group", currency = "123" };

        // Act
        var response = await client.PostAsJsonAsync("/groups", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
