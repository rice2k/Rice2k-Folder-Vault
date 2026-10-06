using System;
using DokanNet;
using DokanNet.Logging;
using Rice2k.FolderVault.Models;

namespace Rice2k.FolderVault.Services;

public sealed class VaultMountService : IDisposable
{
    private readonly object _sync = new();
    private readonly VaultContainerService _containers;

    private DokanNet.Dokan? _dokan;
    private DokanInstance? _instance;
    private string? _mountPoint;

    public VaultMountService(VaultContainerService containers)
    {
        _containers = containers ?? throw new ArgumentNullException(nameof(containers));
    }

    public event EventHandler? StateChanged;

    public bool IsMounted
    {
        get
        {
            lock (_sync)
                return _instance?.IsFileSystemRunning() == true;
        }
    }

    public string? MountPoint
    {
        get
        {
            lock (_sync)
                return _mountPoint;
        }
    }

    public void MountReadOnly(
        VaultRegistration vault,
        VaultSessionKey sessionKey,
        string preferredMountPoint)
    {
        if (vault is null)
            throw new ArgumentNullException(nameof(vault));
        if (sessionKey is null)
            throw new ArgumentNullException(nameof(sessionKey));

        var requestedMountPoint = NormalizeMountPoint(preferredMountPoint);

        lock (_sync)
        {
            if (_instance?.IsFileSystemRunning() == true)
                throw new InvalidOperationException(
                    "The vault is already mounted at " + (_mountPoint ?? requestedMountPoint) + ".");

            DisposeMountObjectsLocked();
            _mountPoint = requestedMountPoint;
        }

        DokanNet.Dokan? dokan = null;
        DokanInstance? instance = null;

        try
        {
            var logger = new NullLogger();
            dokan = new DokanNet.Dokan(logger);

            var fileSystem = new VaultReadOnlyFileSystem(
                vault,
                _containers,
                sessionKey,
                mountedCallback: OnMounted,
                unmountedCallback: OnUnmounted);

            var builder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options =
                        DokanOptions.WriteProtection |
                        DokanOptions.MountManager |
                        DokanOptions.CurrentSession;

                    options.MountPoint = requestedMountPoint;
                    options.TimeOut = TimeSpan.FromSeconds(30);
                });

            instance = builder.Build(fileSystem);

            lock (_sync)
            {
                _dokan = dokan;
                _instance = instance;
                dokan = null;
                instance = null;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (DllNotFoundException ex)
        {
            instance?.Dispose();
            dokan?.Dispose();

            lock (_sync)
            {
                _mountPoint = null;
                DisposeMountObjectsLocked();
            }

            throw new InvalidOperationException(
                "The Dokany 2.x Windows runtime/driver is not installed or could not be loaded. " +
                "Install the supported Dokany runtime before mounting Rice2k Folder Vault.",
                ex);
        }
        catch
        {
            instance?.Dispose();
            dokan?.Dispose();

            lock (_sync)
            {
                _mountPoint = null;
                DisposeMountObjectsLocked();
            }

            throw;
        }
    }

    public void Unmount()
    {
        DokanNet.Dokan? dokan;
        DokanInstance? instance;
        string? mountPoint;

        lock (_sync)
        {
            dokan = _dokan;
            instance = _instance;
            mountPoint = _mountPoint;

            _dokan = null;
            _instance = null;
            _mountPoint = null;
        }

        if (instance is null && dokan is null)
            return;

        try
        {
            if (dokan is not null && !string.IsNullOrWhiteSpace(mountPoint))
            {
                try
                {
                    dokan.RemoveMountPoint(mountPoint);
                }
                catch
                {
                    // Instance disposal still runs below to release Dokan resources.
                }
            }
        }
        finally
        {
            instance?.Dispose();
            dokan?.Dispose();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnMounted(string mountPoint)
    {
        lock (_sync)
            _mountPoint = NormalizeMountPoint(mountPoint);

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnUnmounted()
    {
        lock (_sync)
            _mountPoint = null;

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DisposeMountObjectsLocked()
    {
        _instance?.Dispose();
        _instance = null;

        _dokan?.Dispose();
        _dokan = null;
    }

    private static string NormalizeMountPoint(string mountPoint)
    {
        if (string.IsNullOrWhiteSpace(mountPoint))
            throw new ArgumentException("A mount point is required.", nameof(mountPoint));

        var value = mountPoint.Trim().ToUpperInvariant();

        if (value.Length == 1 && char.IsLetter(value[0]))
            value += ":";

        if (value.Length == 2 && char.IsLetter(value[0]) && value[1] == ':')
            value += "\\";

        if (value.Length != 3 ||
            !char.IsLetter(value[0]) ||
            value[1] != ':' ||
            value[2] != '\\')
        {
            throw new ArgumentException(
                "The alpha mount point must be a Windows drive letter such as V:.",
                nameof(mountPoint));
        }

        return value;
    }

    public void Dispose()
    {
        Unmount();
        GC.SuppressFinalize(this);
    }
}
