using System.Security.Cryptography;

namespace WebAPI.Service;

public static class TeamSecrets
{
    // Hex only: the harness prints the secret unquoted in a shell command for the pair to paste.
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}
