using System.Diagnostics;

namespace Dawn.MuMu.RichPresence.Tools;

public sealed class ProcessBinding : IDisposable
{
    private readonly Action<int> _onExit;
    private readonly Process? _boundProcess;
    private CancellationTokenSource? _exitWaitCts;

    public ProcessBinding(int pid, Action<int> onExit)
    {
        _onExit = onExit;
        try
        {
            _boundProcess = Process.GetProcessById(pid);

            SubscribeOrWaitForExit(_boundProcess);

            // Log.Debug("Bound to process ({Pid})", pid);
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
        _onExit(exitCode);
    }

    public void Dispose()
    {
        // Log.Verbose("Binding on ({Pid}) {Process} is disposed", _boundProcess?.Id, _boundProcess?.ProcessName);
        _exitWaitCts?.Cancel();
        _boundProcess?.Dispose();
    }
}
