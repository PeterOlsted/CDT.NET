// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using CDT;

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkInputReader).Assembly).Run(args);

// ---------------------------------------------------------------------------
// Helpers shared by all benchmarks
// ---------------------------------------------------------------------------
internal static class BenchmarkInputReader
{
    /// <summary>
    /// Reads a CDT input file.
    /// Format: <c>nVerts nEdges\n x y\n … v1 v2\n …</c>
    /// Coordinates are parsed as doubles then truncated to long to match the
    /// integer V2i type used by CDT.Core.
    /// </summary>
    public static (List<V2i> Vertices, List<Edge> Edges) Read(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "inputs", fileName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Benchmark input file not found: {fileName}");

        using var sr = new StreamReader(path);
        var header = sr.ReadLine()!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int nVerts = int.Parse(header[0]);
        int nEdges = int.Parse(header[1]);

        var verts = new List<V2i>(nVerts);
        for (int i = 0; i < nVerts; i++)
        {
            var tok = sr.ReadLine()!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            verts.Add(new V2i(
                (long)double.Parse(tok[0], System.Globalization.CultureInfo.InvariantCulture),
                (long)double.Parse(tok[1], System.Globalization.CultureInfo.InvariantCulture)));
        }

        var edges = new List<Edge>(nEdges);
        for (int i = 0; i < nEdges; i++)
        {
            var tok = sr.ReadLine()!.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            edges.Add(new Edge(int.Parse(tok[0]), int.Parse(tok[1])));
        }

        return (verts, edges);
    }
}

// ---------------------------------------------------------------------------
// Constrained Sweden – mirrors the C++ benchmark suite exactly
// ---------------------------------------------------------------------------

/// <summary>
/// Benchmarks using the "Constrained Sweden" dataset (~2 600 vertices,
/// ~2 600 constraint edges) – the same dataset used in the C++ CDT benchmarks.
/// </summary>
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[ShortRunJob]
public class ConstrainedSwedenBenchmarks
{
    private List<V2i> _vertices = null!;
    private List<Edge> _edges = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_vertices, _edges) = BenchmarkInputReader.Read("Constrained Sweden.txt");
    }

    // -- Vertices only -------------------------------------------------------

    [Benchmark(Description = "Vertices only – AsProvided")]
    [BenchmarkCategory("VerticesOnly")]
    public Triangulation VerticesOnly_AsProvided()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt.InsertVertices(_vertices);
        return cdt;
    }

    [Benchmark(Description = "Vertices only – Auto")]
    [BenchmarkCategory("VerticesOnly")]
    public Triangulation VerticesOnly_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        return cdt;
    }

    // -- Vertices + edges (constrained DT) -----------------------------------

    [Benchmark(Description = "Constrained – AsProvided")]
    [BenchmarkCategory("Constrained")]
    public Triangulation Constrained_AsProvided()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        return cdt;
    }

    [Benchmark(Description = "Constrained – Auto")]
    [BenchmarkCategory("Constrained")]
    public Triangulation Constrained_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        return cdt;
    }

    // -- Conforming DT -------------------------------------------------------

    [Benchmark(Description = "Conforming – Auto")]
    [BenchmarkCategory("Conforming")]
    public Triangulation Conforming_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.ConformToEdges(_edges);
        return cdt;
    }

    [Benchmark(Description = "Conforming – AsProvided")]
    [BenchmarkCategory("Conforming")]
    public Triangulation Conforming_AsProvided()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt.InsertVertices(_vertices);
        cdt.ConformToEdges(_edges);
        return cdt;
    }

    // -- Full pipeline (insert + erase outer + holes) ------------------------

    [Benchmark(Description = "Full pipeline – Auto")]
    [BenchmarkCategory("FullPipeline")]
    public Triangulation FullPipeline_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.TryResolve, 0L);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        cdt.EraseOuterTrianglesAndHoles();
        return cdt;
    }

    // -- Finalization APIs (EraseSuperTriangle / EraseOuterTriangles) ---------

    [Benchmark(Description = "EraseSuperTriangle – Auto")]
    [BenchmarkCategory("Finalization")]
    public Triangulation EraseSuperTriangle_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        cdt.EraseSuperTriangle();
        return cdt;
    }

    [Benchmark(Description = "EraseOuterTriangles – Auto")]
    [BenchmarkCategory("Finalization")]
    public Triangulation EraseOuterTriangles_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        cdt.EraseOuterTriangles();
        return cdt;
    }
}

// ---------------------------------------------------------------------------
// Smaller datasets for micro-benchmarks
// ---------------------------------------------------------------------------

/// <summary>
/// Benchmarks on the smaller "cdt.txt" dataset (~101 vertices, ~103 edges)
/// to measure per-vertex overhead without the noise of large datasets.
/// </summary>
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[ShortRunJob]
public class SmallDatasetBenchmarks
{
    private List<V2i> _vertices = null!;
    private List<Edge> _edges = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_vertices, _edges) = BenchmarkInputReader.Read("cdt.txt");
    }

    [Benchmark(Description = "Small – Constrained Auto")]
    [BenchmarkCategory("Small")]
    public Triangulation Small_Constrained_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        return cdt;
    }

    [Benchmark(Description = "Small – Constrained AsProvided")]
    [BenchmarkCategory("Small")]
    public Triangulation Small_Constrained_AsProvided()
    {
        var cdt = new Triangulation(VertexInsertionOrder.AsProvided);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        return cdt;
    }

    [Benchmark(Description = "Small – Conforming Auto")]
    [BenchmarkCategory("SmallConforming")]
    public Triangulation Small_Conforming_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.ConformToEdges(_edges);
        return cdt;
    }

    [Benchmark(Description = "Small – EraseSuperTriangle Auto")]
    [BenchmarkCategory("SmallFinalization")]
    public Triangulation Small_EraseSuperTriangle_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        cdt.EraseSuperTriangle();
        return cdt;
    }

    [Benchmark(Description = "Small – EraseOuterTrianglesAndHoles Auto")]
    [BenchmarkCategory("SmallFinalization")]
    public Triangulation Small_EraseOuterTrianglesAndHoles_Auto()
    {
        var cdt = new Triangulation(VertexInsertionOrder.Auto,
            IntersectingConstraintEdges.TryResolve, 0L);
        cdt.InsertVertices(_vertices);
        cdt.InsertEdges(_edges);
        cdt.EraseOuterTrianglesAndHoles();
        return cdt;
    }
}
