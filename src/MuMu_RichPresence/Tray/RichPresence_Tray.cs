using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.Tray;

using Serilog;
using WinForms.ContextMenu;

public class RichPresence_Tray
{
    private readonly BehaviorSubject<FileInfo?> _logFile;
    internal NotifyIcon Tray { get; }

    private ToolStripItemCollection? Items => Tray.ContextMenuStrip?.Items;
    public RichPresence_Tray(BehaviorSubject<FileInfo?> logFile)
    {
        _logFile = logFile;
        Tray = new();

        Tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Tray.Text = Application.ProductName;
        Tray.Visible = !Arguments.HideTrayIconOnStart;

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Tray.Disposed += OnTrayDisposed;

        Tray.ContextMenuStrip = new RiotContextMenuStrip();

        AddStripItems(Items!);
    }

    private static void WrapProcessStart(Action start)
    {
        Task.Run(() =>
        {
            try
            {
                start();
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to start process");
            }
        });
    }

    private void AddStripItems(ToolStripItemCollection items)
    {
        items.AddRange(Header());
        try
        {
            items.Add("Open App Directory", null, (_, _) => WrapProcessStart(()=> Process.Start("explorer", $"/select,\"{Application.ExecutablePath}\"")));
            items.AddRange(AddExtendedLoggingFeatures());
            items.AddRange(AddVelopackFeatures());
            items.Add(Enabled());
            items.Add(RunOnStartup());
            items.Add(HideTray());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add tray items");
        }
        finally
        {
            items.Add(Exit());

            LogInteractionsRecursively(items);
        }
    }

    private ToolStripItem[] AddVelopackFeatures()
    {
        if (AutoUpdate.UpdateManager is { IsInstalled: false })
            return []; // We're using Standalone

        // Index 1 is right under the program name header and under the separator
        if (AutoUpdate.HasPendingUpdate.Value)
            Items?.Insert(1, AddPendingUpdateMessage());
        else
            AutoUpdate.HasPendingUpdate
                .Where(x => x)
                .ObserveOn(SynchronizationContext.Current!)
                .Subscribe(_ => Items?.Insert(1, AddPendingUpdateMessage()));

        var downloadPreReleases = new ToolStripMenuItem("Download Pre-Releases");
        downloadPreReleases.Checked = Arguments.CheckPreReleases;

        downloadPreReleases.Click += (_, _) =>
        {
            var enabled = !downloadPreReleases.Checked;

            ChangeEnabledStateOnStartupIfNecessary(enabled);

            downloadPreReleases.Checked = Features.CheckPreReleases = enabled;
            if (enabled)
                Task.Run(AutoUpdate.CheckForUpdates);
        };

        return [downloadPreReleases];
    }

    private static ToolStripMenuItem AddPendingUpdateMessage()
    {
        var pendingUpdate = new ToolStripMenuItem("Apply Pending Update");
        pendingUpdate.Click += (_, _) =>
        {
            if (AutoUpdate.UpdateManager is not { } manager)
                return;

            manager.ApplyUpdatesAndRestart(manager.UpdatePendingRestart);
        };

        return pendingUpdate;
    }

    private ToolStripItem[] AddExtendedLoggingFeatures()
    {
        // The ADB approach accesses no log file, so we just remove the option
        if (Arguments.ExperimentalADB)
            return [];

        Log.Verbose("Adding extended logging features");
        var openLogFileItem = new ToolStripMenuItem("Open Log File", null, (_, _) =>
        {
            WrapProcessStart(() =>
            {
                if (_logFile.Value is { Exists: true } fileInfo)
                    WrapProcessStart(()=> Process.Start(new ProcessStartInfo(fileInfo.FullName) { UseShellExecute = true }));
            });
        });
        openLogFileItem.Enabled = _logFile.Value?.Exists ?? false;
        _logFile
            .ObserveOn(SynchronizationContext.Current!)
            .Subscribe(info => openLogFileItem.Enabled = info?.Exists ?? false);

        return [openLogFileItem];
    }

    private static void LogInteractionsRecursively(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.Click += (_, _) => Log.Verbose("OnMenuItemClick: {MenuItemText}", item.Text?.Trim());

            if (item is ToolStripMenuItem menuItem)
                LogInteractionsRecursively(menuItem.DropDownItems);
        }
    }

    private static ToolStripMenuItem Enabled()
    {
        var enabledItem = new ToolStripMenuItem("Enabled");

        enabledItem.Checked = Arguments.RichPresenceEnabledOnStart;

        enabledItem.Click += (_, _) =>
        {
            var enabled = !enabledItem.Checked;

            ChangeEnabledStateOnStartupIfNecessary(enabled);

            enabledItem.Checked = Features.RichPresenceEnabled = enabled;
        };

        return enabledItem;
    }

    private static void ChangeEnabledStateOnStartupIfNecessary(bool enabled)
    {
        var arg = $"--{LaunchArgs.ToKebabCase(LaunchArgs.RP_DISABLED_ON_START)}";

        ChangeArgumentStateOnStartupIfNecessary(enabled, arg);
    }

    private static void ChangeBranchStateOnStartupIfNecessary(bool enabled)
    {
        var arg = $"--{LaunchArgs.ToKebabCase(LaunchArgs.CHECK_PRE_RELEASES)}";

        ChangeArgumentStateOnStartupIfNecessary(enabled, arg);
    }

    private static void ChangeArgumentStateOnStartupIfNecessary(bool enabled, string arg)
    {
        if (!Startup.StartsWithWindows(Application.ProductName!, Application.ExecutablePath))
            return;

        Startup.StartWithWindows(Application.ProductName!,
            $"\"{Application.ExecutablePath}\" {(
                enabled
                    ? Arguments.CommandLine.Replace(arg, string.Empty)
                    : $"{Arguments.CommandLine} {arg}")}");
    }

    private ToolStripMenuItem HideTray() => new("Hide Tray", null, (_, _) => Tray.Visible = false);

    private static ToolStripMenuItem RunOnStartup()
    {
        var startup = new ToolStripMenuItem("Run on Startup");
        startup.Checked = Startup.StartsWithWindows(Application.ProductName!, Application.ExecutablePath);

        startup.Click += delegate
        {
            if (startup.Checked)
                Startup.RemoveStartup(Application.ProductName!);
            else
                Startup.StartWithWindows(Application.ProductName!, $"\"{Application.ExecutablePath}\" {Arguments.CommandLine}");

            startup.Checked = !startup.Checked;
        };

        return startup;
    }

    private ToolStripMenuItem Exit() => new("Exit", null, (_, _) => Tray.Dispose());

    private static ToolStripItem[] Header()
    {
        var header = new ToolStripMenuItem("MuMu Player Rich Presence");
        header.Enabled = false;

        var separator = new ToolStripSeparator();

        return [header, separator];
    }

    private void OnTrayDisposed(object? sender, EventArgs e)
    {
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Application.Exit();
    }

    private void OnProcessExit(object? sender, EventArgs e) => Tray.Visible = false;
}
