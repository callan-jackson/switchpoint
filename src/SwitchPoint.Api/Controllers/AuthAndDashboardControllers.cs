using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SwitchPoint.Api.Auth;
using SwitchPoint.Application.Dtos;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.UseCases.Clients;
using SwitchPoint.Application.UseCases.Reports;
using SwitchPoint.Domain.Analysis;
using SwitchPoint.Domain.Market;

namespace SwitchPoint.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(IIdentityService identity, JwtTokenIssuer issuer, ICurrentUser current) : ControllerBase
{
    /// <summary>Exchanges email and password for a bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        UserDto? user = await identity.AuthenticateAsync(request.Email, request.Password, ct);
        if (user is null)
        {
            ProblemDetails details = new() { Status = StatusCodes.Status401Unauthorized, Title = "Invalid email or password.", Type = "https://httpstatuses.io/401", Instance = HttpContext.Request.Path };
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(details, options: null, contentType: "application/problem+json", ct);
            return new EmptyResult();
        }

        return Ok(issuer.Issue(user));
    }

    /// <summary>The authenticated user.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        UserDto? user = await identity.GetUserAsync(current.UserId, ct);
        return user is null ? throw new NotFoundException("User", current.UserId) : Ok(user);
    }
}

/// <summary>Headline numbers for the dashboard.</summary>
public sealed record DashboardSummary(int Clients, int AnalysesInProgress, int ReportsThisMonth, int FundsInCatalogue, IReadOnlyList<AnalysisSummary> RecentAnalyses);

[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
public sealed class DashboardController(IClientRepository clients, IAnalysisRepository analyses, IReportRepository reports, IFundCatalogue funds, ICurrentUser user, IClock clock) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummary>> Summary(CancellationToken ct)
    {
        Page<Domain.Clients.Client> clientPage = await clients.SearchAsync(user.FirmId, null, 1, 1, ct);
        IReadOnlyList<AnalysisBase> recent = await analyses.ListRecentAsync(user.FirmId, 50, ct);
        IReadOnlyList<Domain.Analysis.Report> recentReports = await reports.ListAsync(user.FirmId, 200, ct);
        Page<Fund> fundPage = await funds.SearchAsync(null, null, null, 1, 1, ct);
        DateTime monthStart = new(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return Ok(new DashboardSummary(
            clientPage.Total,
            recent.Count(a => a.Status != AnalysisStatus.Locked),
            recentReports.Count(r => r.GeneratedAtUtc >= monthStart),
            fundPage.Total,
            [.. recent.Take(10).Select(AnalysisSummaryMapping.ToSummary)]));
    }
}
