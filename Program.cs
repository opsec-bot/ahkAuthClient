using System;
using System.IO;
using System.Net.Http;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

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
            Console.Title = "AHK Loader";

            while (attempts < 2 && string.IsNullOrEmpty(username))
            {
                username = await EnsureVerifiedUserAsync();

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

            Console.WriteLine("[*] Initiating DH handshake...");
            var sessionId = await exch.InitHandshakeAsync();
            Console.WriteLine($"[+] Session ID: {sessionId}");

            var shareKeyB64 = await exch.FinishHandshakeAsync(sessionId);
            Console.WriteLine("[+] Handshake completed. Shared key established.");

            var (encrypted, iv, tag) = await exch.FetchEncryptedScriptAsync(sessionId);

            var Keybytes = Convert.FromBase64String(shareKeyB64);
            var plainBytes = exch.DecryptScript(encrypted, iv, tag, Keybytes);
            var ahkScript = Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(plainBytes)));
            Console.WriteLine("[+] Script decrypted. Launching AutoHotkey via stdin...");

            await Launcher.LaunchAHK(ahkScript);

            Console.WriteLine("[✓] AutoHotkey launched. Press any key to exit.");
            Console.ReadKey();
        }

        private static async Task<string?> EnsureVerifiedUserAsync()
        {
            var saved = Settings.LoadSavedUser();
            var auth = new Authentication(); 
            if (!string.IsNullOrEmpty(saved)
                && await auth.LocalValidateAsync(saved)
                && await auth.ServerValidateAsync(saved))
            {
                Console.WriteLine($"[✓] Welcome back {saved}!");
                return saved;
            }

            Settings.DeleteSavedUser();

            Console.Write("Enter your Roblox username: ");
            var user = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(user))
                return null;

            // Check GamePass ownership
            if (!await auth.LocalValidateAsync(user))
            {
                Console.Error.WriteLine("[!] You do not own the required GamePass.");
                return null;
            }

            // Check against the server
            if (!await auth.ServerValidateAsync(user))
            {
                Console.Error.WriteLine("[!] Mismatched HWID.");
                return null;
            }

            Settings.SaveUser(user);
            Console.WriteLine($"[✓] Saved and verified user: {user}");
            return user;
        }
    }
}