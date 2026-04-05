// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Numerics;
using CDT.Predicates;

namespace CDT.Tests;

/// <summary>
/// Tests for the <c>Int128</c> type.
/// On .NET 7+ these exercise <see cref="System.Int128"/> (BCL).
/// On earlier runtimes they exercise the <c>CDT.Predicates.Int128</c> polyfill.
/// Both implementations must produce identical results for all inputs in the
/// documented coordinate range.
/// </summary>
public class Int128Tests
{
    // -------------------------------------------------------------------------
    // Oracle helpers
    // -------------------------------------------------------------------------

    /// <summary>Converts an Int128 to a BigInteger for exact cross-checking.</summary>
    private static BigInteger ToBig(Int128 value)
    {
#if NET7_0_OR_GREATER
        return (BigInteger)value;
#else
        // Reconstruct from two's-complement limbs:
        //   (signed hi * 2^64)  |  (unsigned lo)
        return (new BigInteger((long)value._hi) << 64) | new BigInteger(value._lo);
#endif
    }

    // -------------------------------------------------------------------------
    // Cast from long
    // -------------------------------------------------------------------------

    [Fact]
    public void CastFromLong_Positive()
    {
        Int128 v = 42L;
        Assert.Equal(new BigInteger(42), ToBig(v));
    }

    [Fact]
    public void CastFromLong_Negative()
    {
        Int128 v = -1L;
        Assert.Equal(new BigInteger(-1), ToBig(v));
    }

    [Fact]
    public void CastFromLong_Zero()
    {
        Int128 v = 0L;
        Assert.Equal(BigInteger.Zero, ToBig(v));
    }

    [Fact]
    public void CastFromLong_LongMaxValue()
    {
        Int128 v = long.MaxValue;
        Assert.Equal(new BigInteger(long.MaxValue), ToBig(v));
    }

    [Fact]
    public void CastFromLong_LongMinValue()
    {
        Int128 v = long.MinValue;
        Assert.Equal(new BigInteger(long.MinValue), ToBig(v));
    }

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    [Fact]
    public void Zero_IsZero()
    {
        Assert.Equal(Int128.Zero, (Int128)0L);
        Assert.Equal(0, Int128.Zero.CompareTo(Int128.Zero));
    }

    [Fact]
    public void MaxValue_Equals2Pow127Minus1()
    {
        Assert.Equal(BigInteger.Pow(2, 127) - 1, ToBig(Int128.MaxValue));
        Assert.True(Int128.MaxValue > Int128.Zero);
    }

    // -------------------------------------------------------------------------
    // Unary negation
    // -------------------------------------------------------------------------

    [Fact]
    public void Negate_Zero()
    {
        Assert.Equal(Int128.Zero, -Int128.Zero);
    }

    [Fact]
    public void Negate_Positive()
    {
        Assert.Equal(new BigInteger(-7), ToBig(-(Int128)7L));
    }

    [Fact]
    public void Negate_Negative()
    {
        Assert.Equal(new BigInteger(5), ToBig(-(Int128)(-5L)));
    }

    [Fact]
    public void Negate_Involution()
    {
        Int128 x = 12345678901234L;
        Assert.Equal(x, -(-x));
    }

    [Fact]
    public void Negate_LargePositive()
    {
        Int128 x = (Int128)long.MaxValue + (Int128)long.MaxValue; // 2^64 - 2
        Assert.Equal(-(BigInteger.Pow(2, 64) - 2), ToBig(-x));
    }

    // -------------------------------------------------------------------------
    // Addition
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_NoCarry()
    {
        Assert.Equal(new BigInteger(8), ToBig((Int128)3L + (Int128)5L));
    }

    [Fact]
    public void Add_CarryFromLoToHi()
    {
        // (2^64 - 1) + 1 = 2^64 — carry must propagate from lo into hi.
        // Build 2^64 - 1 without using the internal constructor:
        // long.MaxValue = 2^63 - 1 ; 2*(2^63-1) + 1 = 2^64 - 1
        Int128 maxU64 = (Int128)long.MaxValue + (Int128)long.MaxValue + (Int128)1L;
        Int128 result = maxU64 + (Int128)1L;
        Assert.Equal(BigInteger.Pow(2, 64), ToBig(result));
    }

    [Fact]
    public void Add_NegativeAndPositive()
    {
        Assert.Equal(new BigInteger(-3), ToBig((Int128)(-10L) + (Int128)7L));
    }

    [Fact]
    public void Add_TwoNegatives()
    {
        Assert.Equal(new BigInteger(-15), ToBig((Int128)(-7L) + (Int128)(-8L)));
    }

    // -------------------------------------------------------------------------
    // Subtraction
    // -------------------------------------------------------------------------

    [Fact]
    public void Subtract_Basic()
    {
        Assert.Equal(new BigInteger(7), ToBig((Int128)10L - (Int128)3L));
    }

    [Fact]
    public void Subtract_CrossZero()
    {
        Assert.Equal(new BigInteger(-7), ToBig((Int128)3L - (Int128)10L));
    }

    [Fact]
    public void Subtract_Self()
    {
        Int128 x = (Int128)999_999_999L;
        Assert.Equal(Int128.Zero, x - x);
    }

    // -------------------------------------------------------------------------
    // Multiplication
    // -------------------------------------------------------------------------

    [Fact]
    public void Multiply_BothPositive()
    {
        Assert.Equal(new BigInteger(42), ToBig((Int128)6L * (Int128)7L));
    }

    [Fact]
    public void Multiply_PositiveNegative()
    {
        Assert.Equal(new BigInteger(-12), ToBig((Int128)(-3L) * (Int128)4L));
    }

    [Fact]
    public void Multiply_BothNegative()
    {
        Assert.Equal(new BigInteger(25), ToBig((Int128)(-5L) * (Int128)(-5L)));
    }

    [Fact]
    public void Multiply_Zero()
    {
        Int128 x = (Int128)long.MaxValue;
        Assert.Equal(Int128.Zero, x * Int128.Zero);
        Assert.Equal(Int128.Zero, Int128.Zero * x);
    }

    [Fact]
    public void Multiply_One()
    {
        Int128 x = (Int128)(-98765L);
        Assert.Equal(x, x * (Int128)1L);
        Assert.Equal(x, (Int128)1L * x);
    }

    [Fact]
    public void Multiply_LoToHiCarry()
    {
        // (2^32) * (2^32) = 2^64 — product overflows the lo limb into hi.
        Int128 a = (Int128)(1L << 32);
        Int128 result = a * a;
        Assert.Equal(BigInteger.Pow(2, 64), ToBig(result));
    }

    [Fact]
    public void Multiply_MaxValue_LowBits()
    {
        // (2^127 - 1)^2 = 2^254 - 2^128 + 1
        // Low 128 bits = (2^254 - 2^128 + 1) mod 2^128 = 1
        Int128 result = Int128.MaxValue * Int128.MaxValue;
        Assert.Equal((Int128)1L, result);
    }

    // -------------------------------------------------------------------------
    // CompareTo
    // -------------------------------------------------------------------------

    [Fact]
    public void CompareTo_NegativeLessThanPositive()
    {
        Assert.True(((Int128)(-1L)).CompareTo((Int128)1L) < 0);
    }

    [Fact]
    public void CompareTo_Equal()
    {
        Assert.Equal(0, ((Int128)5L).CompareTo((Int128)5L));
    }

    [Fact]
    public void CompareTo_PositiveGreaterThanNegative()
    {
        Assert.True(((Int128)7L).CompareTo((Int128)(-7L)) > 0);
    }

    [Fact]
    public void CompareTo_ZeroGreaterThanNegative()
    {
        Assert.True(Int128.Zero.CompareTo((Int128)(-100L)) > 0);
    }

    [Fact]
    public void CompareTo_NegativeGreaterThanMinValue()
    {
        Assert.True(((Int128)(-1L)).CompareTo(Int128.MinValue) > 0);
    }

    // -------------------------------------------------------------------------
    // Comparison operators
    // -------------------------------------------------------------------------

    [Fact]
    public void Operators_LessThan()
    {
        Assert.True((Int128)(-1L) < (Int128)1L);
        Assert.False((Int128)1L < (Int128)(-1L));
        Assert.False((Int128)5L < (Int128)5L);
    }

    [Fact]
    public void Operators_GreaterThan()
    {
        Assert.True((Int128)3L > (Int128)2L);
        Assert.False((Int128)2L > (Int128)3L);
    }

    [Fact]
    public void Operators_Equality()
    {
        Assert.True((Int128)42L == (Int128)42L);
        Assert.False((Int128)42L == (Int128)(-42L));
    }

    [Fact]
    public void Operators_Inequality()
    {
        Assert.True((Int128)1L != (Int128)(-1L));
        Assert.False((Int128)7L != (Int128)7L);
    }

    // -------------------------------------------------------------------------
    // BigInteger oracle — multiply cross-check
    // These cover the full range of values used by the geometric predicates.
    // -------------------------------------------------------------------------

    [Fact]
    public void Multiply_Oracle_BasicValues()
    {
        long[] values = { 0, 1, -1, 100, -100, long.MaxValue, long.MinValue };
        foreach (long a in values)
        foreach (long b in values)
        {
            Int128 result   = (Int128)a * (Int128)b;
            BigInteger expected = new BigInteger(a) * new BigInteger(b);
            // All products of two long values fit in signed 128-bit (max is
            // long.MinValue^2 = 2^126 < 2^127), so no masking needed.
            Assert.Equal(expected, ToBig(result));
        }
    }

    [Fact]
    public void Multiply_Oracle_CoordinateRange()
    {
        // Values at MaxCoordinate = 2^53 as used by Orient2dRaw.
        const long C = 1L << 53;
        long[] coords = { C, -C, C - 1, -(C - 1), C / 2, -(C / 2) };
        foreach (long a in coords)
        foreach (long b in coords)
        {
            Int128 result   = (Int128)a * (Int128)b;
            BigInteger expected = new BigInteger(a) * new BigInteger(b);
            // Products ≤ (2^54)^2 = 2^108 < 2^127 — no overflow.
            Assert.Equal(expected, ToBig(result));
        }
    }

    [Fact]
    public void Add_Oracle_CoordinateRange()
    {
        // Validate that addition of two Int128 products (as computed in Orient2dRaw)
        // matches BigInteger exactly.
        const long C = 1L << 53;
        Int128 acx = (Int128)C  - (Int128)(-C);     // 2^54
        Int128 bcy = (Int128)C  - (Int128)(-C + 1); // 2^54 - 1
        Int128 acy = (Int128)(-C);
        Int128 bcx = (Int128)(C - 1);

        Int128 det    = acx * bcy - acy * bcx;
        BigInteger expected = (BigInteger)(C) * 2 * ((BigInteger)(C) * 2 - 1)
                            - (BigInteger)(-C) * (BigInteger)(C - 1);

        Assert.Equal(expected, ToBig(det));
    }

    // -------------------------------------------------------------------------
    // LessEqual / GreaterEqual operators
    // -------------------------------------------------------------------------

    [Fact]
    public void Operators_LessOrEqual()
    {
        Int128 n1 = (Int128)(-1L);
        Int128 z  = Int128.Zero;
        Int128 p1 = (Int128)1L;

        Assert.True(n1 <= z);
        Assert.True(n1 <= n1);
        Assert.True(z  <= p1);
        Assert.False(p1 <= z);
    }

    [Fact]
    public void Operators_GreaterOrEqual()
    {
        Int128 n1 = (Int128)(-1L);
        Int128 z  = Int128.Zero;
        Int128 p1 = (Int128)1L;

        Assert.True(p1 >= z);
        Assert.True(p1 >= p1);
        Assert.True(z  >= n1);
        Assert.False(n1 >= z);
    }

    // -------------------------------------------------------------------------
    // NegativeOne and One constants
    // -------------------------------------------------------------------------

    [Fact]
    public void NegativeOne_IsMinusOne()
    {
        Assert.Equal(new BigInteger(-1), ToBig(Int128.NegativeOne));
        Assert.True(Int128.NegativeOne < Int128.Zero);
    }

    [Fact]
    public void One_IsOne()
    {
        Assert.Equal(new BigInteger(1), ToBig(Int128.One));
        Assert.True(Int128.One > Int128.Zero);
    }

    // -------------------------------------------------------------------------
    // Equals (boxed object overload) and GetHashCode
    // -------------------------------------------------------------------------

    [Fact]
    public void Equals_BoxedSameValue_ReturnsTrue()
    {
        Int128 a = (Int128)42L;
        object b = (Int128)42L;
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_BoxedDifferentValue_ReturnsFalse()
    {
        Int128 a = (Int128)1L;
        object b = (Int128)2L;
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_BoxedWrongType_ReturnsFalse()
    {
        // Box the values explicitly so overload resolution uses Equals(object?)
        Int128 a = (Int128)1L;
        object boxedString = "not an Int128";
        Assert.False(a.Equals(boxedString));
    }

    [Fact]
    public void Equals_BoxedNull_ReturnsFalse()
    {
        Int128 a = (Int128)5L;
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void GetHashCode_EqualValues_SameHash()
    {
        Int128 a = (Int128)12345L;
        Int128 b = (Int128)12345L;
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

#if !NET7_0_OR_GREATER
    // -------------------------------------------------------------------------
    // ToString (polyfill only — BCL Int128 has its own ToString)
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_ContainsHexLimbs()
    {
        Int128 v = (Int128)255L;  // Lo=0xFF, Hi=0
        var s = v.ToString();
        Assert.Contains("FF", s, StringComparison.OrdinalIgnoreCase);
    }
#endif
}
