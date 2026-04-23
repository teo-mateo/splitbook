using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Auth.Login;

public static class LoginHandler
{
    public static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> HandleAsync(
        LoginRequest request,
        AppDbContext context,
        PasswordHasher passwordHasher,
        JwtTokenService jwtTokenService)
    {
        var validator = new LoginValidator();
        var result = await validator.ValidateAsync(request);
        if (!result.IsValid)
        {
            return TypedResults.Problem(
                title: "Validation Failed",
                detail: string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
                statusCode: 400
            );
        }

        var email = request.Email.ToLowerInvariant();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return TypedResults.Problem(
                title: "Unauthorized",
                detail: "Invalid credentials",
                statusCode: 401
            );
        }

        var token = jwtTokenService.CreateToken(user.Id, user.Email, user.DisplayName);
        var expiresAt = jwtTokenService.GetExpiry();

        return TypedResults.Ok(new LoginResponse(token, expiresAt));
    }
}
