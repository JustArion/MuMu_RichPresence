// All launch arguments here are converted to lowercase-kebab-case when parsing
// This means that when you see Contains("Extended Logging") it checks the CLI args for "--extended-logging"
// Or the .env file for "extended-logging"
// This makes launch args more readable in code

using System.Diagnostics.CodeAnalysis;

namespace Dawn.MuMu.RichPresence.Models;

[SuppressMessage("ReSharper", "InvertIf")]
public struct LaunchArgs
{
    internal const string RP_DISABLED_ON_START = "RP Disabled On Start";

    public LaunchArgs(string[] args)
    {
        RawArgs = args;
        CommandLine = string.Join(" ", args);
        RichPresenceDisabledOnStart = Contains(RP_DISABLED_ON_START, args);
        ExtendedLogging = Contains("Extended Logging", args);
        NoFileLogging = Contains("No File Logging", args);
        NoAutoUpdate = Contains("No Auto Update", args);
        HideTrayIconOnStart = Contains("Hide Tray Icon On Start", args);

        CustomApplicationId = ExtractArgumentValue("Custom Application ID", args);
        HasCustomApplicationId = !string.IsNullOrWhiteSpace(CustomApplicationId);

        CustomSeqUrl = ExtractArgumentValue("SEQ URL", args);
        HasCustomSeqUrl = Uri.TryCreate(CustomSeqUrl, UriKind.Absolute, out _);

        if (int.TryParse(ExtractArgumentValue("Bind To", args), out var pid))
        {
            ProcessBinding = pid;
            HasProcessBinding = true;
        }

        ExperimentalADB = Contains("Experimental", args);
    }

    public IReadOnlyList<string> RawArgs { get; }
    public string CommandLine { get; }

    // Args
    public bool RichPresenceDisabledOnStart { get; }
    public bool HideTrayIconOnStart { get; }
    public bool NoFileLogging { get; }
    public bool ExtendedLogging { get; init; }
    public bool NoAutoUpdate { get; }
    public bool ExperimentalADB { get; }

    public bool HasCustomApplicationId { get; }
    public string CustomApplicationId { get; }

    public bool HasCustomSeqUrl { get; }
    public string CustomSeqUrl { get; }

    public bool HasProcessBinding { get; }
    public int ProcessBinding { get; }
    // ---

    private static string ExtractArgumentValue(string argumentKey, string[] args)
    {
        argumentKey = ToKebabCase(argumentKey);
        var rawArgument = args.FirstOrDefault(x => x.StartsWith($"--{argumentKey}=", StringComparison.InvariantCultureIgnoreCase));

        if (string.IsNullOrWhiteSpace(rawArgument))
            return Environment.GetEnvironmentVariable(argumentKey) ?? string.Empty;

        var keyValue = rawArgument.Split('=');

        return keyValue.Length > 1 ? keyValue[1] : string.Empty;
    }

    private static bool Contains(string key, string[] cliArgs) => cliArgs.Contains($"--{key = ToKebabCase(key)}", StringComparer.InvariantCultureIgnoreCase) || Environment.GetEnvironmentVariable(key) is not null;
    internal static string ToKebabCase(string str) => str.ToLower().Replace(' ', '-');
}
