using System.Security.Cryptography;

namespace LoopRelay.Core.Models.Identity;

/// <summary>
/// Mints canonical 26-character Crockford base32 ULIDs and prefixed causal spine identifiers.
/// Ordering authority is ledger append order; no monotonic-within-millisecond guarantee is made.
/// </summary>
public static class CausalUlid
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int TimestampCharacterCount = 10;
    private const int RandomnessByteCount = 10;
    private const int UlidCharacterCount = 26;

    public static string NewUlid()
    {
        ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> randomness = stackalloc byte[RandomnessByteCount];
        RandomNumberGenerator.Fill(randomness);
        Span<char> characters = stackalloc char[UlidCharacterCount];
        EncodeTimestamp(timestamp, characters[..TimestampCharacterCount]);
        EncodeRandomness(randomness, characters[TimestampCharacterCount..]);
        return new string(characters);
    }

    public static string NewId(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("Identifier prefix must be non-empty.", nameof(prefix));
        }

        foreach (char character in prefix)
        {
            if (character is < 'a' or > 'z')
            {
                throw new ArgumentException(
                    $"Identifier prefix must be lowercase ascii letters; got `{prefix}`.",
                    nameof(prefix));
            }
        }

        return prefix + "_" + NewUlid();
    }

    private static void EncodeTimestamp(ulong timestamp, Span<char> destination)
    {
        for (int index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = CrockfordAlphabet[(int)(timestamp & 0x1F)];
            timestamp >>= 5;
        }
    }

    private static void EncodeRandomness(ReadOnlySpan<byte> randomness, Span<char> destination)
    {
        int buffer = 0;
        int bitsInBuffer = 0;
        int position = 0;
        foreach (byte value in randomness)
        {
            buffer = (buffer << 8) | value;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                destination[position] = CrockfordAlphabet[(buffer >> bitsInBuffer) & 0x1F];
                position++;
            }

            buffer &= (1 << bitsInBuffer) - 1;
        }
    }
}
