using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Exceptions;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Application.Services;

/// <summary>What the API needs to issue a sign-in cookie.</summary>
public sealed record SessionUser(int Id, string UserName, string DisplayName, string Role, string SecurityStamp, bool MustChangePassword);

public interface IAuthService
{
    /// <summary>Null when the name or password is wrong or the account is disabled.</summary>
    Task<SessionUser?> SignInAsync(LoginDto dto, CancellationToken ct = default);
    Task<SessionUser> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken ct = default);
    /// <summary>False once the account is disabled or its credentials/role changed since the cookie was issued.</summary>
    Task<bool> IsSessionValidAsync(int userId, string? securityStamp, CancellationToken ct = default);
}

public sealed class AuthService(IUnitOfWork uow, IPasswordHasher hasher, IDateTime clock) : IAuthService
{
    private IRepository<AppUser> Users => uow.Repository<AppUser>();

    public async Task<SessionUser?> SignInAsync(LoginDto dto, CancellationToken ct = default)
    {
        var name = (dto.UserName ?? "").Trim();
        var user = await Users.GetAll().FirstOrDefaultAsync(u => u.UserName == name, ct);
        if (user is null || !user.IsActive || !hasher.Verify(dto.Password ?? "", user.PasswordHash))
            return null;

        user.LastLoginAt = clock.Now;
        await uow.CommitAsync();
        return ToSession(user);
    }

    public async Task<SessionUser> ChangePasswordAsync(int userId, ChangePasswordDto dto, CancellationToken ct = default)
    {
        var user = await Users.GetByIdAsync(userId) ?? throw new KeyNotFoundException("ไม่พบผู้ใช้");

        var errors = new ValidationErrors();
        if (!hasher.Verify(dto.CurrentPassword ?? "", user.PasswordHash))
            errors.Add(nameof(dto.CurrentPassword), "รหัสผ่านปัจจุบันไม่ถูกต้อง");
        if ((dto.NewPassword ?? "").Length < UserService.MinPasswordLength)
            errors.Add(nameof(dto.NewPassword), $"รหัสผ่านใหม่ต้องมีอย่างน้อย {UserService.MinPasswordLength} ตัวอักษร");
        else if (dto.NewPassword == dto.CurrentPassword)
            errors.Add(nameof(dto.NewPassword), "รหัสผ่านใหม่ต้องไม่ซ้ำกับรหัสเดิม");
        errors.ThrowIfAny();

        user.PasswordHash = hasher.Hash(dto.NewPassword!);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await uow.CommitAsync();
        return ToSession(user);
    }

    public async Task<bool> IsSessionValidAsync(int userId, string? securityStamp, CancellationToken ct = default)
    {
        var stamp = await Users.GetAll().AsNoTracking().Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.SecurityStamp).FirstOrDefaultAsync(ct);
        return stamp is not null && stamp == securityStamp;
    }

    private static SessionUser ToSession(AppUser u) =>
        new(u.Id, u.UserName, u.DisplayName, u.Role, u.SecurityStamp, u.MustChangePassword);
}
