// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Runtime.CompilerServices;

namespace CDT.Predicates;

/// <summary>
/// Exact geometric predicates using integer arithmetic.
/// All operations are branchless for the common case and produce a
/// fully correct sign (positive / zero / negative) for any input within
/// the documented coordinate range.
/// </summary>
/// <remarks>
/// <para>
/// <b>Coordinate range:</b> input coordinates must satisfy
/// <c>|x|, |y| &lt;= <see cref="MaxCoordinate"/></c> (2^53).
/// With this constraint every intermediate value fits within 256 bits:
/// <list type="bullet">
///   <item>Orient2d: differences ≤ 2^54, products ≤ 2^108 — fits in <see cref="Int128"/>.</item>
///   <item>InCircle: lifts ≤ 2^109, cross-products ≤ 2^109, their product ≤ 2^218 — fits in <see cref="Int256"/>.</item>
/// </list>
/// </para>
/// </remarks>
internal static class PredicatesInt
{
    /// <summary>
    /// Maximum safe absolute value for any input coordinate.
    /// Equals 2^53 (= 9,007,199,254,740,992), matching the exact-integer
    /// representable range of IEEE 754 double precision.
    /// </summary>
    public const long MaxCoordinate = 1L << 53;

    // =========================================================================
    // Orient2d
    // =========================================================================

    /// <summary>
    /// Exact orient2d predicate.
    /// Determines whether point <c>c</c> is to the left of, on, or to the right of
    /// the directed line from <c>a</c> to <c>b</c>.
    /// </summary>
    /// <returns>+1 if c is left of a→b, -1 if right, 0 if collinear.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Orient2dInt(long ax, long ay, long bx, long by, long cx, long cy)
    {
        Int128 det = Orient2dRaw(ax, ay, bx, by, cx, cy);
        return det.CompareTo(Int128.Zero);
    }

    /// <summary>
    /// Convenience overload taking <see cref="V2i"/> points.
    /// Returns +1, 0, or -1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Orient2dInt(V2i p, V2i v1, V2i v2)
        => Orient2dInt(v1.X, v1.Y, v2.X, v2.Y, p.X, p.Y);

    /// <summary>
    /// Returns the raw signed-area determinant of the triangle (a, b, c)
    /// as an <see cref="Int128"/> without reducing to sign.
    /// Required by <c>IntersectionPosition</c> (Batch 3) which needs the
    /// actual determinant value for interpolation parameter computation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 Orient2dRaw(long ax, long ay, long bx, long by, long cx, long cy)
    {
        // det = (ax-cx)*(by-cy) - (ay-cy)*(bx-cx)
        // Each difference fits in Int128 (inputs are long; diff ≤ 2*long.MaxValue ≤ 2^64).
        // Each product ≤ 2^108 for inputs within MaxCoordinate — fits in Int128.
        Int128 acx = (Int128)ax - cx;
        Int128 bcy = (Int128)by - cy;
        Int128 acy = (Int128)ay - cy;
        Int128 bcx = (Int128)bx - cx;
        return acx * bcy - acy * bcx;
    }

    /// <summary>
    /// Convenience overload taking <see cref="V2i"/> points.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 Orient2dRaw(V2i p, V2i v1, V2i v2)
        => Orient2dRaw(v1.X, v1.Y, v2.X, v2.Y, p.X, p.Y);

    // =========================================================================
    // InCircle
    // =========================================================================

    /// <summary>
    /// Exact incircle predicate.
    /// Determines whether point <c>d</c> is inside, on, or outside the
    /// circumcircle of the triangle (<c>a</c>, <c>b</c>, <c>c</c>) given in CCW order.
    /// </summary>
    /// <returns>+1 if d is inside the circumcircle, -1 if outside, 0 if on.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int InCircleInt(
        long ax, long ay, long bx, long by,
        long cx, long cy, long dx, long dy)
    {
        // Translate so d is at the origin (reduces problem to 3×3 determinant).
        // All differences computed in Int128 — each fits comfortably (≤ 2^54 for
        // inputs within MaxCoordinate).
        Int128 adx = (Int128)ax - dx;
        Int128 ady = (Int128)ay - dy;
        Int128 bdx = (Int128)bx - dx;
        Int128 bdy = (Int128)by - dy;
        Int128 cdx = (Int128)cx - dx;
        Int128 cdy = (Int128)cy - dy;

        // Squared distances (lifts) — always non-negative.
        // Each ≤ 2 * (2^54)^2 = 2^109, fits in Int128 (max 2^127).
        Int128 alift = adx * adx + ady * ady;
        Int128 blift = bdx * bdx + bdy * bdy;
        Int128 clift = cdx * cdx + cdy * cdy;

        // 2×2 sub-determinants (cross-products).
        // Each product ≤ (2^54)^2 = 2^108, fits in Int128.
        // Differences of two such products ≤ 2^109, fits in Int128.
        Int128 bcCross = bdx * cdy - cdx * bdy;
        Int128 caCross = cdx * ady - adx * cdy;
        Int128 abCross = adx * bdy - bdx * ady;

        // Full 4×4 determinant as sum of three lift × cross terms.
        // Each product lift * cross ≤ 2^109 * 2^109 = 2^218 — fits in Int256.
        // Sum of three ≤ 3 * 2^218 < 2^220 — fits in Int256 (max 2^255).
        Int256 det = Int256.Multiply(alift, bcCross)
                   + Int256.Multiply(blift, caCross)
                   + Int256.Multiply(clift, abCross);

        return det.Sign();
    }

    /// <summary>
    /// Convenience overload taking <see cref="V2i"/> points.
    /// Returns +1 if <paramref name="p"/> is inside the circumcircle of
    /// (<paramref name="v1"/>, <paramref name="v2"/>, <paramref name="v3"/>),
    /// -1 if outside, 0 if on.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int InCircleInt(V2i p, V2i v1, V2i v2, V2i v3)
        => InCircleInt(v1.X, v1.Y, v2.X, v2.Y, v3.X, v3.Y, p.X, p.Y);
}
