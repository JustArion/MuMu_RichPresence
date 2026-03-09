#define LISTEN_TO_INTEROP
// #define LISTEN_TO_EXECUTIONS
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using CliWrap;
using Dawn.MuMu.RichPresence.Exceptions;

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
            if (!await info.ConnectInternal(useFallback: true, token) && !await info.ConnectInternal(useFallback: false, token))
                throw new NotConnectedException("Could not connect to ADB");

            return new AnonymousAsyncDisposable(async ()=> await info.Disconnect());
        }

        private async Task<bool> ConnectInternal(bool useFallback = false, CancellationToken token = default)
        {
            var port = useFallback
                ? ConnectionInfo.FALLBACK_PORT
                : info.LocalPort;

            var arg = $"connect {info.LocalIP}:{port}";

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            var ts = Stopwatch.GetTimestamp();
            var result = await Cli.Wrap(info.ADBPath)
                .WithValidation(CommandResultValidation.None)
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

            var success = result.ExitCode == 0;

            if (success)
                Log.Debug("Connected to MuMuNxDevice in {Connection:F2} sec", elapsed.TotalSeconds);

            return success;
        }

        private async Task Disconnect()
        {
            #if LISTEN_TO_INTEROP
            Log.Debug("[Exec] adb disconnect");
            #endif
            await Cli.Wrap(info.ADBPath)
                .WithArguments("disconnect")
                .ExecuteAsync();
        }

        public async Task<T> Execute<T>(string command, CancellationToken token = default) where T : IParsable<T> => await info.ExecuteRaw<T>($"shell {command}", token);
        public async Task<T> Execute<T>(string[] command, CancellationToken token = default) where T : IParsable<T> =>
            await info.ExecuteRaw<T>(["shell", ..command], token);
        public async Task<string> Execute(string command, CancellationToken token = default) => await info.ExecuteRaw<string>($"shell {command}", token);

        public async Task<string> Execute(string[] command, CancellationToken token = default) =>
            await info.ExecuteRaw<string>(["shell", ..command], token);

        public async Task<T> ExecuteRaw<T>(string[] args, CancellationToken token = default) where T : IParsable<T>
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            await Cli.Wrap(info.ADBPath)
                .WithArguments(args, true)
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
            await Cli.Wrap(info.ADBPath)
                .WithArguments(command)
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync(token);

            var output = stdOut.ToString().Trim();
            var errors = stdErr.ToString().Trim();
            var exceptions = new List<Exception>(2);
            if (string.IsNullOrWhiteSpace(errors))
                exceptions.Add(new Exception(errors));

            #if LISTEN_TO_INTEROP && LISTEN_TO_EXECUTIONS
            if (exceptions.Count == 0 && !string.IsNullOrWhiteSpace(output))
                Log.Debug("[Exec] adb {Command} -> {Result}", command, output);
            else
                Log.Debug(exceptions.First(), "[Exec] adb {Command} -> {Result}", command, output);
            #endif

            try
            {
                return T.Parse(output, CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                exceptions.Add(e);
                Log.Debug(e, "[Exec] adb {Command} -> {Result}{Errors}", command, output, errors);

                if (exceptions.Count == 1)
                    throw;

                throw new AggregateException(exceptions);
            }
        }

    }
}
