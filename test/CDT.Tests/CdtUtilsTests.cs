// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;

/// <summary>Tests for the static helpers in <see cref="CdtUtils"/>.</summary>
public sealed class CdtUtilsTests
{
    // =========================================================================
    // CalculateTrianglesByVertex
    // =========================================================================

    [Fact]
    public void CalculateTrianglesByVertex_EmptyTriangles_ReturnsEmptyLists()
    {
        var result = CdtUtils.CalculateTrianglesByVertex(ReadOnlySpan<Triangle>.Empty, 3);
        Assert.Equal(3, result.Count);
        Assert.Empty(result[0]);
        Assert.Empty(result[1]);
        Assert.Empty(result[2]);
    }

    [Fact]
    public void CalculateTrianglesByVertex_SingleTriangle_EachVertexHasOne()
    {
        var tris = new Triangle[] { new(0, 1, 2, -1, -1, -1) };
        var result = CdtUtils.CalculateTrianglesByVertex(tris, 3);
        Assert.Equal(3, result.Count);
        Assert.Single(result[0]); Assert.Equal(0, result[0][0]);
        Assert.Single(result[1]); Assert.Equal(0, result[1][0]);
        Assert.Single(result[2]); Assert.Equal(0, result[2][0]);
    }

    [Fact]
    public void CalculateTrianglesByVertex_TwoTrianglesShareVertex_HasTwo()
    {
        // tri0: (0,1,2), tri1: (0,2,3)
        var tris = new Triangle[]
        {
            new(0, 1, 2, -1, -1, -1),
            new(0, 2, 3, -1, -1, -1),
        };
        var result = CdtUtils.CalculateTrianglesByVertex(tris, 4);
        Assert.Equal(2, result[0].Count); // vertex 0 in both triangles
        Assert.Equal(2, result[2].Count); // vertex 2 in both triangles
        Assert.Single(result[1]);         // vertex 1 only in tri0
        Assert.Single(result[3]);         // vertex 3 only in tri1
    }

    // =========================================================================
    // EdgeToPiecesMapping
    // =========================================================================

    [Fact]
    public void EdgeToPiecesMapping_SingleEntry_Inverts()
    {
        var orig = new Edge(0, 10);
        var piece1 = new Edge(0, 5);
        var piece2 = new Edge(5, 10);
        var pieceToOriginals = new Dictionary<Edge, IReadOnlyList<Edge>>
        {
            [piece1] = new List<Edge> { orig },
            [piece2] = new List<Edge> { orig },
        };

        var result = CdtUtils.EdgeToPiecesMapping(pieceToOriginals);

        Assert.True(result.ContainsKey(orig));
        Assert.Equal(2, result[orig].Count);
        Assert.Contains(piece1, result[orig]);
        Assert.Contains(piece2, result[orig]);
    }

    [Fact]
    public void EdgeToPiecesMapping_Empty_ReturnsEmpty()
    {
        var result = CdtUtils.EdgeToPiecesMapping(new Dictionary<Edge, IReadOnlyList<Edge>>());
        Assert.Empty(result);
    }

    // =========================================================================
    // RemapEdges
    // =========================================================================

    [Fact]
    public void RemapEdges_UpdatesEdgeIndices()
    {
        var edges = new List<Edge> { new(0, 2), new(1, 3) };
        // mapping: 0→0, 1→1, 2→0 (dup removed), 3→2
        var mapping = new List<int> { 0, 1, 0, 2 };
        CdtUtils.RemapEdges(edges, mapping);
        Assert.Equal(new Edge(0, 0), edges[0]);
        Assert.Equal(new Edge(1, 2), edges[1]);
    }

    // =========================================================================
    // LocatePointLine with snap tolerance
    // =========================================================================

    [Fact]
    public void LocatePointLine_ZeroTolerance_CollinearIsOnLine()
    {
        // p=(500,0) on the line (0,0)→(1000,0)
        var result = CdtUtils.LocatePointLine(
            new V2i(500, 0), new V2i(0, 0), new V2i(1000, 0));
        Assert.Equal(PtLineLocation.OnLine, result);
    }

    [Fact]
    public void LocatePointLine_WithTolerance_NearlyCollinearSnapsToOnLine()
    {
        // p=(500,1): Orient2dRaw = 1000*1 = 1000.  With tolerance 1000 → OnLine.
        var result = CdtUtils.LocatePointLine(
            new V2i(500, 1), new V2i(0, 0), new V2i(1000, 0), 1000L);
        Assert.Equal(PtLineLocation.OnLine, result);
    }

    [Fact]
    public void LocatePointLine_WithTolerance_JustAbove_IsLeft()
    {
        // area = 1001. tolerance = 1000 → Left.
        var result = CdtUtils.LocatePointLine(
            new V2i(501, 2), new V2i(0, 0), new V2i(1000, 0), 1000L);
        Assert.Equal(PtLineLocation.Left, result);
    }

    [Fact]
    public void LocatePointLine_WithTolerance_BelowLine_IsRight()
    {
        // p below line, area negative
        var result = CdtUtils.LocatePointLine(
            new V2i(500, -2), new V2i(0, 0), new V2i(1000, 0), 1000L);
        Assert.Equal(PtLineLocation.Right, result);
    }

    // =========================================================================
    // ClassifyOrientation
    // =========================================================================

    [Theory]
    [InlineData(-1, PtLineLocation.Right)]
    [InlineData( 0, PtLineLocation.OnLine)]
    [InlineData( 1, PtLineLocation.Left)]
    public void ClassifyOrientation_AllThreeSigns(int sign, PtLineLocation expected)
    {
        Assert.Equal(expected, CdtUtils.ClassifyOrientation(sign));
    }

    // =========================================================================
    // IsOnEdge / EdgeNeighborFromLocation
    // =========================================================================

    [Theory]
    [InlineData(PtTriLocation.OnEdge0, true)]
    [InlineData(PtTriLocation.OnEdge1, true)]
    [InlineData(PtTriLocation.OnEdge2, true)]
    [InlineData(PtTriLocation.Inside,  false)]
    [InlineData(PtTriLocation.Outside, false)]
    [InlineData(PtTriLocation.OnVertex, false)]
    public void IsOnEdge_AllCases(PtTriLocation loc, bool expected)
    {
        Assert.Equal(expected, CdtUtils.IsOnEdge(loc));
    }

    [Theory]
    [InlineData(PtTriLocation.OnEdge0, 0)]
    [InlineData(PtTriLocation.OnEdge1, 1)]
    [InlineData(PtTriLocation.OnEdge2, 2)]
    public void EdgeNeighborFromLocation_OnEdgeLocations(PtTriLocation loc, int expected)
    {
        Assert.Equal(expected, CdtUtils.EdgeNeighborFromLocation(loc));
    }

    // =========================================================================
    // Triangle index helpers
    // =========================================================================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    public void VertexIndex_AllThreePositions(int vField, int expectedIndex)
    {
        // Triangle (10,11,12) — VertexIndex finds where each vertex sits
        var t = new Triangle(10, 11, 12, -1, -1, -1);
        Assert.Equal(expectedIndex, CdtUtils.VertexIndex(t, 10 + vField));
    }

    [Fact]
    public void EdgeNeighborIndex_V0V1()
    {
        var t = new Triangle(0, 1, 2, -1, -1, -1);
        Assert.Equal(0, CdtUtils.EdgeNeighborIndex(t, 0, 1));
        Assert.Equal(0, CdtUtils.EdgeNeighborIndex(t, 1, 0));
    }

    [Fact]
    public void EdgeNeighborIndex_V0V2()
    {
        var t = new Triangle(0, 1, 2, -1, -1, -1);
        Assert.Equal(2, CdtUtils.EdgeNeighborIndex(t, 0, 2));
        Assert.Equal(2, CdtUtils.EdgeNeighborIndex(t, 2, 0));
    }

    [Fact]
    public void EdgeNeighborIndex_V1V2()
    {
        var t = new Triangle(0, 1, 2, -1, -1, -1);
        Assert.Equal(1, CdtUtils.EdgeNeighborIndex(t, 1, 2));
        Assert.Equal(1, CdtUtils.EdgeNeighborIndex(t, 2, 1));
    }

    [Fact]
    public void NeighborIndex_AllThreeNeighbors()
    {
        var t = new Triangle(0, 0, 0, 10, 20, 30);
        Assert.Equal(0, CdtUtils.NeighborIndex(t, 10));
        Assert.Equal(1, CdtUtils.NeighborIndex(t, 20));
        Assert.Equal(2, CdtUtils.NeighborIndex(t, 30));
    }

    [Fact]
    public void OpposedNeighborIndex_AllCases()
    {
        Assert.Equal(1, CdtUtils.OpposedNeighborIndex(0));
        Assert.Equal(2, CdtUtils.OpposedNeighborIndex(1));
        Assert.Equal(0, CdtUtils.OpposedNeighborIndex(2));
    }

    [Fact]
    public void OpposedVertexIndex_AllCases()
    {
        Assert.Equal(2, CdtUtils.OpposedVertexIndex(0));
        Assert.Equal(0, CdtUtils.OpposedVertexIndex(1));
        Assert.Equal(1, CdtUtils.OpposedVertexIndex(2));
    }

    [Fact]
    public void OpposedVertex_ReturnsCorrectVertex()
    {
        // t: V(0,1,2), N(topo,-1,-1)
        // The vertex opposed to neighbor at slot 0 (topo) is the vertex at slot OpposedVertexIndex(NeighborIndex(t,topo))
        int topo = 5;
        var t = new Triangle(10, 20, 30, topo, -1, -1);
        // NeighborIndex(t, topo)=0 → OpposedVertexIndex(0)=2 → vertex at 2 = 30
        Assert.Equal(30, CdtUtils.OpposedVertex(t, topo));
    }

    [Fact]
    public void OpposedTriangle_ReturnsCorrectNeighbor()
    {
        int neighborIdx = 99;
        // VertexIndex finds iV=1 (V1=20), OpposedNeighborIndex(1)=2, GetNeighbor(2)=neighborIdx
        var t = new Triangle(10, 20, 30, -1, -1, neighborIdx);
        Assert.Equal(neighborIdx, CdtUtils.OpposedTriangle(t, 20));
    }

    [Fact]
    public void EdgeNeighbor_ReturnsCorrectNeighbor()
    {
        var t = new Triangle(0, 1, 2, 40, 50, 60);
        Assert.Equal(40, CdtUtils.EdgeNeighbor(t, 0, 1));
        Assert.Equal(50, CdtUtils.EdgeNeighbor(t, 1, 2));
        Assert.Equal(60, CdtUtils.EdgeNeighbor(t, 0, 2));
    }

    // =========================================================================
    // TouchesSuperTriangle
    // =========================================================================

    [Fact]
    public void TouchesSuperTriangle_WithSuperVert_True()
    {
        // Super-triangle vertices are indices 0,1,2
        Assert.True(CdtUtils.TouchesSuperTriangle(new Triangle(0, 5, 6, -1, -1, -1)));
        Assert.True(CdtUtils.TouchesSuperTriangle(new Triangle(5, 1, 6, -1, -1, -1)));
        Assert.True(CdtUtils.TouchesSuperTriangle(new Triangle(5, 6, 2, -1, -1, -1)));
    }

    [Fact]
    public void TouchesSuperTriangle_NoSuperVert_False()
    {
        Assert.False(CdtUtils.TouchesSuperTriangle(new Triangle(3, 4, 5, -1, -1, -1)));
    }

    // =========================================================================
    // VerticesShareEdge (internal)
    // =========================================================================

    [Fact]
    public void VerticesShareEdge_Shared_ReturnsTrue()
    {
        var a = new List<int> { 0, 1, 2 };
        var b = new List<int> { 3, 2, 4 };
        Assert.True(CdtUtils.VerticesShareEdge(a, b));
    }

    [Fact]
    public void VerticesShareEdge_NotShared_ReturnsFalse()
    {
        var a = new List<int> { 0, 1 };
        var b = new List<int> { 2, 3 };
        Assert.False(CdtUtils.VerticesShareEdge(a, b));
    }
}
