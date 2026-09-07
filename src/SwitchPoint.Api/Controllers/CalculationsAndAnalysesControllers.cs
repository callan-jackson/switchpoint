using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwitchPoint.Api.Auth;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.UseCases.Analyses;

namespace SwitchPoint.Api.Controllers;

/// <summary>Stateless live-preview calculations (audited as previews, never persisted).</summary>
[ApiController]
[Route("api/v1/calculations")]
[Produces("application/json")]
[EnableRateLimiting("calculations")]
public sealed class CalculationsController(PreviewCalculationHandler preview) : ControllerBase
{
    [HttpPost("pension-switch")]
    [ProducesResponseType<PensionSwitchResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PensionSwitchResultDto>> PensionSwitch([FromBody] PensionSwitchCalcRequest request, CancellationToken ct) => Ok(await preview.PensionSwitchAsync(request, ct));

    [HttpPost("db-transfer")]
    [ProducesResponseType<DbTransferResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DbTransferResultDto>> DbTransfer([FromBody] DbTransferCalcRequest request, CancellationToken ct) => Ok(await preview.DbTransferAsync(request, ct));

    [HttpPost("cashflow")]
    [ProducesResponseType<CashflowResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashflowResultDto>> Cashflow([FromBody] CashflowCalcRequest request, CancellationToken ct) => Ok(await preview.CashflowAsync(request, ct));

    [HttpPost("cashflow/stochastic")]
    [ProducesResponseType<StochasticResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StochasticResultDto>> Stochastic([FromBody] CashflowCalcRequest request, CancellationToken ct) => Ok(await preview.StochasticAsync(request, ct));

    [HttpPost("riy")]
    [ProducesResponseType<RiyDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiyDto>> Riy([FromBody] RiyCalcRequest request, CancellationToken ct) => Ok(await preview.RiyAsync(request, ct));

    [HttpPost("tax")]
    [ProducesResponseType<TaxComputationDto>(StatusCodes.Status200OK)]
    public ActionResult<TaxComputationDto> Tax([FromBody] TaxCalcRequest request) => Ok(preview.Tax(request));
}

[ApiController]
[Route("api/v1/analyses/pension-switch")]
[Produces("application/json")]
public sealed class PensionSwitchAnalysesController(AnalysisHandlers handlers) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PensionSwitchAnalysisDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PensionSwitchAnalysisDto>> Create([FromBody] PensionSwitchAnalysisWrite write, CancellationToken ct)
    {
        PensionSwitchAnalysisDto created = await handlers.CreatePensionSwitchAsync(write, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PensionSwitchAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PensionSwitchAnalysisDto>> Get(Guid id, CancellationToken ct) => Ok(await handlers.GetPensionSwitchAsync(id, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PensionSwitchAnalysisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PensionSwitchAnalysisDto>> Update(Guid id, [FromBody] PensionSwitchAnalysisWrite write, CancellationToken ct) => Ok(await handlers.UpdatePensionSwitchAsync(id, write, ct));

    [HttpPost("{id:guid}/calculate")]
    [EnableRateLimiting("calculations")]
    [ProducesResponseType<PensionSwitchAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PensionSwitchAnalysisDto>> Calculate(Guid id, CancellationToken ct) => Ok(await handlers.CalculatePensionSwitchAsync(id, ct));

    [HttpPost("{id:guid}/lock")]
    [Authorize(Policy = Policies.Adviser)]
    [ProducesResponseType<PensionSwitchAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PensionSwitchAnalysisDto>> Lock(Guid id, CancellationToken ct) => Ok(await handlers.LockPensionSwitchAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await handlers.DeletePensionSwitchAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/analyses/db-transfer")]
[Produces("application/json")]
public sealed class DbTransferAnalysesController(AnalysisHandlers handlers) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<DbTransferAnalysisDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DbTransferAnalysisDto>> Create([FromBody] DbTransferAnalysisWrite write, CancellationToken ct)
    {
        DbTransferAnalysisDto created = await handlers.CreateDbTransferAsync(write, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DbTransferAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DbTransferAnalysisDto>> Get(Guid id, CancellationToken ct) => Ok(await handlers.GetDbTransferAsync(id, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<DbTransferAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DbTransferAnalysisDto>> Update(Guid id, [FromBody] DbTransferAnalysisWrite write, CancellationToken ct) => Ok(await handlers.UpdateDbTransferAsync(id, write, ct));

    [HttpPost("{id:guid}/calculate")]
    [EnableRateLimiting("calculations")]
    [ProducesResponseType<DbTransferAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DbTransferAnalysisDto>> Calculate(Guid id, CancellationToken ct) => Ok(await handlers.CalculateDbTransferAsync(id, ct));

    [HttpPost("{id:guid}/lock")]
    [Authorize(Policy = Policies.Adviser)]
    [ProducesResponseType<DbTransferAnalysisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DbTransferAnalysisDto>> Lock(Guid id, CancellationToken ct) => Ok(await handlers.LockDbTransferAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await handlers.DeleteDbTransferAsync(id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/analyses/cashflow")]
[Produces("application/json")]
public sealed class CashflowPlansController(AnalysisHandlers handlers) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CashflowPlanDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CashflowPlanDto>> Create([FromBody] CashflowPlanWrite write, CancellationToken ct)
    {
        CashflowPlanDto created = await handlers.CreateCashflowAsync(write, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CashflowPlanDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashflowPlanDto>> Get(Guid id, CancellationToken ct) => Ok(await handlers.GetCashflowAsync(id, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CashflowPlanDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashflowPlanDto>> Update(Guid id, [FromBody] CashflowPlanWrite write, CancellationToken ct) => Ok(await handlers.UpdateCashflowAsync(id, write, ct));

    [HttpPost("{id:guid}/calculate")]
    [EnableRateLimiting("calculations")]
    [ProducesResponseType<CashflowPlanDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashflowPlanDto>> Calculate(Guid id, CancellationToken ct) => Ok(await handlers.CalculateCashflowAsync(id, stochastic: false, ct));

    [HttpPost("{id:guid}/calculate/stochastic")]
    [EnableRateLimiting("calculations")]
    [ProducesResponseType<CashflowPlanDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashflowPlanDto>> CalculateStochastic(Guid id, CancellationToken ct) => Ok(await handlers.CalculateCashflowAsync(id, stochastic: true, ct));

    [HttpPost("{id:guid}/lock")]
    [Authorize(Policy = Policies.Adviser)]
    [ProducesResponseType<CashflowPlanDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CashflowPlanDto>> Lock(Guid id, CancellationToken ct) => Ok(await handlers.LockCashflowAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await handlers.DeleteCashflowAsync(id, ct);
        return NoContent();
    }
}
