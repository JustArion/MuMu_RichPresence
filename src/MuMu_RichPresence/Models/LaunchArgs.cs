using System.Diagnostics.CodeAnalysis;

namespace Dawn.MuMu.RichPresence.Models;

[SuppressMessage("ReSharper", "InvertIf")]
public struct LaunchArgs
{
    internal const string RP_DISABLED_ON_START = "--rp-disabled-on-start";
    internal const string HIDE_TRAY_ICON_ON_START = "--hide-tray-icon-on-start";
    public LaunchArgs(string[] args)
    {
        RawArgs = args;
        CommandLine = string.Join(" ", args);
        RichPresenceDisabledOnStart = args.Contains(RP_DISABLED_ON_START);
        ExtendedLogging  = args.Contains("--extended-logging");
        NoFileLogging = args.Contains("--no-file-logging");
        NoAutoUpdate = args.Contains("--no-auto-update");
        HideTrayIconOnStart = args.Contains(HIDE_TRAY_ICON_ON_START);

        CustomApplicationId = ExtractArgumentValue("--custom-application-id=", args);
        HasCustomApplicationId = !string.IsNullOrWhiteSpace(CustomApplicationId);

        CustomSeqUrl = ExtractArgumentValue("--seq-url=", args);
        HasCustomSeqUrl = Uri.TryCreate(CustomSeqUrl, UriKind.Absolute, out _);

        if (int.TryParse(ExtractArgumentValue("--bind-to=", args), out var pid))
        {
            ProcessBinding = pid;
            HasProcessBinding = true;
        }
    }


    public IReadOnlyList<string> RawArgs { get; }
    public string CommandLine { get; }

    // Args
    public bool RichPresenceDisabledOnStart { get; }
    public bool HideTrayIconOnStart { get; }
    public bool NoFileLogging { get; }
    public bool ExtendedLogging { get; set; }
    public bool NoAutoUpdate { get; set; }

    public bool HasCustomApplicationId { get; }
    public string CustomApplicationId { get; }

    public bool HasCustomSeqUrl { get; }
    public string CustomSeqUrl { get; }

    public bool HasProcessBinding { get; }
    public int ProcessBinding { get; }
    // ---

    private static string ExtractArgumentValue(string argumentKey, string[] args)
    {
        var rawrArgument = args.FirstOrDefault(x => x.StartsWith(argumentKey));

        if (string.IsNullOrWhiteSpace(rawrArgument))
            return string.Empty;

        var keyValue = rawrArgument.Split('=');

        return keyValue.Length > 1 ? keyValue[1] : string.Empty;
    }
}
