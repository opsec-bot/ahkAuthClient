using System.Text.Json;

public static class Settings
{
    private const string SettingsFile = "settings.json";

    public static (string Username, string Password) Load()
    {
        if (!File.Exists(SettingsFile))
            return (string.Empty, string.Empty);

        var doc = JsonDocument.Parse(File.ReadAllText(SettingsFile));
        var root = doc.RootElement;

        string user = root.TryGetProperty("verified_user", out var uProp)
            ? uProp.GetString() ?? ""
            : "";
        string pass = root.TryGetProperty("password", out var pProp) ? pProp.GetString() ?? "" : "";

        return (user, pass);
    }

    public static void Save(string user, string password)
    {
        var obj = new { verified_user = user, password = password };
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(obj));
    }

    public static void Delete()
    {
        if (File.Exists(SettingsFile))
            File.Delete(SettingsFile);
    }
}
