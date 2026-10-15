using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Pitchside.Runtime.Audit;

/// <summary>
/// Universally unique lexicographically sortable identifiers: 48 bits of Unix milliseconds then 80 random bits, in
/// Crockford base 32, so ids sort by creation time. Ids are monotonic within the process: one created in the same
/// millisecond as the last (or with a clock that went back) takes the last one's time and its random part plus one,
/// so each id sorts after every id created before it and an <c>after</c> cursor never skips a later one.
/// </summary>
public static class Ulid
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly UInt128 RandomMask = (UInt128.One << 80) - 1;
    private static readonly Lock Gate = new();
    private static long _lastMillis = -1;
    private static UInt128 _lastRandom;

    public static string New(DateTimeOffset utc)
    {
        var millis = utc.ToUnixTimeMilliseconds();
        UInt128 random;
        lock (Gate)
        {
            if (millis <= _lastMillis && _lastRandom < RandomMask)
            {
                millis = _lastMillis;
                random = _lastRandom + 1;
            }
            else
            {
                millis = Math.Max(millis, _lastMillis + (_lastRandom == RandomMask ? 1 : 0));
                Span<byte> bytes = stackalloc byte[16];
                RandomNumberGenerator.Fill(bytes[6..]);
                random = BinaryPrimitives.ReadUInt128BigEndian(bytes) & RandomMask;
            }

            (_lastMillis, _lastRandom) = (millis, random);
        }

        return Encode(((UInt128)(ulong)millis << 80) | random);
    }

    /// <summary>The 128 bits as 26 base-32 characters, most significant first (the first carries 3 bits).</summary>
    private static string Encode(UInt128 value)
    {
        Span<char> chars = stackalloc char[26];
        for (var i = 25; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(value & 31)];
            value >>= 5;
        }

        return new string(chars);
    }
}
