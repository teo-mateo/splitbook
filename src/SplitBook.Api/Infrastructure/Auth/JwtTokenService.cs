using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SplitBook.Api.Infrastructure.Auth;

public class JwtTokenService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not configured");
        _issuer = configuration["Jwt:Issuer"] ?? "SplitBook";
        _audience = configuration["Jwt:Audience"] ?? "SplitBook";
    }

    public string CreateToken(Guid userId, string email, string displayName)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(24);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, displayName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var payload = new JwtPayload(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            issuedAt: now,
            expires: expires
        );

        var header = new JwtHeader(credentials);
        var token = new JwtSecurityToken(header, payload);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTimeOffset GetExpiry() => DateTimeOffset.UtcNow.AddHours(24);
}
