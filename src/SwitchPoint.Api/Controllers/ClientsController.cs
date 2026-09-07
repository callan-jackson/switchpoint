using Microsoft.AspNetCore.Mvc;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.UseCases.Clients;
using SwitchPoint.Application.UseCases.Reports;

namespace SwitchPoint.Api.Controllers;

[ApiController]
[Route("api/v1/clients")]
[Produces("application/json")]
public sealed class ClientsController(
    ListClientsHandler list,
    GetClientHandler get,
    CreateClientHandler create,
    UpdateClientHandler update,
    DeleteClientHandler delete,
    ListSchemesHandler listSchemes,
    CreateSchemeHandler createScheme,
    UpdateSchemeHandler updateScheme,
    DeleteSchemeHandler deleteScheme,
    ListClientAnalysesHandler listAnalyses,
    ReportHandlers reports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ClientSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientSummary>>> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await list.HandleAsync(search, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetail>> Get(Guid id, CancellationToken ct) => Ok(await get.HandleAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientDetail>> Create([FromBody] ClientWrite write, CancellationToken ct)
    {
        ClientDetail created = await create.HandleAsync(write, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ClientDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClientDetail>> Update(Guid id, [FromBody] ClientWrite write, CancellationToken ct) => Ok(await update.HandleAsync(id, write, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await delete.HandleAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/schemes")]
    [ProducesResponseType<IReadOnlyList<SchemeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchemeDto>>> Schemes(Guid id, CancellationToken ct) => Ok(await listSchemes.HandleAsync(id, ct));

    [HttpPost("{id:guid}/schemes")]
    [ProducesResponseType<SchemeDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SchemeDto>> CreateScheme(Guid id, [FromBody] SchemeWrite write, CancellationToken ct)
    {
        SchemeDto created = await createScheme.HandleAsync(id, write, ct);
        return CreatedAtAction(nameof(Schemes), new { id }, created);
    }

    [HttpPut("{id:guid}/schemes/{schemeId:guid}")]
    [ProducesResponseType<SchemeDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchemeDto>> UpdateScheme(Guid id, Guid schemeId, [FromBody] SchemeWrite write, CancellationToken ct) => Ok(await updateScheme.HandleAsync(id, schemeId, write, ct));

    [HttpDelete("{id:guid}/schemes/{schemeId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteScheme(Guid id, Guid schemeId, CancellationToken ct)
    {
        await deleteScheme.HandleAsync(id, schemeId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/analyses")]
    [ProducesResponseType<IReadOnlyList<AnalysisSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnalysisSummary>>> Analyses(Guid id, CancellationToken ct) => Ok(await listAnalyses.HandleAsync(id, ct));

    [HttpGet("{id:guid}/reports")]
    [ProducesResponseType<IReadOnlyList<ReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> Reports(Guid id, CancellationToken ct) => Ok(await reports.ListForClientAsync(id, ct));
}
