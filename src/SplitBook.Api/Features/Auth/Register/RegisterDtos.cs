namespace SplitBook.Api.Features.Auth.Register;

public record RegisterRequest(string Email, string DisplayName, string Password);
public record RegisterResponse(Guid Id, string Email, string DisplayName);
