#nullable enable

using System;
using System.Security.Cryptography;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal static class WorkRelaxProfileKey
{
    private const string Domain = "BlueSage.WorkRelax.Profile.v1\n";

    public static string Create(string localSteamIdentity)
    {
        if (string.IsNullOrWhiteSpace(localSteamIdentity))
        {
            throw new ArgumentException("A local Steam identity is required.", nameof(localSteamIdentity));
        }

        byte[] input = Encoding.UTF8.GetBytes(Domain + localSteamIdentity.Trim());
        using SHA256 sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(input);
        var hex = new StringBuilder(hash.Length * 2);
        foreach (byte value in hash)
        {
            hex.Append(value.ToString("x2"));
        }

        return "p_" + hex;
    }
}
