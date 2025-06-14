using System.Text.Json;

public static class Settings
{
    private const string SettingsFile = "settings.json";
    public static string LoadSavedUser()
    => File.Exists(SettingsFile)
        ? JsonDocument.Parse(File.ReadAllText(SettingsFile)).RootElement.GetProperty("verified_user").GetString()!
        : string.Empty;
    public static void SaveUser(string user)
    => File.WriteAllText(SettingsFile, JsonSerializer.Serialize(new { verified_user = user }));
    public static void DeleteSavedUser()
    => File.Delete(SettingsFile);
}
