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
}
