using System;
using System.Numerics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GagAuthClient
{
    class Program
    {
        static async Task Main()
        {
            using var client = new HttpClient();

            // INIT handshake
            Console.WriteLine("[*] Initiating DH handshake...");
            var initResp = await client.PostAsync(
                "http://localhost:3000/exchange/init",
                new StringContent("{}", Encoding.UTF8, "application/json")
            );
            initResp.EnsureSuccessStatusCode();
            using var initDoc = JsonDocument.Parse(await initResp.Content.ReadAsStringAsync());

            string sessionId = initDoc.RootElement.GetProperty("sessionId").GetString()!;
            Console.WriteLine($"[+] Session ID: {sessionId}");

            byte[] pBytes = Convert.FromBase64String(initDoc.RootElement.GetProperty("prime").GetString()!);
            byte[] gBytes = Convert.FromBase64String(initDoc.RootElement.GetProperty("generator").GetString()!);
            byte[] serverPubBytes = Convert.FromBase64String(initDoc.RootElement.GetProperty("serverPub").GetString()!);

            BigInteger p = new BigInteger(pBytes, isUnsigned: true, isBigEndian: true);
            BigInteger g = new BigInteger(gBytes, isUnsigned: true, isBigEndian: true);
            BigInteger serverPubBI = new BigInteger(serverPubBytes, isUnsigned: true, isBigEndian: true);

            // Compute client public value
            byte[] privBytes = new byte[pBytes.Length];
            RandomNumberGenerator.Fill(privBytes);
            BigInteger a = new BigInteger(privBytes, isUnsigned: true, isBigEndian: true) % (p - 1);
            if (a < 2) a += 2;

            BigInteger clientPubBI = BigInteger.ModPow(g, a, p);
            byte[] clientPubBytes = clientPubBI.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (clientPubBytes.Length < pBytes.Length)
            {
                var padded = new byte[pBytes.Length];
                Array.Copy(clientPubBytes, 0, padded, pBytes.Length - clientPubBytes.Length, clientPubBytes.Length);
                clientPubBytes = padded;
            }
            string clientPubBase64 = Convert.ToBase64String(clientPubBytes);

            // Finish handshake
            var finishPayload = JsonSerializer.Serialize(new { sessionId, clientPub = clientPubBase64 });
            var finishResp = await client.PostAsync(
                "http://localhost:3000/exchange/finish",
                new StringContent(finishPayload, Encoding.UTF8, "application/json")
            );
            finishResp.EnsureSuccessStatusCode();
            Console.WriteLine("[✓] Handshake complete.");

            // Derive shared secret locally
            BigInteger sharedBI = BigInteger.ModPow(serverPubBI, a, p);
            byte[] sharedBytes = sharedBI.ToByteArray(isUnsigned: true, isBigEndian: true);
            byte[] sharedKey = SHA256.HashData(sharedBytes);

            // Fetch encrypted script
            var scriptResp = await client.GetAsync($"http://localhost:3000/script/{sessionId}");
            scriptResp.EnsureSuccessStatusCode();
            using var scriptDoc = JsonDocument.Parse(await scriptResp.Content.ReadAsStringAsync());

            byte[] encrypted = Convert.FromBase64String(scriptDoc.RootElement.GetProperty("encrypted").GetString()!);
            byte[] iv = Convert.FromBase64String(scriptDoc.RootElement.GetProperty("iv").GetString()!);
            byte[] tag = Convert.FromBase64String(scriptDoc.RootElement.GetProperty("tag").GetString()!);

            // Decrypt
            byte[] decrypted = new byte[encrypted.Length];
            using var aes = new AesGcm(sharedKey);
            aes.Decrypt(iv, encrypted, tag, decrypted);
            var ahkScriptB64 = Encoding.UTF8.GetString(decrypted);
            string ahkScript = Encoding.UTF8.GetString(Convert.FromBase64String(ahkScriptB64));

            Console.WriteLine("[+] Script decrypted. Launching AutoHotkey via stdin...");

            // Launch AHK
            string ahkExePath = @"C:\Program Files\AutoHotkey\v1.1.37.02\AutoHotkeyU64.exe";
            var psi = new ProcessStartInfo(ahkExePath, "*")
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
    }
}
