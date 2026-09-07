using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Ports;
using SwitchPoint.Domain.Tenancy;

namespace SwitchPoint.Api.Auth;

/// <summary>JWT settings (section Auth).</summary>
public sealed class AuthOptions
{
    public string Issuer { get; set; } = "switchpoint";
    public string Audience { get; set; } = "switchpoint-api";
    public string? SigningKey { get; set; }
    public int TokenLifetimeHours { get; set; } = 8;

    /// <summary>Resolves the HMAC key; in Development a stable placeholder is derived when none is configured.</summary>
    public SymmetricSecurityKey ResolveKey(bool isDevelopment)
    {
        string key = SigningKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException("Auth:SigningKey must be configured outside Development (Key Vault secret JwtSigningKey).");
            }

            key = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("switchpoint-development-only-signing-key")));
        }

        byte[] bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < 32)
        {
            bytes = SHA256.HashData(bytes);
        }

        return new SymmetricSecurityKey(bytes);
    }
}

/// <summary>Claim names used by the API.</summary>
public static class SwitchPointClaims
{
    public const string FirmId = "firm_id";
    public const string FirmName = "firm_name";
    public const string Role = "role";
}

/// <summary>Issues HS256 bearer tokens for authenticated users.</summary>
public sealed class JwtTokenIssuer(IOptions<AuthOptions> options, IHostEnvironment environment)
{
    public LoginResponse Issue(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        AuthOptions o = options.Value;
        DateTime expires = DateTime.UtcNow.AddHours(Math.Clamp(o.TokenLifetimeHours, 1, 24));
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(SwitchPointClaims.Role, user.Role.ToString()),
            new(SwitchPointClaims.FirmId, user.FirmId.ToString("D")),
            new(SwitchPointClaims.FirmName, user.FirmName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        ];
        SigningCredentials credentials = new(o.ResolveKey(environment.IsDevelopment()), SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(o.Issuer, o.Audience, claims, notBefore: DateTime.UtcNow.AddMinutes(-1), expires: expires, signingCredentials: credentials);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user);
    }
}

/// <summary>Current user resolved from the bearer token claims.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && Guid.TryParse(Principal.FindFirstValue(SwitchPointClaims.FirmId), out _);

    public Guid UserId => Guid.TryParse(Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out Guid id) ? id : Guid.Empty;

    public Guid FirmId => Guid.TryParse(Principal?.FindFirstValue(SwitchPointClaims.FirmId), out Guid id) ? id : Guid.Empty;

    public UserRole Role => Enum.TryParse(Principal?.FindFirstValue(SwitchPointClaims.Role), out UserRole role) ? role : UserRole.Adviser;

    public string DisplayName => Principal?.FindFirstValue(JwtRegisteredClaimNames.Name) ?? Principal?.FindFirstValue(ClaimTypes.Name) ?? "anonymous";
}

/// <summary>Authorization policy names.</summary>
public static class Policies
{
    public const string Adviser = "Adviser";
    public const string Compliance = "Compliance";
    public const string FirmAdmin = "FirmAdmin";
}
