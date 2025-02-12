namespace Dawn.MuMu.RichPresence;

using Microsoft.Win32;

public static class Startup
{
    private const string STARTUP_SUBKEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
    // Write Only
    public static void StartWithWindows(string key, string pathAndArgs)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, true)!;
        startupKey.SetValue(key, pathAndArgs);

        Log.Verbose("Added {Key} to startup registry, with value: {Value}", key, pathAndArgs);
    }
    public static void RemoveStartup(string key)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, true)!;
        startupKey.DeleteValue(key, false);

        Log.Verbose("Removed {Key} from startup registry", key);
    }
    // ---

    // Read-Only
    public static bool Contains(string key, string value)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, false)!;
        var regVal = startupKey.GetValue(key);

        return regVal?.ToString()?.Contains(value) ?? false;
    }
    public static bool StartsWithWindows(string key)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, false)!;
        var regVal = startupKey.GetValue(key);

        return regVal != null;
    }
    public static bool ValidateStartsWithWindows(string key, string pathAndArgs)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, false)!;
        var regVal = startupKey.GetValue(key);

        return regVal?.ToString() == pathAndArgs;
    }

    public static string? GetValue(string key)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, false)!;

        return startupKey.GetValue(key)?.ToString();
    }
    // ---
}
