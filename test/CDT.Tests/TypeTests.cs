// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;

/// <summary>
/// Tests for primitive types: <see cref="Edge"/>, <see cref="Triangle"/>,
/// <see cref="LayerDepth"/>, <see cref="DuplicateVertexException"/>,
/// and <see cref="CovariantReadOnlyDictionary{TKey,TInner,TOuter}"/>
/// (accessed via <see cref="Triangulation.PieceToOriginals"/>).
/// </summary>
public sealed class TypeTests
{
    // =========================================================================
    // Edge
    // =========================================================================

    [Fact]
    public void Edge_Constructor_AlreadyOrdered_Preserved()
    {
        var e = new Edge(1, 9);
        Assert.Equal(1, e.V1);
        Assert.Equal(9, e.V2);
    }

    [Fact]
    public void Edge_Constructor_OrdersSmallestFirst()
    {
        var e = new Edge(5, 2);
        Assert.Equal(2, e.V1);
        Assert.Equal(5, e.V2);
    }

    [Fact]
    public void Edge_Constructor_EqualVertices()
    {
        var e = new Edge(3, 3);
        Assert.Equal(3, e.V1);
        Assert.Equal(3, e.V2);
    }

    [Fact]
    public void Edge_Equals_Typed_Equal()
    {
        var a = new Edge(1, 4);
        var b = new Edge(4, 1); // normalised to (1,4)
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Edge_Equals_Typed_NotEqual()
    {
        Assert.False(new Edge(1, 2).Equals(new Edge(1, 3)));
    }

    [Fact]
    public void Edge_Equals_BoxedSameValue_True()
    {
        object boxed = new Edge(2, 7);
        Assert.True(new Edge(7, 2).Equals(boxed));
    }

    [Fact]
    public void Edge_Equals_BoxedWrongType_False()
    {
        Assert.False(new Edge(1, 2).Equals("not an edge"));
    }

    [Fact]
    public void Edge_Equals_BoxedNull_False()
    {
        Assert.False(new Edge(1, 2).Equals((object?)null));
    }

    [Fact]
    public void Edge_EqualityOperator_Equal()
    {
        Assert.True(new Edge(1, 3) == new Edge(3, 1));
    }

    [Fact]
    public void Edge_InequalityOperator_NotEqual()
    {
        Assert.True(new Edge(1, 2) != new Edge(1, 3));
    }

    [Fact]
    public void Edge_InequalityOperator_Equal_ReturnsFalse()
    {
        Assert.False(new Edge(2, 5) != new Edge(5, 2));
    }

    [Fact]
    public void Edge_GetHashCode_EqualEdgesMatch()
    {
        Assert.Equal(new Edge(1, 4).GetHashCode(), new Edge(4, 1).GetHashCode());
    }

    [Fact]
    public void Edge_ToString_Format()
    {
        var s = new Edge(3, 7).ToString();
        Assert.Contains("3", s);
        Assert.Contains("7", s);
    }

    // =========================================================================
    // Triangle
    // =========================================================================

    [Fact]
    public void Triangle_DefaultConstructor_UsesNoSentinels()
    {
        var t = new Triangle();
        Assert.Equal(Indices.NoVertex,   t.V0);
        Assert.Equal(Indices.NoVertex,   t.V1);
        Assert.Equal(Indices.NoVertex,   t.V2);
        Assert.Equal(Indices.NoNeighbor, t.N0);
        Assert.Equal(Indices.NoNeighbor, t.N1);
        Assert.Equal(Indices.NoNeighbor, t.N2);
    }

    [Fact]
    public void Triangle_ParameterisedConstructor_StoresAll()
    {
        var t = new Triangle(10, 20, 30, 40, 50, 60);
        Assert.Equal(10, t.V0); Assert.Equal(20, t.V1); Assert.Equal(30, t.V2);
        Assert.Equal(40, t.N0); Assert.Equal(50, t.N1); Assert.Equal(60, t.N2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Triangle_GetVertex_AllIndices(int i)
    {
        var t = new Triangle(10, 20, 30, 0, 0, 0);
        int[] expected = [10, 20, 30];
        Assert.Equal(expected[i], t.GetVertex(i));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Triangle_SetVertex_RoundTrip(int i)
    {
        var t = new Triangle();
        t.SetVertex(i, 99);
        Assert.Equal(99, t.GetVertex(i));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Triangle_GetNeighbor_AllIndices(int i)
    {
        var t = new Triangle(0, 0, 0, 40, 50, 60);
        int[] expected = [40, 50, 60];
        Assert.Equal(expected[i], t.GetNeighbor(i));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Triangle_SetNeighbor_RoundTrip(int i)
    {
        var t = new Triangle();
        t.SetNeighbor(i, 77);
        Assert.Equal(77, t.GetNeighbor(i));
    }

    [Fact]
    public void Triangle_ContainsVertex_True()
    {
        var t = new Triangle(3, 5, 7, 0, 0, 0);
        Assert.True(t.ContainsVertex(3));
        Assert.True(t.ContainsVertex(5));
        Assert.True(t.ContainsVertex(7));
    }

    [Fact]
    public void Triangle_ContainsVertex_False()
    {
        var t = new Triangle(3, 5, 7, 0, 0, 0);
        Assert.False(t.ContainsVertex(99));
    }

    [Fact]
    public void Triangle_Next_V0()
    {
        var t = new Triangle(10, 11, 12, 40, 50, 60);
        var (tri, other) = t.Next(10);
        Assert.Equal(40, tri);
        Assert.Equal(11, other);
    }

    [Fact]
    public void Triangle_Next_V1()
    {
        var t = new Triangle(10, 11, 12, 40, 50, 60);
        var (tri, other) = t.Next(11);
        Assert.Equal(50, tri);
        Assert.Equal(12, other);
    }

    [Fact]
    public void Triangle_Next_V2()
    {
        var t = new Triangle(10, 11, 12, 40, 50, 60);
        var (tri, other) = t.Next(12);
        Assert.Equal(60, tri);
        Assert.Equal(10, other);
    }

    [Fact]
    public void Triangle_Prev_V0()
    {
        var t = new Triangle(10, 11, 12, 40, 50, 60);
        var (tri, other) = t.Prev(10);
        Assert.Equal(60, tri);
        Assert.Equal(12, other);
    }

    [Fact]
    public void Triangle_Prev_V1()
    {
        var t = new Triangle(10, 11, 12, 40, 50, 60);
        var (tri, other) = t.Prev(11);
        Assert.Equal(40, tri);
        Assert.Equal(10, other);
    }

    [Fact]
    public void Triangle_Prev_V2()
    {
        var t = new Triangle(10, 11, 12, 40, 50, 60);
        var (tri, other) = t.Prev(12);
        Assert.Equal(50, tri);
        Assert.Equal(11, other);
    }

    [Fact]
    public void Triangle_ToString_ContainsAllValues()
    {
        var s = new Triangle(1, 2, 3, 4, 5, 6).ToString();
        foreach (var n in new[] { "1", "2", "3", "4", "5", "6" })
            Assert.Contains(n, s);
    }

    // =========================================================================
    // LayerDepth
    // =========================================================================

    [Fact]
    public void LayerDepth_ImplicitFromUshort()
    {
        LayerDepth d = (ushort)42;
        Assert.Equal((ushort)42, d.Value);
    }

    [Fact]
    public void LayerDepth_ImplicitToUshort()
    {
        LayerDepth d = new LayerDepth(7);
        ushort v = d;
        Assert.Equal((ushort)7, v);
    }

    [Fact]
    public void LayerDepth_ToString_ReturnsNumericString()
    {
        Assert.Equal("5", new LayerDepth(5).ToString());
    }

    // =========================================================================
    // DuplicateVertexException
    // =========================================================================

    [Fact]
    public void DuplicateVertexException_Properties_V1V2()
    {
        var ex = new DuplicateVertexException(3, 7);
        Assert.Equal(3, ex.V1);
        Assert.Equal(7, ex.V2);
        Assert.Contains("3", ex.Message);
        Assert.Contains("7", ex.Message);
    }

    [Fact]
    public void DuplicateVertexException_ThrownByTriangulation_HasCorrectIndices()
    {
        var cdt = new Triangulation();
        var pts = new[] { new V2i(0, 0), new V2i(1000, 0), new V2i(0, 0) };
        var ex = Assert.Throws<DuplicateVertexException>(() => cdt.InsertVertices(pts));
        // V1 = newly inserted dup (index 2), V2 = existing vertex (index 0)
        Assert.Equal(2, ex.V1);
        Assert.Equal(0, ex.V2);
    }

    // =========================================================================
    // CovariantReadOnlyDictionary (via Triangulation.PieceToOriginals)
    // =========================================================================

    /// <summary>
    /// Builds a conforming triangulation that produces non-empty PieceToOriginals
    /// (conforming splits an edge into pieces, recording which original edge each piece belongs to).
    /// </summary>
    private static IReadOnlyDictionary<Edge, IReadOnlyList<Edge>> BuildPieceToOriginals()
    {
        // Two triangles sharing an edge; conforming to an edge that crosses the shared edge
        // forces a midpoint split, which populates PieceToOriginals.
        var cdt = new Triangulation(
            VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.TryResolve,
            0L);

        cdt.InsertVertices([
            new V2i(0, 0),
            new V2i(1000000, 0),
            new V2i(1000000, 1000000),
            new V2i(0, 1000000),
        ]);

        // Insert diagonal; then conform to crossing diagonal
        cdt.InsertEdges([new Edge(0, 2)]);
        cdt.ConformToEdges([new Edge(1, 3)]);

        return cdt.PieceToOriginals;
    }

    [Fact]
    public void CovariantReadOnlyDictionary_Count_Positive()
    {
        var d = BuildPieceToOriginals();
        Assert.True(d.Count > 0);
    }

    [Fact]
    public void CovariantReadOnlyDictionary_ContainsKey_True()
    {
        var d = BuildPieceToOriginals();
        var key = d.Keys.First();
        Assert.True(d.ContainsKey(key));
    }

    [Fact]
    public void CovariantReadOnlyDictionary_ContainsKey_False()
    {
        var d = BuildPieceToOriginals();
        Assert.False(d.ContainsKey(new Edge(99999, 100000)));
    }

    [Fact]
    public void CovariantReadOnlyDictionary_Indexer_ReturnsValue()
    {
        var d = BuildPieceToOriginals();
        var key = d.Keys.First();
        var val = d[key];
        Assert.NotNull(val);
    }

    [Fact]
    public void CovariantReadOnlyDictionary_TryGetValue_Found()
    {
        var d = BuildPieceToOriginals();
        var key = d.Keys.First();
        Assert.True(d.TryGetValue(key, out var val));
        Assert.NotNull(val);
    }

    [Fact]
    public void CovariantReadOnlyDictionary_TryGetValue_NotFound()
    {
        var d = BuildPieceToOriginals();
        Assert.False(d.TryGetValue(new Edge(88888, 99999), out _));
    }

    [Fact]
    public void CovariantReadOnlyDictionary_Keys_Enumerable()
    {
        var d = BuildPieceToOriginals();
        Assert.NotEmpty(d.Keys);
    }

    [Fact]
    public void CovariantReadOnlyDictionary_Values_Enumerable()
    {
        var d = BuildPieceToOriginals();
        Assert.NotEmpty(d.Values);
    }

    [Fact]
    public void CovariantReadOnlyDictionary_GetEnumerator_Generic()
    {
        var d = BuildPieceToOriginals();
        int count = 0;
        foreach (var kv in d) { _ = kv.Key; _ = kv.Value; count++; }
        Assert.Equal(d.Count, count);
    }

    [Fact]
    public void CovariantReadOnlyDictionary_GetEnumerator_NonGeneric()
    {
        var d = BuildPieceToOriginals();
        var en = ((System.Collections.IEnumerable)d).GetEnumerator();
        int count = 0;
        while (en.MoveNext()) count++;
        Assert.Equal(d.Count, count);
    }
}
