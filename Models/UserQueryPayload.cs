namespace GagAuthClient.Models;

public class UserQueryPayload
{
    public string[] usernames { get; set; } = [];
    public bool excludeBannedUsers { get; set; } = true;
}
