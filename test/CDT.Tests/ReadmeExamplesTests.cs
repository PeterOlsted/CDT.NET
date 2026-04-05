// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;


/// <summary>
/// Validates the README code examples with the integer <see cref="Triangulation"/>
/// (same scenarios, integer-scaled coordinates).
/// </summary>
public sealed class ReadmeExamplesTests_Int
{
    // -------------------------------------------------------------------------
    // Example 1 – Delaunay triangulation without constraints (convex hull)
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_DelaunayConvexHull()
    {
        var vertices = new List<V2i>
        {
            new(0, 0), new(4_000_000, 0), new(4_000_000, 4_000_000),
            new(0, 4_000_000), new(2_000_000, 2_000_000),
        };

        var cdt = new Triangulation();
        cdt.InsertVertices(vertices);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(5, cdt.Vertices.Length);
        Assert.True(cdt.Triangles.Length > 0);
        Assert.Empty(cdt.FixedEdges);
    }

    // -------------------------------------------------------------------------
    // Example 2 – Constrained Delaunay triangulation (bounded domain)
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_ConstrainedDelaunay_BoundedDomain()
    {
        var vertices = new List<V2i>
        {
            new(0, 0), new(4_000_000, 0), new(4_000_000, 4_000_000), new(0, 4_000_000),
        };
        var edges = new List<Edge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
        };

        var cdt = new Triangulation();
        cdt.InsertVertices(vertices);
        cdt.InsertEdges(edges);
        cdt.EraseOuterTriangles();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(4, cdt.Vertices.Length);
        Assert.Equal(2, cdt.Triangles.Length);
        Assert.Equal(4, cdt.FixedEdges.Count);
    }

    // -------------------------------------------------------------------------
    // Example 3 – Auto-detect outer triangles and holes
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_AutoDetectBoundariesAndHoles()
    {
        var vertices = new List<V2i>
        {
            new(0, 0), new(6_000_000, 0), new(6_000_000, 6_000_000), new(0, 6_000_000),
            new(2_000_000, 2_000_000), new(4_000_000, 2_000_000),
            new(4_000_000, 4_000_000), new(2_000_000, 4_000_000),
        };
        var edges = new List<Edge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
            new(4, 7), new(7, 6), new(6, 5), new(5, 4),
        };

        var cdt = new Triangulation();
        cdt.InsertVertices(vertices);
        cdt.InsertEdges(edges);
        cdt.EraseOuterTrianglesAndHoles();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.Equal(8, cdt.Vertices.Length);
        Assert.True(cdt.Triangles.Length > 0);
    }

    // -------------------------------------------------------------------------
    // Example 4 – Conforming Delaunay triangulation
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_ConformingDelaunay()
    {
        var vertices = new List<V2i>
        {
            new(0, 0), new(4_000_000, 0), new(4_000_000, 4_000_000), new(0, 4_000_000),
        };
        var edges = new List<Edge>
        {
            new(0, 1), new(1, 2), new(2, 3), new(3, 0),
        };

        var cdt = new Triangulation();
        cdt.InsertVertices(vertices);
        cdt.ConformToEdges(edges);
        cdt.EraseOuterTriangles();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.True(cdt.Triangles.Length > 0);
        Assert.True(cdt.Vertices.Length >= 4);
    }

    // -------------------------------------------------------------------------
    // Example 5 – Removing duplicates and remapping edges
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_RemoveDuplicatesAndRemapEdges()
    {
        var vertices = new List<V2i>
        {
            new(0, 0), new(4_000_000, 0), new(4_000_000, 4_000_000), new(0, 4_000_000),
            new(0, 0), // duplicate of vertex 0
        };
        var edges = new List<Edge>
        {
            new(0, 4), new(1, 2),
        };

        CdtUtils.RemoveDuplicatesAndRemapEdges(vertices, edges);

        Assert.Equal(4, vertices.Count);
        Assert.Equal(new Edge(0, 0), edges[0]);
        Assert.Equal(new Edge(1, 2), edges[1]);

        var cdt = new Triangulation();
        cdt.InsertVertices(vertices);
        cdt.InsertEdges(edges.Where(e => e.V1 != e.V2).ToList());
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
    }

    // -------------------------------------------------------------------------
    // Example 6 – Extract all edges from a triangulation
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_ExtractEdgesFromTriangles()
    {
        var cdt = new Triangulation();
        cdt.InsertVertices([new(0, 0), new(2_000_000, 0), new(1_000_000, 2_000_000)]);
        cdt.EraseSuperTriangle();

        var allEdges = CdtUtils.ExtractEdgesFromTriangles(cdt.Triangles.Span);
        Assert.Equal(3, allEdges.Count);
    }

    // -------------------------------------------------------------------------
    // Example 7 – Resolve intersecting constraint edges
    // -------------------------------------------------------------------------

    [Fact]
    public void Example_ResolveIntersectingConstraints()
    {
        var vertices = new List<V2i>
        {
            new(0, 0), new(1_000_000, 0), new(1_000_000, 1_000_000), new(0, 1_000_000),
        };
        var edges = new List<Edge>
        {
            new(0, 2), new(1, 3),
        };

        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.TryResolve,
            0L);

        cdt.InsertVertices(vertices);
        cdt.InsertEdges(edges);
        cdt.EraseSuperTriangle();

        Assert.True(TopologyVerifier.VerifyTopology(cdt));
        Assert.True(cdt.Vertices.Length > 4);
    }
}
