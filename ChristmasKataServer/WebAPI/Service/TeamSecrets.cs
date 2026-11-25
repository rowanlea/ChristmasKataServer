using System.Security.Cryptography;

namespace WebAPI.Service;

public static class TeamSecrets
{
    // Unanchored so the team page can use it as a browser regex too.
    public const string Pattern = "[0-9a-f]{32}";

    // Hex only: the harness prints the secret unquoted in a shell command for the pair to paste.
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
