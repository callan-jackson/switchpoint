using Microsoft.Extensions.DependencyInjection;
using SwitchPoint.Application.Ports;
using SwitchPoint.Reports.Rendering;

namespace SwitchPoint.Reports;

/// <summary>Registers the report renderer (PDF via QuestPDF, DOCX via OpenXML, JSON).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSwitchPointReports(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IReportRenderer, ReportRenderer>();
        return services;
    }
}
