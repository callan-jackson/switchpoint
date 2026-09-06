using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Mapping;
using SwitchPoint.Application.Ports;
using SwitchPoint.Domain.Assumptions;
using SwitchPoint.Domain.Market;

namespace SwitchPoint.Application.UseCases.Catalogue;

public sealed class ListProvidersHandler(IProviderCatalogue providers)
{
    public async Task<IReadOnlyList<ProviderDto>> HandleAsync(CancellationToken ct) => [.. (await providers.ListAsync(ct)).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => p.ToDto())];
}

public sealed class ListProductsHandler(IProductCatalogue products, IProviderCatalogue providers)
{
    public async Task<IReadOnlyList<ProductSummary>> HandleAsync(string? wrapper, string? search, CancellationToken ct)
    {
        WrapperTypes? w = null;
        if (!string.IsNullOrWhiteSpace(wrapper))
        {
            if (!Enum.TryParse(wrapper, true, out WrapperTypes parsed) || parsed == WrapperTypes.None)
            {
                throw new ValidationException("wrapper", $"'{wrapper}' is not a wrapper type.");
            }

            w = parsed;
        }

        Dictionary<Guid, string> names = (await providers.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        return [.. (await products.ListAsync(w, search, ct)).Where(p => p.IsActive).Select(p => p.ToSummary(names.GetValueOrDefault(p.ProviderId, "Unknown provider"))).OrderBy(p => p.ProviderName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }
}

public sealed class GetProductHandler(IProductCatalogue products, IProviderCatalogue providers)
{
    public async Task<ProductDetail> HandleAsync(Guid productId, CancellationToken ct)
    {
        Product p = await products.GetAsync(productId, ct) ?? throw new NotFoundException("Product", productId);
        Provider? provider = await providers.GetAsync(p.ProviderId, ct);
        return p.ToDetail(provider?.Name ?? "Unknown provider");
    }
}

public sealed class SearchFundsHandler(IFundCatalogue funds)
{
    public async Task<PagedResult<FundDto>> HandleAsync(string? search, string? sector, decimal? maxOcfPct, int page, int pageSize, CancellationToken ct)
    {
        Page<Fund> result = await funds.SearchAsync(search, sector, Pct.ToFraction(maxOcfPct), Math.Max(1, page), Math.Clamp(pageSize, 1, 200), ct);
        return new PagedResult<FundDto>([.. result.Items.Select(f => f.ToDto())], result.PageNumber, result.PageSize, result.Total);
    }
}

public sealed class GetFundHandler(IFundCatalogue funds)
{
    public async Task<FundDto> HandleAsync(string isin, CancellationToken ct) =>
        (await funds.GetByIsinAsync(isin, ct) ?? throw new NotFoundException("Fund", isin)).ToDto();
}

public sealed class ListModelPortfoliosHandler(IModelPortfolioCatalogue portfolios, IProviderCatalogue providers)
{
    public async Task<IReadOnlyList<ModelPortfolioDto>> HandleAsync(CancellationToken ct)
    {
        Dictionary<Guid, string> names = (await providers.ListAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        return [.. (await portfolios.ListAsync(ct)).Select(m => m.ToDto(names.GetValueOrDefault(m.ProviderId, "Unknown provider")))];
    }
}

public sealed class ListAssumptionSetsHandler(IAssumptionSetRepository sets, ICurrentUser user)
{
    public async Task<IReadOnlyList<AssumptionSetDto>> HandleAsync(CancellationToken ct) =>
        [.. (await sets.ListAsync(user.FirmId, ct)).OrderByDescending(s => s.IsFcaStandard).ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Select(s => s.ToDto())];
}

public sealed class GetAssumptionSetHandler(IAssumptionSetRepository sets, ICurrentUser user)
{
    public async Task<AssumptionSetDto> HandleAsync(Guid id, CancellationToken ct) =>
        (await sets.GetAsync(user.FirmId, id, ct) ?? throw new NotFoundException("AssumptionSet", id)).ToDto();
}

public sealed class CopyAssumptionSetHandler(IAssumptionSetRepository sets, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
{
    public async Task<AssumptionSetDto> HandleAsync(Guid id, CopyAssumptionSetRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        AssumptionSet source = await sets.GetAsync(user.FirmId, id, ct) ?? throw new NotFoundException("AssumptionSet", id);
        AssumptionSet copy = source.CopyForFirm(Guid.NewGuid(), user.FirmId, request.Name, clock.UtcNow);
        await sets.AddAsync(copy, ct);
        await audit.AppendAsync(user.FirmId, user.UserId, "AssumptionSet", copy.Id, "Copied", new { copy.Id, copy.Name, SourceId = source.Id }, ct);
        await uow.SaveChangesAsync(ct);
        return copy.ToDto();
    }
}

public sealed class UpdateAssumptionSetHandler(IAssumptionSetRepository sets, IAuditLog audit, IUnitOfWork uow, ICurrentUser user, IClock clock)
{
    public async Task<AssumptionSetDto> HandleAsync(Guid id, AssumptionSetWrite write, CancellationToken ct)
    {
        AssumptionSet set = await sets.GetAsync(user.FirmId, id, ct) ?? throw new NotFoundException("AssumptionSet", id);
        if (set.IsFcaStandard || set.FirmId != user.FirmId)
        {
            throw new ForbiddenException("FCA-standard assumption sets are read-only; copy the set into your firm to edit it.");
        }

        set.ApplyWrite(write, clock.UtcNow);
        await audit.AppendAsync(user.FirmId, user.UserId, "AssumptionSet", set.Id, "Updated", new { set.Id, set.Name, set.Version, GrowthIntermediate = set.GrowthIntermediate, set.Inflation }, ct);
        await uow.SaveChangesAsync(ct);
        return set.ToDto();
    }
}
