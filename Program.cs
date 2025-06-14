using System.Text;


namespace GagAuthClient
{
    class Program
    {
        // Configuration
        private const string BaseUrl = "http://localhost:3000/";

        static async Task Main()
        {
            // Check if username provided owns the required GamePass
            string? username = null;
            int attempts = 0;
            Console.Title = "AHK Bootstrapper v1.0";

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
                var ahk = Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(plainBytes)));
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
            var saved = Settings.Load();
            var auth = new Authentication(); 
            if (!string.IsNullOrEmpty(saved)
                && await auth.LocalCheck(saved)
                && await auth.ServerCheck(saved, BaseUrl))
            {
                Console.Clear();
                Console.WriteLine($"[✓] Welcome back {saved}!");
                return saved;
            }

            Settings.Delete();

            Console.Write("Enter your Roblox username: ");
            var user = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(user))
                return null;

            // Check GamePass ownership
            if (!await auth.LocalCheck(user))
            {
                Console.Error.WriteLine("[!] You do not own the required GamePass.");
                return null;
            }

            // Check against the server
            if (!await auth.ServerCheck(user, BaseUrl))
            {
                Console.Error.WriteLine("[!] Mismatched HWID.");
                return null;
            }

            Settings.Save(user);
            Console.WriteLine($"[✓] Sucessfully authenticated welcome {user}!");
            return user;
        }
    }
}