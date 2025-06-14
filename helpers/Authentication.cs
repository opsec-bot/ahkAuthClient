using System.Net;
using System.Text;
using System.Text.Json;

public class Authentication
{
    private static readonly string[] GamePassIds = { "<your_gamepass_id_here>" }; // Update Idea: fetch ID's from API

    private async Task<bool> VerifyHasGamepass(string userId, string gpId)
    {
        using var client = new HttpClient();
        var resp = await client.GetAsync(
            $"https://inventory.roblox.com/v1/users/{userId}/items/GamePass/{gpId}"
        );
        if (!resp.IsSuccessStatusCode)
            return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetArrayLength() > 0;
    }

    private async Task<string?> FetchUserid(HttpClient client, string username)
    {
        var payload = JsonSerializer.Serialize(
            new { usernames = new[] { username }, excludeBannedUsers = true }
        );
        var resp = await client.PostAsync(
            "https://users.roblox.com/v1/usernames/users",
            new StringContent(payload, Encoding.UTF8, "application/json")
        );
        if (!resp.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0)
            return null;

        var idElem = data[0].GetProperty("id");
        string userId = idElem.ValueKind switch
        {
            JsonValueKind.Number => idElem.GetInt64().ToString(),
            JsonValueKind.String => idElem.GetString()!,
            _ => throw new FormatException($"Unexpected JSON type for id: {idElem.ValueKind}"),
        };

        return userId;
    }

    public async Task<bool> LocalCheck(string user, string baseURL)
    {
        using var client = new HttpClient();

        try
        {
            var pingResp = await client.GetAsync($"{baseURL}");
            if (!pingResp.IsSuccessStatusCode)
                throw new HttpRequestException();
        }
        catch
        {
            Console.Clear();
            Console.Error.WriteLine("[X] Application server offline... press any key to close");
            Console.ReadKey();
            Environment.Exit(0);
        }

        var uid = await FetchUserid(client, user);
        if (uid == null)
            return false;
        foreach (var gp in GamePassIds)
            if (await VerifyHasGamepass(uid, gp))
                return true;
        return false;
    }

    public class ServerCheckResult
    {
        public bool Exists { get; set; }
        public bool PasswordMatch { get; set; }
        public bool HwidMatch { get; set; }
    }

    public async Task<ServerCheckResult?> ServerCheck(string user, string pass, string baseURL)
    {
        using var client = new HttpClient();

        try
        {
            var pingResp = await client.GetAsync($"{baseURL}");
            if (!pingResp.IsSuccessStatusCode)
                throw new HttpRequestException();
        }
        catch
        {
            Console.Clear();
            Console.Error.WriteLine("[X] Application server offline... press any key to close");
            Console.ReadKey();
            Environment.Exit(0);
        }

        var url =
            $"{baseURL}verify?username={Uri.EscapeDataString(user)}&password={Uri.EscapeDataString(pass)}&hwid={Hwid.GetHWID()}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return null;

        var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = json.RootElement;

        return new ServerCheckResult
        {
            Exists = root.TryGetProperty("exists", out var existsProp) && existsProp.GetBoolean(),
            PasswordMatch =
                root.TryGetProperty("passwordMatch", out var passProp) && passProp.GetBoolean(),
            HwidMatch = root.TryGetProperty("hwidMatch", out var hwidProp) && hwidProp.GetBoolean(),
        };
    }

    public async Task<bool> UsernameExists(string user, string baseURL)
    {
        using var client = new HttpClient();
        var url = $"{baseURL}verify?username={Uri.EscapeDataString(user)}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode)
            return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("exists", out var val) && val.GetBoolean();
    }

    public async Task<bool> HWIDReset(string user, string pass, string baseURL)
    {
        using var client = new HttpClient();
        var content = new StringContent(
            JsonSerializer.Serialize(
                new
                {
                    username = user,
                    password = pass,
                    newHwid = Hwid.GetHWID(),
                }
            ),
            Encoding.UTF8,
            "application/json"
        );

        var resp = await client.PostAsync($"{baseURL}reset-hwid", content);
        var respBody = await resp.Content.ReadAsStringAsync();

        if (resp.StatusCode == HttpStatusCode.Forbidden) // 403
        {
            Console.Clear();
            Console.Error.WriteLine("[X] Invalid credentials.");
            return false;
        }

        if (resp.StatusCode == (HttpStatusCode)429) // Too Many Requests
        {
            try
            {
                var json = JsonDocument.Parse(respBody);
                if (json.RootElement.TryGetProperty("error", out var errorMsg))
                    Console.Clear();
                Console.Error.WriteLine($"[X] {errorMsg.GetString()}");
                await Task.Delay(2000);
                Environment.Exit(0);
            }
            catch
            {
                Console.Clear();
                Console.Error.WriteLine("[X] HWID reset cooldown active.");
            }
            return false;
        }

        if (resp.IsSuccessStatusCode)
        {
            Console.Clear();
            Console.WriteLine("[✓] HWID Reset successful. Please restart the loader.");
            return true;
        }

        Console.Clear();
        Console.Error.WriteLine("[X] Unexpected error while resetting HWID.");
        return false;
    }
}
