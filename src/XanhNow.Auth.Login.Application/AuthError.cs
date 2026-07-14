namespace XanhNow.Auth.Login.Application;

public sealed record AuthError(string Code, int HttpStatus, string Message)
{
    public static readonly AuthError InvalidRequest = new("AUTH-400-INVALID-REQUEST", 400, "Du lieu gui len khong hop le.");
    public static readonly AuthError InvalidPhone = new("AUTH-400-INVALID-PHONE", 400, "So dien thoai khong hop le.");
    public static readonly AuthError WeakPassword = new("AUTH-400-WEAK-PASSWORD", 400, "Mat khau chua dat yeu cau bao mat.");
    public static readonly AuthError PhoneExists = new("AUTH-409-PHONE-EXISTS", 409, "So dien thoai da duoc su dung.");
    public static readonly AuthError InvalidCredentials = new("AUTH-401-INVALID-CREDENTIALS", 401, "So dien thoai hoac mat khau khong dung.");
    public static readonly AuthError SessionInvalid = new("AUTH-401-SESSION-INVALID", 401, "Phien dang nhap khong hop le hoac da het han.");
    public static readonly AuthError AccountLocked = new("AUTH-423-ACCOUNT-LOCKED", 423, "Tai khoan tam thoi khong the dang nhap.");
    public static readonly AuthError TooManyAttempts = new("AUTH-429-TOO-MANY-ATTEMPTS", 429, "Ban da thu qua nhieu lan, vui long thu lai sau.");
    public static readonly AuthError DependencyUnavailable = new("AUTH-503-DEPENDENCY-UNAVAILABLE", 503, "He thong tam thoi gian doan, vui long thu lai sau.");
}

public sealed record AuthResult<T>(T? Value, AuthError? Error)
{
    public bool Succeeded => Error is null;

    public static AuthResult<T> Success(T value) => new(value, null);

    public static AuthResult<T> Failure(AuthError error) => new(default, error);
}
