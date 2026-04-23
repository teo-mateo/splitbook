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

namespace SplitBook.Api.Tests.Features.Auth.Register;

public class RegisterEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public RegisterEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_Returns201_AndUserDto()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { email = "alice@example.com", displayName = "Alice", password = "Secret123!" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        root.TryGetProperty("id", out var id).Should().BeTrue();
        Guid.Parse(id.GetString()!).Should().NotBe(Guid.Empty);

        root.TryGetProperty("email", out var email).Should().BeTrue();
        email.GetString().Should().Be("alice@example.com");

        root.TryGetProperty("displayName", out var displayName).Should().BeTrue();
        displayName.GetString().Should().Be("Alice");
    }

    [Fact]
    public async Task Register_PersistsUserForLogin()
    {
        // Arrange
        var client = _factory.CreateClient();
        var registerRequest = new { email = "bob@example.com", displayName = "Bob", password = "BobPass123!" };

        // Act — register
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — login with the same credentials
        var loginRequest = new { email = "bob@example.com", password = "BobPass123!" };
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginDoc = JsonDocument.Parse(loginBody);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginDoc.RootElement.TryGetProperty("accessToken", out var token).Should().BeTrue();
        token.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns4xx()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { email = "dave@example.com", displayName = "Dave", password = "DavePass123!" };

        // Act — first registration
        var firstResponse = await client.PostAsJsonAsync("/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — duplicate registration
        var secondResponse = await client.PostAsJsonAsync("/auth/register", request);

        // Assert — should return 409 Conflict or 400 Bad Request
        secondResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_MissingEmail_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { displayName = "NoEmail", password = "Secret123!" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_MissingDisplayName_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { email = "nodisplay@example.com", password = "Secret123!" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_MissingPassword_Returns400()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { email = "nopass@example.com", displayName = "NoPass" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_LowercasesEmail()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new { email = "Test@Example.COM", displayName = "CaseTest", password = "Secret123!" };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        root.TryGetProperty("email", out var email).Should().BeTrue();
        email.GetString().Should().Be("test@example.com");
    }

    [Fact]
    public async Task Register_HashPassword()
    {
        // Arrange
        var client = _factory.CreateClient();
        var plainPassword = "Secret123!";
        var request = new { email = "hashcheck@example.com", displayName = "HashCheck", password = plainPassword };

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — query the database directly to verify the password is hashed
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "hashcheck@example.com");

        user.Should().NotBeNull();
        user!.PasswordHash.Should().NotBeNullOrEmpty();
        user.PasswordHash.Should().NotBe(plainPassword);
        // BCrypt hashes start with "$2a$", "$2b$", or "$2y$"
        var isBcryptHash = user.PasswordHash.StartsWith("$2a$")
            || user.PasswordHash.StartsWith("$2b$")
            || user.PasswordHash.StartsWith("$2y$");
        isBcryptHash.Should().BeTrue($"Password hash should be a BCrypt hash, got: {user.PasswordHash}");
    }
}
