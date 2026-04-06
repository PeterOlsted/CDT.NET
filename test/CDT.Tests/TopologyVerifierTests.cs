// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;

/// <summary>
/// Tests for <see cref="TopologyVerifier.VerifyTopology"/> including all
/// error-return branches, exercised via the internal raw-span overload.
/// </summary>
public sealed class TopologyVerifierTests
{
    // Minimal valid vertices used across several tests.
    private static readonly V2i[] ThreeVerts =
    [
        new V2i(0,    0),
        new V2i(1000, 0),
        new V2i(500,  1000),
    ];

    // A valid single triangle with no neighbors.
    private static readonly Triangle ValidTri =
        new Triangle(0, 1, 2, Indices.NoNeighbor, Indices.NoNeighbor, Indices.NoNeighbor);

    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    public void VerifyTopology_EmptyTriangulation_ReturnsTrue()
    {
        Assert.True(TopologyVerifier.VerifyTopology(
            ReadOnlySpan<Triangle>.Empty, ReadOnlySpan<V2i>.Empty));
    }

    [Fact]
    public void VerifyTopology_ValidSingleTriangle_ReturnsTrue()
    {
        Assert.True(TopologyVerifier.VerifyTopology(
            new Triangle[] { ValidTri }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_ValidPairMutualNeighbors_ReturnsTrue()
    {
        // Two triangles sharing edge (0,1): tri0 N0→1, tri1 N0→0
        var verts = new V2i[]
        {
            new V2i(0,    0),
            new V2i(1000, 0),
            new V2i(500,  1000),
            new V2i(500, -500),
        };
        var t0 = new Triangle(0, 1, 2, 1, Indices.NoNeighbor, Indices.NoNeighbor);
        var t1 = new Triangle(1, 0, 3, 0, Indices.NoNeighbor, Indices.NoNeighbor);
        Assert.True(TopologyVerifier.VerifyTopology(new[] { t0, t1 }, verts));
    }

    // =========================================================================
    // Error branches
    // =========================================================================

    [Fact]
    public void VerifyTopology_NoVertex_V0_ReturnsFalse()
    {
        var bad = new Triangle(Indices.NoVertex, 1, 2, -1, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_NoVertex_V1_ReturnsFalse()
    {
        var bad = new Triangle(0, Indices.NoVertex, 2, -1, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_NoVertex_V2_ReturnsFalse()
    {
        var bad = new Triangle(0, 1, Indices.NoVertex, -1, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_VertexIndexTooLarge_ReturnsFalse()
    {
        var bad = new Triangle(0, 1, 99, -1, -1, -1); // 99 >= 3 vertices
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_DegenerateTriangle_V0EqualsV1_ReturnsFalse()
    {
        var bad = new Triangle(0, 0, 2, -1, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_DegenerateTriangle_V1EqualsV2_ReturnsFalse()
    {
        var bad = new Triangle(0, 1, 1, -1, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_DegenerateTriangle_V0EqualsV2_ReturnsFalse()
    {
        var bad = new Triangle(0, 1, 0, -1, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_NeighborIndexOutOfRange_ReturnsFalse()
    {
        // T0 references neighbor index 5, but there is only 1 triangle
        var bad = new Triangle(0, 1, 2, 5, -1, -1);
        Assert.False(TopologyVerifier.VerifyTopology(new[] { bad }, ThreeVerts));
    }

    [Fact]
    public void VerifyTopology_NeighborNotBackReferencing_ReturnsFalse()
    {
        // T0 says neighbor 0 is T1, but T1 has no back-reference to T0
        var verts = new V2i[]
        {
            new V2i(0,    0),
            new V2i(1000, 0),
            new V2i(500,  1000),
            new V2i(500, -500),
        };
        var t0 = new Triangle(0, 1, 2, 1, -1, -1);                    // claims T1 as neighbor
        var t1 = new Triangle(1, 0, 3, -1, -1, -1);                   // but T1 has no neighbor 0
        Assert.False(TopologyVerifier.VerifyTopology(new[] { t0, t1 }, verts));
    }

    [Fact]
    public void VerifyTopology_SharedEdge_WrongVertices_ReturnsFalse()
    {
        // T0 neighbor-slot 0 → T1, T1 has back-ref to T0, but shared vertices don't match
        var verts = new V2i[]
        {
            new V2i(0,    0),
            new V2i(1000, 0),
            new V2i(500,  1000),
            new V2i(500, -500),
        };
        // T0 slot 0 uses vertices (GetVertex(0)=0, GetVertex(Ccw(0)=1)=1) → shared edge is (0,1)
        // T1 back-references T0 at slot 0 but its own vertex set is (1,0,3) — doesn't contain vertex 2
        // We break it by making T1 use completely different vertices
        var t0 = new Triangle(0, 1, 2,   1, -1, -1);
        var t1 = new Triangle(1, 3, 0,   0, -1, -1);  // back-ref ok, but edge slot 0 vertex != triangle 0 shared
        // T0 slot 0: va=GetVertex(0)=0, vb=GetVertex(1)=1.  T1 must ContainsVertex(0) && ContainsVertex(1).
        // T1 has 1,3,0 → contains 0 and 1 → this would actually pass. Need to craft a real failure.
        // Use: T1 does NOT contain vertex 2, but the expected shared edge from T0 includes vertex 2
        // T0 slot 2: va=GetVertex(2)=2, vb=GetVertex(Ccw(2)=0)=0 → edge vertices (2,0). T1 must have 2 and 0.
        // If T1 = (1, 3, 0): doesn't have vertex 2 → returns false.
        var t0b = new Triangle(0, 1, 2, -1, -1, 1);   // slot 2 → T1
        var t1b = new Triangle(1, 3, 0,  -1, -1, 0);   // back-ref at slot 2 → T0, but T1 lacks vertex 2
        Assert.False(TopologyVerifier.VerifyTopology(new[] { t0b, t1b }, verts));
    }
}
