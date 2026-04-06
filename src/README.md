# src/

This folder contains `UnityCDT.csproj` — a .NET build wrapper used for running
tests, benchmarks, and publishing to NuGet.

The actual source files live in [`../unity-package/Runtime/`](../unity-package/Runtime/).
The `.csproj` compiles those files directly so there is a single source of truth.
