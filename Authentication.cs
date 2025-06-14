using System;
using System.Text;
using System.Text.Json;

public class Authentication
{
    private const string VerifyEndpoint = "http://localhost:3000/verify";
    private static readonly string[] GamePassIds = { "234738190" };
    public async Task<bool> OwnsGamePassAsync(string userId, string gpId)
     {
            using var client = new HttpClient();
            var resp = await client.GetAsync($"https://inventory.roblox.com/v1/users/{userId}/items/GamePass/{gpId}");
            if (!resp.IsSuccessStatusCode) return false;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("data").GetArrayLength() > 0;
     }
public async Task<bool> LocalValidateAsync(string user)
    {
        using var client = new HttpClient();
        var uid = await GetUserIdAsync(client, user);
        if (uid == null) return false;
        foreach (var gp in GamePassIds)
            if (await OwnsGamePassAsync(uid, gp)) return true;
        return false;
    }

    public async Task<bool> ServerValidateAsync(string user)
    {
        var comp = Environment.MachineName;
        using var client = new HttpClient();
        var url = $"{VerifyEndpoint}?username={Uri.EscapeDataString(user)}&hwid={Hwid.GetHWID()}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("matched").GetBoolean();
    }
    private async Task<string?> GetUserIdAsync(HttpClient client, string username)
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
}
