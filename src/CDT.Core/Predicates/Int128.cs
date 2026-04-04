// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

#if !NET7_0_OR_GREATER

using System.Runtime.CompilerServices;

namespace CDT.Predicates;

/// <summary>
/// A 128-bit signed integer for runtimes prior to .NET 7 (e.g. the current
/// Unity runtime). On .NET 7+ <c>System.Int128</c> is used directly and this
/// struct is compiled away by the <c>#if !NET7_0_OR_GREATER</c> guard.
/// </summary>
/// <remarks>
/// Only the operations used by CDT.Core's integer predicate layer are
/// implemented. Stored as two <see cref="ulong"/> limbs in little-endian
/// two's-complement. Blittable unmanaged value type — safe in unmanaged/Burst
/// contexts (Burst support for the polyfill path depends on Burst's approved
/// type list at the time of adoption).
/// </remarks>
internal readonly struct Int128
{
    // _lo = bits 0–63, _hi = bits 64–127.  MSB of _hi is the sign bit.
    internal readonly ulong _lo;
    internal readonly ulong _hi;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Int128(ulong lo, ulong hi) { _lo = lo; _hi = hi; }

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>0.</summary>
    public static readonly Int128 Zero     = new Int128(0UL, 0UL);

    /// <summary>2^127 − 1  (maximum positive value).</summary>
    public static readonly Int128 MaxValue = new Int128(ulong.MaxValue, (ulong)long.MaxValue);

    /// <summary>−2^127  (minimum value).</summary>
    public static readonly Int128 MinValue = new Int128(0UL, 0x8000_0000_0000_0000UL);

    // -------------------------------------------------------------------------
    // Casts
    // -------------------------------------------------------------------------

    /// <summary>Widens a <see cref="long"/> to Int128 via sign extension.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Int128(long value) =>
        new Int128((ulong)value, value < 0L ? ulong.MaxValue : 0UL);

    /// <summary>Returns the low 64 bits as <see cref="ulong"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator ulong(Int128 value) => value._lo;

    /// <summary>Returns the low 64 bits reinterpreted as <see cref="long"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator long(Int128 value) => (long)value._lo;

    // -------------------------------------------------------------------------
    // Unary negation  (~a + 1 in two's complement)
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 operator -(Int128 a)
    {
        ulong nLo   = ~a._lo;
        ulong nHi   = ~a._hi;
        ulong rLo   = nLo + 1UL;
        ulong carry = rLo < nLo ? 1UL : 0UL;  // overflow of nLo+1 iff nLo == ulong.MaxValue
        return new Int128(rLo, nHi + carry);
    }

    // -------------------------------------------------------------------------
    // Addition
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 operator +(Int128 a, Int128 b)
    {
        ulong lo    = a._lo + b._lo;
        ulong carry = lo < a._lo ? 1UL : 0UL;
        return new Int128(lo, a._hi + b._hi + carry);
    }

    // -------------------------------------------------------------------------
    // Subtraction
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 operator -(Int128 a, Int128 b) => a + (-b);

    // -------------------------------------------------------------------------
    // Multiplication  (low 128 bits — bits ≥ 128 are discarded)
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 operator *(Int128 a, Int128 b)
    {
        // Full product bits 0–63.
        ulong rLo = a._lo * b._lo;
        // Full product bits 64–127.
        // MulHi(a._lo, b._lo) = high 64 bits of the lo×lo cross.
        // a._lo * b._hi and a._hi * b._lo contribute their low 64 bits.
        // Any carry into bit 128 is intentionally discarded (modular 128-bit arithmetic).
        ulong rHi = MulHi(a._lo, b._lo) + a._lo * b._hi + a._hi * b._lo;
        return new Int128(rLo, rHi);
    }

    // -------------------------------------------------------------------------
    // Comparison
    // -------------------------------------------------------------------------

    /// <summary>Signed comparison. Returns −1, 0, or +1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Int128 other)
    {
        if (this < other) return -1;
        if (other < this) return  1;
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Int128 a, Int128 b)
    {
        // Compare high words as signed; break ties with low words as unsigned.
        long aHi = (long)a._hi, bHi = (long)b._hi;
        if (aHi != bHi) return aHi < bHi;
        return a._lo < b._lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Int128 a, Int128 b)  => b < a;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Int128 a, Int128 b) => !(b < a);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Int128 a, Int128 b) => !(a < b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Int128 a, Int128 b) => a._lo == b._lo && a._hi == b._hi;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Int128 a, Int128 b) => !(a == b);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Int128 other && this == other;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_lo, _hi);

    /// <inheritdoc/>
    public override string ToString() =>
        $"Int128(Hi=0x{_hi:X16} Lo=0x{_lo:X16})";

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the high 64 bits of the unsigned product <c>a × b</c>.
    /// Uses 32-bit splitting — no hardware widening multiply required.
    /// Also called by <see cref="Int256"/> in the non-NET7 compilation path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong MulHi(ulong a, ulong b)
    {
        ulong aLo = (uint)a, aHi = a >> 32;
        ulong bLo = (uint)b, bHi = b >> 32;
        ulong p   = aLo * bLo;
        ulong t   = aHi * bLo + (p >> 32);
        ulong tLo = (uint)t, tHi = t >> 32;
        t = aLo * bHi + tLo;
        return aHi * bHi + tHi + (t >> 32);
    }
}

#endif
