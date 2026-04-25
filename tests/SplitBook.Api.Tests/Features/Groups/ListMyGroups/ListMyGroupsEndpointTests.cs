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

        var registerRequest = new RegisterRequest(email, "TestUser", password);
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.ReadJsonAsync<LoginResponse>();

        return loginResult!.AccessToken;
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
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMyGroups_ReturnsGroupsUserCreated()
    {
        // Arrange — user creates a group via POST /groups
        var client = await CreateAuthClientAsync("creator@example.com", "Creator123!");
        var createRequest = new CreateGroupRequest("My Trip", "EUR");
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        createResponse.EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/groups");
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result![0].Name.Should().Be("My Trip");
    }

    [Fact]
    public async Task ListMyGroups_ReturnsMultipleGroups()
    {
        // Arrange — user creates two groups
        var client = await CreateAuthClientAsync("multi@example.com", "Multi123!");
        var group1 = new CreateGroupRequest("Group One", "USD");
        var group2 = new CreateGroupRequest("Group Two", "GBP");

        (await client.PostAsJsonAsync("/groups", group1)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/groups", group2)).EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/groups");
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        var names = result!.Select(g => g.Name).ToList();
        names.Should().Contain("Group One");
        names.Should().Contain("Group Two");
    }

    [Fact]
    public async Task ListMyGroups_ExcludesGroupsUserIsNotMemberOf()
    {
        // Arrange — User A creates a group; User B should not see it
        var clientA = await CreateAuthClientAsync("usera_nogroup@example.com", "UserA123!");
        var createRequest = new CreateGroupRequest("A's Private Group", "EUR");
        (await clientA.PostAsJsonAsync("/groups", createRequest)).EnsureSuccessStatusCode();

        var clientB = await CreateAuthClientAsync("userb_nogroup@example.com", "UserB123!");

        // Act
        var response = await clientB.GetAsync("/groups");
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMyGroups_ExcludesRemovedMembers()
    {
        // Arrange — user creates a group, then we manually set RemovedAt on their membership
        var client = await CreateAuthClientAsync("removed@example.com", "Removed123!");
        var createRequest = new CreateGroupRequest("Removed Group", "USD");
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        var createResult = await createResponse.ReadJsonAsync<GroupDto>();
        var groupId = createResult!.Id;

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
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    [Fact]
    public async Task ListMyGroups_ResponseContainsRequiredFields()
    {
        // Arrange — user creates a group
        var client = await CreateAuthClientAsync("fields@example.com", "Fields123!");
        var createRequest = new CreateGroupRequest("Field Test Group", "JPY");
        (await client.PostAsJsonAsync("/groups", createRequest)).EnsureSuccessStatusCode();

        // Act
        var response = await client.GetAsync("/groups");
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        var firstItem = result![0];

        // Must contain id
        firstItem.Id.Should().NotBe(Guid.Empty);

        // Must contain name
        firstItem.Name.Should().Be("Field Test Group");

        // Must contain currency
        firstItem.Currency.Should().Be("JPY");
    }

    [Fact]
    public async Task ListMyGroups_ExcludesArchivedGroups()
    {
        // Arrange — user creates a group, then we manually set ArchivedAt
        var client = await CreateAuthClientAsync("archived@example.com", "Archived123!");
        var createRequest = new CreateGroupRequest("Archived Group", "CHF");
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        var createResult = await createResponse.ReadJsonAsync<GroupDto>();
        var groupId = createResult!.Id;

        // Set ArchivedAt on the group
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = await context.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
        group.Should().NotBeNull();
        group!.ArchivedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        // Act
        var response = await client.GetAsync("/groups");
        var result = await response.ReadJsonAsync<List<GroupDto>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }
}
