using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
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
        var request = new RegisterRequest("alice@example.com", "Alice", "Secret123!");

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);
        var result = await response.ReadJsonAsync<RegisterResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(Guid.Empty);
        result.Email.Should().Be("alice@example.com");
        result.DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task Register_PersistsUserForLogin()
    {
        // Arrange
        var client = _factory.CreateClient();
        var registerRequest = new RegisterRequest("bob@example.com", "Bob", "BobPass123!");

        // Act — register
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — login with the same credentials
        var loginRequest = new LoginRequest("bob@example.com", "BobPass123!");
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.ReadJsonAsync<LoginResponse>();

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        loginResult.Should().NotBeNull();
        loginResult!.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns4xx()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new RegisterRequest("dave@example.com", "Dave", "DavePass123!");

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
        var request = new RegisterRequest("Test@Example.COM", "CaseTest", "Secret123!");

        // Act
        var response = await client.PostAsJsonAsync("/auth/register", request);
        var result = await response.ReadJsonAsync<RegisterResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Register_HashPassword()
    {
        // Arrange
        var client = _factory.CreateClient();
        var plainPassword = "Secret123!";
        var request = new RegisterRequest("hashcheck@example.com", "HashCheck", plainPassword);

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
        var isBcryptHash = user.PasswordHash.StartsWith("$2a$")
            || user.PasswordHash.StartsWith("$2b$")
            || user.PasswordHash.StartsWith("$2y$");
        isBcryptHash.Should().BeTrue($"Password hash should be a BCrypt hash, got: {user.PasswordHash}");
    }
}
