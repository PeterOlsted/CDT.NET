// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CDT;

/// <summary>
/// Undirected edge connecting two vertices. The vertex with the smaller index
/// is always stored first.
/// </summary>
public readonly struct Edge : IEquatable<Edge>
{
    /// <summary>First vertex index (always &lt;= V2).</summary>
    public readonly int V1;

    /// <summary>Second vertex index (always &gt;= V1).</summary>
    public readonly int V2;

    /// <summary>Creates an edge; automatically orders V1 &lt;= V2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Edge(int v1, int v2)
    {
        if (v1 <= v2)
        {
            V1 = v1;
            V2 = v2;
        }
        else
        {
            V1 = v2;
            V2 = v1;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Edge other) => V1 == other.V1 && V2 == other.V2;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Edge e && Equals(e);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(V1, V2);

    /// <inheritdoc/>
    public static bool operator ==(Edge a, Edge b) => a.Equals(b);

    /// <inheritdoc/>
    public static bool operator !=(Edge a, Edge b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => $"({V1}, {V2})";
}

/// <summary>
/// Triangulation triangle with counter-clockwise winding.
/// <code>
///       v[2]
///       /\
///  n[2]/  \n[1]
///     /____\
/// v[0]  n[0]  v[1]
/// </code>
/// </summary>
public struct Triangle
{
    /// <summary>Three vertex indices (CCW winding).</summary>
    public int V0, V1, V2;

    /// <summary>Three neighbor triangle indices (NoNeighbor = -1 if none).</summary>
    public int N0, N1, N2;

    /// <summary>Creates a triangle with no vertices or neighbors.</summary>
    public Triangle()
    {
        V0 = V1 = V2 = Indices.NoVertex;
        N0 = N1 = N2 = Indices.NoNeighbor;
    }

    /// <summary>Creates a triangle with given vertices and neighbors.</summary>
    public Triangle(int v0, int v1, int v2, int n0, int n1, int n2)
    {
        V0 = v0; V1 = v1; V2 = v2;
        N0 = n0; N1 = n1; N2 = n2;
    }

    /// <summary>Gets a vertex by index (0, 1, or 2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetVertex(int i) => i switch { 0 => V0, 1 => V1, _ => V2 };

    /// <summary>Sets a vertex by index (0, 1, or 2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetVertex(int i, int value)
    {
        if (i == 0) V0 = value;
        else if (i == 1) V1 = value;
        else V2 = value;
    }

    /// <summary>Gets a neighbor by index (0, 1, or 2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetNeighbor(int i) => i switch { 0 => N0, 1 => N1, _ => N2 };

    /// <summary>Sets a neighbor by index (0, 1, or 2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetNeighbor(int i, int value)
    {
        if (i == 0) N0 = value;
        else if (i == 1) N1 = value;
        else N2 = value;
    }

    /// <summary>Checks if the triangle contains the given vertex index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool ContainsVertex(int v) => V0 == v || V1 == v || V2 == v;

    /// <summary>
    /// Returns the index of the next triangle adjacent to vertex <paramref name="v"/>
    /// (clockwise), and the other vertex of the shared edge.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly (int triIndex, int otherVertex) Next(int v)
    {
        if (V0 == v) return (N0, V1);
        if (V1 == v) return (N1, V2);
        return (N2, V0);
    }

    /// <summary>
    /// Returns the index of the previous triangle adjacent to vertex <paramref name="v"/>
    /// (counter-clockwise), and the other vertex of the shared edge.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly (int triIndex, int otherVertex) Prev(int v)
    {
        if (V0 == v) return (N2, V2);
        if (V1 == v) return (N0, V0);
        return (N1, V1);
    }

    /// <inheritdoc/>
    public override readonly string ToString() => $"V({V0}, {V1}, {V2}) N({N0}, {N1}, {N2})";
}

/// <summary>Shared index constants.</summary>
public static class Indices
{
    /// <summary>Sentinel for invalid/no neighbor triangle.</summary>
    public const int NoNeighbor = -1;

    /// <summary>Sentinel for invalid/no vertex.</summary>
    public const int NoVertex = -1;

    /// <summary>Number of super-triangle vertices.</summary>
    public const int SuperTriangleVertexCount = 3;
}

/// <summary>Strategy for ordering vertex insertions.</summary>
public enum VertexInsertionOrder
{
    /// <summary>
    /// Automatically optimized: breadth-first KD-tree traversal for initial
    /// bulk-load, randomized for subsequent insertions.
    /// </summary>
    Auto,

    /// <summary>Inserts vertices in the exact order provided.</summary>
    AsProvided,
}

/// <summary>Super-geometry type used to embed the triangulation.</summary>
public enum SuperGeometryType
{
    /// <summary>Conventional super-triangle encompassing all input points.</summary>
    SuperTriangle,

    /// <summary>User-specified custom geometry (e.g., a grid).</summary>
    Custom,
}

/// <summary>Strategy for handling intersecting constraint edges.</summary>
public enum IntersectingConstraintEdges
{
    /// <summary>Intersecting constraints throw an exception.</summary>
    NotAllowed,

    /// <summary>Attempt to resolve intersections by adding split vertices.</summary>
    TryResolve,

    /// <summary>
    /// No checks performed; caller must guarantee no intersections.
    /// Slightly faster but unsafe for bad input.
    /// </summary>
    DontCheck,
}

/// <summary>Layer depth type (used for hole detection).</summary>
public readonly struct LayerDepth
{
    /// <summary>The raw depth value.</summary>
    public readonly ushort Value;

    /// <summary>Initializes a layer depth.</summary>
    public LayerDepth(ushort value) => Value = value;

    /// <summary>Implicit conversion from ushort.</summary>
    public static implicit operator LayerDepth(ushort v) => new(v);

    /// <summary>Implicit conversion to ushort.</summary>
    public static implicit operator ushort(LayerDepth d) => d.Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}

/// <summary>Location of a point relative to a triangle.</summary>
public enum PtTriLocation
{
    Inside,
    Outside,
    OnEdge0,
    OnEdge1,
    OnEdge2,
    OnVertex,
}

/// <summary>Location of a point relative to a directed line.</summary>
public enum PtLineLocation
{
    Left,
    Right,
    OnLine,
}

// =============================================================================
// Integer coordinate types (deterministic / exact-arithmetic API)
// =============================================================================

/// <summary>
/// 2D point with 64-bit integer coordinates.
/// Blittable value type — safe for use in unmanaged / Burst contexts.
/// </summary>
public readonly struct V2i : IEquatable<V2i>
{
    /// <summary>X-coordinate.</summary>
    public readonly long X;

    /// <summary>Y-coordinate.</summary>
    public readonly long Y;

    /// <summary>Initializes a new point with the given coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public V2i(long x, long y) { X = x; Y = y; }

    /// <summary>Zero point (0, 0).</summary>
    public static readonly V2i Zero = new(0L, 0L);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(V2i other) => X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is V2i v && Equals(v);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc/>
    public static bool operator ==(V2i a, V2i b) => a.Equals(b);

    /// <inheritdoc/>
    public static bool operator !=(V2i a, V2i b) => !a.Equals(b);

    /// <inheritdoc/>
    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// 2D axis-aligned bounding box with 64-bit integer coordinates.
/// Sentinels: <c>Min</c> starts at (<see cref="long.MaxValue"/>, <see cref="long.MaxValue"/>)
/// and <c>Max</c> starts at (<see cref="long.MinValue"/>, <see cref="long.MinValue"/>)
/// so that the first <see cref="Envelop(long, long)"/> call sets both correctly.
/// </summary>
public struct Box2i
{
    /// <summary>Minimum corner.</summary>
    public V2i Min;

    /// <summary>Maximum corner.</summary>
    public V2i Max;

    /// <summary>Creates an empty box (sentinel values — no point contained yet).</summary>
    public Box2i()
    {
        Min = new V2i(long.MaxValue, long.MaxValue);
        Max = new V2i(long.MinValue, long.MinValue);
    }

    /// <summary>Expands the box to include the given coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Envelop(long x, long y)
    {
        if (x < Min.X) Min = new V2i(x, Min.Y);
        if (x > Max.X) Max = new V2i(x, Max.Y);
        if (y < Min.Y) Min = new V2i(Min.X, y);
        if (y > Max.Y) Max = new V2i(Max.X, y);
    }

    /// <summary>Expands the box to include the given point.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Envelop(V2i p) => Envelop(p.X, p.Y);

    /// <summary>Expands the box to include all given points.</summary>
    public void Envelop(IReadOnlyList<V2i> points)
    {
        foreach (var p in points) Envelop(p.X, p.Y);
    }

    /// <summary>Expands the box to include all given points.</summary>
    public void Envelop(ReadOnlySpan<V2i> points)
    {
        foreach (ref readonly var p in points) Envelop(p.X, p.Y);
    }

    /// <summary>Creates a box containing all the given points.</summary>
    public static Box2i Of(IReadOnlyList<V2i> points)
    {
        var box = new Box2i();
        box.Envelop(points);
        return box;
    }

    /// <inheritdoc/>
    public override readonly string ToString() => $"[{Min}, {Max}]";
}
