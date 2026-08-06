using Microsoft.AspNetCore.Mvc;
using XanhNow.Auth.Login.Api.Contracts;
using XanhNow.Auth.Login.Api.Middleware;
using XanhNow.Auth.Login.Application;
using XanhNow.Auth.Login.Application.Models;
using XanhNow.Auth.Login.Application.UseCases;

namespace XanhNow.Auth.Login.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] RegisterUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new RegisterUserCommand(request.PhoneNumber, request.Password, CorrelationId()),
            cancellationToken);

        if (!result.Succeeded)
        {
            return Error(result.Error!);
        }

        var value = result.Value!;
        return Created("/api/auth/register", new RegisterResponse(value.UserId, value.PhoneNumberMasked, value.Status));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] LoginUserHandler handler,
        CancellationToken cancellationToken)
    {
        var userAgentHash = Request.Headers.UserAgent.ToString();
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await handler.HandleAsync(
            new LoginUserCommand(request.PhoneNumber, request.Password, clientIp, userAgentHash, CorrelationId()),
            cancellationToken);

        if (!result.Succeeded)
        {
            return Error(result.Error!);
        }

        var value = result.Value!;
        return Ok(new LoginResponse(value.UserId, value.SessionId, value.ExpiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromServices] LogoutUserHandler handler,
        CancellationToken cancellationToken)
    {
        var sessionId = ReadSessionId();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error(AuthError.SessionInvalid);
        }

        var result = await handler.HandleAsync(new LogoutUserCommand(sessionId, CorrelationId()), cancellationToken);
        if (!result.Succeeded)
        {
            return Error(result.Error!);
        }

        return Ok(new LogoutResponse(result.Value!.Message));
    }

    [HttpGet("session")]
    public async Task<IActionResult> ValidateSession(
        [FromServices] ValidateSessionHandler handler,
        CancellationToken cancellationToken)
    {
        var sessionId = ReadSessionId();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Error(AuthError.SessionInvalid);
        }

        var result = await handler.HandleAsync(new ValidateSessionQuery(sessionId, CorrelationId()), cancellationToken);
        if (!result.Succeeded)
        {
            return Error(result.Error!);
        }

        var value = result.Value!;
        return Ok(new ValidateSessionResponse(value.Valid, value.UserId, value.PhoneNumberMasked, value.ExpiresAt));
    }

    [HttpGet("/internal/v1/accounts/{userId:guid}/status")]
    public async Task<IActionResult> GetAccountStatus(
        Guid userId,
        [FromServices] GetAccountStatusHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetAccountStatusQuery(userId), cancellationToken);
        if (!result.Succeeded)
        {
            return Error(result.Error!);
        }

        var value = result.Value!;
        return Ok(new AccountStatusResponse(value.UserId, value.MaskedPhoneNumber, value.Status, value.UpdatedAtUtc));
    }

    [HttpPost("/internal/v1/accounts/{userId:guid}/state")]
    public async Task<IActionResult> ChangeAccountState(
        Guid userId,
        [FromBody] AccountStateChangeRequest request,
        [FromServices] ChangeAccountStateHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ChangeAccountStateCommand(userId, request.TargetState, request.ReasonCode, request.Comment), cancellationToken);
        if (!result.Succeeded)
        {
            return Error(result.Error!);
        }

        var value = result.Value!;
        return Ok(new AccountStateChangeResponse(value.UserId, value.Status, value.ChangedAtUtc));
    }

    private string CorrelationId() => HttpContext.Items[CorrelationIdMiddleware.ItemName]?.ToString() ?? $"req-{Guid.NewGuid():N}";

    private string? ReadSessionId()
    {
        if (Request.Headers.TryGetValue("X-Session-Id", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        return Request.Cookies.TryGetValue("auth_login_session", out var cookieValue) ? cookieValue : null;
    }

    private IActionResult Error(AuthError error)
    {
        return StatusCode(error.HttpStatus, new ErrorResponse(error.Code, error.Message, CorrelationId()));
    }
}
