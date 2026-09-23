using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Exceptions;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Application.Services;

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, CancellationToken ct = default);
    Task ResetPasswordAsync(int id, ResetPasswordDto dto, CancellationToken ct = default);
}

public sealed class UserService(IUnitOfWork uow, IPasswordHasher hasher, ICurrentUserService currentUser) : IUserService
{
    public const int MinPasswordLength = 6;

    private IRepository<AppUser> Users => uow.Repository<AppUser>();

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default) =>
        await Users.GetAll().AsNoTracking().OrderBy(u => u.UserName).Select(u => new UserDto
        {
            Id = u.Id,
            UserName = u.UserName,
            DisplayName = u.DisplayName,
            Role = u.Role,
            IsActive = u.IsActive,
            MustChangePassword = u.MustChangePassword,
            LastLoginAt = u.LastLoginAt,
        }).ToListAsync(ct);

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        var userName = (dto.UserName ?? "").Trim();
        var errors = new ValidationErrors();
        if (userName.Length is < 2 or > 50 || !userName.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            errors.Add(nameof(dto.UserName), "ใช้ได้เฉพาะ a-z, 0-9, จุด, ขีด (2–50 ตัว)");
        else if (await Users.GetAll().AnyAsync(u => u.UserName == userName, ct))
            errors.Add(nameof(dto.UserName), $"มีชื่อผู้ใช้ {userName} อยู่แล้ว");
        if (!Roles.All.Contains(dto.Role))
            errors.Add(nameof(dto.Role), "กรุณาเลือกสิทธิ์");
        if ((dto.Password ?? "").Length < MinPasswordLength)
            errors.Add(nameof(dto.Password), $"รหัสผ่านต้องมีอย่างน้อย {MinPasswordLength} ตัวอักษร");
        errors.ThrowIfAny();

        var user = Users.New();
        user.UserName = userName;
        user.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? userName : dto.DisplayName.Trim();
        user.Role = dto.Role!;
        user.PasswordHash = hasher.Hash(dto.Password!);
        user.MustChangePassword = true;
        await Users.AddAsync(user);
        await uow.CommitAsync();
        return await GetAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await FindAsync(id);
        if (!Roles.All.Contains(dto.Role))
            throw ValidationException.For(nameof(dto.Role), "กรุณาเลือกสิทธิ์");

        // Always leaves at least one active admin: the one making the change.
        var isSelf = string.Equals(user.UserName, currentUser.UserId, StringComparison.OrdinalIgnoreCase);
        if (isSelf && (dto.Role != Roles.Admin || !dto.IsActive))
            throw new BusinessRuleException("ลดสิทธิ์หรือปิดบัญชีของตัวเองไม่ได้ ให้ผู้ดูแลระบบคนอื่นทำแทน");

        if (user.Role != dto.Role || user.IsActive != dto.IsActive)
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? user.UserName : dto.DisplayName.Trim();
        user.Role = dto.Role!;
        user.IsActive = dto.IsActive;
        await uow.CommitAsync();
        return await GetAsync(id, ct);
    }

    public async Task ResetPasswordAsync(int id, ResetPasswordDto dto, CancellationToken ct = default)
    {
        var user = await FindAsync(id);
        if ((dto.Password ?? "").Length < MinPasswordLength)
            throw ValidationException.For(nameof(dto.Password), $"รหัสผ่านชั่วคราวต้องมีอย่างน้อย {MinPasswordLength} ตัวอักษร");

        user.PasswordHash = hasher.Hash(dto.Password!);
        user.MustChangePassword = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await uow.CommitAsync();
    }

    private async Task<AppUser> FindAsync(int id) =>
        await Users.GetByIdAsync(id) ?? throw new KeyNotFoundException($"ไม่พบผู้ใช้รหัส {id}");

    private async Task<UserDto> GetAsync(int id, CancellationToken ct) => (await ListAsync(ct)).Single(u => u.Id == id);
}
