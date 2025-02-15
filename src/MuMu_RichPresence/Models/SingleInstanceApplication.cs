using System.Diagnostics;
using System.Text;

namespace Dawn.MuMu.RichPresence.Models;

public static class SingleInstanceApplication
{
    [Conditional("RELEASE")]
    public static void Ensure()
    {
        var name = Application.ProductName!;
        name = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));

        var mutex = new Mutex(true, name, out var createdNew);

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            mutex.Dispose();
            Log.CloseAndFlush();
        };

        if (createdNew)
        {
            GC.KeepAlive(mutex);
            Log.Verbose("Mutex registered as {ProductName} [{Base64ProductName}]", Application.ProductName!, name);
            return;
        }

        CloseImposterProcess();
    }

    /// <summary>
    /// If the previous instance is open but non-responsive. We kill it, otherwise we terminate our current process
    /// </summary>
    private static void CloseImposterProcess()
    {
        var currentProcess = Process.GetCurrentProcess();

        var otherProcess = Process.GetProcessesByName(currentProcess.ProcessName).FirstOrDefault(p => p.Id != currentProcess.Id);

        if (otherProcess == null)
            return;

        if (otherProcess.Responding == false && otherProcess.StartTime < currentProcess.StartTime)
        {
            try
            {
                Log.Debug("Duplicate process is not responding and has a singleton lock. Killing duplicate process[{DuplicateId}]", otherProcess.Id);
                otherProcess.Kill();
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to kill other process: {Pid}", otherProcess.Id);
            }

            return;
        }

        Log.Warning("Another instance of the application is already running '{ProcessName}' ({OtherProcessId})", otherProcess.ProcessName + ".exe", otherProcess.Id);
        Environment.Exit(1);
    }
}
