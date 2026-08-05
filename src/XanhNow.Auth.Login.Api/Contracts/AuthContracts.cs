namespace XanhNow.Auth.Login.Api.Contracts;

public sealed record RegisterRequest(string PhoneNumber, string Password);

public sealed record RegisterResponse(Guid UserId, string PhoneNumberMasked, string Status);

public sealed record LoginRequest(string PhoneNumber, string Password);

public sealed record LoginResponse(Guid UserId, string SessionId, DateTimeOffset ExpiresAt);

public sealed record LogoutResponse(string Message);

public sealed record ValidateSessionResponse(bool Valid, Guid UserId, string PhoneNumberMasked, DateTimeOffset ExpiresAt);

public sealed record AccountStatusResponse(Guid UserId, string MaskedPhoneNumber, string Status, DateTimeOffset UpdatedAtUtc);

public sealed record ErrorResponse(string Code, string Message, string CorrelationId);
