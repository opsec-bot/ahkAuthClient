using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

public static class Hwid
{
    private static string Sha256Hex(string data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder();
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static string GetHWID()
    {
        string uuid = "";
        string diskSerial = "";
        string boardSerial = "";
        string machineGuid = "";

        try
        {
            // 1) BIOS UUID
            using var searcher1 = new ManagementObjectSearcher(
                "SELECT UUID FROM Win32_ComputerSystemProduct"
            );
            foreach (var obj in searcher1.Get())
                uuid = obj["UUID"]?.ToString()?.Trim() ?? "";

            // 2) Disk Serial Number
            using var searcher2 = new ManagementObjectSearcher(
                "SELECT SerialNumber FROM Win32_DiskDrive"
            );
            foreach (var obj in searcher2.Get())
            {
                diskSerial = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                break;
            }

            // 3) Motherboard Serial Number
            using var searcher3 = new ManagementObjectSearcher(
                "SELECT SerialNumber FROM Win32_BaseBoard"
            );
            foreach (var obj in searcher3.Get())
                boardSerial = obj["SerialNumber"]?.ToString()?.Trim() ?? "";

            // 4) MachineGuid from registry
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            machineGuid = key?.GetValue("MachineGuid")?.ToString() ?? "";
        }
        catch
        {
            // Handle errors if needed
        }

        string concatenated = $"{uuid}:{machineGuid}:{diskSerial}:{boardSerial}";
        return Sha256Hex(concatenated);
    }
}
