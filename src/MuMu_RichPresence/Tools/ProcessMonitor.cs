using System.Management;
using System.Reactive;
using System.Reactive.Subjects;

namespace Dawn.MuMu.RichPresence.Tools;

public class ProcessMonitor : IDisposable
{
    private readonly Subject<Unit> _started = new();
    private readonly Subject<Unit> _closed = new();
    public IObservable<Unit> Started => _started;
    public IObservable<Unit> Closed => _closed;

	private readonly ManagementEventWatcher _watcher;
    private const int POLLING_RATE_SECONDS = 5;

	public ProcessMonitor(string appName)
	{
		var queryString = $"SELECT * FROM __InstanceOperationEvent WITHIN {POLLING_RATE_SECONDS} WHERE TargetInstance ISA 'Win32_Process' AND TargetInstance.Name = '{appName}'";

		_watcher = new ManagementEventWatcher(@"\\.\root\CIMV2", queryString);
		_watcher.EventArrived += OnEventArrived;
		_watcher.Start();
	}

	private void OnEventArrived(object _, EventArrivedEventArgs args)
	{
		try
        {
            var eventType = args.NewEvent.ClassPath.ClassName;

            switch (eventType)
            {
                case "__InstanceCreationEvent":
                    _started.OnNext(Unit.Default);
                    break;
                case "__InstanceDeletionEvent":
                    _closed.OnNext(Unit.Default);
                    break;
            }
        }
		catch
		{
			// ignored
		}
	}

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _watcher.Stop();
        _watcher.Dispose();
    }
}
