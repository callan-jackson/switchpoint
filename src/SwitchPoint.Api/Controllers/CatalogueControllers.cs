using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwitchPoint.Api.Auth;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.UseCases.Catalogue;

namespace SwitchPoint.Api.Controllers;

[ApiController]
[Route("api/v1/providers")]
[Produces("application/json")]
public sealed class ProvidersController(ListProvidersHandler list) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProviderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProviderDto>>> List(CancellationToken ct) => Ok(await list.HandleAsync(ct));
}

[ApiController]
[Route("api/v1/products")]
[Produces("application/json")]
public sealed class ProductsController(ListProductsHandler list, GetProductHandler get) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductSummary>>> List([FromQuery] string? wrapper, [FromQuery] string? search, CancellationToken ct) => Ok(await list.HandleAsync(wrapper, search, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProductDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetail>> Get(Guid id, CancellationToken ct) => Ok(await get.HandleAsync(id, ct));
}

[ApiController]
[Route("api/v1/funds")]
[Produces("application/json")]
public sealed class FundsController(SearchFundsHandler search, GetFundHandler get) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<FundDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<FundDto>>> Search([FromQuery(Name = "search")] string? term, [FromQuery] string? sector, [FromQuery] decimal? maxOcfPct, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(await search.HandleAsync(term, sector, maxOcfPct, page, pageSize, ct));

    [HttpGet("{isin}")]
    [ProducesResponseType<FundDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FundDto>> Get(string isin, CancellationToken ct) => Ok(await get.HandleAsync(isin, ct));
}

[ApiController]
[Route("api/v1/model-portfolios")]
[Produces("application/json")]
public sealed class ModelPortfoliosController(ListModelPortfoliosHandler list) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ModelPortfolioDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ModelPortfolioDto>>> List(CancellationToken ct) => Ok(await list.HandleAsync(ct));
}

[ApiController]
[Route("api/v1/assumption-sets")]
[Produces("application/json")]
public sealed class AssumptionSetsController(ListAssumptionSetsHandler list, GetAssumptionSetHandler get, CopyAssumptionSetHandler copy, UpdateAssumptionSetHandler update) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AssumptionSetDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssumptionSetDto>>> List(CancellationToken ct) => Ok(await list.HandleAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AssumptionSetDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssumptionSetDto>> Get(Guid id, CancellationToken ct) => Ok(await get.HandleAsync(id, ct));

    [HttpPost("{id:guid}/copy")]
    [Authorize(Policy = Policies.FirmAdmin)]
    [ProducesResponseType<AssumptionSetDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AssumptionSetDto>> Copy(Guid id, [FromBody] CopyAssumptionSetRequest request, CancellationToken ct)
    {
        AssumptionSetDto created = await copy.HandleAsync(id, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.FirmAdmin)]
    [ProducesResponseType<AssumptionSetDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AssumptionSetDto>> Update(Guid id, [FromBody] AssumptionSetWrite write, CancellationToken ct) => Ok(await update.HandleAsync(id, write, ct));
}
