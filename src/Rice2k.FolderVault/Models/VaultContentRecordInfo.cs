namespace Rice2k.FolderVault.Models;

public sealed class VaultContentRecordInfo
{
    public string RecordIdBase64 { get; set; } = string.Empty;
    public long PlaintextLength { get; set; }
    public int ChunkSize { get; set; }
    public int ChunkCount { get; set; }
}
