using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Groups.ListMyGroups;

public class ListMyGroupsEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ListMyGroupsEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetAuthTokenAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        var registerRequest = new { email, displayName = "TestUser", password };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new { email, password };
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginDoc = JsonDocument.Parse(loginBody);
        var accessToken = loginDoc.RootElement.GetProperty("accessToken").GetString();

        return accessToken!;
    }

    private async Task<HttpClient> CreateAuthClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ListMyGroups_WithoutToken_Returns401()
    {
        // Arrange — no auth header
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/groups");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListMyGroups_UserWithNoGroups_ReturnsEmptyArray()
    {
        // Arrange — register a user but create no groups
        var client = await CreateAuthClientAsync("nogroups@example.com", "NoGroups123!");

        // Act
        var response = await client.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ListMyGroups_ReturnsGroupsUserCreated()
    {
        // Arrange — user creates a group via POST /groups
        var client = await CreateAuthClientAsync("creator@example.com", "Creator123!");
        var createRequest = new { name = "My Trip", currency = "EUR" };
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        createResponse.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);

        var firstItem = doc.RootElement[0];
        firstItem.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().Be("My Trip");
    }

    [Fact]
    public async Task ListMyGroups_ReturnsMultipleGroups()
    {
        // Arrange — user creates two groups
        var client = await CreateAuthClientAsync("multi@example.com", "Multi123!");
        var group1 = new { name = "Group One", currency = "USD" };
        var group2 = new { name = "Group Two", currency = "GBP" };

        (await client.PostAsJsonAsync("/groups", group1)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/groups", group2)).EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(2);

        var names = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("name").GetString())
            .ToList();
        names.Should().Contain("Group One");
        names.Should().Contain("Group Two");
    }

    [Fact]
    public async Task ListMyGroups_ExcludesGroupsUserIsNotMemberOf()
    {
        // Arrange — User A creates a group; User B should not see it
        var clientA = await CreateAuthClientAsync("usera_nogroup@example.com", "UserA123!");
        var createRequest = new { name = "A's Private Group", currency = "EUR" };
        (await clientA.PostAsJsonAsync("/groups", createRequest)).EnsureSuccessStatusCode();

        var clientB = await CreateAuthClientAsync("userb_nogroup@example.com", "UserB123!");

        // Act
        var response = await clientB.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ListMyGroups_ExcludesRemovedMembers()
    {
        // Arrange — user creates a group, then we manually set RemovedAt on their membership
        var client = await CreateAuthClientAsync("removed@example.com", "Removed123!");
        var createRequest = new { name = "Removed Group", currency = "USD" };
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var createDoc = JsonDocument.Parse(createBody);
        var groupId = Guid.Parse(createDoc.RootElement.GetProperty("id").GetString()!);

        // Get the user's ID from the DB
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "removed@example.com");
        user.Should().NotBeNull();

        // Set RemovedAt on the membership
        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == user!.Id);
        membership.Should().NotBeNull();
        membership!.RemovedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ListMyGroups_ResponseContainsRequiredFields()
    {
        // Arrange — user creates a group
        var client = await CreateAuthClientAsync("fields@example.com", "Fields123!");
        var createRequest = new { name = "Field Test Group", currency = "JPY" };
        (await client.PostAsJsonAsync("/groups", createRequest)).EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        var firstItem = doc.RootElement[0];

        // Must contain id
        firstItem.TryGetProperty("id", out var id).Should().BeTrue();
        Guid.Parse(id.GetString()!).Should().NotBe(Guid.Empty);

        // Must contain name
        firstItem.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().Be("Field Test Group");

        // Must contain currency
        firstItem.TryGetProperty("currency", out var currency).Should().BeTrue();
        currency.GetString().Should().Be("JPY");
    }

    [Fact]
    public async Task ListMyGroups_ExcludesArchivedGroups()
    {
        // Arrange — user creates a group, then we manually set ArchivedAt
        var client = await CreateAuthClientAsync("archived@example.com", "Archived123!");
        var createRequest = new { name = "Archived Group", currency = "CHF" };
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        var createBody = await createResponse.Content.ReadAsStringAsync();
        var createDoc = JsonDocument.Parse(createBody);
        var groupId = Guid.Parse(createDoc.RootElement.GetProperty("id").GetString()!);

        // Set ArchivedAt on the group
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = await context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        group.Should().NotBeNull();
        group!.ArchivedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/groups");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetArrayLength().Should().Be(0);
    }
}
