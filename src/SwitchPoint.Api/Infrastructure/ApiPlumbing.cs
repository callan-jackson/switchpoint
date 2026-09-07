using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SwitchPoint.Application.Exceptions;
using SwitchPoint.Calculation.Numerics;
using SwitchPoint.Domain.Common;

namespace SwitchPoint.Api.Infrastructure;

/// <summary>Maps application and domain exceptions to RFC 9457 problem details without leaking internals.</summary>
public sealed class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);
        (int status, string title, IReadOnlyDictionary<string, string[]>? errors) = exception switch
        {
            Application.Exceptions.ValidationException v => (StatusCodes.Status400BadRequest, "The request is invalid.", v.Errors),
            FluentValidation.ValidationException fv => (StatusCodes.Status400BadRequest, "The request is invalid.", ToDictionary(fv)),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found.", null),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden.", null),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict.", null),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "The request could not be processed.", null),
            RootNotBracketedException or RootNotConvergedException => (StatusCodes.Status422UnprocessableEntity, "Calculation did not converge.", null),
            ArgumentException => (StatusCodes.Status422UnprocessableEntity, "The request could not be processed.", null),
            OperationCanceledException => (StatusCodes.Status499ClientClosedRequest, "Request cancelled.", null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(exception, "Request failed with {Status} for {Method} {Path}", status, httpContext.Request.Method, httpContext.Request.Path);
        }

        ProblemDetails problem = new()
        {
            Status = status,
            Title = title,
            Type = $"https://httpstatuses.io/{status}",
            Instance = httpContext.Request.Path,
        };
        if (status < 500 || environment.IsDevelopment())
        {
            problem.Detail = status < 500 ? exception.Message : exception.ToString();
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }

    private static IReadOnlyDictionary<string, string[]> ToDictionary(FluentValidation.ValidationException fv) =>
        fv.Errors.GroupBy(e => e.PropertyName, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
}

/// <summary>Runs the registered FluentValidation validator for every body-bound action argument.</summary>
public sealed class FluentValidationActionFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        foreach (object? argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            Type validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new Application.Exceptions.ValidationException(result);
            }
        }

        await next();
    }
}

/// <summary>Adds conservative security headers to every response.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IHeaderDictionary h = context.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (!context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
        {
            h["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com data:; img-src 'self' data: blob:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        }

        await next(context);
    }
}

/// <summary>Echoes or creates an X-Correlation-Id and adds it to the log scope.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string id = context.Request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues v) && !string.IsNullOrWhiteSpace(v) && v.ToString().Length <= 64
            ? v.ToString()
            : Guid.NewGuid().ToString("N");
        context.Response.Headers[HeaderName] = id;
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}
