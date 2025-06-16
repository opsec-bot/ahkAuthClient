using System.Text.Json.Serialization;
using GagAuthClient.Models;

[JsonSerializable(typeof(UserQueryPayload))]
[JsonSerializable(typeof(HwidPayload))]
[JsonSerializable(typeof(ExchangePayload))]
[JsonSerializable(typeof(User))]
public partial class AppJsonContext : JsonSerializerContext { }
