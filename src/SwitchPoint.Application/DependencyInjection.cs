using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SwitchPoint.Application.Services;
using SwitchPoint.Application.UseCases.Analyses;
using SwitchPoint.Application.UseCases.Audit;
using SwitchPoint.Application.UseCases.Catalogue;
using SwitchPoint.Application.UseCases.Clients;
using SwitchPoint.Application.UseCases.Reports;
using SwitchPoint.Application.Validation;
using SwitchPoint.Calculation.Annuities;
using SwitchPoint.Calculation.Cashflow;
using SwitchPoint.Calculation.CriticalYield;
using SwitchPoint.Calculation.DbTransfer;
using SwitchPoint.Calculation.MonteCarlo;
using SwitchPoint.Calculation.Mortality;
using SwitchPoint.Calculation.Projection;
using SwitchPoint.Calculation.Riy;

namespace SwitchPoint.Application;

/// <summary>Registers engines, services, use-case handlers and validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSwitchPointApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Engines: stateless, thread-safe singletons.
        services.AddSingleton<ProjectionEngine>();
        services.AddSingleton<ReductionInYieldCalculator>();
        services.AddSingleton<CriticalYieldCalculator>();
        services.AddSingleton<ILifeTable>(GompertzMakehamLifeTable.Default);
        services.AddSingleton<AnnuityPricer>();
        services.AddSingleton<DbTransferCalculator>();
        services.AddSingleton<CashflowEngine>();
        services.AddSingleton<MonteCarloSimulator>();

        // Orchestration and handlers: scoped (they use scoped repositories and the current user).
        services.AddScoped<CalculationService>();
        services.AddScoped<PreviewCalculationHandler>();
        services.AddScoped<AnalysisHandlers>();
        services.AddScoped<ReportHandlers>();
        services.AddScoped<ListClientsHandler>();
        services.AddScoped<GetClientHandler>();
        services.AddScoped<CreateClientHandler>();
        services.AddScoped<UpdateClientHandler>();
        services.AddScoped<DeleteClientHandler>();
        services.AddScoped<ListSchemesHandler>();
        services.AddScoped<CreateSchemeHandler>();
        services.AddScoped<UpdateSchemeHandler>();
        services.AddScoped<DeleteSchemeHandler>();
        services.AddScoped<ListClientAnalysesHandler>();
        services.AddScoped<ListRecentAnalysesHandler>();
        services.AddScoped<ListProvidersHandler>();
        services.AddScoped<ListProductsHandler>();
        services.AddScoped<GetProductHandler>();
        services.AddScoped<SearchFundsHandler>();
        services.AddScoped<GetFundHandler>();
        services.AddScoped<ListModelPortfoliosHandler>();
        services.AddScoped<ListAssumptionSetsHandler>();
        services.AddScoped<GetAssumptionSetHandler>();
        services.AddScoped<CopyAssumptionSetHandler>();
        services.AddScoped<UpdateAssumptionSetHandler>();
        services.AddScoped<QueryAuditHandler>();
        services.AddScoped<VerifyAuditChainHandler>();
        services.AddScoped<ListIntegrationsHandler>();
        services.AddScoped<ImportFromBackOfficeHandler>();
        services.AddScoped<SyncFundsHandler>();

        // Validators.
        services.AddValidatorsFromAssemblyContaining<ClientWriteValidator>(ServiceLifetime.Singleton);
        return services;
    }
}
