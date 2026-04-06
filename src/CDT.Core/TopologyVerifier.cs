// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT;

/// <summary>
/// Topology verification utilities for debugging and testing.
/// </summary>
public static partial class TopologyVerifier
{
    /// <summary>
    /// Verifies the topological consistency of a <see cref="Triangulation"/> (integer).
    /// Checks neighbor relationships and shared-edge consistency.
    /// </summary>
    /// <returns>true if topology is valid.</returns>
    public static bool VerifyTopology(Triangulation cdt)
        => VerifyTopology(cdt.Triangles.Span, cdt.Vertices.Span);

    /// <summary>
    /// Verifies the topological consistency of raw triangle and vertex arrays.
    /// Exposed internally for unit-testing error branches with hand-crafted bad topology.
    /// </summary>
    internal static bool VerifyTopology(ReadOnlySpan<Triangle> triangles, ReadOnlySpan<V2i> vertices)
    {
        for (int iT = 0; iT < triangles.Length; iT++)
        {
            var t = triangles[iT];
            if (t.V0 == Indices.NoVertex || t.V1 == Indices.NoVertex || t.V2 == Indices.NoVertex)
                return false;
            if (t.V0 >= vertices.Length || t.V1 >= vertices.Length || t.V2 >= vertices.Length)
                return false;
            if (t.V0 == t.V1 || t.V1 == t.V2 || t.V0 == t.V2)
                return false;

            for (int i = 0; i < 3; i++)
            {
                int iN = t.GetNeighbor(i);
                if (iN == Indices.NoNeighbor) continue;
                if (iN >= triangles.Length) return false;
                var tN = triangles[iN];
                if (tN.N0 != iT && tN.N1 != iT && tN.N2 != iT)
                    return false;
                int va = t.GetVertex(i);
                int vb = t.GetVertex(CdtUtils.Ccw(i));
                if (!tN.ContainsVertex(va) || !tN.ContainsVertex(vb))
                    return false;
            }
        }
        return true;
    }
}

