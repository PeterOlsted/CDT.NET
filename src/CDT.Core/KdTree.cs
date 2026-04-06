// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// Contribution of original implementation:
// Andre Fecteau <andre.fecteau1@gmail.com>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CDT.Predicates;

namespace CDT;

/// <summary>
/// Incrementally-built 2D KD-tree for nearest-point queries using exact
/// 64-bit integer coordinates. Replaces the generic KdTree&lt;T&gt; for the
/// deterministic integer triangulation path.
/// Distance comparisons use <see cref="Int128"/> squared distances —
/// no square root, no floating-point.
/// </summary>
internal sealed class KdTree
{
    private const int NumVerticesInLeaf = 32;
    private const int InitialStackDepth = 64;
    private const long Two = 2L;

    private enum SplitDir { X, Y }

    private struct Node
    {
        public int Child0, Child1;
        public List<int>? Data;
        public bool IsLeaf => Child0 == Child1;
        public static Node NewLeaf() => new() { Child0 = 0, Child1 = 0, Data = new List<int>(NumVerticesInLeaf) };
    }

    private readonly struct NearestTask
    {
        public readonly int NodeIndex;
        public readonly long MinX, MinY, MaxX, MaxY;
        public readonly SplitDir Dir;
        public readonly Int128 DistSq;

        public NearestTask(int node, long minX, long minY, long maxX, long maxY, SplitDir dir, Int128 distSq)
        {
            NodeIndex = node; MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
            Dir = dir; DistSq = distSq;
        }
    }

    private readonly List<Node> _nodes = new(64);
    private int _root;
    private SplitDir _rootDir = SplitDir.X;

    private long _minX, _minY, _maxX, _maxY;
    private bool _boxInitialized;
    private int _size;

    private NearestTask[] _stack = new NearestTask[InitialStackDepth];

    /// <summary>Initializes an empty KD-tree; box is determined lazily from data.</summary>
    public KdTree()
    {
        // Sentinel bounds: any long coordinate passes IsInsideBox until the real
        // box is set by InitializeRootBox after the first leaf fills.
        _minX = long.MinValue; _minY = long.MinValue;
        _maxX = long.MaxValue; _maxY = long.MaxValue;
        _root = AddNewNode();
        _boxInitialized = false;
    }

    /// <summary>Initializes an empty KD-tree with a known bounding box.</summary>
    public KdTree(long minX, long minY, long maxX, long maxY)
    {
        _minX = minX; _minY = minY; _maxX = maxX; _maxY = maxY;
        _root = AddNewNode();
        _boxInitialized = true;
    }

    /// <summary>Number of points stored.</summary>
    public int Size => _size;

    /// <summary>Inserts point at index <paramref name="iPoint"/> from the external buffer.</summary>
    public void Insert(int iPoint, IReadOnlyList<V2i> points)
    {
        _size++;
        long px = points[iPoint].X, py = points[iPoint].Y;

        while (!IsInsideBox(px, py, _minX, _minY, _maxX, _maxY))
            ExtendTree(px, py);

        int node = _root;
        long minX = _minX, minY = _minY, maxX = _maxX, maxY = _maxY;
        SplitDir dir = _rootDir;

        while (true)
        {
            ref Node n = ref CollectionsMarshal.AsSpan(_nodes)[node];
            if (n.IsLeaf)
            {
                if (n.Data!.Count < NumVerticesInLeaf)
                {
                    n.Data.Add(iPoint);
                    return;
                }

                // Lazy box init: only needed when we first have to split a leaf.
                if (!_boxInitialized)
                {
                    InitializeRootBox(points);
                    minX = _minX; minY = _minY; maxX = _maxX; maxY = _maxY;
                }

                long mid = GetMid(minX, minY, maxX, maxY, dir);
                int c1 = AddNewNode(), c2 = AddNewNode();
                n = ref CollectionsMarshal.AsSpan(_nodes)[node]; // re-acquire after resize
                n.Child0 = c1; n.Child1 = c2;

                foreach (int ip in n.Data!)
                {
                    long cx = points[ip].X, cy = points[ip].Y;
                    _nodes[WhichChild(cx, cy, mid, dir) == 0 ? c1 : c2].Data!.Add(ip);
                }
                n.Data = null;
            }

            long midVal = GetMid(minX, minY, maxX, maxY, dir);
            int childIdx = WhichChild(px, py, midVal, dir);
            if (dir == SplitDir.X) { if (childIdx == 0) maxX = midVal; else minX = midVal; }
            else                   { if (childIdx == 0) maxY = midVal; else minY = midVal; }

            node = childIdx == 0 ? _nodes[node].Child0 : _nodes[node].Child1;
            dir = dir == SplitDir.X ? SplitDir.Y : SplitDir.X;
        }
    }

    /// <summary>Finds the nearest point to (<paramref name="qx"/>, <paramref name="qy"/>) in the external buffer.</summary>
    public int Nearest(long qx, long qy, IReadOnlyList<V2i> points)
    {
        int resultIdx = 0;
        Int128 minDistSq = Int128.MaxValue;
        int stackTop = -1;

        Int128 rootDistSq = DistSqToBox(qx, qy, _minX, _minY, _maxX, _maxY);
        PushStack(ref stackTop, new NearestTask(_root, _minX, _minY, _maxX, _maxY, _rootDir, rootDistSq));

        while (stackTop >= 0)
        {
            NearestTask task = _stack[stackTop--];
            if (task.DistSq > minDistSq) continue;

            ref Node n = ref CollectionsMarshal.AsSpan(_nodes)[task.NodeIndex];
            if (n.IsLeaf)
            {
                foreach (int ip in n.Data!)
                {
                    long dx = points[ip].X - qx;
                    long dy = points[ip].Y - qy;
                    Int128 d2 = (Int128)dx * dx + (Int128)dy * dy;
                    if (d2 < minDistSq) { minDistSq = d2; resultIdx = ip; }
                }
            }
            else
            {
                long mid = GetMid(task.MinX, task.MinY, task.MaxX, task.MaxY, task.Dir);
                SplitDir childDir = task.Dir == SplitDir.X ? SplitDir.Y : SplitDir.X;
                bool afterSplit = IsAfterSplit(qx, qy, mid, task.Dir);
                Int128 dSqFarther = FartherBoxDistSq(qx, qy, task.MinX, task.MinY, task.MaxX, task.MaxY, mid, task.Dir);

                if (stackTop + 2 >= _stack.Length)
                    Array.Resize(ref _stack, _stack.Length + InitialStackDepth);

                if (afterSplit)
                {
                    long fMinX = task.MinX, fMinY = task.MinY, fMaxX = task.MaxX, fMaxY = task.MaxY;
                    long cMinX = task.MinX, cMinY = task.MinY, cMaxX = task.MaxX, cMaxY = task.MaxY;
                    if (task.Dir == SplitDir.X) { fMaxX = mid; cMinX = mid; }
                    else                        { fMaxY = mid; cMinY = mid; }
                    if (dSqFarther <= minDistSq)
                        PushStack(ref stackTop, new NearestTask(n.Child0, fMinX, fMinY, fMaxX, fMaxY, childDir, dSqFarther));
                    PushStack(ref stackTop, new NearestTask(n.Child1, cMinX, cMinY, cMaxX, cMaxY, childDir, task.DistSq));
                }
                else
                {
                    long cMinX = task.MinX, cMinY = task.MinY, cMaxX = task.MaxX, cMaxY = task.MaxY;
                    long fMinX = task.MinX, fMinY = task.MinY, fMaxX = task.MaxX, fMaxY = task.MaxY;
                    if (task.Dir == SplitDir.X) { cMaxX = mid; fMinX = mid; }
                    else                        { cMaxY = mid; fMinY = mid; }
                    if (dSqFarther <= minDistSq)
                        PushStack(ref stackTop, new NearestTask(n.Child1, fMinX, fMinY, fMaxX, fMaxY, childDir, dSqFarther));
                    PushStack(ref stackTop, new NearestTask(n.Child0, cMinX, cMinY, cMaxX, cMaxY, childDir, task.DistSq));
                }
            }
        }
        return resultIdx;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PushStack(ref int top, NearestTask task) => _stack[++top] = task;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInsideBox(long px, long py, long minX, long minY, long maxX, long maxY)
        => px >= minX && px <= maxX && py >= minY && py <= maxY;

    /// <summary>
    /// Returns the midpoint of the interval [a, b] without overflow.
    /// Uses <c>a + (b - a) / 2</c> — correct for the full long range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long GetMid(long minX, long minY, long maxX, long maxY, SplitDir dir)
        => dir == SplitDir.X
            ? minX + (maxX - minX) / Two
            : minY + (maxY - minY) / Two;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAfterSplit(long px, long py, long split, SplitDir dir)
        => dir == SplitDir.X ? px > split : py > split;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WhichChild(long px, long py, long split, SplitDir dir)
        => IsAfterSplit(px, py, split, dir) ? 1 : 0;

    /// <summary>Returns squared distance from point to box; zero if inside.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Int128 DistSqToBox(long px, long py, long minX, long minY, long maxX, long maxY)
    {
        long dx = Math.Max(Math.Max(minX - px, 0L), px - maxX);
        long dy = Math.Max(Math.Max(minY - py, 0L), py - maxY);
        return (Int128)dx * dx + (Int128)dy * dy;
    }

    private static Int128 FartherBoxDistSq(
        long px, long py, long minX, long minY, long maxX, long maxY, long mid, SplitDir dir)
    {
        if (dir == SplitDir.X)
        {
            long dx = px - mid;
            long dy = Math.Max(Math.Max(minY - py, 0L), py - maxY);
            return (Int128)dx * dx + (Int128)dy * dy;
        }
        else
        {
            long dx = Math.Max(Math.Max(minX - px, 0L), px - maxX);
            long dy = py - mid;
            return (Int128)dx * dx + (Int128)dy * dy;
        }
    }

    private int AddNewNode()
    {
        int idx = _nodes.Count;
        _nodes.Add(Node.NewLeaf());
        return idx;
    }

    private void ExtendTree(long px, long py)
    {
        int newRoot = AddNewNode();
        int newLeaf = AddNewNode();
        ref Node nr = ref CollectionsMarshal.AsSpan(_nodes)[newRoot];

        switch (_rootDir)
        {
            case SplitDir.X:
                _rootDir = SplitDir.Y;
                if (py < _minY) { _minY -= _maxY - _minY; nr.Child0 = newLeaf; nr.Child1 = _root; }
                else            { _maxY += _maxY - _minY; nr.Child0 = _root;   nr.Child1 = newLeaf; }
                break;
            case SplitDir.Y:
                _rootDir = SplitDir.X;
                if (px < _minX) { _minX -= _maxX - _minX; nr.Child0 = newLeaf; nr.Child1 = _root; }
                else            { _maxX += _maxX - _minX; nr.Child0 = _root;   nr.Child1 = newLeaf; }
                break;
        }
        _root = newRoot;
    }

    private void InitializeRootBox(IReadOnlyList<V2i> points)
    {
        Node rootNode = _nodes[_root];
        long mxn = points[rootNode.Data![0]].X, myn = points[rootNode.Data[0]].Y;
        long mxx = mxn, mxy = myn;
        foreach (int ip in rootNode.Data)
        {
            long cx = points[ip].X, cy = points[ip].Y;
            if (cx < mxn) mxn = cx; if (cx > mxx) mxx = cx;
            if (cy < myn) myn = cy; if (cy > mxy) mxy = cy;
        }

        const long Padding = 1L;
        if (mxn == mxx) { mxn -= Padding; mxx += Padding; }
        if (myn == mxy) { myn -= Padding; mxy += Padding; }

        _minX = mxn; _minY = myn; _maxX = mxx; _maxY = mxy;
        _boxInitialized = true;
    }
}
