using System.Diagnostics;

namespace Dawn.MuMu.RichPresence.Tools;

internal static class ProcessExit
{
    internal delegate void AppExit(int exitCode);
    internal static void Subscribe(int processId, AppExit onExit, CancellationToken cts)
    {
        try
        {
            var process = Process.GetProcessById(processId);

            try
            {
                EventHandler del = (_, _) =>
                {
                    onExit(process.ExitCode);
                    process.Dispose();
                };

                process.EnableRaisingEvents = true;
                process.Exited += del;
                cts.Register(() =>
                {
                    process.Exited -= del;
                    // Log.Verbose("Unsubscribed from app exit for {ProcessName}", $"{process.ProcessName}.exe");
                });

                // Log.Verbose("Subscribed to app exit for {ProcessName}", $"{process.ProcessName}.exe");
            }
            catch (AccessViolationException e)
            {
                Log.Warning(e, "Failed to subscribe to app exit, using fallback");
                Task.Run(async () =>
                {
                    try
                    {
                        await process.WaitForExitAsync(cts);
                        onExit(process.ExitCode);
                        process.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Log.Error(exception, "Failed to subscribe to app exit");
                    }

                }, cts);
            }

        }
        catch (ArgumentException)
        {
            // app is not running
        }
    }

    internal static void Subscribe(string processName, AppExit onExit, CancellationToken cts)
    {
        var process = Process.GetProcessesByName(processName).OrderBy(x => x.StartTime).FirstOrDefault();
        if (process is null)
        {
            Log.Warning("Process {ProcessName} not found", processName);
            return;
        }

        try
        {
            EventHandler del = (_, _) =>
            {
                onExit(process.ExitCode);
                process.Dispose();
            };

            process.EnableRaisingEvents = true;
            process.Exited += del;
            cts.Register(() =>
            {
                process.Exited -= del;
                // Log.Verbose("Unsubscribed from app exit for {ProcessName}", $"{processName}.exe");
            });

            // Log.Verbose("Subscribed to app exit for {ProcessName}", $"{processName}.exe");
        }
        catch (AccessViolationException e)
        {
            Log.Warning(e, "Failed to subscribe to app exit, using fallback");
            Task.Run(async () =>
            {
                try
                {
                    await process.WaitForExitAsync(cts);
                    onExit(process.ExitCode);
                    process.Dispose();
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Failed to subscribe to app exit");
                }

            }, cts);
        }

    }
}
