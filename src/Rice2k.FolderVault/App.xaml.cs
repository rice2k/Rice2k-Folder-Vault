using System.Drawing;
using System.Windows;
using Rice2k.FolderVault.Models;
using Rice2k.FolderVault.Services;
using Rice2k.FolderVault.Views;
using Forms = System.Windows.Forms;

namespace Rice2k.FolderVault;

public partial class App : Application
{
    public static VaultStateService VaultState { get; } = new();
    public static AppSettings Settings { get; } = new();
    public static VaultCryptoService VaultCrypto { get; } = new();
    public static VaultContainerService VaultContainers { get; } = new(VaultCrypto);
    public static VaultRegistryService VaultRegistry { get; } = new();

    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _openOrUnlockItem;
    private Forms.ToolStripMenuItem? _lockItem;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (VaultRegistry.PrimaryVault is null)
        {
            var createVault = new CreateVaultWindow();
            if (createVault.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        _mainWindow = new MainWindow();
        ConfigureTrayIcon();

        VaultState.StateChanged += (_, _) => RefreshTrayState();

        _mainWindow.Show();
        RefreshTrayState();
    }

    private void ConfigureTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();

        _openOrUnlockItem = new Forms.ToolStripMenuItem("Unlock Vault");
        _openOrUnlockItem.Click += (_, _) => ShowMainWindow(requestUnlock: !VaultState.IsUnlocked);

        _lockItem = new Forms.ToolStripMenuItem("Lock Now");
        _lockItem.Click += (_, _) => VaultState.Lock("Manual lock from system tray");

        var settingsItem = new Forms.ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) =>
        {
            EnsureMainWindowVisible();
            _mainWindow?.OpenSettings();
        };

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _mainWindow?.AllowApplicationExit();
            Shutdown();
        };

        menu.Items.Add(_openOrUnlockItem);
        menu.Items.Add(_lockItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "Rice2k Folder Vault",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowMainWindow(requestUnlock: !VaultState.IsUnlocked);
    }

    private void ShowMainWindow(bool requestUnlock)
    {
        EnsureMainWindowVisible();

        if (requestUnlock)
            _mainWindow?.BeginUnlock();
    }

    private void EnsureMainWindowVisible()
    {
        if (_mainWindow is null)
            return;

        if (!_mainWindow.IsVisible)
            _mainWindow.Show();

        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void RefreshTrayState()
    {
        if (_trayIcon is null)
            return;

        if (_openOrUnlockItem is not null)
            _openOrUnlockItem.Text = VaultState.IsUnlocked ? "Open Vault" : "Unlock Vault";

        if (_lockItem is not null)
            _lockItem.Enabled = VaultState.IsUnlocked;

        _trayIcon.Text = VaultState.IsUnlocked
            ? "Rice2k Folder Vault — Unlocked"
            : "Rice2k Folder Vault — Locked";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        VaultState.Lock("Application exit");

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        base.OnExit(e);
    }
}
