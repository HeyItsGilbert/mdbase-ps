using System.Security.Cryptography;
using System.Text;

namespace Mdbase.Core.Write;

/// <summary>
/// Minimal ULID generator (48-bit millisecond timestamp + 80 bits of cryptographic randomness,
/// Crockford base32 encoded to 26 characters) for the `{ ulid: true }` standard lifecycle
/// provider (spec Ch.09). No third-party dependency — ULID's spec is small and stable enough
/// to implement directly rather than adding a package for seven lines of bit-packing.
/// </summary>
internal static class Ulid
{
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string NewUlid()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var randomness = RandomNumberGenerator.GetBytes(10);

        var bytes = new byte[16];
        bytes[0] = (byte)(timestamp >> 40);
        bytes[1] = (byte)(timestamp >> 32);
        bytes[2] = (byte)(timestamp >> 24);
        bytes[3] = (byte)(timestamp >> 16);
        bytes[4] = (byte)(timestamp >> 8);
        bytes[5] = (byte)timestamp;
        Array.Copy(randomness, 0, bytes, 6, 10);

        return Encode(bytes);
    }

    private static string Encode(byte[] bytes)
    {
        // 128 bits -> 26 Crockford base32 characters (5 bits each = 130 bits, top 2 bits of the
        // first character are always zero for a 128-bit value). Encoded via a 128-bit
        // big-endian bit buffer since 80/48-bit halves don't align to 5-bit groups cleanly.
        Span<char> output = stackalloc char[26];
        var bits = new bool[128];
        for (var i = 0; i < 16; i++)
        {
            for (var b = 0; b < 8; b++)
            {
                bits[i * 8 + b] = (bytes[i] & (1 << (7 - b))) != 0;
            }
        }

        for (var charIndex = 0; charIndex < 26; charIndex++)
        {
            var value = 0;
            for (var b = 0; b < 5; b++)
            {
                var bitIndex = charIndex * 5 + b;
                value <<= 1;
                if (bitIndex < 128 && bits[bitIndex])
                {
                    value |= 1;
                }
            }

            output[charIndex] = CrockfordAlphabet[value];
        }

        return new string(output);
    }
}
