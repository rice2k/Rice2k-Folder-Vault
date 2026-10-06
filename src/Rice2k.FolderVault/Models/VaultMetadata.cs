using System.Collections.Generic;

namespace Rice2k.FolderVault.Models;

public sealed class VaultMetadata
{
    public int MetadataVersion { get; set; } = 1;
    public long Revision { get; set; }
    public string RootDirectoryId { get; set; } = "root";
    public List<VaultEntryMetadata> Entries { get; set; } = new();
}

public sealed class VaultEntryMetadata
{
    public string EntryId { get; set; } = string.Empty;
    public string ParentDirectoryId { get; set; } = "root";
    public string Name { get; set; } = string.Empty;
    public string EntryType { get; set; } = "file";
    public long PlaintextLength { get; set; }
    public long LastWriteTimeUtcTicks { get; set; }
    public string ContentRecordId { get; set; } = string.Empty;
}
