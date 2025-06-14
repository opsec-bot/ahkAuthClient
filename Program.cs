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
        private const string SettingsFile = "settings.json";
        private const string VerifyEndpoint = "http://localhost:3000/verify";
        private static readonly string[] GamePassIds = { "234738190" };
        private static readonly string AHKExePath = @"C:\Program Files\AutoHotkey\v1.1.37.02\AutoHotkeyU64.exe";
        private const int RateLimitWindowMs = 15000;

        static async Task Main()
        {
            var username = await EnsureVerifiedUserAsync();
            if (string.IsNullOrEmpty(username))
            {
                Console.Error.WriteLine("[!] User verification failed. Exiting.");
                return;
            }

            using var client = new HttpClient();
            Console.WriteLine("[*] Initiating DH handshake...");
            var initResp = await client.PostAsync(
                "http://localhost:3000/exchange/init",
                new StringContent("{}", Encoding.UTF8, "application/json")
            );
            initResp.EnsureSuccessStatusCode();
            using var initDoc = JsonDocument.Parse(await initResp.Content.ReadAsStringAsync());

            var sessionId = initDoc.RootElement.GetProperty("sessionId").GetString()!;
            Console.WriteLine($"[+] Session ID: {sessionId}");

            var pBytes = Convert.FromBase64String(initDoc.RootElement.GetProperty("prime").GetString()!);
            var gBytes = Convert.FromBase64String(initDoc.RootElement.GetProperty("generator").GetString()!);
            var serverPubBytes = Convert.FromBase64String(initDoc.RootElement.GetProperty("serverPub").GetString()!);

            var p = new BigInteger(pBytes, isUnsigned: true, isBigEndian: true);
            var g = new BigInteger(gBytes, isUnsigned: true, isBigEndian: true);
            var serverPubBI = new BigInteger(serverPubBytes, isUnsigned: true, isBigEndian: true);

            var privBytes = new byte[pBytes.Length];
            RandomNumberGenerator.Fill(privBytes);
            var a = new BigInteger(privBytes, isUnsigned: true, isBigEndian: true) % (p - 1);
            if (a < 2) a += 2;

            var clientPubBI = BigInteger.ModPow(g, a, p);
            var clientPubBytes = clientPubBI.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (clientPubBytes.Length < pBytes.Length)
            {
                var padded = new byte[pBytes.Length];
                Array.Copy(clientPubBytes, 0, padded, pBytes.Length - clientPubBytes.Length, clientPubBytes.Length);
                clientPubBytes = padded;
            }
            var clientPubBase64 = Convert.ToBase64String(clientPubBytes);

            var finishPayload = JsonSerializer.Serialize(new { sessionId, clientPub = clientPubBase64 });
            var finishResp = await client.PostAsync(
                "http://localhost:3000/exchange/finish",
                new StringContent(finishPayload, Encoding.UTF8, "application/json")
            );
            finishResp.EnsureSuccessStatusCode();
            Console.WriteLine("[✓] Handshake complete.");

            var sharedBI = BigInteger.ModPow(serverPubBI, a, p);
            var sharedBytes = sharedBI.ToByteArray(isUnsigned: true, isBigEndian: true);
            var sharedKey = SHA256.HashData(sharedBytes);

            var scriptResp = await client.GetAsync($"http://localhost:3000/script/{sessionId}");
            scriptResp.EnsureSuccessStatusCode();
            using var scriptDoc = JsonDocument.Parse(await scriptResp.Content.ReadAsStringAsync());

            var encrypted = Convert.FromBase64String(scriptDoc.RootElement.GetProperty("encrypted").GetString()!);
            var iv = Convert.FromBase64String(scriptDoc.RootElement.GetProperty("iv").GetString()!);
            var tag = Convert.FromBase64String(scriptDoc.RootElement.GetProperty("tag").GetString()!);

            var decrypted = new byte[encrypted.Length];
            using (var aes = new AesGcm(sharedKey))
            {
                aes.Decrypt(iv, encrypted, tag, decrypted);
            }
            var ahkScriptB64 = Encoding.UTF8.GetString(decrypted);
            var ahkScript = Encoding.UTF8.GetString(Convert.FromBase64String(ahkScriptB64));

            Console.WriteLine("[+] Script decrypted. Launching AutoHotkey via stdin...");

            var psi = new ProcessStartInfo(AHKExePath, "*")
            {
                UseShellExecute = false,
                RedirectStandardInput = true
            };
            using var process = Process.Start(psi)!;
            await process.StandardInput.WriteAsync(ahkScript);
            process.StandardInput.Close();

            Console.WriteLine("[✓] AutoHotkey launched. Press any key to exit.");
            Console.ReadKey();
        }

        private static async Task<string?> EnsureVerifiedUserAsync()
        {
            var saved = LoadSavedUser();
            if (!string.IsNullOrEmpty(saved)
                && await LocalValidateAsync(saved)
                && await ServerValidateAsync(saved))
            {
                Console.WriteLine($"[✓] Using saved user: {saved}");
                return saved;
            }

            DeleteSavedUser();

            Console.Write("Enter your Roblox username: ");
            var user = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(user))
                return null;

            // Check GamePass ownership
            if (!await LocalValidateAsync(user))
            {
                Console.Error.WriteLine("[!] You do not own the required GamePass.");
                return null;
            }

            // Check against the server
            if (!await ServerValidateAsync(user))
            {
                Console.Error.WriteLine("[!] Server verification failed.");
                return null;
            }

            SaveUser(user);
            Console.WriteLine($"[✓] Saved and verified user: {user}");
            return user;
        }

        private static string LoadSavedUser()
            => File.Exists(SettingsFile)
                ? JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.GetProperty("verified_user").GetString()!
                : string.Empty;

        private static void SaveUser(string user)
            => File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new { verified_user = user }));

        private static void DeleteSavedUser()
            => File.Delete(SettingsFile);

        private static async Task<bool> LocalValidateAsync(string user)
        {
            using var client = new HttpClient(); 
            var uid = await GetUserIdAsync(client, user); 
            if (uid == null) return false;
            foreach (var gp in GamePassIds)
                if (await OwnsGamePassAsync(uid, gp)) return true;
            return false;
        }

        private static async Task<bool> ServerValidateAsync(string user)
        {
            var comp = Environment.MachineName;
            using var client = new HttpClient();
            var url = $"{VerifyEndpoint}?username={Uri.EscapeDataString(user)}&computer={Uri.EscapeDataString(comp)}";
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return false;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("matched").GetBoolean();
        }

        static async Task<string?> GetUserIdAsync(HttpClient client, string username)
        {
            var payload = JsonSerializer.Serialize(new { usernames = new[] { username }, excludeBannedUsers = true });
            var resp = await client.PostAsync(
                "https://users.roblox.com/v1/usernames/users",
                new StringContent(payload, Encoding.UTF8, "application/json")
            );
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;

            var idElem = data[0].GetProperty("id"); 
            string userId = idElem.ValueKind switch
            {
                JsonValueKind.Number => idElem.GetInt64().ToString(),
                JsonValueKind.String => idElem.GetString()!,
                _ => throw new FormatException($"Unexpected JSON type for id: {idElem.ValueKind}")
            };

            return userId;
        }

        private static async Task<bool> OwnsGamePassAsync(string userId, string gpId)
        {
            using var client = new HttpClient();
            var resp = await client.GetAsync($"https://inventory.roblox.com/v1/users/{userId}/items/GamePass/{gpId}");
            if (!resp.IsSuccessStatusCode) return false;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("data").GetArrayLength() > 0;
        }
    }
}