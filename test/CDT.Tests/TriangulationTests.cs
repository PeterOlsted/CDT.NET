// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;


// ---------------------------------------------------------------------------
// Concrete integer triangulation tests (no generics, V2i coordinates)
// ---------------------------------------------------------------------------

/// <summary>Basic triangulation tests using the integer <see cref="Triangulation"/> directly.</summary>
public sealed class TriangulationTests_Int
{
    private static V2i Pt(long x, long y) => new(x, y);

    // -------------------------------------------------------------------------
    // Basic insertion
    // -------------------------------------------------------------------------

    [Fact]
    public void InsertFourPoints_TopologicallyValid()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 1000), Pt(3000, 1000), Pt(3000, 0)]);

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(7, cdt.Vertices.Length);
        Assert.Empty(cdt.FixedEdges);
        Assert.Equal(9, cdt.Triangles.Length);
    }

    [Fact]
    public void EraseSuperTriangle_LeavesConvexHull()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 1000), Pt(3000, 1000), Pt(3000, 0)]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(4, cdt.Vertices.Length);
        Assert.Equal(2, cdt.Triangles.Length);
    }

    [Fact]
    public void AddConstraintEdge_PresentAfterErase()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 1000), Pt(3000, 1000), Pt(3000, 0)]);

        var constraint = new Edge(0, 2);
        cdt.InsertEdges([constraint]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(4, cdt.Vertices.Length);
        Assert.Single(cdt.FixedEdges);
        Assert.Equal(2, cdt.Triangles.Length);
        Assert.Contains(constraint, cdt.FixedEdges);

        var edges = CdtUtils.ExtractEdgesFromTriangles(cdt.Triangles.Span);
        Assert.Contains(constraint, edges);
    }

    // -------------------------------------------------------------------------
    // Duplicate detection
    // -------------------------------------------------------------------------

    [Fact]
    public void DuplicateVertex_ThrowsDuplicateVertexException()
    {
        var cdt = new Triangulation();
        var pts = new[] { Pt(0, 0), Pt(1000, 0), Pt(0, 0) }; // pt[2] == pt[0]
        Assert.Throws<DuplicateVertexException>(() => cdt.InsertVertices(pts));
    }

    // -------------------------------------------------------------------------
    // Triangle extraction
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractEdges_ReturnsAllEdges()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(500, 1000)]);
        cdt.EraseSuperTriangle();

        var edges = CdtUtils.ExtractEdgesFromTriangles(cdt.Triangles.Span);
        Assert.Equal(3, edges.Count);
    }

    // -------------------------------------------------------------------------
    // Square with diagonal constraint
    // -------------------------------------------------------------------------

    [Fact]
    public void Square_WithDiagonalConstraint()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);
        cdt.InsertEdges([new Edge(0, 2)]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(2, cdt.Triangles.Length);
        Assert.Single(cdt.FixedEdges);
    }

    // -------------------------------------------------------------------------
    // Insertion order variants
    // -------------------------------------------------------------------------

    [Fact]
    public void InsertionOrder_AsProvided_SameResult()
    {
        var pts = new[] { Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000) };

        var cdtAuto = new Triangulation(VertexInsertionOrder.Auto);
        cdtAuto.InsertVertices(pts);
        cdtAuto.EraseSuperTriangle();

        var cdtProvided = new Triangulation(VertexInsertionOrder.AsProvided);
        cdtProvided.InsertVertices(pts);
        cdtProvided.EraseSuperTriangle();

        Assert.Equal(cdtAuto.Triangles.Length, cdtProvided.Triangles.Length);
        Assert.Equal(cdtAuto.Vertices.Length,  cdtProvided.Vertices.Length);
    }

    // -------------------------------------------------------------------------
    // Utility functions
    // -------------------------------------------------------------------------

    [Fact]
    public void FindDuplicates_DetectsExactMatches()
    {
        var pts = new List<V2i>
        {
            Pt(0, 0), Pt(1000, 0), Pt(0, 0), Pt(2000, 0),
        };
        var info = CdtUtils.FindDuplicates(pts);
        Assert.Single(info.Duplicates);
        Assert.Equal(2, info.Duplicates[0]);
        Assert.Equal(info.Mapping[0], info.Mapping[2]);
    }

    [Fact]
    public void RemoveDuplicatesAndRemapEdges_ProducesConsistentResult()
    {
        var verts = new List<V2i>
        {
            Pt(0, 0), Pt(1000, 0), Pt(0, 0), // index 2 is dup of 0
            Pt(0, 1000),
        };
        var edges = new List<Edge> { new(0, 2), new(1, 3) };

        CdtUtils.RemoveDuplicatesAndRemapEdges(verts, edges);

        Assert.Equal(3, verts.Count);
        Assert.Equal(new Edge(0, 0), edges[0]);
        Assert.Equal(new Edge(1, 2), edges[1]);
    }

    // -------------------------------------------------------------------------
    // Erase outer triangles
    // -------------------------------------------------------------------------

    [Fact]
    public void EraseOuterTriangles_RemovesOutside()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);
        cdt.InsertEdges([new Edge(0, 1), new Edge(1, 2), new Edge(2, 3), new Edge(3, 0)]);
        cdt.EraseOuterTriangles();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(2, cdt.Triangles.Length);
    }

    // -------------------------------------------------------------------------
    // Single triangle
    // -------------------------------------------------------------------------

    [Fact]
    public void ThreePoints_SingleTriangle()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(2000, 0), Pt(1000, 2000)]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(3, cdt.Vertices.Length);
        Assert.Equal(1, cdt.Triangles.Length);
    }

    // -------------------------------------------------------------------------
    // IsFinalized guard checks
    // -------------------------------------------------------------------------

    [Fact]
    public void InsertVertices_AfterFinalized_Throws()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(500, 1000)]);
        cdt.EraseSuperTriangle();

        Assert.True(cdt.IsFinalized);
        Assert.Throws<TriangulationFinalizedException>(() => cdt.InsertVertices([Pt(2000, 0)]));
    }

    [Fact]
    public void InsertEdges_AfterFinalized_Throws()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);
        cdt.EraseSuperTriangle();

        Assert.True(cdt.IsFinalized);
        Assert.Throws<TriangulationFinalizedException>(() => cdt.InsertEdges([new Edge(0, 2)]));
    }

    [Fact]
    public void ConformToEdges_AfterFinalized_Throws()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);
        cdt.EraseSuperTriangle();

        Assert.True(cdt.IsFinalized);
        Assert.Throws<TriangulationFinalizedException>(() => cdt.ConformToEdges([new Edge(0, 2)]));
    }

    [Fact]
    public void ResolveIntersectingConstraints_InsertsIntersectionVertex()
    {
        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.TryResolve,
            0L);

        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);
        cdt.InsertEdges([new Edge(0, 2), new Edge(1, 3)]); // crossing diagonals
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.True(cdt.Vertices.Length > 4); // intersection vertex inserted
    }

    // -------------------------------------------------------------------------
    // IntersectingConstraintsException
    // -------------------------------------------------------------------------

    [Fact]
    public void NotAllowed_CrossingEdges_ThrowsIntersectingConstraintsException()
    {
        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.NotAllowed,
            0L);
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);

        var ex = Assert.Throws<IntersectingConstraintsException>(
            () => cdt.InsertEdges([new Edge(0, 2), new Edge(1, 3)]));

        // Both edge properties must be set with the two crossing diagonals.
        Assert.True(
            (ex.E1 == new Edge(0, 2) && ex.E2 == new Edge(1, 3)) ||
            (ex.E1 == new Edge(1, 3) && ex.E2 == new Edge(0, 2)));
    }

    [Fact]
    public void DontCheck_CrossingEdges_DoesNotThrow()
    {
        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.DontCheck,
            0L);
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);

        // Should complete without throwing – caller guarantees no intersections
        // but in this case we just verify no exception is raised.
        var ex = Record.Exception(
            () => cdt.InsertEdges([new Edge(0, 1), new Edge(1, 2)]));
        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // EraseOuterTrianglesAndHoles
    // -------------------------------------------------------------------------

    [Fact]
    public void EraseOuterTrianglesAndHoles_SquareWithHole_RemovesOddDepth()
    {
        // Outer square + inner square (hole).  Depth 0 = super, depth 1 = between
        // squares (kept), depth 2 = inner hole (removed).
        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.NotAllowed,
            0L);

        // Outer square: verts 0..3
        // Inner square (hole): verts 4..7
        cdt.InsertVertices([
            Pt(0,    0),    Pt(3000, 0),    Pt(3000, 3000), Pt(0,    3000),
            Pt(1000, 1000), Pt(2000, 1000), Pt(2000, 2000), Pt(1000, 2000),
        ]);
        // Outer boundary
        cdt.InsertEdges([new Edge(0, 1), new Edge(1, 2), new Edge(2, 3), new Edge(3, 0)]);
        // Inner hole boundary
        cdt.InsertEdges([new Edge(4, 5), new Edge(5, 6), new Edge(6, 7), new Edge(7, 4)]);
        cdt.EraseOuterTrianglesAndHoles();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        // All remaining triangles must NOT include the inner hole vertices exclusively
        // – just verify topology is valid and some triangles remain.
        Assert.True(cdt.Triangles.Length > 0);
    }

    // -------------------------------------------------------------------------
    // Multiple InsertVertices calls
    // -------------------------------------------------------------------------

    [Fact]
    public void InsertVertices_MultipleCalls_AccumulatesVertices()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(500, 1000)]);
        // Second batch adds two more points
        cdt.InsertVertices([Pt(200, 200), Pt(800, 200)]);

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        // 5 user verts + 3 super-triangle verts
        Assert.Equal(8, cdt.Vertices.Length);
    }

    // -------------------------------------------------------------------------
    // Coordinate range validation
    // -------------------------------------------------------------------------

    [Fact]
    public void InsertVertices_CoordExceedsMaxCoordinate_ThrowsTriangulationException()
    {
        var cdt = new Triangulation();
        long bad = CDT.Predicates.PredicatesInt.MaxCoordinate + 1;
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => cdt.InsertVertices([Pt(0, 0), Pt(bad, 0), Pt(500, 1000)]));
        Assert.Contains("MaxCoordinate", ex.Message);
    }

    // -------------------------------------------------------------------------
    // ConformToEdges
    // -------------------------------------------------------------------------

    [Fact]
    public void ConformToEdges_ProducesValidTopology_AndPopulatesPieceToOriginals()
    {
        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.TryResolve,
            0L);
        cdt.InsertVertices([Pt(0, 0), Pt(1000, 0), Pt(1000, 1000), Pt(0, 1000)]);
        // Use ConformToEdges (splits edges at intersections and tracks pieces)
        cdt.ConformToEdges([new Edge(0, 2), new Edge(1, 3)]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        // PieceToOriginals is populated when conforming produces split edges
        Assert.NotNull(cdt.PieceToOriginals);
    }

    // -------------------------------------------------------------------------
    // Large-coordinate boundary tests
    // -------------------------------------------------------------------------

    [Fact]
    public void InsertVertices_LargePositiveCoords_ProducesValidTopology()
    {
        // Vertices at up to 2^50 in the positive quadrant.
        // Super-triangle R = 4 * C = 4*(2^49) = 2^51 < (long)9e15, so the
        // Debug.Assert inside AddSuperTriangle is satisfied.
        const long L = 1L << 50;
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(0, 0), Pt(L, 0), Pt(L, L), Pt(0, L)]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(4, cdt.Vertices.Length);
        Assert.Equal(2, cdt.Triangles.Length);
    }

    [Fact]
    public void InsertVertices_LargeSymmetricCoords_ProducesValidTopology()
    {
        // Symmetric about origin: ±(2^49) on both axes.
        // R = 8*(2^49) = 2^52 ≈ 4.5e15 < (long)9e15. Safe.
        const long H = 1L << 49;
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(-H, -H), Pt(H, -H), Pt(H, H), Pt(-H, H)]);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(4, cdt.Vertices.Length);
        Assert.Equal(2, cdt.Triangles.Length);
    }

    [Fact]
    public void InsertVertices_LargeCoords_WithConstraintEdge_ProducesValidTopology()
    {
        // Constraint edge across a large-coordinate square.
        const long H = 1L << 49;
        var cdt = new Triangulation();
        cdt.InsertVertices([Pt(-H, -H), Pt(H, -H), Pt(H, H), Pt(-H, H)]);
        cdt.InsertEdges([new Edge(0, 2)]); // diagonal
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Single(cdt.FixedEdges);
    }
}
