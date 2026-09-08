using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using Serilog;
using SwitchPoint.Api.Auth;
using SwitchPoint.Api.Infrastructure;
using SwitchPoint.Application;
using SwitchPoint.Application.Ports;
using SwitchPoint.Application.Services;
using SwitchPoint.Domain.Tenancy;
using SwitchPoint.Infrastructure;
using SwitchPoint.Infrastructure.Persistence;
using SwitchPoint.Reports;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddSwitchPointKeyVault();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

// --- Application layers ---
builder.Services.AddSwitchPointApplication();
builder.Services.AddSwitchPointInfrastructure(builder.Configuration);
builder.Services.AddSwitchPointReports();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

// --- MVC + JSON ---
builder.Services.AddControllers(o => o.Filters.Add<FluentValidationActionFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.JsonSerializerOptions.Converters.Add(new NormalisedDecimalConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// --- Auth ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Adviser, p => p.RequireRole(nameof(UserRole.Adviser), nameof(UserRole.Paraplanner), nameof(UserRole.FirmAdmin), nameof(UserRole.PlatformAdmin)))
    .AddPolicy(Policies.Compliance, p => p.RequireRole(nameof(UserRole.Compliance), nameof(UserRole.FirmAdmin), nameof(UserRole.PlatformAdmin)))
    .AddPolicy(Policies.FirmAdmin, p => p.RequireRole(nameof(UserRole.FirmAdmin), nameof(UserRole.PlatformAdmin)))
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// --- CORS, rate limiting, health, OpenAPI ---
string[] origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition", CorrelationIdMiddleware.HeaderName)));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/problem+json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { status = 429, title = "Too many requests.", detail = "Rate limit exceeded; retry shortly." }, ct);
    };
    // Limits are read from the request's configuration so late sources (and tests) are honoured.
    static string Partition(HttpContext ctx) => ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
    static int Limit(HttpContext ctx, string key, int fallback) => ctx.RequestServices.GetRequiredService<IConfiguration>().GetValue($"RateLimiting:{key}", fallback);
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
        Partition(ctx), _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit(ctx, "GlobalPerMinute", 600), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("calculations", ctx => RateLimitPartition.GetFixedWindowLimiter(
        $"calc:{Partition(ctx)}", _ => new FixedWindowRateLimiterOptions { PermitLimit = Limit(ctx, "CalculationsPerMinute", 60), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHealthChecks().AddDbContextCheck<SwitchPointDbContext>("database");
builder.Services.AddOpenApi("v1", o =>
{
    o.AddDocumentTransformer((doc, _, _) =>
    {
        doc.Info.Title = "SwitchPoint API";
        doc.Info.Version = "v1";
        doc.Info.Description = "Implements the FCA-prescribed pension switching, DB transfer and cashflow analyses for UK IFAs. Authenticate with POST /api/v1/auth/login and send 'Authorization: Bearer <token>'.";
        return Task.CompletedTask;
    });
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

WebApplication app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await app.Services.InitialiseDatabaseAsync(app.Environment.ContentRootPath);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

// The SPA's bundles are served before authentication runs. The fallback authorization policy
// requires an authenticated user and applies to any request the authorization middleware sees
// without explicit authorization metadata, including one that matched no endpoint at all: with the
// static file middleware registered after it, every /assets/*.js came back 401 and the browser
// rendered a blank page. Nothing under wwwroot is secret; the API below it is what needs the token.
bool hasSpa = File.Exists(Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html"));
if (hasSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapScalarApiReference("/scalar", o => o.WithTitle("SwitchPoint API").WithTheme(ScalarTheme.BluePlanet)).AllowAnonymous();
app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapControllers();

// Client-side routing: any path the API did not claim renders the SPA shell.
if (hasSpa)
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

await app.RunAsync();

/// <summary>Entry point marker used by integration tests (WebApplicationFactory).</summary>
public partial class Program;
