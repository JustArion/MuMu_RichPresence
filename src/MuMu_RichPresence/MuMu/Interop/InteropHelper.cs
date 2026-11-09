#define LISTEN_TO_INTEROP
#if DEBUG
// #define LISTEN_TO_EXECUTIONS
#endif
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CliWrap;
using Dawn.MuMu.RichPresence.Models.Exceptions;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

[SuppressMessage("ReSharper", "InvokeAsExtensionMember")]
public static class InteropHelper
{
    extension(ConnectionInfo info)
    {
        public async Task<IAsyncDisposable> Connect(CancellationToken token = default)
        {
            // We do the fallback first then the specified port
            // On my observation, the fallback port is more used in my case, this should theoretically shave off +- 5sec to initial ADB connections
            if (!await info.ConnectInternal(useFallback: false, token) && !await info.ConnectInternal(useFallback: true, token))
                throw new NotConnectedException("Could not connect to ADB");

            return new AnonymousAsyncDisposable(async ()=> await info.Disconnect());
        }

        private async Task<bool> ConnectInternal(bool useFallback = false, CancellationToken token = default)
        {
            var port = useFallback
                ? ConnectionInfo.FALLBACK_PORT
                : info.LocalPort;

            if (useFallback)
                Log.Debug("Using fallback port of {FallbackPort} instead of {LocalPort}", ConnectionInfo.FALLBACK_PORT, info.LocalPort);

            var arg = $"connect {info.LocalIP}:{port}";

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            var ts = Stopwatch.GetTimestamp();
            var result = await Cli.Wrap(info.ADBPath)
                .WithValidation(CommandResultValidation.None)
                .WithWorkingDirectory(info.WorkingDirectory)
                .WithArguments(arg)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync(token);
            var elapsed = Stopwatch.GetElapsedTime(ts);

            var @out = stdOut.ToString().Trim();
            var err = stdErr.ToString().Trim();

            #if LISTEN_TO_INTEROP
            Log.Debug("[Exec] adb {Args} -> {Output}{StdErr}", arg, @out, err);
            #endif

            const string CONNECTION_FAILED_INDICATOR = "cannot connect";
            if (@out.Contains(CONNECTION_FAILED_INDICATOR) || err.Contains(CONNECTION_FAILED_INDICATOR))
                return false;

            if (result.ExitCode != 0)
                return false;

            Log.Debug("Connected to MuMuNxDevice in {Connection:F2} sec", elapsed.TotalSeconds);
            if (useFallback)
                info.LocalPort = ConnectionInfo.FALLBACK_PORT;

            return true;
        }

        private async Task Disconnect()
        {
            #if LISTEN_TO_INTEROP
            Log.Debug("[Exec] adb disconnect {Instance}", info.ConnectionString);
            #endif
            await Cli.Wrap(info.ADBPath)
                .WithArguments("disconnect")
                .ExecuteAsync();
        }

        public async Task<T> Execute<T>(string command, CancellationToken token = default) where T : IParsable<T> =>
            await info.ExecuteRaw<T>($"shell {command}", token);
        public async Task<T> Execute<T>(string[] command, CancellationToken token = default) where T : IParsable<T> =>
            await info.ExecuteRaw<T>(["shell", ..command], token);
        public async Task<string> Execute(string command, CancellationToken token = default) =>
            await info.ExecuteRaw<string>($"shell {command}", token);

        public async Task<string> Execute(string[] command, CancellationToken token = default) =>
            await info.ExecuteRaw<string>(["shell", ..command], token);

        public async Task<T> ExecuteRaw<T>(string[] args, CancellationToken token = default) where T : IParsable<T>
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            args = ["-s", info.ConnectionString, .. args];

            await Cli.Wrap(info.ADBPath)
                .WithArguments(args, true)
                .WithWorkingDirectory(info.WorkingDirectory)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync(token);

            var val = T.Parse(stdOut.ToString().Trim(), CultureInfo.InvariantCulture);

            #if LISTEN_TO_INTEROP && LISTEN_TO_EXECUTIONS
            var errors = stdErr.ToString().Trim();
            if (string.IsNullOrWhiteSpace(errors))
                Log.Debug("[Exec] adb {Command} -> {Result}", string.Join(" ", args), val);
            else
                Log.Error(new Exception(errors), "[Exec] adb {Command} -> {Result}", string.Join(" ", args), val);
            #endif
            return val;
        }

        public async Task<T> ExecuteRaw<T>(string command, CancellationToken token = default) where T : IParsable<T>
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            string[] args = ["-s", info.ConnectionString, command];

            await Cli.Wrap(info.ADBPath)
                .WithArguments(args, false)
                .WithWorkingDirectory(info.WorkingDirectory)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync(token);

            var output = stdOut.ToString().Trim();
            var errors = stdErr.ToString().Trim();
            var exceptions = new List<Exception>(2);
            if (!string.IsNullOrWhiteSpace(errors))
                exceptions.Add(new Exception(errors));

            #if LISTEN_TO_INTEROP && LISTEN_TO_EXECUTIONS
            if (exceptions.Count == 0)
                Log.Debug("[Exec] adb {Command} -> {Result}", string.Join(" ", args), output);
            else
                Log.Debug(exceptions.First(), "[Exec] adb {Command} -> {Result}",  string.Join(" ", args), output);
            #endif

            try
            {
                return T.Parse(output, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
                Log.Debug(e, "[Exec] adb {Command} -> {Result}{Errors}", string.Join(" ", args), output, errors);

                if (exceptions.Count == 1)
                    throw;

                throw new AggregateException(exceptions);
            }
        }

    }
}
