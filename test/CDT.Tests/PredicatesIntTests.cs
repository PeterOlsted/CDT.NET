// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using CDT.Predicates;

namespace CDT.Tests;

/// <summary>
/// Tests for <see cref="PredicatesInt"/> — exact integer geometric predicates.
///
/// Each test specifies expected sign (+1/0/-1) with a brief geometric
/// justification. Degenerate cases (collinear, cocircular) are tested
/// explicitly because they are the cases where floating-point predicates
/// most often produce incorrect results.
/// </summary>
public sealed class PredicatesIntTests
{
    // =========================================================================
    // Orient2dInt
    // =========================================================================

    // ---- Basic orientation ----

    [Fact]
    public void Orient2d_CounterClockwise_ReturnsPositive()
    {
        // a=(0,0), b=(2,0), c=(1,1): c is to the LEFT of a→b
        int r = PredicatesInt.Orient2dInt(0, 0, 2, 0, 1, 1);
        Assert.Equal(1, r);
    }

    [Fact]
    public void Orient2d_Clockwise_ReturnsNegative()
    {
        // a=(0,0), b=(2,0), c=(1,-1): c is to the RIGHT of a→b
        int r = PredicatesInt.Orient2dInt(0, 0, 2, 0, 1, -1);
        Assert.Equal(-1, r);
    }

    [Fact]
    public void Orient2d_Collinear_ReturnsZero()
    {
        // a=(0,0), b=(1,1), c=(2,2): exactly collinear
        int r = PredicatesInt.Orient2dInt(0, 0, 1, 1, 2, 2);
        Assert.Equal(0, r);
    }

    [Fact]
    public void Orient2d_CollinearOnAxis_ReturnsZero()
    {
        int r = PredicatesInt.Orient2dInt(0, 0, 5, 0, 3, 0);
        Assert.Equal(0, r);
    }

    [Fact]
    public void Orient2d_SamePoint_ReturnsZero()
    {
        // All three points identical → degenerate, area = 0
        int r = PredicatesInt.Orient2dInt(3, 3, 3, 3, 3, 3);
        Assert.Equal(0, r);
    }

    // ---- Sign is antisymmetric: swapping a and b flips sign ----

    [Fact]
    public void Orient2d_SwapAB_FlipsSign()
    {
        int r1 = PredicatesInt.Orient2dInt(0, 0, 4, 0, 2, 1);
        int r2 = PredicatesInt.Orient2dInt(4, 0, 0, 0, 2, 1);
        Assert.Equal(1, r1);
        Assert.Equal(-1, r2);
    }

    // ---- Negative coordinates ----

    [Fact]
    public void Orient2d_NegativeCoordinates_CorrectSign()
    {
        // a=(-2,-2), b=(2,-2), c=(0,1): standard CCW triangle in lower half-plane
        int r = PredicatesInt.Orient2dInt(-2, -2, 2, -2, 0, 1);
        Assert.Equal(1, r);
    }

    // ---- Large coordinates (near MaxCoordinate) ----

    [Fact]
    public void Orient2d_LargeCoordinates_CCW()
    {
        long M = PredicatesInt.MaxCoordinate;
        // Points near the coordinate limit: (0,0), (M,0), (0,M) → CCW
        int r = PredicatesInt.Orient2dInt(0, 0, M, 0, 0, M);
        Assert.Equal(1, r);
    }

    [Fact]
    public void Orient2d_LargeCoordinates_Collinear()
    {
        long M = PredicatesInt.MaxCoordinate;
        // Three collinear points with large coordinates
        int r = PredicatesInt.Orient2dInt(0, 0, M, M, M / 2, M / 2);
        Assert.Equal(0, r);
    }

    // ---- V2i overload ----

    [Fact]
    public void Orient2d_V2iOverload_MatchesScalar()
    {
        var p  = new V2i(1, 1);
        var v1 = new V2i(0, 0);
        var v2 = new V2i(2, 0);
        Assert.Equal(
            PredicatesInt.Orient2dInt(0, 0, 2, 0, 1, 1),
            PredicatesInt.Orient2dInt(p, v1, v2));
    }

    // ---- Orient2dRaw returns actual determinant value ----

    [Fact]
    public void Orient2dRaw_CCW_ReturnsPositiveInt128()
    {
        Int128 r = PredicatesInt.Orient2dRaw(0, 0, 2, 0, 1, 1);
        Assert.True(r > Int128.Zero);
    }

    [Fact]
    public void Orient2dRaw_Collinear_ReturnsZero()
    {
        Int128 r = PredicatesInt.Orient2dRaw(0, 0, 3, 3, 1, 1);
        Assert.Equal(Int128.Zero, r);
    }

    [Fact]
    public void Orient2dRaw_KnownValue()
    {
        // Triangle (0,0),(4,0),(2,3): det = 4*3 - 0*2 = 12
        // det = (ax-cx)*(by-cy) - (ay-cy)*(bx-cx)
        //     = (0-2)*(0-3) - (0-3)*(4-2)
        //     = (-2)*(-3) - (-3)*(2) = 6 + 6 = 12
        Int128 r = PredicatesInt.Orient2dRaw(0, 0, 4, 0, 2, 3);
        Assert.Equal((Int128)12, r);
    }

    // =========================================================================
    // InCircleInt
    // =========================================================================

    // ---- Reference triangle: (0,0), (2,0), (0,2) ----
    // Circumcenter: (1,1), radius sqrt(2).
    // Point (2,2) is exactly ON the circumcircle (distance to center = sqrt(2)).
    // Point (1,1) is INSIDE.
    // Point (3,3) is OUTSIDE.

    [Fact]
    public void InCircle_PointInside_ReturnsPositive()
    {
        // d=(1,1) is strictly inside circumcircle of (0,0),(2,0),(0,2)
        int r = PredicatesInt.InCircleInt(0, 0, 2, 0, 0, 2, 1, 1);
        Assert.Equal(1, r);
    }

    [Fact]
    public void InCircle_PointOutside_ReturnsNegative()
    {
        // d=(3,3) is strictly outside
        int r = PredicatesInt.InCircleInt(0, 0, 2, 0, 0, 2, 3, 3);
        Assert.Equal(-1, r);
    }

    [Fact]
    public void InCircle_PointOnCircumcircle_ReturnsZero()
    {
        // d=(2,2) lies exactly on the circumcircle of (0,0),(2,0),(0,2).
        // Verified analytically: det = 0.
        int r = PredicatesInt.InCircleInt(0, 0, 2, 0, 0, 2, 2, 2);
        Assert.Equal(0, r);
    }

    // ---- Axis-aligned right triangle: (0,0),(4,0),(0,3) ----
    // Circumcenter: (2, 1.5) — not integer, but (4,3) is on the circumcircle
    // since distance from (2,1.5) to (4,3) = sqrt(4+2.25) = sqrt(6.25) = 2.5 = radius.

    [Fact]
    public void InCircle_RightTriangle_PointOnCircumcircle_ReturnsZero()
    {
        // (4,3) is on circumcircle of (0,0),(4,0),(0,3) — verified: det=0
        int r = PredicatesInt.InCircleInt(0, 0, 4, 0, 0, 3, 4, 3);
        Assert.Equal(0, r);
    }

    // ---- Symmetry: result sign flips when triangle vertices are swapped (CW → CCW) ----

    [Fact]
    public void InCircle_CwTriangle_SignFlips()
    {
        // Swap b and c to reverse triangle orientation — sign of det flips.
        int ccw = PredicatesInt.InCircleInt(0, 0, 2, 0, 0, 2, 1, 1);  // inside → +1
        int cw  = PredicatesInt.InCircleInt(0, 0, 0, 2, 2, 0, 1, 1);  // same but CW → -1
        Assert.Equal( 1, ccw);
        Assert.Equal(-1, cw);
    }

    // ---- Negative coordinates ----

    [Fact]
    public void InCircle_NegativeCoordinates_InsideReturnsPositive()
    {
        // Translate the reference triangle to (-10,-10)
        int r = PredicatesInt.InCircleInt(-10, -10, -8, -10, -10, -8, -9, -9);
        Assert.Equal(1, r);
    }

    // ---- Large coordinates (near MaxCoordinate) ----

    [Fact]
    public void InCircle_LargeCoordinates_InsideReturnsPositive()
    {
        long M = PredicatesInt.MaxCoordinate / 2;
        // Scale reference triangle by M/2
        int r = PredicatesInt.InCircleInt(0, 0, 2*M, 0, 0, 2*M, M, M);
        Assert.Equal(1, r);
    }

    [Fact]
    public void InCircle_LargeCoordinates_OnCircumcircle_ReturnsZero()
    {
        long M = PredicatesInt.MaxCoordinate / 2;
        int r = PredicatesInt.InCircleInt(0, 0, 2*M, 0, 0, 2*M, 2*M, 2*M);
        Assert.Equal(0, r);
    }

    // ---- V2i overload ----

    [Fact]
    public void InCircle_V2iOverload_MatchesScalar()
    {
        var p  = new V2i(1, 1);
        var v1 = new V2i(0, 0);
        var v2 = new V2i(2, 0);
        var v3 = new V2i(0, 2);
        Assert.Equal(
            PredicatesInt.InCircleInt(0, 0, 2, 0, 0, 2, 1, 1),
            PredicatesInt.InCircleInt(p, v1, v2, v3));
    }

    // ---- InCircle sign on known non-degenerate cases ----

    [Theory]
    [InlineData(0, 0, 2, 0, 0, 2,  1,  1,  1)]   // inside
    [InlineData(0, 0, 2, 0, 0, 2,  3,  3, -1)]   // outside
    [InlineData(0, 0, 4, 0, 0, 3,  1,  1,  1)]   // inside
    [InlineData(0, 0, 4, 0, 0, 3,  5,  5, -1)]   // outside
    public void InCircle_KnownSignOnNonDegenerate(
        long ax, long ay, long bx, long by, long cx, long cy, long dx, long dy, int expected)
    {
        int result = PredicatesInt.InCircleInt(ax, ay, bx, by, cx, cy, dx, dy);
        Assert.Equal(expected, result);
    }

    // ---- Degenerate: point at one of the triangle vertices ----

    [Fact]
    public void InCircle_QueryAtVertex_ReturnsZero()
    {
        // d coincides with vertex a — it's on the circumcircle
        int r = PredicatesInt.InCircleInt(0, 0, 2, 0, 0, 2, 0, 0);
        Assert.Equal(0, r);
    }

    // ---- Orient2d consistency: sign of 3rd term matches orientation ----

    [Fact]
    public void Orient2d_IsConsistentWithTriangleArea()
    {
        // Triangle (0,0),(3,0),(0,4): area = (1/2)*3*4 = 6
        // det = (0-0)*(0-4) - (0-4)*(3-0) = 0 - (-12) = 12 = 2*area ✓
        Int128 raw = PredicatesInt.Orient2dRaw(0, 0, 3, 0, 0, 4);
        Assert.Equal((Int128)12, raw);
    }
}
