using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFHub.Api.Extensions;
using PDFHub.Application.Services;
using PDFHub.Domain.DTOs;

namespace PDFHub.Api.Controllers;

/// <summary>Manual backups to a folder the admin chooses. The copy runs in the background; poll the overview.</summary>
[ApiController]
[Route("api/backups")]
[Authorize(Policy = Policies.Admin)]
public class BackupsController(IBackupService backups) : ControllerBase
{
    [HttpGet]
    public Task<BackupOverviewDto> Overview(CancellationToken ct) => backups.GetOverviewAsync(ct);

    [HttpPost("check")]
    public BackupPathCheckDto Check(StartBackupDto dto) => backups.CheckDestination(dto.Destination);

    [HttpPost]
    public async Task<IActionResult> Start(StartBackupDto dto, CancellationToken ct) =>
        Accepted(await backups.StartAsync(dto, ct));
}
