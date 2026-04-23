using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Auth.Register;

public static class RegisterHandler
{
    public static async Task<Results<Created<RegisterResponse>, ProblemHttpResult>> HandleAsync(
        RegisterRequest request,
        AppDbContext context,
        PasswordHasher passwordHasher)
    {
        var validator = new RegisterValidator();
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

        var existing = await context.Users.AnyAsync(u => u.Email == email);
        if (existing)
        {
            return TypedResults.Problem(
                title: "Conflict",
                detail: "A user with this email already exists",
                statusCode: 409
            );
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName,
            PasswordHash = passwordHasher.Hash(request.Password),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return TypedResults.Created(
            $"/users/{user.Id}",
            new RegisterResponse(user.Id, user.Email, user.DisplayName)
        );
    }
}
