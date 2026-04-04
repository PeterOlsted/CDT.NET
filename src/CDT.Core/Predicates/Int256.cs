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
        ulong lo = (ulong)value;
        ulong m1 = (ulong)((UInt128)value >> 64);
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

        UInt128 ua = a < Int128.Zero ? (UInt128)(-a) : (UInt128)a;
        UInt128 ub = b < Int128.Zero ? (UInt128)(-b) : (UInt128)b;

        Int256 product = MultiplyUnsigned128(ua, ub);
        return negative ? Negate(product) : product;
    }

    /// <summary>
    /// Unsigned 128×128 → 256-bit multiplication.
    /// Splits each operand into two 64-bit halves and accumulates four
    /// 64×64 partial products, propagating carries exactly.
    /// </summary>
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

    // -------------------------------------------------------------------------
    // Operators (convenience wrappers used by predicate code)
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 operator +(Int256 a, Int256 b) => Add(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 operator -(Int256 a, Int256 b) => Subtract(a, b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int256 operator -(Int256 a) => Negate(a);

    /// <inheritdoc/>
    public override string ToString() =>
        $"Int256(Hi=0x{Hi:X16} M2=0x{M2:X16} M1=0x{M1:X16} Lo=0x{Lo:X16})";
}
