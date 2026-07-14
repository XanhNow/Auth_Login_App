namespace XanhNow.Auth.Login.Api.Contracts;

public sealed record RegisterRequest(string PhoneNumber, string Password);

public sealed record RegisterResponse(Guid UserId, string PhoneNumberMasked, string Status);

public sealed record LoginRequest(string PhoneNumber, string Password);

public sealed record LoginResponse(string SessionId, DateTimeOffset ExpiresAt);

public sealed record LogoutResponse(string Message);

public sealed record ValidateSessionResponse(bool Valid, Guid UserId, string PhoneNumberMasked, DateTimeOffset ExpiresAt);

public sealed record ErrorResponse(string Code, string Message, string CorrelationId);
