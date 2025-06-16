using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GagAuthClient.Models;

public class CryptoClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    private BigInteger _p;
    private BigInteger _g;
    private BigInteger _serverPub;
    private BigInteger _privateA;
    private int _pBytesLength;

    public CryptoClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient();
    }

    private string GetClientPublicBase64()
    {
        var clientPubBI = BigInteger.ModPow(_g, _privateA, _p);
        var clientPubBytes = clientPubBI.ToByteArray(isUnsigned: true, isBigEndian: true);

        if (clientPubBytes.Length < _pBytesLength)
        {
            var padded = new byte[_pBytesLength];
            Array.Copy(
                clientPubBytes,
                0,
                padded,
                _pBytesLength - clientPubBytes.Length,
                clientPubBytes.Length
            );
            clientPubBytes = padded;
        }

        return Convert.ToBase64String(clientPubBytes);
    }

    public async Task<string> InitHandshake()
    {
        var resp = await _http.PostAsync(
            $"{_baseUrl}/exchange/init",
            new StringContent("{}", Encoding.UTF8, "application/json")
        );
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var sessionId = root.GetProperty("sessionId").GetString()!;

        var pBytes = Convert.FromBase64String(root.GetProperty("prime").GetString()!);
        var gBytes = Convert.FromBase64String(root.GetProperty("generator").GetString()!);
        var serverPubBytes = Convert.FromBase64String(root.GetProperty("serverPub").GetString()!);

        _p = new BigInteger(pBytes, isUnsigned: true, isBigEndian: true);
        _g = new BigInteger(gBytes, isUnsigned: true, isBigEndian: true);
        _serverPub = new BigInteger(serverPubBytes, isUnsigned: true, isBigEndian: true);

        var priv = new byte[pBytes.Length];
        RandomNumberGenerator.Fill(priv);
        _privateA = new BigInteger(priv, isUnsigned: true, isBigEndian: true) % (_p - 1);
        if (_privateA < 2)
            _privateA += 2;

        return sessionId;
    }

    public async Task<string> EstablishHandshake(string sessionId)
    {
        var obj = new ExchangePayload
        {
            sessionId = sessionId,
            clientPub = GetClientPublicBase64(),
        };

        var payload = JsonSerializer.Serialize(obj, AppJsonContext.Default.ExchangePayload);

        var resp = await _http.PostAsync(
            $"{_baseUrl}/exchange/finish",
            new StringContent(payload, Encoding.UTF8, "application/json")
        );
        resp.EnsureSuccessStatusCode();

        var sharedBI = BigInteger.ModPow(_serverPub, _privateA, _p);
        var sharedBytes = sharedBI.ToByteArray(isUnsigned: true, isBigEndian: true);
        var sharedKey = SHA256.HashData(sharedBytes);

        return Convert.ToBase64String(sharedKey);
    }

    public async Task<(byte[] encrypted, byte[] iv, byte[] tag)> FetchEncryptedScript(
        string sessionId
    )
    {
        var resp = await _http.GetAsync($"{_baseUrl}/script/{sessionId}");
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var encrypted = Convert.FromBase64String(root.GetProperty("encrypted").GetString()!);
        var iv = Convert.FromBase64String(root.GetProperty("iv").GetString()!);
        var tag = Convert.FromBase64String(root.GetProperty("tag").GetString()!);

        return (encrypted, iv, tag);
    }

    public byte[] DecryptScript(byte[] encrypted, byte[] iv, byte[] tag, byte[] sharedKey)
    {
        var plaintext = new byte[encrypted.Length];
        using var aes = new AesGcm(sharedKey);
        aes.Decrypt(iv, encrypted, tag, plaintext);
        return plaintext;
    }

    public void Dispose() => _http?.Dispose();
}
