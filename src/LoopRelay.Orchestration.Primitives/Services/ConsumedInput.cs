using System.Security.Cryptography;
using System.Text;

namespace LoopRelay.Orchestration.Services;

// The manifest entry for one file read from disk as workflow input. The hash is computed over
// the exact string content as read, so receipt-time and status-time hashes are comparable; the
// receipt's commit hash is the retrieval key for exact content, the file hash is corroborating
// evidence and the staleness comparand.
public sealed record ConsumedInputFile(string Path, string Sha256)
{
    public static ConsumedInputFile FromContent(string path, string content) =>
        new(path, HashContent(content));

    public static string HashContent(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
