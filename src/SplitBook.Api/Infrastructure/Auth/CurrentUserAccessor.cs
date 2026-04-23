using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SplitBook.Api.Infrastructure.Auth;

public record CurrentUser(Guid Id, string Email);

public class CurrentUserAccessor
{
    public CurrentUser GetCurrentUser(HttpContext context)
    {
        var idClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue(JwtRegisteredClaimNames.Email);

        if (idClaim == null)
            throw new InvalidOperationException("No user ID claim found");

        return new CurrentUser(Guid.Parse(idClaim), email ?? string.Empty);
    }
}
