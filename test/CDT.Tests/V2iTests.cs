// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace CDT.Tests;

/// <summary>Tests for <see cref="V2i"/> and <see cref="Box2i"/>.</summary>
public sealed class V2iTests
{
    // -------------------------------------------------------------------------
    // V2i construction and equality
    // -------------------------------------------------------------------------

    [Fact]
    public void V2i_CoordinatesAreStored()
    {
        var p = new V2i(3L, -7L);
        Assert.Equal(3L, p.X);
        Assert.Equal(-7L, p.Y);
    }

    [Fact]
    public void V2i_Zero_IsBothZero()
    {
        Assert.Equal(0L, V2i.Zero.X);
        Assert.Equal(0L, V2i.Zero.Y);
    }

    [Fact]
    public void V2i_Equality_SameCoords_AreEqual()
    {
        var a = new V2i(1L, 2L);
        var b = new V2i(1L, 2L);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void V2i_Equality_DifferentX_NotEqual()
    {
        Assert.NotEqual(new V2i(1L, 0L), new V2i(2L, 0L));
    }

    [Fact]
    public void V2i_Equality_DifferentY_NotEqual()
    {
        Assert.NotEqual(new V2i(0L, 1L), new V2i(0L, 2L));
    }

    [Fact]
    public void V2i_HashCode_EqualPointsHaveSameHash()
    {
        var a = new V2i(42L, -99L);
        var b = new V2i(42L, -99L);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void V2i_UsableAsHashSetKey()
    {
        var set = new HashSet<V2i> { new V2i(1L, 2L), new V2i(3L, 4L) };
        Assert.Contains(new V2i(1L, 2L), set);
        Assert.DoesNotContain(new V2i(5L, 6L), set);
    }

    [Fact]
    public void V2i_EqualsObject_NullReturnsFalse()
    {
        var p = new V2i(1L, 2L);
        Assert.False(p.Equals(null));
    }

    [Fact]
    public void V2i_ExtremeValues_Stored()
    {
        var p = new V2i(long.MaxValue, long.MinValue);
        Assert.Equal(long.MaxValue, p.X);
        Assert.Equal(long.MinValue, p.Y);
    }

    [Fact]
    public void V2i_ToString_ContainsCoordinates()
    {
        var p = new V2i(10L, -20L);
        var s = p.ToString();
        Assert.Contains("10", s);
        Assert.Contains("-20", s);
    }

    // -------------------------------------------------------------------------
    // Box2i construction
    // -------------------------------------------------------------------------

    [Fact]
    public void Box2i_DefaultConstructor_SentinelValues()
    {
        var box = new Box2i();
        Assert.Equal(long.MaxValue, box.Min.X);
        Assert.Equal(long.MaxValue, box.Min.Y);
        Assert.Equal(long.MinValue, box.Max.X);
        Assert.Equal(long.MinValue, box.Max.Y);
    }

    // -------------------------------------------------------------------------
    // Box2i.Envelop (scalar)
    // -------------------------------------------------------------------------

    [Fact]
    public void Box2i_Envelop_FirstPoint_SetsMinAndMax()
    {
        var box = new Box2i();
        box.Envelop(5L, -3L);
        Assert.Equal(new V2i(5L, -3L), box.Min);
        Assert.Equal(new V2i(5L, -3L), box.Max);
    }

    [Fact]
    public void Box2i_Envelop_ExpandsMinCorrectly()
    {
        var box = new Box2i();
        box.Envelop(10L, 10L);
        box.Envelop(2L, 15L);   // X shrinks Min, Y expands Max
        Assert.Equal(2L, box.Min.X);
        Assert.Equal(10L, box.Min.Y);
        Assert.Equal(10L, box.Max.X);
        Assert.Equal(15L, box.Max.Y);
    }

    [Fact]
    public void Box2i_Envelop_ExpandsMaxCorrectly()
    {
        var box = new Box2i();
        box.Envelop(0L, 0L);
        box.Envelop(100L, 200L);
        Assert.Equal(0L, box.Min.X);
        Assert.Equal(0L, box.Min.Y);
        Assert.Equal(100L, box.Max.X);
        Assert.Equal(200L, box.Max.Y);
    }

    [Fact]
    public void Box2i_Envelop_SamePointTwice_Unchanged()
    {
        var box = new Box2i();
        box.Envelop(7L, 7L);
        box.Envelop(7L, 7L);
        Assert.Equal(new V2i(7L, 7L), box.Min);
        Assert.Equal(new V2i(7L, 7L), box.Max);
    }

    // -------------------------------------------------------------------------
    // Box2i.Envelop (V2i)
    // -------------------------------------------------------------------------

    [Fact]
    public void Box2i_EnvelopV2i_Works()
    {
        var box = new Box2i();
        box.Envelop(new V2i(-1L, 3L));
        box.Envelop(new V2i(5L, -2L));
        Assert.Equal(-1L, box.Min.X);
        Assert.Equal(-2L, box.Min.Y);
        Assert.Equal(5L, box.Max.X);
        Assert.Equal(3L, box.Max.Y);
    }

    // -------------------------------------------------------------------------
    // Box2i.Envelop (list)
    // -------------------------------------------------------------------------

    [Fact]
    public void Box2i_EnvelopList_ComputesCorrectBounds()
    {
        var points = new List<V2i>
        {
            new V2i(-10L, 5L),
            new V2i(0L, 0L),
            new V2i(20L, -3L),
            new V2i(7L, 100L),
        };
        var box = new Box2i();
        box.Envelop(points);
        Assert.Equal(-10L, box.Min.X);
        Assert.Equal(-3L, box.Min.Y);
        Assert.Equal(20L, box.Max.X);
        Assert.Equal(100L, box.Max.Y);
    }

    [Fact]
    public void Box2i_EnvelopReadOnlySpan_ComputesCorrectBounds()
    {
        ReadOnlySpan<V2i> pts = [new V2i(3L, 4L), new V2i(-1L, 10L), new V2i(5L, -5L)];
        var box = new Box2i();
        box.Envelop(pts);
        Assert.Equal(-1L, box.Min.X);
        Assert.Equal(-5L, box.Min.Y);
        Assert.Equal(5L, box.Max.X);
        Assert.Equal(10L, box.Max.Y);
    }

    // -------------------------------------------------------------------------
    // Box2i.Of
    // -------------------------------------------------------------------------

    [Fact]
    public void Box2i_Of_SinglePoint()
    {
        var box = Box2i.Of([new V2i(3L, 7L)]);
        Assert.Equal(new V2i(3L, 7L), box.Min);
        Assert.Equal(new V2i(3L, 7L), box.Max);
    }

    [Fact]
    public void Box2i_Of_MultiplePoints_MatchesManualEnvelop()
    {
        var points = new List<V2i>
        {
            new(0L, 0L), new(-5L, 10L), new(3L, -8L), new(1L, 1L)
        };
        var box = Box2i.Of(points);
        Assert.Equal(-5L, box.Min.X);
        Assert.Equal(-8L, box.Min.Y);
        Assert.Equal(3L, box.Max.X);
        Assert.Equal(10L, box.Max.Y);
    }

    [Fact]
    public void Box2i_ToString_ContainsCoordinates()
    {
        var box = Box2i.Of([new V2i(1L, 2L), new V2i(3L, 4L)]);
        var s = box.ToString();
        Assert.Contains("1", s);
        Assert.Contains("4", s);
    }

    // -------------------------------------------------------------------------
    // V2i — additional equality coverage
    // -------------------------------------------------------------------------

    [Fact]
    public void V2i_EqualsObject_WrongType_ReturnsFalse()
    {
        var p = new V2i(1L, 2L);
        Assert.False(p.Equals("not a V2i"));
        Assert.False(p.Equals(42));
    }

    [Fact]
    public void V2i_NotEqual_Operator_ReturnsTrue_WhenDifferent()
    {
        var a = new V2i(1L, 0L);
        var b = new V2i(2L, 0L);
        Assert.True(a != b);
    }
}
