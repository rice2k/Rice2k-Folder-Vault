namespace Rice2k.FolderVault.Models;

public sealed class VaultHeader
{
    public int FormatVersion { get; set; } = 1;
    public string VaultIdBase64 { get; set; } = string.Empty;
    public string CipherSuite { get; set; } = "AES-256-GCM";
    public string KdfAlgorithm { get; set; } = "argon2id";
    public int KdfMemorySizeKiB { get; set; } = 65536;
    public int KdfIterations { get; set; } = 4;
    public int KdfDegreeOfParallelism { get; set; } = 2;
    public string KdfSaltBase64 { get; set; } = string.Empty;
    public string WrappedMasterKeyNonceBase64 { get; set; } = string.Empty;
    public string WrappedMasterKeyCiphertextBase64 { get; set; } = string.Empty;
    public string WrappedMasterKeyTagBase64 { get; set; } = string.Empty;
}
