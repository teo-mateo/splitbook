using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
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

    /// <summary>
    /// Helper: register a user and return the email/password pair.
    /// </summary>
    private async Task<(string Email, string Password)> RegisterUserAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var request = new { email, displayName = "TestUser", password };
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
        var loginRequest = new { email, password };
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        root.TryGetProperty("accessToken", out var token).Should().BeTrue();
        token.GetString().Should().NotBeNullOrEmpty();

        root.TryGetProperty("expiresAt", out var expiresAt).Should().BeTrue();
        expiresAt.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();
        await RegisterUserAsync("wrongpw@example.com", "CorrectPass123!");

        // Act
        var loginRequest = new { email = "wrongpw@example.com", password = "WrongPassword!" };
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
        var loginRequest = new { email = "doesnotexist@example.com", password = "SomePassword123!" };
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
        var loginRequest = new { email, password };
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString();

        // Assert
        accessToken.Should().NotBeNullOrEmpty();

        // Decode the JWT payload (middle segment) to inspect claims
        var parts = accessToken!.Split('.');
        parts.Should().HaveCount(3, "JWT must have three parts (header.payload.signature)");

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var payload = payloadDoc.RootElement;

        // Check required claims per technical-spec §5
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
        var loginRequest = new { email, password };
        var response = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var accessToken = doc.RootElement.GetProperty("accessToken").GetString();

        // Assert
        var parts = accessToken!.Split('.');
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        var payloadDoc = JsonDocument.Parse(payloadJson);
        var payload = payloadDoc.RootElement;

        var exp = payload.GetProperty("exp").GetInt64();
        var iat = payload.GetProperty("iat").GetInt64();

        var lifetimeSeconds = exp - iat;
        var expectedSeconds = 24L * 60 * 60; // 24 hours in seconds

        // Allow a small tolerance (up to 60 seconds) for clock skew during test execution
        lifetimeSeconds.Should().BeInRange(expectedSeconds - 60, expectedSeconds + 60);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act — hit a route that should require authentication
        // /groups is the group list endpoint which requires auth per technical-spec §4
        var response = await client.GetAsync("/groups");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Decodes a Base64URL-encoded string (JWT segment) back to bytes.
    /// Handles missing padding that JWT segments often omit.
    /// </summary>
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
