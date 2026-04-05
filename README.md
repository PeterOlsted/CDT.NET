# CDT.NET

[![Build](https://github.com/PeterOlsted/CDT.NET/actions/workflows/ci-cd.yml/badge.svg?branch=main)](https://github.com/PeterOlsted/CDT.NET/actions/workflows/ci-cd.yml)
[![NuGet](https://img.shields.io/nuget/v/CDT.NET.svg)](https://www.nuget.org/packages/CDT.NET)

**Deterministic Constrained Delaunay Triangulation for Unity and .NET.**

A C# port of [artem-ogre/CDT](https://github.com/artem-ogre/CDT), converted to exact integer arithmetic for bit-reproducible results across platforms — designed for use in Unity (including Burst-compatible pipelines) and any .NET 5+ environment.

> **Credits:** Based on the [CDT C++ library](https://github.com/artem-ogre/CDT) by Artem Amirkhanov and contributors (MPL-2.0).
> Algorithm documentation and research references: [original C++ repository](https://github.com/artem-ogre/CDT) and its [online docs](https://artem-ogre.github.io/CDT/).

---

## Why this fork?

The upstream C++ CDT library uses floating-point predicates (Shewchuk adaptive arithmetic). For game engines and simulation code that requires **deterministic, platform-independent results**, floating-point is insufficient — the same input can produce different triangulations on different CPUs, compilers, or runtimes.

This fork replaces all floating-point arithmetic with **exact integer predicates**:

- All coordinates are `long` (`V2i`), quantised to a fixed integer grid
- Orient2d and InCircle predicates use `Int128` / `Int256` intermediates — no rounding, no adaptive fallback
- `IntersectionPosition` uses 256-bit exact arithmetic with round-half-away-from-zero snapping
- Zero `float` or `double` in the core algorithm — results are identical on every platform

**Unity compatibility:** `System.Int128` / `System.UInt128` are unavailable before Unity 6.8 (.NET 8 BCL). CDT.NET ships a minimal `Int128` polyfill gated behind `#if !NET7_0_OR_GREATER`, and rewrites `Int256` multiplication to avoid `UInt128` entirely using 32-bit schoolbook arithmetic. The `net5.0` build target is verified to compile and run correctly on the current Unity runtime.

---

## Features

- **Constrained Delaunay Triangulation** — force specific edges into the triangulation
- **Conforming Delaunay Triangulation** — split edges and insert Steiner points until constraints are represented
- **Convex-hull triangulation** — triangulate all points with no constraints
- **Automatic hole detection** — `EraseOuterTrianglesAndHoles` removes outer regions and holes by even-odd winding depth
- **Exact integer predicates** — Orient2d and InCircle with Int128/Int256 intermediates, bit-identical on all platforms
- **Int128 polyfill** — works on Unity's current runtime (net5.0) without requiring .NET 7+
- **KD-tree spatial indexing** — integer KD-tree for fast nearest-neighbor lookup
- **Duplicate handling** — `CdtUtils.RemoveDuplicatesAndRemapEdges` before triangulation
- **Intersecting constraint edges** — optionally resolve by splitting at the exact integer intersection point
- **Multi-target:** `net5.0`, `net6.0`, `net8.0`, `net10.0`

**Pre-conditions:**
- No duplicate vertices (use `CdtUtils.RemoveDuplicatesAndRemapEdges` to clean input)
- No two constraint edges may intersect (or pass `IntersectingConstraintEdges.TryResolve`)
- Coordinates must be within `±PredicatesInt.MaxCoordinate` (2⁵³ ≈ 9 × 10¹⁵)

**Post-conditions:**
- All triangles have **counter-clockwise (CCW) winding** in a coordinate system where X points right and Y points up

---

## Installation

```
dotnet add package CDT.NET
```

---

## Usage

All coordinates use `V2i` (integer X/Y as `long`). Quantise your world-space coordinates to a fixed integer grid before passing them in.

### Delaunay triangulation (convex hull, no constraints)

```csharp
using CDT;

var vertices = new List<V2i>
{
    new(0, 0), new(4000, 0), new(4000, 4000), new(0, 4000), new(2000, 2000),
};

var cdt = new Triangulation();
cdt.InsertVertices(vertices);
cdt.EraseSuperTriangle(); // produces convex hull

ReadOnlyMemory<Triangle> triangles = cdt.Triangles;
ReadOnlyMemory<V2i>      points    = cdt.Vertices;
HashSet<Edge>            allEdges  = CdtUtils.ExtractEdgesFromTriangles(triangles.Span);
```

### Constrained Delaunay triangulation (bounded domain)

```csharp
using CDT;

var vertices = new List<V2i>
{
    new(0, 0), new(4000, 0), new(4000, 4000), new(0, 4000),
};
var edges = new List<Edge>
{
    new(0, 1), new(1, 2), new(2, 3), new(3, 0), // square boundary
};

var cdt = new Triangulation();
cdt.InsertVertices(vertices);
cdt.InsertEdges(edges);
cdt.EraseOuterTriangles();

ReadOnlyMemory<Triangle> triangles  = cdt.Triangles;
IReadOnlySet<Edge>       fixedEdges = cdt.FixedEdges;
```

### Auto-detect boundaries and holes

```csharp
using CDT;

// Outer square (0-3) + inner hole (4-7)
var vertices = new List<V2i>
{
    new(0, 0), new(6000, 0), new(6000, 6000), new(0, 6000),
    new(2000, 2000), new(4000, 2000), new(4000, 4000), new(2000, 4000),
};
var edges = new List<Edge>
{
    new(0, 1), new(1, 2), new(2, 3), new(3, 0), // outer (CCW)
    new(4, 7), new(7, 6), new(6, 5), new(5, 4), // inner hole (CW)
};

var cdt = new Triangulation();
cdt.InsertVertices(vertices);
cdt.InsertEdges(edges);
cdt.EraseOuterTrianglesAndHoles();
```

### Resolving intersecting constraint edges

```csharp
using CDT;

var vertices = new List<V2i>
{
    new(0, 0), new(1000, 0), new(1000, 1000), new(0, 1000),
};
var edges = new List<Edge>
{
    new(0, 2), // diagonal ↗
    new(1, 3), // diagonal ↖ — intersects (0,2)
};

var cdt = new Triangulation(
    VertexInsertionOrder.Auto,
    IntersectingConstraintEdges.TryResolve,
    snapTolerance: 0L);

cdt.InsertVertices(vertices);
cdt.InsertEdges(edges); // intersection resolved by inserting an exact integer vertex
cdt.EraseSuperTriangle();
```

### Removing duplicate vertices

```csharp
using CDT;

var vertices = new List<V2i>
{
    new(0, 0), new(4000, 0), new(4000, 4000), new(0, 4000),
    new(0, 0), // duplicate of vertex 0
};
var edges = new List<Edge>
{
    new(0, 4), // will be remapped since vertex 4 duplicates vertex 0
    new(1, 2),
};

CdtUtils.RemoveDuplicatesAndRemapEdges(vertices, edges);

var cdt = new Triangulation();
cdt.InsertVertices(vertices);
cdt.InsertEdges(edges.Where(e => e.V1 != e.V2).ToList());
cdt.EraseSuperTriangle();
```

---

## Coordinate system

Inputs are integer coordinates (`long`). Quantise your floating-point world coordinates before use:

```csharp
const long Scale = 1_000_000; // 1 unit = 1 µm if world coords are in metres

V2i ToGrid(float x, float y) => new((long)(x * Scale), (long)(y * Scale));
```

The maximum safe coordinate magnitude is `PredicatesInt.MaxCoordinate` (2⁵³ ≈ 9 × 10¹⁵). With a scale of 10⁶ this accommodates world coordinates up to ~9 × 10⁹ metres.

---

## Building

```bash
dotnet build
```

## Testing

```bash
dotnet test test/CDT.Tests
```

## Benchmarking

```bash
dotnet run -c Release --project benchmark/CDT.Benchmarks
```

---

## License

[Mozilla Public License Version 2.0](LICENSE)

### Third-party

This project is a fork and C# port of [CDT — C++ library for constrained Delaunay triangulation](https://github.com/artem-ogre/CDT):

```
Copyright © 2019 Leica Geosystems Technology AB
Copyright © The CDT Contributors
Licensed under the Mozilla Public License, Version 2.0
https://github.com/artem-ogre/CDT
```

Modifications in this repository (integer arithmetic conversion, Unity compatibility layer, .NET port):

```
Copyright © 2024 Peter Ølsted
Licensed under the Mozilla Public License, Version 2.0
```
