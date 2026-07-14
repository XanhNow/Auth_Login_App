using System.Net;
using XanhNow.Auth.Login.Api.Contracts;

namespace XanhNow.Auth.Login.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ExceptionHandlingMiddleware> logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? $"req-{Guid.NewGuid():N}";
        logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            "InternalServerError",
            "An unexpected error occurred.",
            correlationId);

        await context.Response.WriteAsJsonAsync(response);
    }
}