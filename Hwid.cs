using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

public static class Hwid
{
    private static string Exec(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        string output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return output.Trim();
    }

    private static string Sha256Hex(string data)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    public static string GetHWID()
    {
        // 1) BIOS UUID
        string rawUuid = Exec("wmic", "csproduct get UUID");
        var uuidLines = rawUuid.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string uuid = uuidLines.Length > 1 ? uuidLines[1].Trim() : "";

        // 2) Windows MachineGuid
        string rawGuid = Exec("reg", "query HKLM\\SOFTWARE\\Microsoft\\Cryptography /v MachineGuid");
        var parts = rawGuid.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string machineGuid = parts.Length >= 3 ? parts[^1] : "";

        // 3) Disk serial
        string rawDisk = Exec("wmic", "diskdrive get serialnumber");
        var diskLines = rawDisk.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string diskSerial = diskLines.Length > 1 ? diskLines[1].Trim() : "";

        // 4) Motherboard serial
        string rawBoard = Exec("wmic", "baseboard get serialnumber");
        var boardLines = rawBoard.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string boardSerial = boardLines.Length > 1 ? boardLines[1].Trim() : "";

        // 5) Combine and hash into HWID
        string concatenated = $"{uuid}:{machineGuid}:{diskSerial}:{boardSerial}";
        return Sha256Hex(concatenated);
    }
}
