using System.Security.Cryptography;

namespace Pitchside.Runtime.Audit;

/// <summary>
/// Universally unique lexicographically sortable identifiers: 48 bits of Unix milliseconds then 80 random bits, in
/// Crockford base 32, so ids sort by creation time.
/// </summary>
public static class Ulid
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string New(DateTimeOffset utc)
    {
        Span<byte> bytes = stackalloc byte[16];
        var millis = utc.ToUnixTimeMilliseconds();
        for (var i = 5; i >= 0; i--)
        {
            bytes[i] = (byte)(millis & 0xFF);
            millis >>= 8;
        }

        RandomNumberGenerator.Fill(bytes[6..]);
        return Encode(bytes);
    }

    /// <summary>The 128 bits as 26 base-32 characters, most significant first (the first carries 3 bits).</summary>
    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        var high = ((UInt128)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes) << 64)
            | System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]);
        Span<char> chars = stackalloc char[26];
        for (var i = 25; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(high & 31)];
            high >>= 5;
        }

        return new string(chars);
    }
}
