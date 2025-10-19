namespace Dawn.MuMu.RichPresence.Tools;

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
        try
        {
            using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, true)!;
            startupKey.DeleteValue(key, false);

            Log.Verbose("Removed {Key} from startup registry", key);
        }
        catch (Exception e)
        {
            Log.Error(e, "Exception occurred when attempting to remove the startup key '{Key}'", key);
        }
    }
    // ---

    // Read-Only
    public static bool Contains(string key, string value)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, false)!;
        var regVal = startupKey.GetValue(key);

        return regVal?.ToString()?.Contains(value) ?? false;
    }
    public static bool StartsWithWindows(string key, string path)
    {
        using var startupKey = Registry.CurrentUser.OpenSubKey(STARTUP_SUBKEY, false)!;
        var regVal = startupKey.GetValue(key);

        var str = regVal?.ToString();

        return str != null && str.Contains(path);
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
