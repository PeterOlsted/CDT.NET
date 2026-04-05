# .NET Compatibility Analysis

## Question
Can the integer predicate layer (`Int128`, `UInt128`) run on .NET Standard 2.1 or .NET 5?

## Short Answer
No — and it matters. Unity does not ship the .NET 8 BCL until Unity 6.8, which is approximately a year away. `System.Int128` and `System.UInt128` are unavailable on the current Unity runtime. A polyfill is required.

## What Requires .NET 7+

`Int128` and `UInt128` were introduced in .NET 7. Every usage in CDT.Core depends on them:

| Type | Used in | Count |
|------|---------|-------|
| `System.Int128` | `PredicatesInt.cs` — all intermediates and return type of `Orient2dRaw` | ~30 |
| `System.Int128` | `KdTree.cs` — squared-distance storage and comparisons | ~8 |
| `System.Int128` | `Int256.cs` — signature of `Multiply`, `FromInt128` | ~6 |
| `System.UInt128` | `Int256.cs` — internal to `MultiplyUnsigned128` only | ~12 |

Everything else in CDT.Core is compatible with .NET Standard 2.1+:
- `ReadOnlySpan<T>`, `ReadOnlyMemory<T>` — .NET Standard 2.1+
- `HashSet<T>`, `Dictionary<T>` — any target
- `MethodImpl(AggressiveInlining)` — any target
- `System.Runtime.Intrinsics` — only in `PredicatesAdaptive.cs`, which is deleted in Batch 4

## The Polyfill (Required)

### Int128 polyfill

A minimal `Int128` polyfill covering only the operations actually used in CDT.Core:

**Operators required** (from audit of `PredicatesInt.cs`, `KdTree.cs`, `Int256.cs`):
- Arithmetic: `+`, `-`, `*`, unary `-`
- Comparison: `<`, `>`, `<=`, `>=`, `==`, `!=`, `CompareTo`
- Constants: `Zero`, `MaxValue`, `MinValue`
- Casts: implicit from `long`, explicit to `ulong`, explicit to `long`
- Bitwise: `>>` (right shift, arithmetic), used in `Int256.FromInt128`

Estimated size: ~180 lines. Two `ulong` fields (`_lo`, `_hi`), two's complement throughout.

### UInt128 polyfill

`UInt128` is used only inside `Int256.MultiplyUnsigned128`. The alternative — **rewrite `MultiplyUnsigned128` to avoid `UInt128`** — is simpler:

Replace the four `UInt128` partial products with explicit `ulong` 32-bit splitting:
- Split each `ulong` half of each operand into two 32-bit words
- Accumulate 16 partial products of `ulong × ulong` (each fits in `ulong`)
- Propagate carries through four result limbs

This is the standard schoolbook 4×4 approach, ~40 lines, zero managed types, Burst-compatible. No `UInt128` polyfill required.

### Naming

Place the polyfill in `src/CDT.Core/Predicates/Int128Polyfill.cs`, namespace `CDT.Predicates`, gated behind:

```csharp
#if !NET7_0_OR_GREATER
internal readonly struct Int128 { ... }
#endif
```

The fields `_lo` and `_hi` are `internal` so `Int256.FromInt128` can read them directly without a shift operator.

This means the polyfill is dead code on .NET 7+ and gets stripped by the compiler. No runtime cost on Unity 6.8+.

### What the polyfill does NOT need

The BCL `Int128` has `ToString`, `Parse`, `IFormattable`, `ISpanParsable`, `IBinaryInteger<Int128>`, and a pile of other interfaces. The CDT.Core polyfill needs none of this. Only implement what the audit above lists. Every extra method is a liability.

## Decision

Implement the `Int128` polyfill (180 lines) and rewrite `MultiplyUnsigned128` to avoid `UInt128`. Gate the polyfill behind `#if !NET7_0_OR_GREATER`. This unblocks CDT.Core on the current Unity runtime while being zero-cost on Unity 6.8+ when it ships BCL support.

Target `net8.0`/`net10.0` for the standalone test/benchmark build (already configured). Add `net5.0` to `<TargetFrameworks>` in `CDT.Core.csproj` for the Unity package build. The `#if !NET7_0_OR_GREATER` gate fires on `net5.0` and is silent on `net8.0`+.

## Burst Caveat

`System.Int128` (BCL or polyfill) is not on Burst's approved type list. If Burst compilation of the integer predicate hot path is ever required:

1. Write a `BurstInt128` struct (two `ulong` fields, only the operations Burst needs) at that point.
2. Gate it behind `#if BURST` or a separate Burst-specific code path.
3. This is deferred work — Burst support is aspirational, not committed.

`Int256` is already a custom `unmanaged` struct with no `Int128` in its internal representation (four `ulong` limbs). Once `MultiplyUnsigned128` is rewritten to avoid `UInt128` (see above), `Int256` itself will be Burst-compatible. Only the public `Multiply(Int128, Int128)` entry point references `Int128`.
