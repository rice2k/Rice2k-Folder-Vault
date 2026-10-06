using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using DokanNet;
using Rice2k.FolderVault.Models;
using DokanFileAccess = DokanNet.FileAccess;

namespace Rice2k.FolderVault.Services;

public sealed class VaultReadOnlyFileSystem : IDokanOperations
{
    private static readonly DokanFileAccess WriteAccess =
        DokanFileAccess.WriteData |
        DokanFileAccess.AppendData |
        DokanFileAccess.WriteExtendedAttributes |
        DokanFileAccess.WriteAttributes |
        DokanFileAccess.Delete |
        DokanFileAccess.DeleteChild |
        DokanFileAccess.ChangePermissions |
        DokanFileAccess.SetOwnership |
        DokanFileAccess.GenericWrite |
        DokanFileAccess.GenericAll;

    private readonly VaultRegistration _vault;
    private readonly VaultContainerService _containers;
    private readonly VaultSessionKey _sessionKey;
    private readonly Action<string>? _mountedCallback;
    private readonly Action? _unmountedCallback;

    public VaultReadOnlyFileSystem(
        VaultRegistration vault,
        VaultContainerService containers,
        VaultSessionKey sessionKey,
        Action<string>? mountedCallback = null,
        Action? unmountedCallback = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _containers = containers ?? throw new ArgumentNullException(nameof(containers));
        _sessionKey = sessionKey ?? throw new ArgumentNullException(nameof(sessionKey));
        _mountedCallback = mountedCallback;
        _unmountedCallback = unmountedCallback;
    }

    public NtStatus CreateFile(
        string fileName,
        DokanFileAccess access,
        FileShare share,
        FileMode mode,
        FileOptions options,
        FileAttributes attributes,
        IDokanFileInfo info)
    {
        if (mode != FileMode.Open || (access & WriteAccess) != 0)
            return DokanResult.AccessDenied;

        try
        {
            if (!TryResolve(fileName, out _, out var entry, out var isRoot))
                return DokanResult.FileNotFound;

            var isDirectory = isRoot ||
                string.Equals(entry?.EntryType, "directory", StringComparison.Ordinal);

            if (info.IsDirectory && !isDirectory)
                return DokanResult.NotADirectory;

            info.IsDirectory = isDirectory;
            return DokanResult.Success;
        }
        catch
        {
            return DokanResult.InternalError;
        }
    }

    public void Cleanup(string fileName, IDokanFileInfo info)
    {
        info.Context = null;
    }

    public void CloseFile(string fileName, IDokanFileInfo info)
    {
        info.Context = null;
    }

    public NtStatus ReadFile(
        string fileName,
        byte[] buffer,
        out int bytesRead,
        long offset,
        IDokanFileInfo info)
    {
        bytesRead = 0;

        try
        {
            if (!TryResolve(fileName, out _, out var entry, out var isRoot))
                return DokanResult.FileNotFound;

            if (isRoot || entry is null ||
                string.Equals(entry.EntryType, "directory", StringComparison.Ordinal))
            {
                return DokanResult.FileIsADirectory;
            }

            bytesRead = _containers.ReadFileRangeByEntryId(
                _vault.ContainerPath,
                _sessionKey,
                entry.EntryId,
                offset,
                buffer.AsSpan());

            return DokanResult.Success;
        }
        catch (FileNotFoundException)
        {
            return DokanResult.FileNotFound;
        }
        catch (InvalidDataException)
        {
            return DokanResult.InternalError;
        }
        catch
        {
            return DokanResult.InternalError;
        }
    }

    public NtStatus WriteFile(
        string fileName,
        byte[] buffer,
        out int bytesWritten,
        long offset,
        IDokanFileInfo info)
    {
        bytesWritten = 0;
        return DokanResult.AccessDenied;
    }

    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    public NtStatus GetFileInformation(
        string fileName,
        out FileInformation fileInfo,
        IDokanFileInfo info)
    {
        try
        {
            if (!TryResolve(fileName, out _, out var entry, out var isRoot))
            {
                fileInfo = default;
                return DokanResult.FileNotFound;
            }

            fileInfo = BuildInformation(
                isRoot ? fileName : entry!.Name,
                entry,
                isRoot);

            return DokanResult.Success;
        }
        catch
        {
            fileInfo = default;
            return DokanResult.InternalError;
        }
    }

    public NtStatus FindFiles(
        string fileName,
        out IList<FileInformation> files,
        IDokanFileInfo info)
    {
        files = Array.Empty<FileInformation>();

        try
        {
            if (!TryResolve(fileName, out var metadata, out var entry, out var isRoot))
                return DokanResult.PathNotFound;

            var directoryId = isRoot
                ? metadata.RootDirectoryId
                : entry is not null &&
                  string.Equals(entry.EntryType, "directory", StringComparison.Ordinal)
                    ? entry.EntryId
                    : null;

            if (directoryId is null)
                return DokanResult.NotADirectory;

            files = metadata.Entries
                .Where(e => string.Equals(
                    e.ParentDirectoryId,
                    directoryId,
                    StringComparison.Ordinal))
                .OrderByDescending(e =>
                    string.Equals(e.EntryType, "directory", StringComparison.Ordinal))
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => BuildInformation(e.Name, e, isRoot: false))
                .ToList();

            return DokanResult.Success;
        }
        catch
        {
            files = Array.Empty<FileInformation>();
            return DokanResult.InternalError;
        }
    }

    public NtStatus FindFilesWithPattern(
        string fileName,
        string searchPattern,
        out IList<FileInformation> files,
        IDokanFileInfo info)
    {
        files = Array.Empty<FileInformation>();
        return DokanResult.NotImplemented;
    }

    public NtStatus SetFileAttributes(
        string fileName,
        FileAttributes attributes,
        IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus SetFileTime(
        string fileName,
        DateTime? creationTime,
        DateTime? lastAccessTime,
        DateTime? lastWriteTime,
        IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus DeleteFile(string fileName, IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus MoveFile(
        string oldName,
        string newName,
        bool replace,
        IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus SetEndOfFile(
        string fileName,
        long length,
        IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus SetAllocationSize(
        string fileName,
        long length,
        IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus LockFile(
        string fileName,
        long offset,
        long length,
        IDokanFileInfo info) =>
        DokanResult.NotImplemented;

    public NtStatus UnlockFile(
        string fileName,
        long offset,
        long length,
        IDokanFileInfo info) =>
        DokanResult.NotImplemented;

    public NtStatus GetDiskFreeSpace(
        out long freeBytesAvailable,
        out long totalNumberOfBytes,
        out long totalNumberOfFreeBytes,
        IDokanFileInfo info)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_vault.ContainerPath));
            if (string.IsNullOrWhiteSpace(root))
                throw new IOException("Vault storage root could not be resolved.");

            var drive = new DriveInfo(root);
            freeBytesAvailable = drive.AvailableFreeSpace;
            totalNumberOfBytes = drive.TotalSize;
            totalNumberOfFreeBytes = drive.TotalFreeSpace;
            return DokanResult.Success;
        }
        catch
        {
            freeBytesAvailable = 0;
            totalNumberOfBytes = 0;
            totalNumberOfFreeBytes = 0;
            return DokanResult.InternalError;
        }
    }

    public NtStatus GetVolumeInformation(
        out string volumeLabel,
        out FileSystemFeatures features,
        out string fileSystemName,
        out uint maximumComponentLength,
        IDokanFileInfo info)
    {
        volumeLabel = string.IsNullOrWhiteSpace(_vault.DisplayName)
            ? "Rice2k Folder Vault"
            : _vault.DisplayName;

        features =
            FileSystemFeatures.CasePreservedNames |
            FileSystemFeatures.UnicodeOnDisk |
            FileSystemFeatures.ReadOnlyVolume;

        fileSystemName = "R2VAULT";
        maximumComponentLength = 255;
        return DokanResult.Success;
    }

    public NtStatus GetFileSecurity(
        string fileName,
        out FileSystemSecurity? security,
        AccessControlSections sections,
        IDokanFileInfo info)
    {
        security = null;
        return DokanResult.NotImplemented;
    }

    public NtStatus SetFileSecurity(
        string fileName,
        FileSystemSecurity security,
        AccessControlSections sections,
        IDokanFileInfo info) =>
        DokanResult.AccessDenied;

    public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
    {
        try
        {
            _mountedCallback?.Invoke(mountPoint);
        }
        catch
        {
            // Mount success must not be reversed by a UI/status callback.
        }

        return DokanResult.Success;
    }

    public NtStatus Unmounted(IDokanFileInfo info)
    {
        try
        {
            _unmountedCallback?.Invoke();
        }
        catch
        {
            // Unmount completion must not be blocked by a UI/status callback.
        }

        return DokanResult.Success;
    }

    public NtStatus FindStreams(
        string fileName,
        out IList<FileInformation> streams,
        IDokanFileInfo info)
    {
        streams = Array.Empty<FileInformation>();
        return DokanResult.NotImplemented;
    }

    private bool TryResolve(
        string fileName,
        out VaultMetadata metadata,
        out VaultEntryMetadata? entry,
        out bool isRoot)
    {
        metadata = _containers.ReadMetadata(_vault.ContainerPath, _sessionKey);
        entry = null;

        var segments = (fileName ?? string.Empty)
            .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            isRoot = true;
            return true;
        }

        isRoot = false;
        var parentId = metadata.RootDirectoryId;

        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];

            entry = metadata.Entries.FirstOrDefault(e =>
                string.Equals(e.ParentDirectoryId, parentId, StringComparison.Ordinal) &&
                string.Equals(e.Name, segment, StringComparison.OrdinalIgnoreCase));

            if (entry is null)
                return false;

            if (index < segments.Length - 1)
            {
                if (!string.Equals(entry.EntryType, "directory", StringComparison.Ordinal))
                    return false;

                parentId = entry.EntryId;
            }
        }

        return true;
    }

    private FileInformation BuildInformation(
        string name,
        VaultEntryMetadata? entry,
        bool isRoot)
    {
        var isDirectory = isRoot ||
            string.Equals(entry?.EntryType, "directory", StringComparison.Ordinal);

        var timestamp = isRoot
            ? GetVaultTimestamp()
            : ToLocalTime(entry!.LastWriteTimeUtcTicks);

        return new FileInformation
        {
            FileName = name,
            Attributes = isDirectory
                ? FileAttributes.Directory | FileAttributes.ReadOnly
                : FileAttributes.ReadOnly | FileAttributes.Archive,
            CreationTime = timestamp,
            LastAccessTime = timestamp,
            LastWriteTime = timestamp,
            Length = isDirectory ? 0 : entry!.PlaintextLength
        };
    }

    private DateTime GetVaultTimestamp()
    {
        try
        {
            return File.GetLastWriteTime(_vault.ContainerPath);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static DateTime ToLocalTime(long utcTicks)
    {
        if (utcTicks <= 0)
            return DateTime.Now;

        try
        {
            return new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
        }
        catch
        {
            return DateTime.Now;
        }
    }
}
