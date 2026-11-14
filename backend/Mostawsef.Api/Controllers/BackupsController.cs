using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Api.Controllers;

/// <summary>Database backups: Super only. Only the settings of a backup are stored; the file is built on every export.</summary>
[ApiController]
[Route("api/backups")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class BackupsController(IBackupService backups) : ControllerBase
{
    /// <summary>The tables that can be exported.</summary>
    [HttpGet("tables")]
    public IReadOnlyList<string> Tables() => backups.GetTables();

    /// <summary>The backup history, newest first.</summary>
    [HttpGet]
    public Task<PagedResult<BackupRecordDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        backups.GetPageAsync(query, cancellationToken);

    [HttpPost]
    [ProducesResponseType<BackupRecordDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateBackupRequest request, CancellationToken cancellationToken)
    {
        var record = await backups.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Export), new { id = record.Id }, record);
    }

    /// <summary>Downloads the current data with the settings saved in this history row.</summary>
    [HttpGet("{id:guid}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var file = await backups.ExportAsync(id, cancellationToken);
        return File(file.Content, "application/octet-stream", file.FileName);
    }
}
