namespace SplitBook.Api.Features.Auth.Login;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
