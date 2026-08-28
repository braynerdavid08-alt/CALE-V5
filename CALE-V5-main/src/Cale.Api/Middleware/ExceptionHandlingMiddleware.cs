using System.Security.Claims;
using Cale.BuildingBlocks.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client navigated away or closed the connection while a query was in flight.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var requestId = RequestTelemetryMiddleware.GetRequestId(context);
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-";
        var (status, title, code) = Map(ex);

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["UserId"] = userId,
            ["ErrorCode"] = code,
            ["StatusCode"] = status
        }))
        {
            if (status >= 500)
            {
                _logger.LogError(
                    ex,
                    "Unhandled error {ErrorCode} on {Method} {Path} RequestId={RequestId} UserId={UserId}",
                    code,
                    context.Request.Method,
                    context.Request.Path.Value,
                    requestId,
                    userId);
            }
            else if (ex is UnauthorizedException or ForbiddenException)
            {
                _logger.LogWarning(
                    "Auth failure {ErrorCode} on {Method} {Path} RequestId={RequestId} UserId={UserId}",
                    code,
                    context.Request.Method,
                    context.Request.Path.Value,
                    requestId,
                    userId);
            }
            else if (ex is DomainException)
            {
                _logger.LogWarning(
                    "Domain error {ErrorCode}: {Title} on {Method} {Path} RequestId={RequestId} UserId={UserId}",
                    code,
                    title,
                    context.Request.Method,
                    context.Request.Path.Value,
                    requestId,
                    userId);
            }
            else
            {
                _logger.LogWarning(
                    ex,
                    "Request failed {ErrorCode} status {StatusCode} RequestId={RequestId} UserId={UserId}",
                    code,
                    status,
                    requestId,
                    userId);
            }
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers[RequestTelemetryMiddleware.HeaderName] = requestId;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = code,
            Type = $"https://httpstatuses.com/{status}",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = requestId;

        await context.Response.WriteAsJsonAsync(problem);
    }

    private (int Status, string Title, string Code) Map(Exception ex)
    {
        if (ex is DomainException domain)
        {
            return (domain.StatusCode, domain.Message, domain.ErrorCode);
        }

        if (ex is UnauthorizedAccessException)
        {
            return (401, "Unauthorized.", "unauthorized");
        }

        if (ex is DbUpdateException)
        {
            return (
                500,
                _env.IsDevelopment() ? ex.InnerException?.Message ?? ex.Message : "Database error.",
                "db_error");
        }

        if (ex is TimeoutException)
        {
            return (504, "Operation timed out.", "timeout");
        }

        if (ex is OperationCanceledException)
        {
            return (499, "Request canceled.", "request_canceled");
        }

        var title = _env.IsDevelopment()
            ? ex.Message
            : "Unexpected error.";
        return (500, title, "internal_error");
    }
}
