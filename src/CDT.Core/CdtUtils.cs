// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CDT.Predicates;

namespace CDT;

// =========================================================================
// DuplicatesInfo — no generic dependency, available on all target frameworks
// =========================================================================

/// <summary>
/// Duplicates information for vertex deduplication.
/// </summary>
public sealed class DuplicatesInfo
{
    /// <summary>
    /// Maps each original vertex index to its canonical (deduplicated) index.
    /// </summary>
    public IReadOnlyList<int> Mapping { get; }

    /// <summary>Indices of duplicate vertices in the original list.</summary>
    public IReadOnlyList<int> Duplicates { get; }

    internal DuplicatesInfo(int[] mapping, List<int> duplicates)
    {
        Mapping = Array.AsReadOnly(mapping);
        Duplicates = duplicates.AsReadOnly();
    }
}

// =========================================================================
// CdtUtils (partial) — non-generic helpers + integer geometry overloads.
// Available on all target frameworks (net5.0+).
// =========================================================================

/// <summary>
/// Utility methods for duplicate removal, edge remapping, edge extraction,
/// and geometry helpers. Corresponds to C++ <c>CDTUtils.h</c> free functions
/// in the flat <c>CDT::</c> namespace.
/// </summary>
public static partial class CdtUtils
{
    // -------------------------------------------------------------------------
    // Duplicate removal (integer / non-generic)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds duplicate vertices (same X and Y) in a list of integer-coordinate
    /// vertices and returns mapping and duplicate index information.
    /// </summary>
    public static DuplicatesInfo FindDuplicates(IReadOnlyList<V2i> vertices)
    {
        int n = vertices.Count;
        var mapping = new int[n];
        var duplicates = new List<int>();
        var posToIndex = new Dictionary<(long, long), int>(n);

        int iOut = 0;
        for (int iIn = 0; iIn < n; iIn++)
        {
            var key = (vertices[iIn].X, vertices[iIn].Y);
            if (posToIndex.TryGetValue(key, out int existing))
            {
                mapping[iIn] = existing;
                duplicates.Add(iIn);
            }
            else
            {
                posToIndex[key] = iOut;
                mapping[iIn] = iOut++;
            }
        }
        return new DuplicatesInfo(mapping, duplicates);
    }

    /// <summary>
    /// Removes duplicate vertices from the list in-place using the result from
    /// <see cref="FindDuplicates(IReadOnlyList{V2i})"/>.
    /// </summary>
    public static void RemoveDuplicates(List<V2i> vertices, IReadOnlyList<int> duplicates)
    {
        if (duplicates.Count == 0) return;
        var toRemove = new HashSet<int>(duplicates);
        int write = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            if (!toRemove.Contains(i))
                vertices[write++] = vertices[i];
        }
        vertices.RemoveRange(write, vertices.Count - write);
    }

    /// <summary>
    /// Finds and removes duplicate integer-coordinate vertices, and remaps all
    /// edges accordingly.
    /// </summary>
    public static DuplicatesInfo RemoveDuplicatesAndRemapEdges(List<V2i> vertices, List<Edge> edges)
    {
        var info = FindDuplicates(vertices);
        RemoveDuplicates(vertices, info.Duplicates);
        RemapEdges(edges, info.Mapping);
        return info;
    }

    // -------------------------------------------------------------------------
    // Edge remapping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Remaps all edges using the given vertex index mapping (in-place).
    /// </summary>
    public static void RemapEdges(List<Edge> edges, IReadOnlyList<int> mapping)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            edges[i] = new Edge(mapping[edges[i].V1], mapping[edges[i].V2]);
        }
    }

    // -------------------------------------------------------------------------
    // Edge extraction
    // -------------------------------------------------------------------------

    /// <summary>Extracts all unique edges from a triangle list.</summary>
    public static HashSet<Edge> ExtractEdgesFromTriangles(ReadOnlySpan<Triangle> triangles)
    {
        var edges = new HashSet<Edge>(triangles.Length * 3);
        foreach (ref readonly var t in triangles)
        {
            edges.Add(new Edge(t.V0, t.V1));
            edges.Add(new Edge(t.V1, t.V2));
            edges.Add(new Edge(t.V2, t.V0));
        }
        return edges;
    }

    /// <summary>
    /// Calculates per-vertex triangle adjacency lists.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> CalculateTrianglesByVertex(
        ReadOnlySpan<Triangle> triangles,
        int verticesCount)
    {
        var result = new List<int>[verticesCount];
        for (int i = 0; i < verticesCount; i++) result[i] = new List<int>();
        for (int i = 0; i < triangles.Length; i++)
        {
            var t = triangles[i];
            result[t.V0].Add(i);
            result[t.V1].Add(i);
            result[t.V2].Add(i);
        }
        var readOnly = new IReadOnlyList<int>[verticesCount];
        for (int i = 0; i < verticesCount; i++) readOnly[i] = result[i].AsReadOnly();
        return new ReadOnlyCollection<IReadOnlyList<int>>(readOnly);
    }

    /// <summary>
    /// Converts a piece→originals edge mapping to the inverse: original→pieces.
    /// </summary>
    public static IReadOnlyDictionary<Edge, IReadOnlyList<Edge>> EdgeToPiecesMapping(
        IReadOnlyDictionary<Edge, IReadOnlyList<Edge>> pieceToOriginals)
    {
        var edgeToPieces = new Dictionary<Edge, List<Edge>>();
        foreach (var kv in pieceToOriginals)
        {
            foreach (var orig in kv.Value)
            {
                if (!edgeToPieces.TryGetValue(orig, out var pieces))
                {
                    pieces = new List<Edge>();
                    edgeToPieces[orig] = pieces;
                }
                if (!pieces.Contains(kv.Key)) pieces.Add(kv.Key);
            }
        }
        var result = new Dictionary<Edge, IReadOnlyList<Edge>>(edgeToPieces.Count);
        foreach (var kv in edgeToPieces) result[kv.Key] = kv.Value.AsReadOnly();
        return result;
    }

    // -------------------------------------------------------------------------
    // Index cycling (C++: ccw / cw)
    // -------------------------------------------------------------------------

    /// <summary>Advances index CCW within a triangle: 0→1→2→0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Ccw(int i) => (i + 1) % 3;

    /// <summary>Advances index CW within a triangle: 0→2→1→0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Cw(int i) => (i + 2) % 3;

    // -------------------------------------------------------------------------
    // Triangle index queries
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OpposedNeighborIndex(int vertexIndex) => vertexIndex switch
    {
        0 => 1,
        1 => 2,
        _ => 0,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OpposedVertexIndex(int neighborIndex) => neighborIndex switch
    {
        0 => 2,
        1 => 0,
        _ => 1,
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VertexIndex(in Triangle t, int v)
    {
        if (t.V0 == v) return 0;
        if (t.V1 == v) return 1;
        return 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EdgeNeighborIndex(in Triangle t, int va, int vb)
    {
        if (t.V0 == va)
        {
            if (t.V1 == vb) return 0;
            return 2;
        }
        if (t.V0 == vb)
        {
            if (t.V1 == va) return 0;
            return 2;
        }
        return 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NeighborIndex(in Triangle t, int iTopo)
    {
        if (t.N0 == iTopo) return 0;
        if (t.N1 == iTopo) return 1;
        return 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OpposedVertex(in Triangle topo, int iT)
        => topo.GetVertex(OpposedVertexIndex(NeighborIndex(topo, iT)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int OpposedTriangle(in Triangle t, int iV)
        => t.GetNeighbor(OpposedNeighborIndex(VertexIndex(t, iV)));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EdgeNeighbor(in Triangle t, int va, int vb)
        => t.GetNeighbor(EdgeNeighborIndex(t, va, vb));

    // -------------------------------------------------------------------------
    // Super-triangle
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TouchesSuperTriangle(in Triangle t)
        => t.V0 < Indices.SuperTriangleVertexCount
        || t.V1 < Indices.SuperTriangleVertexCount
        || t.V2 < Indices.SuperTriangleVertexCount;

    // -------------------------------------------------------------------------
    // Point location (integer)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Exact orient2d for integer coordinates.
    /// Returns +1 if <paramref name="p"/> is to the left of v1→v2, −1 if right, 0 if collinear.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Orient2D(V2i p, V2i v1, V2i v2)
        => PredicatesInt.Orient2dInt(p, v1, v2);

    /// <summary>
    /// Raw signed-area determinant (Int128) of the triangle formed by
    /// <paramref name="p"/>, <paramref name="v1"/>, <paramref name="v2"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 Orient2DRaw(V2i p, V2i v1, V2i v2)
        => PredicatesInt.Orient2dRaw(p, v1, v2);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="p"/> lies strictly inside the
    /// circumcircle of (<paramref name="v1"/>, <paramref name="v2"/>, <paramref name="v3"/>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInCircumcircle(V2i p, V2i v1, V2i v2, V2i v3)
        => PredicatesInt.InCircleInt(v1.X, v1.Y, v2.X, v2.Y, v3.X, v3.Y, p.X, p.Y) > 0;

    /// <summary>
    /// Squared Euclidean distance between two integer-coordinate points.
    /// Result fits in <see cref="Int128"/> for coordinates within
    /// <see cref="PredicatesInt.MaxCoordinate"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Int128 DistanceSquared(V2i a, V2i b)
    {
        Int128 dx = (Int128)b.X - a.X;
        Int128 dy = (Int128)b.Y - a.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>
    /// Classifies an integer orient2d sign (+1/0/−1) into a point-line location.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PtLineLocation ClassifyOrientation(int sign)
    {
        if (sign < 0) return PtLineLocation.Right;
        if (sign > 0) return PtLineLocation.Left;
        return PtLineLocation.OnLine;
    }

    /// <summary>
    /// Determines whether <paramref name="p"/> lies to the left of, on, or to
    /// the right of the directed line from <paramref name="v1"/> to
    /// <paramref name="v2"/> using exact integer arithmetic.
    /// </summary>
    /// <param name="areaSnapTolerance">
    /// Area-units tolerance: a point whose |Orient2dRaw| ≤ this value is
    /// classified as <see cref="PtLineLocation.OnLine"/>. Must be ≥ 0.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PtLineLocation LocatePointLine(V2i p, V2i v1, V2i v2, long areaSnapTolerance = 0L)
    {
        Int128 area = PredicatesInt.Orient2dRaw(p, v1, v2);
        if (area > areaSnapTolerance) return PtLineLocation.Left;
        if (area < (Int128)(-areaSnapTolerance)) return PtLineLocation.Right;
        return PtLineLocation.OnLine;
    }

    /// <summary>
    /// Determines whether <paramref name="p"/> is inside, outside, on an edge of,
    /// or on a vertex of the CCW triangle (<paramref name="v1"/>, <paramref name="v2"/>,
    /// <paramref name="v3"/>).
    /// </summary>
    public static PtTriLocation LocatePointTriangle(V2i p, V2i v1, V2i v2, V2i v3)
    {
        PtTriLocation result = PtTriLocation.Inside;

        PtLineLocation e = LocatePointLine(p, v1, v2);
        if (e == PtLineLocation.Right) return PtTriLocation.Outside;
        if (e == PtLineLocation.OnLine) result = PtTriLocation.OnEdge0;

        e = LocatePointLine(p, v2, v3);
        if (e == PtLineLocation.Right) return PtTriLocation.Outside;
        if (e == PtLineLocation.OnLine)
            result = result == PtTriLocation.Inside ? PtTriLocation.OnEdge1 : PtTriLocation.OnVertex;

        e = LocatePointLine(p, v3, v1);
        if (e == PtLineLocation.Right) return PtTriLocation.Outside;
        if (e == PtLineLocation.OnLine)
            result = result == PtTriLocation.Inside ? PtTriLocation.OnEdge2 : PtTriLocation.OnVertex;

        return result;
    }

    /// <summary>Returns <c>true</c> if the location is on any triangle edge.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnEdge(PtTriLocation location)
        => location is PtTriLocation.OnEdge0
                    or PtTriLocation.OnEdge1
                    or PtTriLocation.OnEdge2;

    /// <summary>
    /// Returns the neighbor-slot index (0, 1, or 2) for the on-edge location.
    /// Only valid when <see cref="IsOnEdge"/> returns <c>true</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int EdgeNeighborFromLocation(PtTriLocation location)
        => (int)(location - PtTriLocation.OnEdge0);

    /// <summary>
    /// Computes the intersection point of line segments a–b and c–d using
    /// 256-bit integer arithmetic, rounded half-away-from-zero.
    /// </summary>
    public static V2i IntersectionPosition(V2i a, V2i b, V2i c, V2i d)
    {
        Int128 acd   = PredicatesInt.Orient2dRaw(a, c, d);
        Int128 bcd   = PredicatesInt.Orient2dRaw(b, c, d);
        Int128 denom = acd - bcd;

        Int256 numX = Int256.Multiply(acd, (Int128)(b.X - a.X));
        Int256 numY = Int256.Multiply(acd, (Int128)(b.Y - a.Y));

        long x = a.X + Int256.DivideToInt64(numX, denom);
        long y = a.Y + Int256.DivideToInt64(numY, denom);
        return new V2i(x, y);
    }

    // -------------------------------------------------------------------------
    // Adjacency helpers (internal use)
    // -------------------------------------------------------------------------

    internal static bool VerticesShareEdge(List<int> aTris, List<int> bTris)
    {
        foreach (int t in aTris)
        {
            if (bTris.Contains(t)) return true;
        }
        return false;
    }
}
