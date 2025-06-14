using System.Diagnostics;

public static class Launcher
{
    private const string AHKExePath = @"C:\Program Files\AutoHotkey\v1.1.37.02\AutoHotkeyU64.exe"; // Later on make this a function that checks for V1 and finds latest version

    public static async Task LaunchAHK(string script)
    {
        var psi = new ProcessStartInfo(AHKExePath, "*")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
        };

        using var process = Process.Start(psi)!;
        await process.StandardInput.WriteAsync(script);
        process.StandardInput.Close();
    }

    public static async Task StartSpinner(Task task, string message = "[*] Working")
    {
        var spinner = new[] { "|", "/", "-", "\\" };
        var fillerMessages = new[]
        {
            "Injecting into Notepad...",
            "Spoofing Synapse just to feel something...",
            "Running macros like it's a paid executor.",
            "Pretending this is 'just for keybinds'...",
            "Launching JJsploit...",
            "Fluxus couldn’t handle this drip.",
            "Installing ransomware...",
            "Bribing Roblox client with keySTROKES. Get it... strokes.. hahaha..",
            "AutoHotkey.exe renamed to 'MinecraftUpdater'.",
            "Rebinding 'F' to do... everything.",
            "Beep Beep Boop Boop.",
            "Hiding malware in ~/home/Discord",
            "Fun fact This macro created with Notepad++",
            "Passed anti-cheat by wearing a hat.",
            "It’s not cheating if it’s a macro.",
            "AHK is love. AHK is life.",
            "Delaying launch time.. i mean inputs to act more human...",
            "Disabling key logging... after logging keys.",
            "Deleting \"Homework\" folder.. We know it isn't homework.",
            "When in doubt, send more keySTROKES. Get it... STROKESSS... Still not funny...",
            "Pretending AHK is a programming real language.",
            "Stealth mode: ON (kinda).",
            "Exiting process... Reason: Just kidding!",
            "Rewriting reality, one key at a time.",
            "Macro didn't work correctly? Blame Roblox, not me.",
            "Executing Lua... wait wrong loader. Whoops.",
            "Still better than Fluxus on a school laptop.",
            "Injecting into the Matrix...",
            "Running on 100% caffeine and macros.",
            "AutoHotkey: Because sometimes you just need to automate your procrastination.",
            "KRNL? More like KR-NOPE, this is AHK time!",
            "Lua scripts? Nah, we do it the AHK way.",
            "Synapse? Never heard of her.",
        };

        int spinIndex = 0;
        var rand = new Random();
        var lastFiller = Environment.TickCount;
        int topLine = Console.CursorTop;

        Console.WriteLine($"{message} {spinner[0]}");
        int logLine = Console.CursorTop;

        while (!task.IsCompleted)
        {
            Console.SetCursorPosition(message.Length + 1, topLine);
            Console.Write(spinner[spinIndex++ % spinner.Length]);

            await Task.Delay(100);

            if (Environment.TickCount - lastFiller > 5000)
            {
                Console.SetCursorPosition(0, logLine++);
                Console.WriteLine($"[*] {fillerMessages[rand.Next(fillerMessages.Length)]}");
                lastFiller = Environment.TickCount;
            }
        }

        await task;

        Console.SetCursorPosition(0, logLine++);
        Console.WriteLine("done.");
    }
}
