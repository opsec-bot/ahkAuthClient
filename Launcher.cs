using System.Diagnostics;

public static class Launcher
{
    private const string AHKExePath = @"C:\Program Files\AutoHotkey\v1.1.37.02\AutoHotkeyU64.exe"; // Later on make this a function that checks for V1 and finds latest version
    public async static Task LaunchAHK(string script)
    {
        var psi = new ProcessStartInfo(AHKExePath, "*")
        {
            UseShellExecute = false,
            RedirectStandardInput = true
        };

        using var process = Process.Start(psi)!;
        await process.StandardInput.WriteAsync(script);
        process.StandardInput.Close();

    }
}
