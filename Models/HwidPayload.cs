namespace GagAuthClient.Models;

public class HwidPayload
{
    public string username { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string newHwid { get; set; } = string.Empty;
}
