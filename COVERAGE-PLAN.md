# CDT.NET Coverage Improvement Plan

## Baseline vs Target

| Type | Line (now) | Branch (now) | Line (target) | Branch (target) |
|------|-----------|-------------|--------------|----------------|
| **CDT.Core (total)** | **90.7%** | **82.9%** | **≥97%** | **≥94%** |
| CDT.Box2i | 100% | 100% | 100% | 100% |
| CDT.CdtUtils | 75.1% | 67% | 95% | 90% |
| CDT.CovariantReadOnlyDictionary`3 | 18.1% | 50% | 95% | 90% |
| CDT.DictionaryExtensions | 100% | 100% | 100% | 100% |
| CDT.DuplicatesInfo | 100% | — | 100% | — |
| CDT.DuplicateVertexException | 66.6% | — | 100% | — |
| CDT.Edge | 75% | 66.6% | 100% | 100% |
| CDT.IntersectingConstraintsException | 0% | — | 100% | — |
| CDT.KdTree | 99.2% | 96.2% | 99.5% | 98% |
| CDT.LayerDepth | 0% | — | 100% | — |
| CDT.Predicates.Int128 | 88% | 91.6% | 97% | 97% |
| CDT.Predicates.Int256 | 99.1% | 89% | 99.5% | 96% |
| CDT.Predicates.PredicatesInt | 100% | — | 100% | — |
| CDT.TopologyVerifier | 77.2% | 63.8% | 97% | 93% |
| CDT.Triangle | 66.6% | 71.4% | 100% | 97% |
| CDT.Triangulation | 93.7% | 85.1% | 97% | 93% |
| CDT.TriangulationException | 100% | 100% | 100% | 100% |
| CDT.TriangulationFinalizedException | 100% | 100% | 100% | 100% |
| CDT.V2i | 100% | 75% | 100% | 100% |

---

## Prerequisites

### 1. Add `InternalsVisibleTo` to CDT.Core.csproj
Required by `CdtUtilsTests` (for `VerticesShareEdge`) and `TopologyVerifierTests` (raw-span overload).

```xml
<!-- src/CDT.Core/CDT.Core.csproj -->
<ItemGroup>
  <InternalsVisibleTo Include="CDT.Tests" />
</ItemGroup>
```

### 2. Add internal raw-span overload to TopologyVerifier
The public `VerifyTopology(Triangulation)` can only be called with a structurally valid
triangulation produced by the CDT algorithm — it is impossible to induce the `return false`
branches via the public API. An internal overload avoids reflection and keeps the tests clean.

```csharp
// src/CDT.Core/TopologyVerifier.cs  (add to existing partial class)
internal static bool VerifyTopology(
    ReadOnlySpan<Triangle> triangles,
    ReadOnlySpan<V2i> vertices)
{
    // — identical body to the public overload —
}
```

The public overload becomes a one-liner delegating to the internal one.

---

## Test File Plan

### New file: `test/CDT.Tests/TypeTests.cs`
Covers: `Edge`, `Triangle`, `LayerDepth`, `DuplicateVertexException`,
`CovariantReadOnlyDictionary`.

#### Edge (75% → 100% / 66.6% → 100%)
Missing branches / lines:
- Constructor swap branch (`v1 > v2`) — never tested
- `Equals(object?)` with wrong type (`obj is Edge` fails)
- `operator!=` with equal edges (false branch)
- `ToString()`

```
Edge_Constructor_OrdersSmallestFirst     new Edge(5, 2)  → V1==2, V2==5
Edge_Constructor_AlreadyOrdered          new Edge(1, 9)  → V1==1, V2==9
Edge_Constructor_Equal                   new Edge(3, 3)  → V1==3, V2==3
Edge_Equals_BoxedEdge                    ((object)e1).Equals(e2)
Edge_Equals_BoxedWrongType_ReturnsFalse  ((object)e1).Equals("x")
Edge_Equals_BoxedNull_ReturnsFalse       ((object)e1).Equals(null)
Edge_Inequality_NotEqual                 asserts != is true
Edge_Inequality_Equal_ReturnsFalse       asserts != is false
Edge_ToString_Format                     "(V1, V2)" format
Edge_GetHashCode_EqualEdgesMatch
```

#### Triangle (66.6% → 100% / 71.4% → 97%)
Missing: `Next`, `Prev`, `SetVertex`/`SetNeighbor` all-arm coverage,
`GetVertex`/`GetNeighbor` index 0 vs non-0 vs 2, `ToString`, `ContainsVertex` false path,
parameterless constructor.

```
Triangle_DefaultConstructor_UsesNoSentinels
Triangle_ParameterisedConstructor_StoresAll
Triangle_GetVertex_AllThreeIndices        indices 0,1,2
Triangle_SetVertex_AllThreeIndices        round-trip set then get
Triangle_GetNeighbor_AllThreeIndices
Triangle_SetNeighbor_AllThreeIndices
Triangle_ContainsVertex_True             vertex present
Triangle_ContainsVertex_False            vertex absent
Triangle_Next_V0                        (N0, V1) returned
Triangle_Next_V1                        (N1, V2) returned
Triangle_Next_V2                        (N2, V0) returned
Triangle_Prev_V0                        (N2, V2) returned
Triangle_Prev_V1                        (N0, V0) returned
Triangle_Prev_V2                        (N1, V1) returned
Triangle_ToString_Format
```

#### LayerDepth (0% → 100%)
```
LayerDepth_ImplicitFromUshort
LayerDepth_ImplicitToUshort
LayerDepth_ToString
```

#### DuplicateVertexException (66.6% → 100%)
```
DuplicateVertexException_Properties_V1V2
  // catch DuplicateVertexException ex; Assert ex.V1 and ex.V2
```

#### CovariantReadOnlyDictionary (18.1% → 95%)
Reached via `Triangulation.PieceToOriginals` after `ConformToEdges` on a conforming
overlapping boundary (the test in `GroundTruthTests` uses it but doesn't enumerate/index it).

```
CovariantReadOnlyDictionary_Count
CovariantReadOnlyDictionary_IndexerHit
CovariantReadOnlyDictionary_ContainsKey_True
CovariantReadOnlyDictionary_ContainsKey_False
CovariantReadOnlyDictionary_TryGetValue_Found
CovariantReadOnlyDictionary_TryGetValue_NotFound
CovariantReadOnlyDictionary_Keys_Enumerable
CovariantReadOnlyDictionary_Values_Enumerable
CovariantReadOnlyDictionary_GetEnumerator_Generic
CovariantReadOnlyDictionary_GetEnumerator_NonGeneric
```

Setup: build a conforming triangulation that splits an edge so `PieceToOriginals` is
non-empty, then exercise each property.

---

### New file: `test/CDT.Tests/CdtUtilsTests.cs`
Covers: uncovered helpers in `CdtUtils`.

#### Current gaps (75.1% → 95%)

```
CalculateTrianglesByVertex_EmptyTriangles
CalculateTrianglesByVertex_AdjacencyCorrect   triangle soup with known adjacency
EdgeToPiecesMapping_InvertsMapping            hand-crafted pieceToOriginals dict
RemapEdges_UpdatesEdgeIndices
LocatePointLine_WithSnapTolerance_Left        area just above tolerance → Left
LocatePointLine_WithSnapTolerance_OnLine      area within tolerance → OnLine
LocatePointLine_WithSnapTolerance_Right       area just below −tolerance → Right
ClassifyOrientation_AllThreeSigns
IsOnEdge_AllCases                             OnEdge0/1/2 → true; Inside → false
EdgeNeighborFromLocation_OnEdge0/1/2
VertexIndex_AllThreePositions
EdgeNeighborIndex_V0V1 / V0V2 / other
NeighborIndex_AllThreeNeighbors
OpposedVertex_AllPositions
OpposedTriangle_AllPositions
EdgeNeighbor_AllPositions
TouchesSuperTriangle_WithSuperVert / Without
VerticesShareEdge_Shared / NotShared       (internal, needs InternalsVisibleTo)
```

---

### New file: `test/CDT.Tests/TopologyVerifierTests.cs`
Covers the `return false` branches (77.2% → 97%).

Uses the internal `VerifyTopology(ReadOnlySpan<Triangle>, ReadOnlySpan<V2i>)` overload.

```
VerifyTopology_ValidSingleTriangle_ReturnsTrue
VerifyTopology_NoVertex_ReturnsFalse
  // triangle.V0 = Indices.NoVertex
VerifyTopology_VertexIndexTooLarge_ReturnsFalse
  // triangle.V0 = vertices.Length (out of range)
VerifyTopology_DegenerateTriangle_SameVertex_ReturnsFalse
  // V0 == V1
VerifyTopology_NeighborIndexTooLarge_ReturnsFalse
  // triangle.N0 = triangles.Length (out of range)
VerifyTopology_NeighborNotBackReferencing_ReturnsFalse
  // iTopo's neighbor says a different triangle is my neighbor
VerifyTopology_SharedEdge_WrongVertices_ReturnsFalse
  // neighbors agree on back-reference but don't share the expected edge vertices
VerifyTopology_EmptyTriangulation_ReturnsTrue
```

---

### Extensions to `test/CDT.Tests/TriangulationTests.cs`

#### IntersectingConstraintsException (0% → 100%)
```
NotAllowed_ThrowsIntersectingConstraintsException
  // two diagonals of a square with NotAllowed; catch; assert E1,E2 indices

NotAllowed_IntuitiveEdgeIndicesInException
  // verify exception message contains correct vertex indices
```

#### DontCheck strategy
```
DontCheck_NoExceptionOnIntersection
  // crossing diagonals with DontCheck; must not throw
```

#### EraseOuterTrianglesAndHoles
```
EraseOuterTrianglesAndHoles_RemovesHole
  // outer square + inner square hole; only hull ring remains
EraseOuterTrianglesAndHoles_NoHoles_SameAsEraseOuter
```

#### MultipleInsertVertices calls
```
InsertVertices_MultipleCalls_AccumulatesVertices
  // call InsertVertices twice; vertex count should be sum
```

#### Coordinate range validation
```
InsertVertices_ExceedsMaxCoordinate_Throws
  // coordinate > 2^53; expect ArgumentOutOfRangeException
```

#### PieceToOriginals population
```
ConformToEdges_OverlappingBoundary_PieceToOriginalsPopulated
```

---

### Extensions to `test/CDT.Tests/Int128Tests.cs`

Missing from 88% (polyfill path only — BCL path is 100%):

```
Int128_ToString_Format
  // polyfill only: "Int128(Hi=0x... Lo=0x...)"
Int128_Equals_BoxedSameValue_True
Int128_Equals_BoxedDifferentValue_False
Int128_Equals_BoxedWrongType_False
Int128_Equals_BoxedNull_False
Int128_GetHashCode_EqualValuesMatch
Int128_GetHashCode_DifferentValuesDiffer
Int128_NegativeOne_IsMinusOne
Int128_MinValue_IsMinimum
Int128_One_IsOne
Int128_LessEqual_AllCases             a <= a; a <= b (a<b); b <= a (b>a)
Int128_GreaterEqual_AllCases
```

---

### Extensions to `test/CDT.Tests/Int256Tests.cs`

Missing branches in `DivideToInt64` and `CompareUnsigned` (89% → 96%):

```
DivideToInt64_NumeratorZero_ReturnsZero
DivideToInt64_BothNegative_ReturnsPositive
DivideToInt64_ExactDivision_NoRounding
DivideToInt64_RoundingHalfUp_AwayFromZero    5/2 → 3
DivideToInt64_LargeValuesMatchBigInteger
DivideToInt64_DenominatorNegative
Int256_ToString_Format
Int256_CompareUnsigned_EqualLo               a.Lo == b.Lo, higher limbs equal → 0
Int256_CompareUnsigned_LoGreater             returns 1
```

---

### Extensions to `test/CDT.Tests/V2iTests.cs`

V2i: 100% line / 75% branch → 100% / 100%:

```
V2i_EqualsObject_WrongType_ReturnsFalse
  // p.Equals("hello") — covers the false arm of `obj is V2i v`
V2i_NotEqual_Operator_True
  // explicitly test !=
```

---

## Estimated Coverage After All Changes

| Type | Predicted Line | Predicted Branch |
|------|---------------|-----------------|
| CDT.Core (total) | ~97.5% | ~94.5% |
| CDT.CdtUtils | ~95% | ~90% |
| CDT.CovariantReadOnlyDictionary`3 | ~95% | ~90% |
| CDT.DuplicateVertexException | 100% | — |
| CDT.Edge | 100% | 100% |
| CDT.IntersectingConstraintsException | 100% | — |
| CDT.LayerDepth | 100% | — |
| CDT.Predicates.Int128 | ~97% | ~97% |
| CDT.Predicates.Int256 | ~99.5% | ~96% |
| CDT.TopologyVerifier | ~97% | ~93% |
| CDT.Triangle | 100% | ~97% |
| CDT.Triangulation | ~97% | ~93% |
| CDT.V2i | 100% | 100% |

---

## Execution Order

```
1. Prerequisite: InternalsVisibleTo + internal TopologyVerifier overload
2. TypeTests.cs          (no deps)
3. CdtUtilsTests.cs      (needs InternalsVisibleTo)
4. TopologyVerifierTests.cs  (needs internal overload)
5. Extend TriangulationTests.cs
6. Extend Int128Tests.cs
7. Extend Int256Tests.cs
8. Extend V2iTests.cs
9. dotnet test --coverage; verify all targets met
```
