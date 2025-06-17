using System.Text;
using System.Text.Json;

namespace GagAuthClient
{
    class Program
    {
        // Configuration
        private const string BaseUrl = "http://159.89.89.27:3000"; // Change to your server IP
        private const string LoaderVersion = "1.0.3"; // change this on each release

        static async Task Main()
        {
            // Check if username provided owns the required GamePass
            string? username = null;
            int attempts = 0;
            Console.Title = "AHK Bootstrapper";

            using var versionClient = new HttpClient();
            try
            {
                var versionResp = await versionClient.GetAsync($"{BaseUrl}/");
                var content = await versionResp.Content.ReadAsStringAsync();
                var versionJson = JsonDocument.Parse(content);
                var serverVersion = versionJson.RootElement.GetProperty("version").GetString();

                if (serverVersion != LoaderVersion)
                {
                    Console.Error.WriteLine(
                        $"[!] New release available (v{serverVersion}). Please update your loader."
                    );
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }
            }
            catch
            {
                Console.Error.WriteLine("[X] Failed to check version.");
                Console.ReadKey();
                return;
            }

            while (attempts < 2 && string.IsNullOrEmpty(username))
            {
                username = await AuthenticateUser();

                if (string.IsNullOrEmpty(username))
                {
                    attempts++;
                    await Task.Delay(3000);
                    Console.Clear();
                }
            }

            if (string.IsNullOrEmpty(username))
            {
                Console.Error.WriteLine("[X] Maximum attempts reached. Exiting.");
                return;
            }

            using var exch = new CryptoClient(BaseUrl);

            Console.WriteLine();
            var fetchTask = Task.Run(async () =>
            {
                var sessionId = await exch.InitHandshake();
                var shareKeyB64 = await exch.EstablishHandshake(sessionId);
                var (encrypted, iv, tag) = await exch.FetchEncryptedScript(sessionId);
                var keyBytes = Convert.FromBase64String(shareKeyB64);
                var plainBytes = exch.DecryptScript(encrypted, iv, tag, keyBytes);
                var ahk = Encoding.UTF8.GetString(
                    Convert.FromBase64String(Encoding.UTF8.GetString(plainBytes))
                );
                return ahk;
            });

            await Launcher.StartSpinner(fetchTask, "[*] Fetching latest update");
            var ahkScript = fetchTask.Result;
            await Launcher.LaunchAHK(ahkScript);

            Console.Clear();
            Console.Write("[✓] Bootstrapper finished! Closing in 3...");
            for (int i = 2; i >= 1; i--)
            {
                await Task.Delay(1000);
                Console.Write($"{i}...");
            }

            await Task.Delay(1000);
            Console.WriteLine("Goodbye.");

            Environment.Exit(0);
        }

        private static async Task<string?> AuthenticateUser()
        {
            var (savedUser, savedPass) = Settings.Load();
            var auth = new Authentication();

            if (
                !string.IsNullOrEmpty(savedUser)
                && !string.IsNullOrEmpty(savedPass)
                && await auth.InitalCheck(savedUser, BaseUrl)
                && (await auth.ServerCheck(savedUser, savedPass, BaseUrl))?.HwidMatch == true
            )
            {
                Console.Clear();
                Console.WriteLine($"[✓] Welcome back {savedUser}!");
                return savedUser;
            }

            Settings.Delete();

            Console.Write("Enter your Roblox username: ");
            var user = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(user))
                return null;

            bool userExists = await auth.UsernameExists(user, BaseUrl);
            string? pass;

            if (!userExists)
            {
                Console.Write("Set a password: ");
                pass = Authentication.ReadPassword();
            }
            else
            {
                Console.Write("Enter your password: ");
                pass = Authentication.ReadPassword();
            }

            if (string.IsNullOrEmpty(pass))
                return null;

            // Check GamePass ownership
            if (!await auth.InitalCheck(user, BaseUrl))
            {
                Console.Error.WriteLine("[!] You do not own the required GamePass.");
                return null;
            }

            // Full server-side check
            var serverResult = await auth.ServerCheck(user, pass, BaseUrl);
            if (serverResult == null)
            {
                Console.Error.WriteLine("[X] Server error.");
                return null;
            }

            if (!serverResult.Exists)
            {
                Console.Error.WriteLine("[!] Account creation failed.");
                return null;
            }

            if (!serverResult.PasswordMatch)
            {
                Console.Error.WriteLine("[!] Wrong password.");
                return null;
            }

            if (!serverResult.HwidMatch)
            {
                Console.Clear();
                Console.Error.WriteLine("[!] HWID does not match.");
                Console.Write("Would you like to reset your HWID? (y/n): ");
                var choice = Console.ReadLine()?.Trim().ToLower();

                if (choice == "y")
                {
                    if (await auth.HWIDReset(user, pass, BaseUrl))
                    {
                        Environment.Exit(0);
                    }
                    else
                    {
                        return null;
                    }
                }

                Console.WriteLine("[*] HWID reset cancelled.");
                return null;
            }

            Settings.Save(user, pass);
            Console.Clear();
            Console.WriteLine($"[✓] Successfully authenticated. Welcome {user}!");
            return user;
        }
    }
}
