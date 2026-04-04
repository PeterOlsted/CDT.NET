// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CDT.Predicates;

namespace CDT.Tests;

/// <summary>
/// Exhaustive tests for <see cref="Int256"/> arithmetic.
/// Covers construction, sign, negation, addition, subtraction, and
/// multiplication — including word-boundary carry cases, large operands,
/// and all sign combinations.
/// </summary>
public sealed class Int256Tests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Converts a signed decimal string to Int256 via Int128.</summary>
    private static Int256 FromInt128(Int128 v) => Int256.FromInt128(v);

    /// <summary>Small positive Int256 from a long.</summary>
    private static Int256 I(long v) => Int256.FromInt128(v);

    // -------------------------------------------------------------------------
    // FromInt128 / sign extension
    // -------------------------------------------------------------------------

    [Fact]
    public void FromInt128_Zero_IsAllZeroes()
    {
        var v = Int256.FromInt128(Int128.Zero);
        Assert.Equal(0, v.Sign());
        Assert.Equal(Int256.Zero, v);
    }

    [Fact]
    public void FromInt128_One_SignIsPositive()
    {
        Assert.Equal(1, Int256.FromInt128(Int128.One).Sign());
    }

    [Fact]
    public void FromInt128_NegativeOne_SignIsNegative()
    {
        Assert.Equal(-1, Int256.FromInt128(Int128.NegativeOne).Sign());
    }

    [Fact]
    public void FromInt128_MaxValue_SignIsPositive()
    {
        Assert.Equal(1, Int256.FromInt128(Int128.MaxValue).Sign());
    }

    [Fact]
    public void FromInt128_MinValue_SignIsNegative()
    {
        Assert.Equal(-1, Int256.FromInt128(Int128.MinValue).Sign());
    }

    [Fact]
    public void FromInt128_SignExtendsPositive_UpperLimbsAreZero()
    {
        var v = Int256.FromInt128(42);
        Assert.Equal(0UL, v.M2);
        Assert.Equal(0UL, v.Hi);
    }

    [Fact]
    public void FromInt128_SignExtendsNegative_UpperLimbsAreAllOnes()
    {
        var v = Int256.FromInt128(-1);
        Assert.Equal(ulong.MaxValue, v.M2);
        Assert.Equal(ulong.MaxValue, v.Hi);
    }

    // -------------------------------------------------------------------------
    // Sign
    // -------------------------------------------------------------------------

    [Fact]
    public void Sign_Zero_ReturnsZero() => Assert.Equal(0, Int256.Zero.Sign());

    [Fact]
    public void Sign_PositiveLarge_ReturnsOne()
    {
        var v = Int256.FromInt128(Int128.MaxValue);
        Assert.Equal(1, v.Sign());
    }

    [Fact]
    public void Sign_NegativeLarge_ReturnsMinusOne()
    {
        var v = Int256.FromInt128(Int128.MinValue);
        Assert.Equal(-1, v.Sign());
    }

    // -------------------------------------------------------------------------
    // Negation
    // -------------------------------------------------------------------------

    [Fact]
    public void Negate_Zero_IsZero()
    {
        Assert.Equal(0, (-Int256.Zero).Sign());
    }

    [Fact]
    public void Negate_One_IsMinusOne()
    {
        var v = -I(1);
        Assert.Equal(-1, v.Sign());
        // -1 in two's complement: all bits set
        Assert.Equal(ulong.MaxValue, v.Lo);
        Assert.Equal(ulong.MaxValue, v.M1);
        Assert.Equal(ulong.MaxValue, v.M2);
        Assert.Equal(ulong.MaxValue, v.Hi);
    }

    [Fact]
    public void Negate_MinusOne_IsOne()
    {
        var v = -I(-1);
        Assert.Equal(1, v.Sign());
        Assert.Equal(1UL, v.Lo);
        Assert.Equal(0UL, v.M1);
    }

    [Fact]
    public void Negate_InvolutionHoldsForSmallValues()
    {
        foreach (long x in new long[] { 0, 1, -1, 127, -127, long.MaxValue, long.MinValue })
        {
            var v = I(x);
            Assert.Equal(v, -(-v));
        }
    }

    [Fact]
    public void Negate_InvolutionHoldsForInt128Values()
    {
        foreach (Int128 x in new Int128[] { 0, 1, -1, Int128.MaxValue, Int128.MinValue })
        {
            var v = FromInt128(x);
            Assert.Equal(v, -(-v));
        }
    }

    [Fact]
    public void Negate_CarryPropagatesAcrossAllLimbs()
    {
        // Build a value where Lo == 0, M1 == 0, M2 == 0, Hi == 1 → i.e. 2^192
        // Negation should give Lo=0, M1=0, M2=0, Hi = ulong.MaxValue (... 111...000 in binary → two's complement)
        // Actually: ~(2^192) + 1 = -2^192; the carry must propagate through Lo, M1, M2 all being 0.
        var big = new Int256(0UL, 0UL, 0UL, 1UL);   // value is +2^192 (positive, Hi=1)
        var neg = -big;
        // -2^192 in two's complement: Lo=0, M1=0, M2=0, Hi = (~1+0 carry from lower) = 0xFFFF...FFFF
        // Carry: ~Lo + 1 = 0xFFFF...FFFF + 1 = 0 with carry=1
        //        ~M1 + carry = 0xFFFF...FFFF + 1 = 0 with carry=1
        //        ~M2 + carry = 0xFFFF...FFFF + 1 = 0 with carry=1
        //        ~Hi + carry = ~1 + 1 = 0xFFFF...FFFE + 1 = 0xFFFF...FFFF
        Assert.Equal(0UL, neg.Lo);
        Assert.Equal(0UL, neg.M1);
        Assert.Equal(0UL, neg.M2);
        Assert.Equal(ulong.MaxValue, neg.Hi);
        Assert.Equal(-1, neg.Sign());
    }

    // -------------------------------------------------------------------------
    // Addition
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_ZeroPlusZero_IsZero()
    {
        Assert.Equal(0, (Int256.Zero + Int256.Zero).Sign());
    }

    [Fact]
    public void Add_OnePlusOne_IsTwo()
    {
        var r = I(1) + I(1);
        Assert.Equal(1, r.Sign());
        Assert.Equal(2UL, r.Lo);
    }

    [Fact]
    public void Add_PositivePlusNegative_IsZero()
    {
        var r = I(42) + I(-42);
        Assert.Equal(0, r.Sign());
    }

    [Fact]
    public void Add_CarryFromLoToM1()
    {
        // ulong.MaxValue + 1 should carry into M1
        var a = new Int256(ulong.MaxValue, 0UL, 0UL, 0UL);
        var b = new Int256(1UL, 0UL, 0UL, 0UL);
        var r = a + b;
        Assert.Equal(0UL, r.Lo);
        Assert.Equal(1UL, r.M1);
        Assert.Equal(0UL, r.M2);
        Assert.Equal(0UL, r.Hi);
    }

    [Fact]
    public void Add_CarryPropagatesThroughAllLimbs()
    {
        // 0xFFFF...FFFF (all bits set across all limbs) + 1 = 0 (256-bit wrap)
        var allOnes = new Int256(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var r = allOnes + I(1);
        Assert.Equal(0UL, r.Lo);
        Assert.Equal(0UL, r.M1);
        Assert.Equal(0UL, r.M2);
        Assert.Equal(0UL, r.Hi);
    }

    [Fact]
    public void Add_LargeInt128Values_MatchesInt128Arithmetic()
    {
        Int128 a = Int128.MaxValue;
        Int128 b = Int128.MaxValue;
        // 2 * Int128.MaxValue = 2^128 - 2  (positive, fits in 256 bits)
        var r = FromInt128(a) + FromInt128(b);
        Assert.Equal(1, r.Sign());
        // Low 128 bits: MaxValue + MaxValue = 0xFFFF...FFFE in 128 bits
        // i.e. Lo = ulong.MaxValue - 1, M1 = ulong.MaxValue
        Assert.Equal(ulong.MaxValue - 1UL, r.Lo);
        Assert.Equal(ulong.MaxValue, r.M1);
        Assert.Equal(0UL, r.M2);
        Assert.Equal(0UL, r.Hi);
    }

    // -------------------------------------------------------------------------
    // Subtraction
    // -------------------------------------------------------------------------

    [Fact]
    public void Subtract_ValueMinusItself_IsZero()
    {
        var v = I(12345);
        Assert.Equal(0, (v - v).Sign());
    }

    [Fact]
    public void Subtract_OneMinusTwo_IsMinusOne()
    {
        var r = I(1) - I(2);
        Assert.Equal(-1, r.Sign());
    }

    [Fact]
    public void Subtract_LargePositiveMinusLargePositive()
    {
        var a = FromInt128(Int128.MaxValue);
        var b = FromInt128(Int128.MaxValue - 1);
        var r = a - b;
        Assert.Equal(1, r.Sign());
        Assert.Equal(1UL, r.Lo);
    }

    // -------------------------------------------------------------------------
    // Multiplication
    // -------------------------------------------------------------------------

    [Fact]
    public void Multiply_ZeroByAnything_IsZero()
    {
        Assert.Equal(0, Int256.Multiply(Int128.Zero, Int128.MaxValue).Sign());
        Assert.Equal(0, Int256.Multiply(Int128.MaxValue, Int128.Zero).Sign());
    }

    [Fact]
    public void Multiply_OneByOne_IsOne()
    {
        var r = Int256.Multiply(Int128.One, Int128.One);
        Assert.Equal(1, r.Sign());
        Assert.Equal(1UL, r.Lo);
    }

    [Fact]
    public void Multiply_NegativeOneByOne_IsMinusOne()
    {
        var r = Int256.Multiply(Int128.NegativeOne, Int128.One);
        Assert.Equal(-1, r.Sign());
    }

    [Fact]
    public void Multiply_NegativeOneByNegativeOne_IsOne()
    {
        var r = Int256.Multiply(Int128.NegativeOne, Int128.NegativeOne);
        Assert.Equal(1, r.Sign());
        Assert.Equal(1UL, r.Lo);
    }

    [Fact]
    public void Multiply_TwoByThree_IsSix()
    {
        var r = Int256.Multiply(2, 3);
        Assert.Equal(1, r.Sign());
        Assert.Equal(6UL, r.Lo);
    }

    [Fact]
    public void Multiply_NegativeTwoByThree_IsMinusSix()
    {
        var r = Int256.Multiply(-2, 3);
        Assert.Equal(-1, r.Sign());
        // -6 in two's complement
        var expected = -I(6);
        Assert.Equal(expected, r);
    }

    [Fact]
    public void Multiply_MatchesBigIntegerForMediumValues()
    {
        // Use System.Numerics.BigInteger as reference oracle.
        var cases = new (Int128 a, Int128 b)[]
        {
            (12345678L, 987654321L),
            (-12345678L, 987654321L),
            (12345678L, -987654321L),
            (-12345678L, -987654321L),
            (long.MaxValue, long.MaxValue),
            (long.MinValue, long.MaxValue),
            (long.MaxValue, long.MinValue),
            (long.MinValue, long.MinValue),
        };

        foreach (var (a, b) in cases)
        {
            var result = Int256.Multiply(a, b);
            var expected = (System.Numerics.BigInteger)a * (System.Numerics.BigInteger)b;

            // Compare signs
            int expectedSign = expected.Sign;
            Assert.Equal(expectedSign, result.Sign());

            // Compare magnitude via Lo limb
            if (expectedSign != 0)
            {
                var absExpected = System.Numerics.BigInteger.Abs(expected);
                ulong expectedLo = (ulong)(absExpected & ulong.MaxValue);
                var absResult = result.Sign() < 0 ? -result : result;
                Assert.Equal(expectedLo, absResult.Lo);
            }
        }
    }

    [Fact]
    public void Multiply_MaxValueByMaxValue_SignIsPositive()
    {
        // Int128.MaxValue * Int128.MaxValue is a large positive number (~2^254)
        var r = Int256.Multiply(Int128.MaxValue, Int128.MaxValue);
        Assert.Equal(1, r.Sign());
    }

    [Fact]
    public void Multiply_MaxValueByMinValue_SignIsNegative()
    {
        var r = Int256.Multiply(Int128.MaxValue, Int128.MinValue);
        Assert.Equal(-1, r.Sign());
    }

    [Fact]
    public void Multiply_MinValueByMinValue_SignIsPositive()
    {
        // (-2^127) * (-2^127) = 2^254, positive
        var r = Int256.Multiply(Int128.MinValue, Int128.MinValue);
        Assert.Equal(1, r.Sign());
    }

    [Fact]
    public void Multiply_FullOracleCheck_AgainstBigInteger()
    {
        // Cross-check the entire product (all four limbs) against BigInteger for
        // cases small enough that BigInteger is authoritative and fast.
        var cases = new (long a, long b)[]
        {
            (0, 0), (1, 0), (0, 1), (1, 1),
            (-1, 1), (1, -1), (-1, -1),
            (100, 200), (-100, 200), (100, -200), (-100, -200),
            (long.MaxValue, 2L),
            (long.MaxValue, -2L),
            (long.MinValue, 2L),
            (long.MinValue, -1L),   // Int128.MinValue * -1 = Int128.MaxValue + 1, fits in 128 bits
        };

        foreach (var (a, b) in cases)
        {
            Int128 ia = a, ib = b;
            var result = Int256.Multiply(ia, ib);
            var expected = (System.Numerics.BigInteger)a * (System.Numerics.BigInteger)b;

            AssertEqualsBigInteger(expected, result, $"({a}) * ({b})");
        }
    }

    // -------------------------------------------------------------------------
    // Equality
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = I(42);
        var b = I(42);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        Assert.NotEqual(I(1), I(2));
    }

    // -------------------------------------------------------------------------
    // Round-trip: a + (-a) == 0 and -(-a) == a for all Int128 values
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public void AddNegation_IsZero(long x)
    {
        var v = I(x);
        Assert.Equal(0, (v + (-v)).Sign());
    }

    // -------------------------------------------------------------------------
    // Oracle helper
    // -------------------------------------------------------------------------

    private static void AssertEqualsBigInteger(
        System.Numerics.BigInteger expected,
        Int256 actual,
        string label)
    {
        // Reconstruct BigInteger from the four limbs treating as two's complement.
        bool negative = actual.Sign() < 0;
        Int256 abs = negative ? -actual : actual;

        var reconstructed = (System.Numerics.BigInteger)abs.Hi;
        reconstructed = (reconstructed << 64) | abs.M2;
        reconstructed = (reconstructed << 64) | abs.M1;
        reconstructed = (reconstructed << 64) | abs.Lo;
        if (negative) reconstructed = -reconstructed;

        Assert.True(
            expected == reconstructed,
            $"{label}: expected {expected}, got {reconstructed}");
    }

    // -------------------------------------------------------------------------
    // DivideToInt64
    // -------------------------------------------------------------------------

    [Fact]
    public void DivideToInt64_BothPositive()
    {
        // 15 / 4 → 4  (floor 3.75, round half-away = 4)
        Assert.Equal(4L, Int256.DivideToInt64(I(15), (Int128)4));
    }

    [Fact]
    public void DivideToInt64_NumeratorNegative()
    {
        // -15 / 4 → -4
        Assert.Equal(-4L, Int256.DivideToInt64(I(-15), (Int128)4));
    }

    [Fact]
    public void DivideToInt64_DenominatorNegative()
    {
        // 15 / -4 → -4
        Assert.Equal(-4L, Int256.DivideToInt64(I(15), (Int128)(-4)));
    }

    [Fact]
    public void DivideToInt64_BothNegative()
    {
        // -15 / -4 → 4
        Assert.Equal(4L, Int256.DivideToInt64(I(-15), (Int128)(-4)));
    }

    [Fact]
    public void DivideToInt64_ExactDivision()
    {
        // 12 / 4 = 3 exactly
        Assert.Equal(3L, Int256.DivideToInt64(I(12), (Int128)4));
    }

    [Fact]
    public void DivideToInt64_NumeratorZero()
    {
        Assert.Equal(0L, Int256.DivideToInt64(Int256.Zero, (Int128)7));
        Assert.Equal(0L, Int256.DivideToInt64(Int256.Zero, (Int128)(-7)));
    }

    [Fact]
    public void DivideToInt64_DenominatorOne()
    {
        Assert.Equal(42L, Int256.DivideToInt64(I(42), (Int128)1));
        Assert.Equal(-42L, Int256.DivideToInt64(I(-42), (Int128)1));
    }

    [Fact]
    public void DivideToInt64_DenominatorMinusOne()
    {
        Assert.Equal(-42L, Int256.DivideToInt64(I(42), (Int128)(-1)));
        Assert.Equal(42L, Int256.DivideToInt64(I(-42), (Int128)(-1)));
    }

    [Fact]
    public void DivideToInt64_RoundingHalfUp_Positive()
    {
        // 5 / 2 = 2.5 → rounds to 3 (half-away-from-zero)
        Assert.Equal(3L, Int256.DivideToInt64(I(5), (Int128)2));
    }

    [Fact]
    public void DivideToInt64_RoundingHalfUp_Negative()
    {
        // -5 / 2 = -2.5 → rounds to -3 (half-away-from-zero)
        Assert.Equal(-3L, Int256.DivideToInt64(I(-5), (Int128)2));
    }

    [Fact]
    public void DivideToInt64_Rounding_BelowHalf()
    {
        // 7 / 4 = 1.75 → rounds to 2
        Assert.Equal(2L, Int256.DivideToInt64(I(7), (Int128)4));
        // 5 / 4 = 1.25 → rounds to 1
        Assert.Equal(1L, Int256.DivideToInt64(I(5), (Int128)4));
    }

    [Fact]
    public void DivideToInt64_LargeValues_AgainstBigInteger()
    {
        // num ≈ 2^163, den ≈ 2^110, quotient ≈ 2^53
        // Build numerator as Int256 via Multiply
        Int128 a = (Int128)1 << 81;   // 2^81
        Int128 b = (Int128)1 << 82;   // 2^82
        Int256 num = Int256.Multiply(a, b); // exact product = 2^163

        Int128 den = (Int128)1 << 110; // 2^110
        long quotient = Int256.DivideToInt64(num, den);

        // 2^163 / 2^110 = 2^53 exactly
        Assert.Equal(1L << 53, quotient);
    }

    [Fact]
    public void DivideToInt64_LargeValues_WithRemainder_AgainstBigInteger()
    {
        // Use BigInteger as oracle for a non-divisible large case
        long aRaw  = PredicatesInt.MaxCoordinate;     // 2^53
        long bRaw  = aRaw - 1;
        long cRaw  = -(aRaw - 2);
        long dRaw  = aRaw - 3;

        // Orient2dRaw returns at most magnitude ~2^109 for MaxCoordinate inputs
        Int128 acd = PredicatesInt.Orient2dRaw(
            new V2i(aRaw, 0), new V2i(cRaw, 0), new V2i(0, dRaw));
        Int128 bcd = PredicatesInt.Orient2dRaw(
            new V2i(bRaw, 0), new V2i(cRaw, 0), new V2i(0, dRaw));
        Int128 denom = acd - bcd;
        if (denom == Int128.Zero) return; // degenerate input, skip

        long delta = bRaw - aRaw; // = -1
        Int256 numX = Int256.Multiply(acd, (Int128)delta);

        long result = Int256.DivideToInt64(numX, denom);

        // Verify against BigInteger
        var bigAcd  = ToBig128(acd);
        var bigDen  = ToBig128(denom);
        var bigNumX = bigAcd * (System.Numerics.BigInteger)delta;
        var bigQ    = System.Numerics.BigInteger.DivRem(bigNumX, bigDen, out var bigR);
        // Round half-away-from-zero
        if (2 * System.Numerics.BigInteger.Abs(bigR) >= System.Numerics.BigInteger.Abs(bigDen))
            bigQ += bigQ < 0 ? -1 : 1;

        Assert.True(bigQ == (System.Numerics.BigInteger)result,
            $"expected {bigQ}, got {result}");
    }

    // Helper: Int128 → BigInteger
    private static System.Numerics.BigInteger ToBig128(Int128 v)
    {
#if NET7_0_OR_GREATER
        return (System.Numerics.BigInteger)v;
#else
        bool neg = v < Int128.Zero;
        Int128 abs = neg ? -v : v;
        var result = (new System.Numerics.BigInteger((long)abs._hi) << 64)
                   | new System.Numerics.BigInteger(abs._lo);
        return neg ? -result : result;
#endif
    }
}
