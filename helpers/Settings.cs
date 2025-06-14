using System.Text.Json;

public static class Settings
{
    private const string SettingsFile = "settings.json";
    public static string Load()
    => File.Exists(SettingsFile)
        ? JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.GetProperty("verified_user").GetString()!
        : string.Empty;
    public static void Save(string user)
    => File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new { verified_user = user }));
    public static void Delete()
    => File.Delete(SettingsFile);
}
