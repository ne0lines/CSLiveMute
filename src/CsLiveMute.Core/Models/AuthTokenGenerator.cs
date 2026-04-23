using System.Security.Cryptography;

namespace CsLiveMute.Core.Models;

public static class AuthTokenGenerator
{
    public static string Create()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}
