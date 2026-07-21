using System.Security.Cryptography;

var keyId = args.Length > 0 ? args[0] : "primary";
var keySize = 32; // 256-bit key for HMAC-SHA256

var key = new byte[keySize];
RandomNumberGenerator.Fill(key);
var keyBase64 = Convert.ToBase64String(key);

Console.WriteLine($"Key ID:  {keyId}");
Console.WriteLine($"Secret:  {keyBase64}");
Console.WriteLine();
Console.WriteLine("── Lobby env vars ──");
Console.WriteLine($"HMAC_KEY_ID={keyId}");
Console.WriteLine($"HMAC_SECRET_KEY={keyBase64}");
Console.WriteLine();
Console.WriteLine("── Game Master env var ──");
Console.WriteLine($"GM_HMAC_KEYS={keyId}={keyBase64}");
