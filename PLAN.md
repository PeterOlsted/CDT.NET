# Plan: CDT.NET Deterministic Integer Conversion (Batches 3–4)

## Status

| Batch | Status |
|-------|--------|
| Batch 1 — Int256, V2i, Box2i | ✅ COMPLETE |
| Batch 2 — PredicatesInt, KdTree (concrete) | ✅ COMPLETE |
| Prereq — Int128 polyfill + Int256.MultiplyUnsigned128 rewrite | ✅ COMPLETE |
| Prereq — Int256.DivideToInt64 | ✅ COMPLETE |
| Batch 3 — CdtUtils + Triangulation | ✅ COMPLETE |
| Batch 4 — Delete dead code | ✅ COMPLETE |

## TL;DR

Remove all generic floating-point (`<T>`) from `CdtUtils.cs`, `Triangulation.cs`, and `TopologyVerifier.cs`. Replace with concrete `V2i`/`long` types, route predicates through `PredicatesInt`, implement integer `IntersectionPosition` with Int256 intermediates and grid-snapping, replace the cos(30°) super-triangle with integer bounding geometry, and convert the test infrastructure. Then delete `PredicatesAdaptive.cs`, `PredicatesExact.cs`, and all dead float imports.

---

## Prerequisite: Int128 Polyfill *(must land before Phase 3A)*

Unity does not ship the .NET 8 BCL until Unity 6.8 (~1 year away). `System.Int128` and `System.UInt128` are unavailable on the current Unity runtime. See `PLAN-DOTNET-COMPAT.md` for the full analysis.

### TODO — `src/CDT.Core/Predicates/Int128Polyfill.cs` *(new file)*

- [ ] Add `net5.0` to `<TargetFrameworks>` in `src/CDT.Core/CDT.Core.csproj` (Unity target). Verify the project compiles clean under all TFMs before proceeding.

- [ ] Implement `internal readonly struct Int128` gated behind `#if !NET7_0_OR_GREATER`
  - Two `internal` `ulong` fields: `_lo` (bits 0–63), `_hi` (bits 64–127), two's complement
  - Operators: `+`, `-`, `*`, unary `-`
  - Comparison: `<`, `>`, `<=`, `>=`, `==`, `!=`, `CompareTo(Int128)`
  - Constants: `Zero`, `MaxValue`, `MinValue`
  - Casts: implicit from `long`; explicit to `ulong`; explicit to `long` *(explicit to `long` is not used by current code but is required by `DivideToInt64` once implemented — include it)*
  - No `>>` operator — not needed. `Int256.FromInt128` must be rewritten (see below) to read `_lo`/`_hi` directly instead of using `(UInt128)value >> 64`
  - No `ToString`, no `IFormattable`, no BCL interfaces — only what the audit requires

### TODO — `src/CDT.Core/Predicates/Int256.cs`

- [ ] Rewrite `FromInt128` to read polyfill fields directly, eliminating the `UInt128` dependency:
  ```csharp
  // Before: ulong m1 = (ulong)((UInt128)value >> 64);
  // After:
  ulong lo = value._lo;
  ulong m1 = value._hi;
  ```
  On .NET 7+ where `Int128` is the BCL type, use `(ulong)(value >> 64)` instead — gate with `#if NET7_0_OR_GREATER`.

- [ ] Rewrite `MultiplyUnsigned128` to avoid `UInt128`: use explicit 32-bit word splitting (4×4 schoolbook, 16 `ulong` partial products). This makes `Int256` fully Burst-compatible and eliminates the need for a `UInt128` polyfill.

### TODO — `test/CDT.Tests/Int128PolyfillTests.cs` *(new file)*

- [ ] `Add_Positive` — e.g. `3 + 5 = 8`
- [ ] `Add_WithCarry` — overflow across the `_lo`/`_hi` boundary
- [ ] `Subtract_Positive`
- [ ] `Subtract_Underflow`
- [ ] `Multiply_SmallValues`
- [ ] `Multiply_LargeValues` — cross-check against BCL `System.Int128` on .NET 8 (tests run on all TFMs; on .NET 8 both paths are exercised via `#if`)
- [ ] `Negate_Positive` and `Negate_Negative`
- [ ] `Comparison_AllSixOperators`
- [ ] `Cast_FromLong_RoundTrip`
- [ ] `Cast_ToUlong_Truncates`
- [ ] `Cast_ToLong_RoundTrip`

---

## Prerequisite: Int256.DivideToInt64 *(must land before Phase 3A)*

`IntersectionPosition` requires dividing an `Int256` numerator by an `Int128` denominator to recover a `long` coordinate. This operation does not exist in `Int256.cs`.

### Bit-width analysis

```
acd, bcd  : Int128,  magnitude ≤ 2¹⁰⁹  (Orient2dRaw outputs with |coord| ≤ 2⁵³)
denom     : Int128,  magnitude ≤ 2¹¹⁰  (acd − bcd)
delta     : long,    magnitude ≤ 2⁵⁴   (coordinate difference)
num       : Int256,  magnitude ≤ 2¹⁶³  (Int256.Multiply(acd, (Int128)delta))
quotient  : long,    magnitude ≤ 2⁵³   (it is a snapped coordinate — guaranteed)
```

Because `|quotient| ≤ 2⁵³`, the top 128 bits of `|numerator|` are strictly less than `|denominator|`. Knuth algorithm D degenerates to a single-"digit" quotient. ~50 lines, no allocation, stays `unmanaged`. No `BigInteger`.

### TODO — `src/CDT.Core/Predicates/Int256.cs`

- [ ] Add `public static long DivideToInt64(Int256 numerator, Int128 denominator)`

  **Algorithm — round-half-away-from-zero:**
  1. Extract signs; work with unsigned magnitudes `absNum` (Int256) and `absDenom` (Int128)
  2. Compute truncated quotient `q = absNum / absDenom` via single-word Knuth D (quotient fits in `ulong` by the bit-width constraint above)
  3. Compute remainder `r = absNum − q × absDenom`; since `r < absDenom ≤ 2¹¹⁰`, `r` fits in `Int128`
  4. If `2 × r ≥ absDenom`: `q += 1` (safe: `2r < 2¹¹¹` fits in `Int128`)
  5. Apply combined sign: if `num` and `denominator` differ in sign, negate `q`

  Document the rounding convention in the XML doc comment. Precondition: `denominator != 0` and result fits in `long` (Debug.Assert, no-op in release).

### TODO — `test/CDT.Tests/Int256Tests.cs`

- [ ] `DivideToInt64_BothPositive` — e.g. `15 / 4 = 4` (rounds toward nearest)
- [ ] `DivideToInt64_NumeratorNegative` — e.g. `−15 / 4 = −4`
- [ ] `DivideToInt64_DenominatorNegative` — e.g. `15 / −4 = −4`
- [ ] `DivideToInt64_BothNegative` — e.g. `−15 / −4 = 4`
- [ ] `DivideToInt64_ExactDivision` — no rounding case, e.g. `12 / 4 = 3`
- [ ] `DivideToInt64_NumeratorZero` — always returns `0`
- [ ] `DivideToInt64_DenominatorOne` — quotient equals numerator cast to long
- [ ] `DivideToInt64_DenominatorMinusOne` — quotient equals negated numerator
- [ ] `DivideToInt64_RoundingHalfUp` — tie-break case, e.g. `5 / 2 = 3` (half-away-from-zero)
- [ ] `DivideToInt64_LargeValues` — numerator ~2¹⁶³, denominator ~2¹¹⁰, quotient ~2⁵³ — cross-check against `System.Numerics.BigInteger`
- [ ] `DivideToInt64_KnownIntersection` — construct two non-degenerate segments with a known integer intersection point; verify `IntersectionPosition` (once wired in Phase 3A) returns exactly that point

---

## Phase 3A — CdtUtils.cs *(do first; Triangulation depends on it)*

### Step 1: De-genericize predicate wrappers

- `Orient2D<T>(V2d<T>, V2d<T>, V2d<T>)` → `Orient2D(V2i, V2i, V2i)` returning `int` (+1/0/−1) via `PredicatesInt.Orient2dInt`
- `IsInCircumcircle<T>(V2d<T>, V2d<T>, V2d<T>, V2d<T>)` → `IsInCircumcircle(V2i, V2i, V2i, V2i)` returning `bool` via `PredicatesInt.InCircleInt`
- Add `Orient2DRaw(V2i, V2i, V2i)` → `Int128` (needed by `IntersectionPosition`)

### Step 2: Replace distance methods

- `DistanceSquared<T>` → `DistanceSquared(V2i a, V2i b)` returning `Int128`
  (dx and dy are `long`; products fit in `Int128`)
- **Delete** `Distance<T>()` entirely — no sqrt in integer world
- Remove `IRootFunctions<T>` constraint from everything

### Step 3: ClassifyOrientation / LocatePointLine / LocatePointTriangle

Integer predicates are exact so the default tolerance is zero. The existing `_minDistToConstraintEdge` mechanism is replaced by `_snapTolerance: long` — an absolute threshold on `|Orient2dRaw(p, v1, v2)|`.

**Semantics of `_snapTolerance`:** `Orient2dRaw` returns `length(v1v2) × perpendicular_distance(p, line(v1,v2))` in coordinate-units². Specifically, for a point at perpendicular distance `d` from an edge of length `L`, `|Orient2dRaw| = L × d`. A threshold `snapTolerance` therefore snaps any point within `d ≤ snapTolerance / L` coordinate units of the edge. This is an **absolute area-units value**, not a dimensionless ratio.

**Migration from the old API:** The old `minDistToConstraintEdge = f` produced `distTol = f × Length(edge)`, and the check was `|Orient2d| ≤ f × L`. Since `|Orient2d| = L × d`, this snapped points within `d ≤ f` coordinate units. To replicate: `snapTolerance = round(f × typical_edge_length_in_integer_units)`. Document this clearly in the constructor XML comment so callers are not confused by the unit change.

- `ClassifyOrientation(int sign)` — exact: negative → right, zero → on, positive → left. No tolerance parameter for the default path.
- `LocatePointLine(V2i p, V2i v1, V2i v2, long areaSnapTolerance = 0)` → `PtLineLocation`
- `LocatePointTriangle(V2i p, V2i v1, V2i v2, V2i v3)` → `PtTriLocation`

### Step 4: IntersectionPosition (integer, Int256 intermediates)

The float algorithm carried a "pick the axis with the larger span" heuristic to reduce catastrophic cancellation. In exact integer arithmetic both parameterizations produce the same rational value, so **drop the heuristic**. Always parameterise along the a–b segment:

1. `acd = Orient2DRaw(a, c, d)` and `bcd = Orient2DRaw(b, c, d)` as `Int128`
2. `denom = acd - bcd` (Int128)
3. `numX = Int256.Multiply(acd, (Int128)(b.X − a.X))` → `Int256`
4. `numY = Int256.Multiply(acd, (Int128)(b.Y − a.Y))` → `Int256`
5. `x = a.X + Int256.DivideToInt64(numX, denom)` (round-half-away-from-zero)
6. `y = a.Y + Int256.DivideToInt64(numY, denom)`
7. Return `new V2i(x, y)` — at most ±1 unit of quantisation error per coordinate

### Step 5: FindDuplicates / RemoveDuplicates

Mechanical type swap. Already uses exact equality (tuple key) so logic is unchanged:

- `FindDuplicates<T>(IReadOnlyList<V2d<T>>)` → `FindDuplicates(IReadOnlyList<V2i>)`
- `RemoveDuplicates<T>` → `RemoveDuplicates(List<V2i>, ...)`
- `RemoveDuplicatesAndRemapEdges<T>` → `RemoveDuplicatesAndRemapEdges(List<V2i>, List<Edge>)`

**Files**: `src/CDT.Core/CdtUtils.cs`  
**Key references**: `PredicatesInt.Orient2dInt`, `PredicatesInt.Orient2dRaw`, `PredicatesInt.InCircleInt`, `Int256.Multiply`

---

## Phase 3B — Triangulation.cs *(depends on 3A)*

### Step 6: Remove generic parameter

- `Triangulation<T> where T : unmanaged, IFloatingPoint<T>, IMinMaxValue<T>, IRootFunctions<T>` → `Triangulation` (no generics)
- All `V2d<T>` → `V2i`
- All `Box2d<T>` → `Box2i`
- `KdTree<T>?` → `KdTree?` (concrete integer KdTree)
- Remove `_two` cached field (use literal `2L`)
- `ReadOnlyMemory<V2d<T>>` Vertices property → `ReadOnlyMemory<V2i>`

### Step 7: Super-triangle construction (integer, no floating-point trig)

**As-built implementation** (differs from the axis-aligned spec below — see note):

```csharp
long r = Math.Max(Math.Max(w, h), 1L) * 2L;   // generous radius
long R = 2L * r;
long shiftX = R * 866L / 1000L + 1L;          // cos30 ≈ 866/1000, +1 guarantees containment

var v0 = new V2i(cx - shiftX, cy - r);
var v1 = new V2i(cx + shiftX, cy - r);
var v2 = new V2i(cx,           cy + R);
```

This mirrors the floating-point CDT C++ super-triangle more closely, producing identical combinatorial topology for matching inputs. The original spec below was written before the ground-truth tests were locked to the cos30 output; changing to the axis-aligned form would require regenerating all expected files.

**Original axis-aligned spec (preserved for reference):**

```
pad = max(w, h) + 1
v0  = (minX − pad,        minY − 1)
v1  = (maxX + pad,        minY − 1)
v2  = ((minX + maxX) / 2, maxY + pad)
```

**Overflow proof (as-built):** With `|coord| ≤ MaxCoordinate = 2^53`, `w, h ≤ 2^54`, `r ≤ 2^55`, `R ≤ 2^56`, `shiftX ≤ 2^56`. Super-triangle vertex coordinates are bounded by `2^53 + 2^56 < 2^57`, well within `long.MaxValue ≈ 2^63`. A `Debug.Assert` at construction time verifies this.

**Containment:** For any input point inside the bounding box, all three orientation tests are strictly positive by the generous margin (`r ≥ max(w,h)` with `R = 2r`).

Remove the `cos30` constant.

### Step 8: Replace `_minDistToConstraintEdge` with `_snapTolerance: long`

| | Before | After |
|--|--------|-------|
| Field type | `T _minDistToConstraintEdge` | `long _snapTolerance` |
| Usage (lines 875, 1028) | `distTol = _minDistToConstraintEdge × Distance(a, b)` | eliminates `Distance` call entirely |
| Usage (lines 909, 1056) | `LocatePointLine(p, v1, v2, distTol)` | `LocatePointLine(p, v1, v2, _snapTolerance)` |
| Constructor | `T minDistToConstraintEdge` param | `long snapTolerance = 0` param |

The constructor XML doc comment **must** explain the unit change (see Step 3 for the conversion formula). Callers passing zero get the same zero-tolerance behaviour as before. Default is zero.

### Step 9: Wire integer predicates throughout

| Location | Change |
|----------|--------|
| Lines 830, 1208 — `IsInCircumcircle` | → integer version (Step 1) |
| Lines 1232, 1238 — `Orient2D` | → integer version (Step 1) |
| Lines 992, 1109 — `IntersectionPosition` | → integer version (Step 4) |
| Lines 875, 1028 — `Distance(a, b)` | eliminate; replaced by `_snapTolerance` |
| Line 1154 — midpoint | `new V2i(a.X + (b.X − a.X) / 2, a.Y + (b.Y − a.Y) / 2)` (overflow-safe) |
| Line 1557 — KdTree construction | `new KdTree(...)` (concrete long version) |
| Lines 1559, 1565 — KdTree Insert | `V2i` signature |
| Lines 436, 445, 556 — KdTree Nearest | `long qx, long qy` signature |

### Step 10: Public API update

- `InsertVertices(IReadOnlyList<V2d<T>>)` → `InsertVertices(IReadOnlyList<V2i>)`
- All read-only properties: `V2d<T>` → `V2i`
- `InsertEdges`, `ConformToEdges` — unchanged (Edge is not generic)

**Coordinate range validation:** Add an input guard in `InsertVertices`:
```csharp
foreach (var v in vertices)
    if ((ulong)(v.X + PredicatesInt.MaxCoordinate) > (ulong)(2 * PredicatesInt.MaxCoordinate) ||
        (ulong)(v.Y + PredicatesInt.MaxCoordinate) > (ulong)(2 * PredicatesInt.MaxCoordinate))
        throw new ArgumentOutOfRangeException(nameof(vertices),
            $"Vertex ({v.X},{v.Y}) exceeds MaxCoordinate = {PredicatesInt.MaxCoordinate}. " +
            "Integer predicates produce incorrect results outside this range.");
```
Fail loudly. Silent wrong answers are not acceptable.

**Files**: `src/CDT.Core/Triangulation.cs`

---

## Phase 3C — TopologyVerifier.cs *(parallel with 3B)*

### Step 11: De-genericize TopologyVerifier

- `VerifyTopology<T>()` → `VerifyTopology()` operating on `V2i[]` and `Triangle[]`
- Remove all `IFloatingPoint<T>`, `IMinMaxValue<T>`, `IRootFunctions<T>` constraints
- No predicate calls inside — only topology checks. Purely mechanical type swap.

**Files**: `src/CDT.Core/TopologyVerifier.cs`

---

## Phase 3D — Test Infrastructure *(parallel with 3B/3C once 3A compiles)*

### Step 12: Integer test input files

Write a one-time converter: read float inputs → multiply by a scale factor → round to `long` → write integer input files into a new `inputs_int/` directory (or overwrite `inputs/`). Many existing inputs already use integer-valued coordinates.

**Recommended scale factor**: ×1,000,000 (preserves ~6 decimal digits of precision from original float data).

**Constraint:** `max(|original_coordinate|) × scale_factor ≤ MaxCoordinate = 2^53 ≈ 9.0 × 10^15`. With scale = 10^6 this permits original coordinates up to ~9.0 × 10^9. The converter must assert this bound for every converted vertex and abort with a clear error if violated.

### Step 13: TestInputReader for V2i

Add `ReadInputInt(string path)` → `(List<V2i>, List<Edge>)` parsing long coordinates. Alternatively extend the existing reader to branch on type.

### Step 14: Verify and regenerate ground-truth expected outputs

Do **not** simply run the new integer code, accept its output, and call that ground truth. That deletes the tests.

**Correct protocol:**
1. Convert float inputs to integer via Step 12 converter.
2. Run both the **old float** triangulation (on original inputs) and the **new integer** triangulation (on converted inputs) for every test case.
3. Compare topologies — vertex connectivity and neighbour indices should be **identical** for all non-degenerate inputs. Topology is combinatorial; it is unaffected by coordinate scaling.
4. For any test case where the topologies differ: investigate. Classify as either (a) a legitimate rounding artifact at a near-degenerate configuration, or (b) a bug in the integer implementation. Only (a) cases may be accepted as new expected output — and only after manual geometric verification.
5. Add a separate correctness check: for every vertex inserted by `IntersectionPosition`, verify `Orient2dInt(v, edge_v0, edge_v1) == 0` — it must lie exactly on the constraint edge.

Expect the topologies to match on all existing test inputs, since none were deliberately designed to be degenerate for float arithmetic.

### Step 15: Update test classes

| Before | After |
|--------|-------|
| `TriangulationTestsBase<T>` + `_Double` / `_Float` subclasses | `TriangulationTests` (concrete, no generics) |
| `GroundTruthTestsBase<T>` + `_Double` / `_Float` subclasses | `GroundTruthTests` (concrete) |
| `ReadmeExamplesTests` (`Triangulation<double>`) | `Triangulation` with `V2i` coordinates |
| `TopologyVerifier` calls with type arguments | Remove type arguments |
| `PredicatesTests` | Keep until Batch 4 |

**Files**:
- `test/CDT.Tests/GroundTruthTests.cs`
- `test/CDT.Tests/TriangulationTests.cs`
- `test/CDT.Tests/ReadmeExamplesTests.cs`
- `test/CDT.Tests/inputs/*` — convert to integer
- `test/CDT.Tests/expected/*` — regenerate

---

## Phase 4 — Delete Dead Code *(after all Batch 3 tests green)*

### Step 16: Delete float predicate files and their tests

Delete all three in the same commit — the project does not compile between them:

- Delete `src/CDT.Core/Predicates/PredicatesAdaptive.cs`
- Delete `src/CDT.Core/Predicates/PredicatesExact.cs`
- Delete `test/CDT.Tests/PredicatesTests.cs` *(moved here from Step 19 — must be concurrent with above)*

### Step 17: Remove dead generic types from Types.cs

- Delete `V2d<T>` struct
- Delete `Box2d<T>` struct
- Remove `IFloatingPoint<T>`, `IMinMaxValue<T>`, `IRootFunctions<T>` from any remaining `using` statements

### Step 18: Remove dead generic KdTree

- Delete the generic `KdTree<T>` class from `KdTree.cs` (keep only the concrete `KdTree` for `long`)

### Step 19: Remove remaining float test class variants

- Remove any remaining `_Double` / `_Float` test class variants
- `PredicatesTests.cs` was already deleted in Step 16

### Step 20: Final verification

```
dotnet build src/CDT.Core          # zero warnings
dotnet test test/CDT.Tests         # all green
```

Grep CDT.Core for the following — all must return zero hits:

```
float|double|IFloatingPoint|IRootFunctions|System\.Runtime\.Intrinsics
```

**Files deleted**:
- `src/CDT.Core/Predicates/PredicatesAdaptive.cs`
- `src/CDT.Core/Predicates/PredicatesExact.cs`
- `test/CDT.Tests/PredicatesTests.cs`

**Files modified**:
- `src/CDT.Core/Types.cs` (remove `V2d<T>`, `Box2d<T>`)
- `src/CDT.Core/KdTree.cs` (remove generic `KdTree<T>`)

---

## Dependency Graph (phases)

```
3A: CdtUtils.cs
      │
      ├─────────────────────────────────────┐
      │                                     │
3B: Triangulation.cs               3C: TopologyVerifier.cs
      │                                     │
      └──────────────┬──────────────────────┘
                     │
              3D: Tests compile
                     │
              3D: Tests green
                     │
              Phase 4: Delete dead code
```

---

## Key Decisions

1. **Snap tolerance semantics**: `_snapTolerance: long` is an area-units threshold (units of `Orient2dRaw`), not a distance. This avoids sqrt entirely. Users set it based on their coordinate scale.
2. **IntersectionPosition snapping**: The rational result is rounded to nearest `long` via sign-aware integer division. At most ±1 unit of quantisation error.
3. **Super-triangle**: Axis-aligned oversized triangle — no trig, no floats. Slightly less symmetric than equilateral but functionally equivalent for the algorithm.
4. **Test data**: Convert existing float inputs to scaled integers; preserves test coverage without writing new inputs from scratch.
5. **Unity .NET polyfill**: Unity does not ship .NET 8 BCL until Unity 6.8 (~1 year away). CDT.Core ships a minimal `Int128` polyfill (~180 lines) gated behind `#if !NET7_0_OR_GREATER`. `UInt128` is avoided entirely by rewriting `Int256.MultiplyUnsigned128` with 32-bit word splitting. Zero cost on .NET 8+.
6. **Burst (aspirational)**: No managed references in hot-path structs. `V2i`, `Box2i`, `Int256`, `Triangle`, `Edge` are all `unmanaged`. Virtual dispatch is absent from the core algorithm. `[MethodImpl(AggressiveInlining)]` preserved where already present.

---

## Open Questions

1. **Coordinate scale for test data**: ×1,000,000 recommended. Consider ×1,000 if input precision does not require 6 decimal places.
2. **Backward compatibility**: This plan is a clean break — all float types deleted in Batch 4. If external consumers need the old API, a compatibility branch or separate package version should be discussed.
3. **Benchmark project** (`CDT.Benchmarks`): Update to new integer API or defer?
4. **Viz project** (`CDT.Viz`): Update to new integer API or defer?
5. ~~**`Int128` availability in Unity**~~: **Resolved** — Unity 6.8 ships BCL, but it is ~1 year away. Polyfill is required now. See `PLAN-DOTNET-COMPAT.md` and the Int128 Polyfill prerequisite above.
