using System.Diagnostics;
using Dawn.MuMu.RichPresence.Models;
using Dawn.MuMu.RichPresence.Tools;

namespace Dawn.MuMu.RichPresence.Tray;

using Serilog;
using WinForms.ContextMenu;

public class RichPresence_Tray
{
    internal NotifyIcon Tray { get; }
    public RichPresence_Tray()
    {
        Tray = new();

        Tray.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Tray.Text = Application.ProductName;
        Tray.Visible = !Arguments.HideTrayIconOnStart;

        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Tray.Disposed += OnTrayDisposed;

        Tray.ContextMenuStrip = new RiotContextMenuStrip();

        AddStripItems(Tray.ContextMenuStrip.Items);
    }

    private static void StartProcess(Action start)
    {
        try
        {
            start();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to start process");
        }

    }

    private void AddStripItems(ToolStripItemCollection items)
    {
        items.AddRange(Header());
        try
        {
            items.Add("Open App Directory", null, (_, _) => StartProcess(()=> Process.Start("explorer", $"/select,\"{Application.ExecutablePath}\"")));
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

    private static void LogInteractionsRecursively(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.Click += (_, _) => Log.Verbose("OnMenuItemClick: {MenuItemText}", item.Text);

            if (item is ToolStripMenuItem menuItem)
                LogInteractionsRecursively(menuItem.DropDownItems);
        }
    }

    public event EventHandler<bool> RichPresenceEnabledChanged = delegate { };

    private ToolStripMenuItem Enabled()
    {
        ApplicationFeatures.SyncFeature(f => f.RichPresenceEnabled, !Arguments.RichPresenceDisabledOnStart);

        var enabledItem = new ToolStripMenuItem("Enabled");

        enabledItem.Checked = !Arguments.RichPresenceDisabledOnStart;

        enabledItem.Click += (_, _) =>
        {
            var enabled = !enabledItem.Checked;

            ChangeEnabledStateOnStartupIfNecessary(enabled);

            ApplicationFeatures.SetFeature(f => f.RichPresenceEnabled, enabled);
            enabledItem.Checked = enabled;

            RichPresenceEnabledChanged.Invoke(this, enabled);
        };

        return enabledItem;
    }

    private static void ChangeEnabledStateOnStartupIfNecessary(bool enabled)
    {
        if (!Startup.StartsWithWindows(Application.ProductName!, Application.ExecutablePath))
            return;

        Startup.StartWithWindows(Application.ProductName!,
            $"\"{Application.ExecutablePath}\" {(
                enabled
                    ? Arguments.CommandLine.Replace(LaunchArgs.RP_DISABLED_ON_START, string.Empty)
                    : $"{Arguments.CommandLine} {LaunchArgs.RP_DISABLED_ON_START}")}");
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
