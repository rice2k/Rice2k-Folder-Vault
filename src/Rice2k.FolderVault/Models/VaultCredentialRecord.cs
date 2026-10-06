namespace Rice2k.FolderVault.Models;

public sealed class VaultCredentialRecord
{
    public int FormatVersion { get; set; } = 1;
    public string Algorithm { get; set; } = "argon2id";
    public string SaltBase64 { get; set; } = string.Empty;
    public string PasswordVerifierBase64 { get; set; } = string.Empty;
    public int MemorySizeKiB { get; set; } = 65536;
    public int Iterations { get; set; } = 4;
    public int DegreeOfParallelism { get; set; } = 2;
}
