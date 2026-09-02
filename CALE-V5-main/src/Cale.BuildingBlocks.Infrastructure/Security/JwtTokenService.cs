using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Domain.Time;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cale.BuildingBlocks.Infrastructure.Security;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string Create(int userId, string email, string name, string role)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var normalized = Roles.Normalize(role);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Role, normalized)
        };

        var accessMinutes = _options.AccessTokenMinutes > 0
            ? _options.AccessTokenMinutes
            : _options.ExpirationHours * 60;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: _clock.UtcNow.AddMinutes(accessMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
