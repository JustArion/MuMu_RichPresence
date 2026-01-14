using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reactive.Disposables;
using System.Text;
using CliWrap;

namespace Dawn.MuMu.RichPresence.MuMu.Interop;

[SuppressMessage("ReSharper", "InvokeAsExtensionMember")]
public static class InteropHelper
{
    extension(ConnectionInfo info)
    {
        public async Task<IAsyncDisposable> Connect()
        {
            if (!await info.ConnectInternal() && !await info.ConnectInternal(true))
                throw new Exception("Could not connect to ADB");

            return new AnonymousAsyncDisposable(async ()=> await info.Disconnect());
        }

        private async Task<bool> ConnectInternal(bool useFallback = false)
        {
            var port = useFallback
                ? ConnectionInfo.FALLBACK_PORT
                : info.LocalPort;

            var arg = $"connect {info.LocalIP}:{port}";

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            var result = await Cli.Wrap(info.ADBPath)
                .WithValidation(CommandResultValidation.None)
                .WithArguments(arg)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync();

            var @out = stdOut.ToString();
            var err = stdErr.ToString();
            Log.Debug("[Exec] adb {Args} -> {Output}{Errors}", arg, @out, err);

            const string ERROR = "cannot connect";
            if (@out.Contains(ERROR) || err.Contains(ERROR))
                return false;

            return result.ExitCode == 0;
        }

        private async Task Disconnect()
        {
            Log.Debug("[Exec] adb disconnect");
            await Cli.Wrap(info.ADBPath)
                .WithArguments("disconnect")
                .ExecuteAsync();
        }

        public async Task<T> Execute<T>(string command) where T : IParsable<T>
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            await Cli.Wrap(info.ADBPath)
                .WithArguments(["shell", command])
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync();

            var val = T.Parse(stdOut.ToString().Trim(), CultureInfo.InvariantCulture);
            var errors = stdErr.ToString().Trim();
            Log.Debug("[Exec] adb shell {Command} -> {Result}{Errors}", command, val, errors);
            return val;
        }

        public async Task<string> Execute(string command)
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            await Cli.Wrap(info.ADBPath)
                .WithArguments(["shell", command])
                .WithValidation(CommandResultValidation.None)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .ExecuteAsync();

            var val = stdOut.ToString().Trim();
            var errors = stdErr.ToString().Trim();
            Log.Debug("[Exec] adb shell {Command} -> {Result}{Errors}", command, val, errors);

            return errors.Length > 0
                ? throw new Exception(errors)
                : val;
        }
    }
}
