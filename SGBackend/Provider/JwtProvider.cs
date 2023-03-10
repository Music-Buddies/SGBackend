using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SecretsProvider;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Provider;

public class JwtProvider
{
    private readonly ISecretsProvider _secretsProvider;


    public JwtProvider(ISecretsProvider secretsProvider)
    {
        _secretsProvider = secretsProvider;
    }

    public string GetJwt(User dbUser, Claim[]? additionalClaims = null)
    {
        // issue token with user id
        var key = Encoding.UTF8.GetBytes(_secretsProvider.GetSecret<Secrets>().JwtKey);

        var defaultClaims = new List<Claim>
        {
            new("sub", dbUser.Id.ToString()),
            new("name", dbUser.Name)
        };
        if (additionalClaims != null) defaultClaims.AddRange(additionalClaims);

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "http://localhost:5173",
            Subject = new ClaimsIdentity(defaultClaims.ToArray()),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha512Signature),
            Expires = DateTime.Now.AddDays(3)
        });

        if (token == null) throw new Exception($"could not create jwt for user {dbUser.Id.ToString()}");

        return token;
    }

    public TokenValidationParameters GetJwtValidationParameters(string JwtKey)
    {
        return new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "http://localhost:5173",
            IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey))
        };
    }
}