using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Exceptions;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Application.Services;

public interface ISectionService
{
    Task<IReadOnlyList<SectionDto>> ListAsync(CancellationToken ct = default);
    Task<SectionDto> CreateAsync(CreateSectionDto dto, CancellationToken ct = default);
    Task<SectionDto> UpdateAsync(int id, UpdateSectionDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class SectionService(IUnitOfWork uow) : ISectionService
{
    private IRepository<Section> Sections => uow.Repository<Section>();
    private IRepository<Drawing> Drawings => uow.Repository<Drawing>();

    public async Task<IReadOnlyList<SectionDto>> ListAsync(CancellationToken ct = default)
    {
        var drawings = Drawings.GetAll();
        return await Sections.GetAll().AsNoTracking().OrderBy(s => s.Code).Select(s => new SectionDto
        {
            Id = s.Id,
            Code = s.Code,
            Name = s.Name,
            IsActive = s.IsActive,
            DrawingCount = drawings.Count(d => d.SectionId == s.Id),
        }).ToListAsync(ct);
    }

    public async Task<SectionDto> CreateAsync(CreateSectionDto dto, CancellationToken ct = default)
    {
        var code = (dto.Code ?? "").Trim().ToUpperInvariant();
        var name = (dto.Name ?? "").Trim();

        var errors = new ValidationErrors();
        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
            errors.Add(nameof(dto.Code), "รหัสต้องเป็นตัวอักษรภาษาอังกฤษ 2 ตัว เช่น NB");
        else if (await Sections.GetAll().AnyAsync(s => s.Code == code, ct))
            errors.Add(nameof(dto.Code), $"มีรหัส {code} อยู่แล้ว");
        if (name.Length == 0)
            errors.Add(nameof(dto.Name), "กรุณากรอกชื่อ Section");
        else if (name.Length > 100)
            errors.Add(nameof(dto.Name), "ชื่อยาวเกิน 100 ตัวอักษร");
        errors.ThrowIfAny();

        var section = Sections.New();
        section.Code = code;
        section.Name = name;
        await Sections.AddAsync(section);
        await uow.CommitAsync();
        return await GetAsync(section.Id, ct);
    }

    public async Task<SectionDto> UpdateAsync(int id, UpdateSectionDto dto, CancellationToken ct = default)
    {
        var section = await Sections.GetByIdAsync(id) ?? throw new KeyNotFoundException($"ไม่พบ Section รหัส {id}");
        var name = (dto.Name ?? "").Trim();
        if (name.Length == 0)
            throw ValidationException.For(nameof(dto.Name), "กรุณากรอกชื่อ Section");

        section.Name = name;
        section.IsActive = dto.IsActive;
        await uow.CommitAsync();
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var section = await Sections.GetByIdAsync(id) ?? throw new KeyNotFoundException($"ไม่พบ Section รหัส {id}");
        if (await Drawings.GetAll().AnyAsync(d => d.SectionId == id, ct))
            throw new BusinessRuleException($"ลบ Section {section.Code} ไม่ได้ เพราะมี Drawing ใช้อยู่ ให้ปิดใช้งานแทน");

        await Sections.RemoveAsync(section);
        await uow.CommitAsync();
    }

    private async Task<SectionDto> GetAsync(int id, CancellationToken ct) =>
        (await ListAsync(ct)).Single(s => s.Id == id);
}
