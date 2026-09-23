using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using PDFHub.Application;
using PDFHub.Application.Abstractions;

namespace PDFHub.Infrastructure.Services;

public sealed class DateTimeService : IDateTime
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string UserId => User?.Identity?.IsAuthenticated == true ? User.Identity.Name ?? "" : "";
    public string FullName => User?.FindFirstValue(PdfHubClaims.DisplayName) ?? UserId;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}

/// <summary>PBKDF2-SHA512, stored as "v1.{iterations}.{salt}.{hash}".</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashSize);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
            return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
