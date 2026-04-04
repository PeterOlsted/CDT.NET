// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace CDT.Predicates;

/// <summary>
/// A 256-bit signed integer stored as four 64-bit unsigned limbs in
/// little-endian order (Lo = bits 0–63, M1 = 64–127, M2 = 128–191, Hi = 192–255).
/// Two's-complement representation.
/// Blittable value type — safe to use in unmanaged/Burst contexts.
/// </summary>
/// <remarks>
/// Only the operations required by the integer geometric predicates are
/// implemented: construction from Int128, negation, addition, subtraction,
/// and multiplication of two Int128 values into an Int256 result.
/// </remarks>
internal readonly struct Int256
{
    // Little-endian limbs: Lo is the least-significant 64 bits.
    internal readonly ulong Lo;
    internal readonly ulong M1;
    internal readonly ulong M2;
    internal readonly ulong Hi;

    /// <summary>Zero.</summary>
    public static readonly Int256 Zero = new(0UL, 0UL, 0UL, 0UL);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Int256(ulong lo, ulong m1, ulong m2, ulong hi)
    {
        Lo = lo; M1 = m1; M2 = m2; Hi = hi;
    }

    // -------------------------------------------------------------------------
    // Conversion from Int128
    // -------------------------------------------------------------------------

    /// <summary>Sign-extends an <see cref="Int128"/> into an <see cref="Int256"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 FromInt128(Int128 value)
    {
#if NET7_0_OR_GREATER
        ulong lo = (ulong)value;
        ulong m1 = (ulong)((UInt128)value >> 64);
#else
        ulong lo = value._lo;
        ulong m1 = value._hi;
#endif
        // Sign-extend: if negative fill upper limbs with 0xFFFF…, else 0.
        ulong ext = value < Int128.Zero ? ulong.MaxValue : 0UL;
        return new Int256(lo, m1, ext, ext);
    }

    // -------------------------------------------------------------------------
    // Sign
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns +1 if positive, -1 if negative, 0 if zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Sign()
    {
        // High bit of Hi is the sign bit in two's-complement.
        if ((Hi & 0x8000_0000_0000_0000UL) != 0) return -1;
        if ((Hi | M2 | M1 | Lo) == 0UL) return 0;
        return 1;
    }

    // -------------------------------------------------------------------------
    // Negation (two's complement)
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 Negate(Int256 a)
    {
        // ~a + 1
        ulong lo = ~a.Lo;
        ulong m1 = ~a.M1;
        ulong m2 = ~a.M2;
        ulong hi = ~a.Hi;

        // Add 1 with carry propagation.
        ulong rLo = lo + 1UL;
        ulong c1  = rLo < lo ? 1UL : 0UL;
        ulong rM1 = m1 + c1;
        ulong c2  = (c1 != 0UL && rM1 < m1) ? 1UL : 0UL;
        ulong rM2 = m2 + c2;
        ulong c3  = (c2 != 0UL && rM2 < m2) ? 1UL : 0UL;
        ulong rHi = hi + c3;

        return new Int256(rLo, rM1, rM2, rHi);
    }

    // -------------------------------------------------------------------------
    // Addition
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 Add(Int256 a, Int256 b)
    {
        ulong lo = a.Lo + b.Lo;
        ulong c1 = lo < a.Lo ? 1UL : 0UL;

        ulong m1 = a.M1 + b.M1;
        ulong c2 = m1 < a.M1 ? 1UL : 0UL;
        m1 += c1;
        c2 += m1 < c1 ? 1UL : 0UL;     // carry from adding c1

        ulong m2 = a.M2 + b.M2;
        ulong c3 = m2 < a.M2 ? 1UL : 0UL;
        m2 += c2;
        c3 += m2 < c2 ? 1UL : 0UL;

        ulong hi = a.Hi + b.Hi + c3;    // overflow at 256 bits is intentional
                                         // (wraps for correctness of sign-bit math)
        return new Int256(lo, m1, m2, hi);
    }

    // -------------------------------------------------------------------------
    // Subtraction
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 Subtract(Int256 a, Int256 b) => Add(a, Negate(b));

    // -------------------------------------------------------------------------
    // Multiplication  Int128 × Int128 → Int256
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes the exact 256-bit product of two signed 128-bit integers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 Multiply(Int128 a, Int128 b)
    {
        // Record sign, then work with unsigned magnitudes.
        bool negative = (a < Int128.Zero) != (b < Int128.Zero);

#if NET7_0_OR_GREATER
        UInt128 ua = a < Int128.Zero ? (UInt128)(-a) : (UInt128)a;
        UInt128 ub = b < Int128.Zero ? (UInt128)(-b) : (UInt128)b;
        Int256 product = MultiplyUnsigned128(ua, ub);
#else
        Int128 ua = a < Int128.Zero ? -a : a;
        Int128 ub = b < Int128.Zero ? -b : b;
        Int256 product = MultiplyUnsigned128(ua._lo, ua._hi, ub._lo, ub._hi);
#endif
        return negative ? Negate(product) : product;
    }

    /// <summary>
    /// Unsigned 128×128 → 256-bit multiplication.
    /// Splits each operand into two 64-bit halves and accumulates four
    /// 64×64 partial products, propagating carries exactly.
    /// </summary>
#if NET7_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int256 MultiplyUnsigned128(UInt128 a, UInt128 b)
    {
        ulong aLo = (ulong)a;
        ulong aHi = (ulong)(a >> 64);
        ulong bLo = (ulong)b;
        ulong bHi = (ulong)(b >> 64);

        // Four 64×64 partial products, each fitting in 128 bits.
        UInt128 p0 = (UInt128)aLo * bLo;   // bits   0–127
        UInt128 p1 = (UInt128)aLo * bHi;   // bits  64–191
        UInt128 p2 = (UInt128)aHi * bLo;   // bits  64–191
        UInt128 p3 = (UInt128)aHi * bHi;   // bits 128–255

        // r0 = low 64 bits of p0 (no overlap).
        ulong r0 = (ulong)p0;

        // Accumulate bits 64–127: high 64 of p0  +  low 64 of p1  +  low 64 of p2.
        UInt128 mid1 = (p0 >> 64) + (UInt128)(ulong)p1 + (UInt128)(ulong)p2;
        ulong r1 = (ulong)mid1;

        // Accumulate bits 128–191: overflow of mid1  +  high 64 of p1  +  high 64 of p2  +  low 64 of p3.
        UInt128 mid2 = (mid1 >> 64) + (p1 >> 64) + (p2 >> 64) + (UInt128)(ulong)p3;
        ulong r2 = (ulong)mid2;

        // Bits 192–255: overflow of mid2  +  high 64 of p3.
        ulong r3 = (ulong)(mid2 >> 64) + (ulong)(p3 >> 64);

        return new Int256(r0, r1, r2, r3);
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int256 MultiplyUnsigned128(ulong aLo, ulong aHi, ulong bLo, ulong bHi)
    {
        // Four 64×64 partial products; MulHi gives the high 64 bits of each.
        ulong p0Lo = aLo * bLo;  ulong p0Hi = MulHi(aLo, bLo);
        ulong p1Lo = aLo * bHi;  ulong p1Hi = MulHi(aLo, bHi);
        ulong p2Lo = aHi * bLo;  ulong p2Hi = MulHi(aHi, bLo);
        ulong p3Lo = aHi * bHi;  ulong p3Hi = MulHi(aHi, bHi);

        ulong r0 = p0Lo;

        // Accumulate bits 64–127.
        ulong r1 = p0Hi;
        ulong c1 = 0UL;
        ulong t  = r1 + p1Lo; if (t < r1) c1++; r1 = t;
        t         = r1 + p2Lo; if (t < r1) c1++; r1 = t;

        // Accumulate bits 128–191.
        ulong r2 = c1;
        ulong c2 = 0UL;
        t = r2 + p1Hi; if (t < r2) c2++; r2 = t;
        t = r2 + p2Hi; if (t < r2) c2++; r2 = t;
        t = r2 + p3Lo; if (t < r2) c2++; r2 = t;

        // Bits 192–255.
        ulong r3 = p3Hi + c2;

        return new Int256(r0, r1, r2, r3);
    }

    /// <summary>
    /// High 64 bits of the unsigned product <paramref name="a"/> × <paramref name="b"/>.
    /// Equivalent to <see cref="Int128.MulHi"/> — duplicated here to keep Int256 self-contained.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MulHi(ulong a, ulong b)
    {
        ulong aLo = (uint)a, aHi = a >> 32;
        ulong bLo = (uint)b, bHi = b >> 32;
        ulong p   = aLo * bLo;
        ulong t   = aHi * bLo + (p >> 32);
        ulong tLo = (uint)t, tHi = t >> 32;
        t = aLo * bHi + tLo;
        return aHi * bHi + tHi + (t >> 32);
    }
#endif

    // -------------------------------------------------------------------------
    // Operators (convenience wrappers used by predicate code)
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 operator +(Int256 a, Int256 b) => Add(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 operator -(Int256 a, Int256 b) => Subtract(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 operator -(Int256 a) => Negate(a);

    // -------------------------------------------------------------------------
    // Division: Int256 / Int128 → long  (round-half-away-from-zero)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Divides <paramref name="numerator"/> by <paramref name="denominator"/> and
    /// returns the result rounded half-away-from-zero as a <see cref="long"/>.
    /// </summary>
    /// <remarks>
    /// Preconditions enforced with <see cref="System.Diagnostics.Debug.Assert"/>:
    /// <list type="bullet">
    ///   <item><paramref name="denominator"/> ≠ 0.</item>
    ///   <item>The exact quotient fits in <see cref="long"/>
    ///         (i.e. |result| ≤ 2^53 = <c>MaxCoordinate</c>).</item>
    /// </list>
    /// Used by <c>IntersectionPosition</c>; CDT geometry guarantees both
    /// preconditions hold (see PLAN.md bit-width analysis).
    /// </remarks>
    public static long DivideToInt64(Int256 numerator, Int128 denominator)
    {
        System.Diagnostics.Debug.Assert(denominator != Int128.Zero, "denominator must not be zero");

        int numSign = numerator.Sign();
        if (numSign == 0) return 0L;

        bool negative = (numSign < 0) ^ (denominator < Int128.Zero);

        Int256 absNum    = numSign < 0 ? Negate(numerator) : numerator;
        Int128 absDen128 = denominator < Int128.Zero ? -denominator : denominator;
        Int256 absDen    = FromInt128(absDen128);

        // Binary long division.  |quotient| ≤ 2^53, so 54 iterations suffice.
        ulong  q   = 0;
        Int256 rem = absNum;
        for (int bit = 53; bit >= 0; bit--)
        {
            Int256 shifted = bit == 0 ? absDen : ShiftLeftUnsigned(absDen, bit);
            if (CompareUnsigned(rem, shifted) >= 0)
            {
                rem  = Subtract(rem, shifted);
                q   |= 1UL << bit;
            }
        }

        // Round half-away-from-zero: if 2*rem ≥ absDen, increment quotient.
        if (CompareUnsigned(Add(rem, rem), absDen) >= 0)
            q++;

        System.Diagnostics.Debug.Assert(q <= (ulong)long.MaxValue, "quotient does not fit in long");
        return negative ? -(long)q : (long)q;
    }

    /// <summary>
    /// Left-shifts a non-negative <see cref="Int256"/> by <paramref name="k"/> bits (1 ≤ k ≤ 63).
    /// No overflow checking — caller must ensure the result fits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int256 ShiftLeftUnsigned(Int256 a, int k)
    {
        int rk = 64 - k;
        return new Int256(
             a.Lo << k,
            (a.M1 << k) | (a.Lo >> rk),
            (a.M2 << k) | (a.M1 >> rk),
            (a.Hi << k) | (a.M2 >> rk));
    }

    /// <summary>
    /// Unsigned comparison of two <see cref="Int256"/> values.
    /// Returns −1, 0, or +1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareUnsigned(Int256 a, Int256 b)
    {
        if (a.Hi != b.Hi) return a.Hi < b.Hi ? -1 : 1;
        if (a.M2 != b.M2) return a.M2 < b.M2 ? -1 : 1;
        if (a.M1 != b.M1) return a.M1 < b.M1 ? -1 : 1;
        return a.Lo < b.Lo ? -1 : a.Lo > b.Lo ? 1 : 0;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"Int256(Hi=0x{Hi:X16} M2=0x{M2:X16} M1=0x{M1:X16} Lo=0x{Lo:X16})";
}
