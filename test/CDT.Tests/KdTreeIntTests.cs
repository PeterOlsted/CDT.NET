// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;

/// <summary>
/// Tests for the integer <see cref="KdTree"/> (concrete, long-coordinate variant).
/// </summary>
public sealed class KdTreeIntTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (KdTree tree, List<V2i> pts) Build(IEnumerable<(long x, long y)> coords)
    {
        var pts = coords.Select(c => new V2i(c.x, c.y)).ToList();
        var tree = new KdTree();
        for (int i = 0; i < pts.Count; i++)
            tree.Insert(i, pts);
        return (tree, pts);
    }

    /// <summary>Brute-force nearest neighbour (ground truth).</summary>
    private static int BruteNearest(List<V2i> pts, long qx, long qy)
    {
        int best = 0;
        Int128 bestD = Int128.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            long dx = pts[i].X - qx, dy = pts[i].Y - qy;
            Int128 d = (Int128)dx * dx + (Int128)dy * dy;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Fact]
    public void EmptyTree_SizeIsZero()
    {
        var tree = new KdTree();
        Assert.Equal(0, tree.Size);
    }

    [Fact]
    public void AfterInsert_SizeIncreases()
    {
        var pts = new List<V2i> { new(0, 0) };
        var tree = new KdTree();
        tree.Insert(0, pts);
        Assert.Equal(1, tree.Size);
    }

    // -------------------------------------------------------------------------
    // Nearest — trivial cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Nearest_SinglePoint_ReturnsThatPoint()
    {
        var (tree, pts) = Build([(10, 20)]);
        Assert.Equal(0, tree.Nearest(0, 0, pts));
    }

    [Fact]
    public void Nearest_QueryAtExactPoint_ReturnsThatIndex()
    {
        var (tree, pts) = Build([(1, 2), (5, 6), (9, 3)]);
        Assert.Equal(1, tree.Nearest(5, 6, pts));
    }

    [Fact]
    public void Nearest_TwoPoints_ReturnsCloser()
    {
        var (tree, pts) = Build([(0, 0), (100, 100)]);
        // Query near origin → should return index 0
        Assert.Equal(0, tree.Nearest(1, 1, pts));
        // Query near far corner → should return index 1
        Assert.Equal(1, tree.Nearest(99, 99, pts));
    }

    // -------------------------------------------------------------------------
    // Nearest — multiple points (cross-check against brute force)
    // -------------------------------------------------------------------------

    [Fact]
    public void Nearest_Grid_MatchesBruteForce()
    {
        // 5×5 integer grid
        var coords = new List<(long, long)>();
        for (int x = 0; x < 5; x++)
            for (int y = 0; y < 5; y++)
                coords.Add((x * 10, y * 10));

        var (tree, pts) = Build(coords);

        long[] queries = [-5, 0, 5, 15, 22, 37, 45];
        foreach (long qx in queries)
            foreach (long qy in queries)
            {
                int kdNearest = tree.Nearest(qx, qy, pts);
                int brute = BruteNearest(pts, qx, qy);
                // Both should yield same squared distance (may be different index if tied)
                long dx1 = pts[kdNearest].X - qx, dy1 = pts[kdNearest].Y - qy;
                long dx2 = pts[brute].X - qx, dy2 = pts[brute].Y - qy;
                Int128 d1 = (Int128)dx1*dx1 + (Int128)dy1*dy1;
                Int128 d2 = (Int128)dx2*dx2 + (Int128)dy2*dy2;
                Assert.Equal(d2, d1);
            }
    }

    [Fact]
    public void Nearest_Random100Points_MatchesBruteForce()
    {
        var rng = new Random(42);
        var coords = Enumerable.Range(0, 100)
            .Select(_ => ((long)rng.Next(-1000, 1000), (long)rng.Next(-1000, 1000)))
            .ToList();
        var (tree, pts) = Build(coords);

        for (int q = 0; q < 50; q++)
        {
            long qx = rng.Next(-1100, 1100), qy = rng.Next(-1100, 1100);
            int kdNearest = tree.Nearest(qx, qy, pts);
            int brute = BruteNearest(pts, qx, qy);

            long dx1 = pts[kdNearest].X - qx, dy1 = pts[kdNearest].Y - qy;
            long dx2 = pts[brute].X - qx,    dy2 = pts[brute].Y - qy;
            Int128 d1 = (Int128)dx1*dx1 + (Int128)dy1*dy1;
            Int128 d2 = (Int128)dx2*dx2 + (Int128)dy2*dy2;
            Assert.Equal(d2, d1);
        }
    }

    // -------------------------------------------------------------------------
    // Nearest — more than NumVerticesInLeaf (32) points → forces splits
    // -------------------------------------------------------------------------

    [Fact]
    public void Nearest_ManyPoints_ForcesLeafSplits_MatchesBruteForce()
    {
        // Insert 100 points along a line — forces the tree to split many times.
        var coords = Enumerable.Range(0, 100).Select(i => ((long)i, 0L)).ToList();
        var (tree, pts) = Build(coords);

        Assert.Equal(100, tree.Size);

        // Query at every integer from -5 to 105
        for (long qx = -5; qx <= 105; qx++)
        {
            int kdNearest = tree.Nearest(qx, 0, pts);
            int brute = BruteNearest(pts, qx, 0);

            long dx1 = pts[kdNearest].X - qx, dy1 = pts[kdNearest].Y - qx;
            long dx2 = pts[brute].X - qx,    dy2 = pts[brute].Y - qx;
            Int128 d1 = (Int128)dx1*dx1 + (Int128)dy1*dy1;
            Int128 d2 = (Int128)dx2*dx2 + (Int128)dy2*dy2;
            Assert.Equal(d2, d1);
        }
    }

    // -------------------------------------------------------------------------
    // Pre-supplied bounding box constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Nearest_WithPresetBox_MatchesBruteForce()
    {
        var pts = new List<V2i>
        {
            new(10, 10), new(20, 10), new(10, 20), new(20, 20), new(15, 15)
        };
        var tree = new KdTree(0, 0, 30, 30);
        for (int i = 0; i < pts.Count; i++)
            tree.Insert(i, pts);

        for (long qx = 0; qx <= 30; qx += 5)
            for (long qy = 0; qy <= 30; qy += 5)
            {
                int kdNearest = tree.Nearest(qx, qy, pts);
                int brute = BruteNearest(pts, qx, qy);

                long dx1 = pts[kdNearest].X - qx, dy1 = pts[kdNearest].Y - qy;
                long dx2 = pts[brute].X - qx,    dy2 = pts[brute].Y - qy;
                Int128 d1 = (Int128)dx1*dx1 + (Int128)dy1*dy1;
                Int128 d2 = (Int128)dx2*dx2 + (Int128)dy2*dy2;
                Assert.Equal(d2, d1);
            }
    }

    // -------------------------------------------------------------------------
    // Negative and large coordinates
    // -------------------------------------------------------------------------

    [Fact]
    public void Nearest_NegativeCoordinates_MatchesBruteForce()
    {
        var (tree, pts) = Build([(-100, -50), (-10, -10), (-1, -1), (0, 0)]);
        Assert.Equal(BruteNearest(pts, -5, -5), tree.Nearest(-5, -5, pts));
        Assert.Equal(BruteNearest(pts, -99, -49), tree.Nearest(-99, -49, pts));
    }

    [Fact]
    public void Nearest_LargeCoordinates_MatchesBruteForce()
    {
        long M = 1L << 40;
        var (tree, pts) = Build([(0, 0), (M, 0), (0, M), (M, M)]);
        long qx = M / 3, qy = M / 3;
        Assert.Equal(BruteNearest(pts, qx, qy), tree.Nearest(qx, qy, pts));
    }

    // -------------------------------------------------------------------------
    // GetMid overflow safety
    // -------------------------------------------------------------------------

    [Fact]
    public void GetMid_DoesNotOverflowForSymmetricBox()
    {
        // A box centred on zero with half-width 2^53 would use
        // minX + (maxX - minX) / 2 = -M + 2M/2 = 0. Verify tree works.
        long M = 1L << 53;
        var pts = new List<V2i> { new(-M, 0), new(M, 0), new(0, -M), new(0, M) };
        var tree = new KdTree(-M, -M, M, M);
        for (int i = 0; i < pts.Count; i++) tree.Insert(i, pts);

        Assert.Equal(BruteNearest(pts, 1, 1), tree.Nearest(1, 1, pts));
        Assert.Equal(BruteNearest(pts, -1, -1), tree.Nearest(-1, -1, pts));
    }
}
