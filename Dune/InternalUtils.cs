
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Dune;

internal static class InternalUtils {

    public static void Assert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string? message = null
    ) {
        Debug.Assert(condition, message);
    }

    public static void ThrowIfArgumentNull(
        [NotNull] object? obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    ) {
        if (obj is null)
            throw new ArgumentNullException(paramName);
    }


    public static void ThrowIfArgumentNullOrWhitespace(
        [NotNull] string? obj,
        [CallerArgumentExpression(nameof(obj))] string? paramName = null
    ) {
        if (obj is null)
            throw new ArgumentNullException(paramName);

        if (obj.Trim().Length == 0)
            throw new ArgumentException($"Parameter {paramName} must not be an empty string or only whitespace.");
    }

    public static int HashCodeCombineRaw(int a, int b) {
        int hash = 17;
        hash = hash * 31 + a;
        hash = hash * 31 + b;
        return hash;
    }

    private static int HashCode<T>(T obj) => obj?.GetHashCode() ?? 0;

    public static int HashCodeCombine<A, B>(A a, B b) =>
        HashCodeCombineRaw(HashCode(a), HashCode(b));

    public static int HashCodeCombine<A, B, C>(A a, B b, C c) =>
        HashCodeCombineRaw(HashCodeCombine(a, b), HashCode(c));

    public static int HashCodeCombine<A, B, C, D>(A a, B b, C c, D d) =>
        HashCodeCombineRaw(HashCodeCombine(a, b, c), HashCode(d));

    public static int HashCodeCombine<A, B, C, D, E>(A a, B b, C c, D d, E e) =>
        HashCodeCombineRaw(HashCodeCombine(a, b, c, d), HashCode(e));


    // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/BinaryWriter.cs#L475C13-L490C10
    public static void DuneWrite7BitEncodedInt(this BinaryWriter writer, int value) {
        uint uValue = (uint)value;

        // Write out an int 7 bits at a time. The high bit of the byte,
        // when on, tells reader to continue reading more bytes.
        //
        // Using the constants 0x7F and ~0x7F below offers smaller
        // codegen than using the constant 0x80.

        while (uValue > 0x7Fu) {
            writer.Write((byte)(uValue | ~0x7Fu));
            uValue >>= 7;
        }

        writer.Write((byte)uValue);
    }

    // https://github.com/dotnet/dotnet/blob/b0f34d51fccc69fd334253924abd8d6853fad7aa/src/runtime/src/libraries/System.Private.CoreLib/src/System/IO/BinaryReader.cs#L538C13-L573C32
    public static int DuneRead7BitEncodedInt(this BinaryReader reader) {
        // Unlike writing, we can't delegate to the 64-bit read on
        // 64-bit platforms. The reason for this is that we want to
        // stop consuming bytes if we encounter an integer overflow.

        uint result = 0;
        byte byteReadJustNow;

        // Read the integer 7 bits at a time. The high bit
        // of the byte when on means to continue reading more bytes.
        //
        // There are two failure cases: we've read more than 5 bytes,
        // or the fifth byte is about to cause integer overflow.
        // This means that we can read the first 4 bytes without
        // worrying about integer overflow.

        const int MaxBytesWithoutOverflow = 4;
        for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7) {
            // ReadByte handles end of stream cases for us.
            byteReadJustNow = reader.ReadByte();
            result |= (byteReadJustNow & 0x7Fu) << shift;

            if (byteReadJustNow <= 0x7Fu) {
                return (int)result; // early exit
            }
        }

        // Read the 5th byte. Since we already read 28 bits,
        // the value of this byte must fit within 4 bits (32 - 28),
        // and it must not have the high bit set.

        byteReadJustNow = reader.ReadByte();
        if (byteReadJustNow > 0b_1111u) {
            throw new FormatException("Bad int format");
        }

        result |= (uint)byteReadJustNow << (MaxBytesWithoutOverflow * 7);
        return (int)result;
    }

    public static StringBuilder AppendEnumerable<T>(this StringBuilder sb, IEnumerable<T> values, Action<T, StringBuilder> stringifier, string separator = ", ") {
        using IEnumerator<T> enumerator = values.GetEnumerator();

        if (!enumerator.MoveNext()) return sb;

        stringifier(enumerator.Current, sb);

        while (enumerator.MoveNext()) {
            sb.Append(separator);
            stringifier(enumerator.Current, sb);
        }

        return sb;
    }
}