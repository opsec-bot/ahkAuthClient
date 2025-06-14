using System.Text;
using System.Text.Json;

public class Authentication
{
    private static readonly string[] GamePassIds = { "234738190" };
    private async Task<bool> VerifyHasGamepass(string userId, string gpId)
     {
            using var client = new HttpClient();
            var resp = await client.GetAsync($"https://inventory.roblox.com/v1/users/{userId}/items/GamePass/{gpId}");
            if (!resp.IsSuccessStatusCode) return false;
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("data").GetArrayLength() > 0;
     }

    private async Task<string?> FetchUserid(HttpClient client, string username)
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
    public async Task<bool> LocalCheck(string user)
    {
        using var client = new HttpClient();
        var uid = await FetchUserid(client, user);
        if (uid == null) return false;
        foreach (var gp in GamePassIds)
            if (await VerifyHasGamepass(uid, gp)) return true;
        return false;
    }

    public async Task<bool> ServerCheck(string user, string baseURL)
    {
        var comp = Environment.MachineName;
        using var client = new HttpClient();
        var url = $"{baseURL}verify?username={Uri.EscapeDataString(user)}&hwid={Hwid.GetHWID()}";
        var resp = await client.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return false;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("matched").GetBoolean();
    }
}
