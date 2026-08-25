using Cale.BuildingBlocks.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

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
        catch (Exception ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, title, code) = Map(ex);
        if (status >= 500)
        {
            _logger.LogError(ex, "Unhandled error");
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = code,
            Type = $"https://httpstatuses.com/{status}"
        };

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

        var title = _env.IsDevelopment()
            ? ex.Message
            : "Unexpected error.";
        return (500, title, "internal_error");
    }
}
