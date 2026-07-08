using System.Security.Cryptography;
using System.Text;

namespace LoopRelay.Projections.Services;

public static class ProjectionHash
{
    public static string Sha256(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
