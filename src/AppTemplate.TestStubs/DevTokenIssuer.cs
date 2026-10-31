using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AppTemplate.TestStubs;

/// <summary>
/// Emite JWTs HS256 para desenvolvimento e testes. O JWT carrega:
///   sub   — userId fake (ObjectId.Empty, parseável pelo handler dos endpoints),
///   email — email informado no login (ou default),
///   tid   — tenantId vindo de <see cref="DevJwtOptions.TenantId"/>.
/// </summary>
public class DevTokenIssuer
{
    public const string DevUserId = "000000000000000000000000";

    private readonly DevJwtOptions _options;

    public DevTokenIssuer(DevJwtOptions options)
    {
        _options = options;
    }

    public (string Token, string TenantId) Issue(string email)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, DevUserId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("tid", _options.TenantId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddHours(_options.ExpirationHours),
            signingCredentials: creds);

        return (handler.WriteToken(token), _options.TenantId);
    }

    /// <summary>
    /// Parametros de validacao que devem ser usados pelo pipeline do
    /// JwtBearer em dev para aceitar os JWTs emitidos por este issuer.
    /// </summary>
    public static TokenValidationParameters BuildValidationParameters(DevJwtOptions options)
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
        };
    }
}
