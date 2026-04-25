using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Auth.Login;

public class LoginEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public LoginEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Email, string Password)> RegisterUserAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest(email, "TestUser", password);
        var response = await client.PostAsJsonAsync("/auth/register", request);
        response.EnsureSuccessStatusCode();
        return (email, password);
    }

    [Fact]
    public async Task Login_Returns200_WithJwt()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (email, password) = await RegisterUserAsync("jwttest@example.com", "JwtPass123!");

        // Act
        var loginRequest = new LoginRequest(email, password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var result = await response.ReadJsonAsync<LoginResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterUserAsync("wrongpw@example.com", "CorrectPass123!");

        // Act
        var loginRequest = new LoginRequest("wrongpw@example.com", "WrongPassword!");
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_NonExistentUser_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var loginRequest = new LoginRequest("doesnotexist@example.com", "SomePassword123!");
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_JwtHasRequiredClaims()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (email, password) = await RegisterUserAsync("claims@example.com", "ClaimsPass123!");

        // Act
        var loginRequest = new LoginRequest(email, password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var result = await response.ReadJsonAsync<LoginResponse>();

        // Assert
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();

        var parts = result.AccessToken.Split('.');
        parts.Should().HaveCount(3, "JWT must have three parts (header.payload.signature)");

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var payload = payloadDoc.RootElement;

        payload.TryGetProperty("sub", out var sub).Should().BeTrue("JWT must contain 'sub' claim");
        sub.GetString().Should().NotBeNullOrEmpty();

        payload.TryGetProperty("email", out var emailClaim).Should().BeTrue("JWT must contain 'email' claim");
        emailClaim.GetString().Should().Be("claims@example.com");

        payload.TryGetProperty("name", out var nameClaim).Should().BeTrue("JWT must contain 'name' claim");
        nameClaim.GetString().Should().NotBeNullOrEmpty();

        payload.TryGetProperty("exp", out var exp).Should().BeTrue("JWT must contain 'exp' claim");
        exp.GetInt64().Should().BePositive();

        payload.TryGetProperty("iat", out var iat).Should().BeTrue("JWT must contain 'iat' claim");
        iat.GetInt64().Should().BePositive();

        payload.TryGetProperty("iss", out var iss).Should().BeTrue("JWT must contain 'iss' claim");
        iss.GetString().Should().NotBeNullOrEmpty();

        payload.TryGetProperty("aud", out var aud).Should().BeTrue("JWT must contain 'aud' claim");
        aud.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_JwtExpiresIn24Hours()
    {
        // Arrange
        var client = _factory.CreateClient();
        var (email, password) = await RegisterUserAsync("expiry@example.com", "ExpiryPass123!");

        // Act
        var loginRequest = new LoginRequest(email, password);
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var result = await response.ReadJsonAsync<LoginResponse>();

        // Assert
        result.Should().NotBeNull();
        var parts = result!.AccessToken.Split('.');
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var payload = payloadDoc.RootElement;

        var exp = payload.GetProperty("exp").GetInt64();
        var iat = payload.GetProperty("iat").GetInt64();

        var lifetimeSeconds = exp - iat;
        var expectedSeconds = 24L * 60 * 60;

        lifetimeSeconds.Should().BeInRange(expectedSeconds - 60, expectedSeconds + 60);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act — hit a route that should require authentication
        var response = await client.PostAsJsonAsync("/groups", new { name = "test", currency = "USD" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
