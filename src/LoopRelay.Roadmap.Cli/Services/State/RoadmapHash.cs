using System.Security.Cryptography;
using System.Text;

namespace LoopRelay.Roadmap.Cli.Services.State;

internal static class RoadmapHash
{
    public static string Sha256(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
