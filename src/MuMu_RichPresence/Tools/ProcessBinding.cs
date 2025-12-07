using System.Diagnostics;

namespace Dawn.MuMu.RichPresence.Tools;

internal sealed class ProcessBinding : IDisposable
{
    private readonly Process? _boundProcess;
    private CancellationTokenSource? _exitWaitCts;

    public ProcessBinding(int pid)
    {
        try
        {
            _boundProcess = Process.GetProcessById(pid);

            SubscribeOrWaitForExit(_boundProcess);

            Log.Information("Bound to process ({Pid})", pid);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to bind to process ({Pid})", pid);
        }

    }

    private void SubscribeOrWaitForExit(Process proc)
    {
        try
        {
            proc.EnableRaisingEvents = true;
            proc.Exited += OnProcessExit;
        }
        catch (Exception e)
        {
            Log.Verbose(e, "Unable to get notified when '{ProceName}' ({Pid}) exits. Spawning wait task instead", proc.ProcessName, proc.Id);

            _exitWaitCts = new();
            Task.Factory.StartNew(()=> WaitForProcessExitAsync(_exitWaitCts.Token), TaskCreationOptions.LongRunning);
        }
    }

    private async Task WaitForProcessExitAsync(CancellationToken token = default)
    {
        await _boundProcess!.WaitForExitAsync(token);
        OnProcessExit(this, EventArgs.Empty);
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        var exitCode = _boundProcess!.ExitCode;
        Log.Information("Bound process has exited (Exit Code: {ExitCode})", exitCode);
        Environment.Exit(exitCode);
    }

    public void Dispose()
    {
        _exitWaitCts?.Cancel();
        _boundProcess?.Dispose();
    }
}
