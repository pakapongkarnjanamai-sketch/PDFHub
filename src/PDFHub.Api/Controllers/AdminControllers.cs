using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFHub.Api.Extensions;
using PDFHub.Application.Exceptions;
using PDFHub.Application.Services;
using PDFHub.Domain.DTOs;

namespace PDFHub.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Policy = Policies.CanEdit)]
public class ImportController(IImportService import) : ControllerBase
{
    [HttpPost("excel")]
    public async Task<ImportResultDto> Excel(IFormFile file, [FromForm] ImportMode mode, [FromForm] bool dryRun, CancellationToken ct)
    {
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw ValidationException.For("File", "กรุณาเลือกไฟล์ Excel นามสกุล .xlsx (ถ้าเป็น .xls หรือ .xlsm ให้ Save As เป็น .xlsx ก่อน)");

        await using var stream = file.OpenReadStream();
        return await import.ImportAsync(stream, mode, dryRun, ct);
    }
}

[ApiController]
[Route("api/sections")]
[Authorize(Policy = Policies.Admin)]
public class SectionsController(ISectionService sections) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SectionDto>> List(CancellationToken ct) => sections.ListAsync(ct);

    [HttpPost]
    public Task<SectionDto> Create(CreateSectionDto dto, CancellationToken ct) => sections.CreateAsync(dto, ct);

    [HttpPut("{id:int}")]
    public Task<SectionDto> Update(int id, UpdateSectionDto dto, CancellationToken ct) => sections.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await sections.DeleteAsync(id, ct);
        return Ok(new { success = true });
    }
}

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Admin)]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => users.ListAsync(ct);

    [HttpPost]
    public Task<UserDto> Create(CreateUserDto dto, CancellationToken ct) => users.CreateAsync(dto, ct);

    [HttpPut("{id:int}")]
    public Task<UserDto> Update(int id, UpdateUserDto dto, CancellationToken ct) => users.UpdateAsync(id, dto, ct);

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordDto dto, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, dto, ct);
        return Ok(new { success = true });
    }
}
