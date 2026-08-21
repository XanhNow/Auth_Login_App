namespace XanhNow.Auth.Login.Infrastructure.Vault;

public sealed class VaultOptions
{
    public string Address { get; set; } = "https://192.168.2.81:8200";
    public string AuthMethod { get; set; } = "approle";
    public string MountPath { get; set; } = "kv";
    public string BasePath { get; set; } = "xanhnow/s101/auth-login";
    public string RoleIdEnvironmentVariable { get; set; } = "XANHNOW_AUTH_LOGIN_VAULT_ROLE_ID";
    public string SecretIdEnvironmentVariable { get; set; } = "XANHNOW_AUTH_LOGIN_VAULT_SECRET_ID";
    public string RoleIdFile { get; set; } = string.Empty;
    public string SecretIdFile { get; set; } = string.Empty;
    public string CaCertFile { get; set; } = string.Empty;
    public string CaCertificatePath { get; set; } = string.Empty;
}
