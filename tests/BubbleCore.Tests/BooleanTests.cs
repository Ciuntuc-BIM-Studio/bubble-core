using BubbleBim.Geometry;
using BubbleBim.Booleans;

namespace BubbleCore.Tests;

/// <summary>Booleans by manifold, back as planar-faced B-rep: exact volumes, valid topology, one body where bodies merge.</summary>
public class BooleanTests
{
    static readonly ManifoldBooleans B = new();
    static Solid Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
        Prism.ExtrudeFootprint([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)], z0, z1 - z0)!;

    static void Valid(Solid s) => Assert.DoesNotContain(Topology.Validate(s), d => d.Severity == DiagnosticSeverity.Error);

    [Fact]
    public void The_native_library_is_there() => Assert.True(ManifoldBooleans.Available);

    [Fact]
    public void A_notch_cut_from_a_box()
    {
        var r = B.Difference(Box(0, 0, 0, 2, 2, 2), [Box(1, 1, 1, 3, 3, 3)])!;
        Valid(r);
        Assert.Equal(8 - 1, Measure.Volume(r), 9);
        Assert.Equal(9, r.Faces.Length);                                // 6 sides, 3 of them L-shaped… plus the notch's 3
    }

    [Fact]
    public void Union_and_intersection_of_overlapping_boxes()
    {
        var a = Box(0, 0, 0, 2, 2, 2); var b = Box(1, 1, 1, 3, 3, 3);
        var u = B.Union([a, b])!;
        Valid(u);
        Assert.Equal(8 + 8 - 1, Measure.Volume(u), 9);
        var i = B.Intersection(a, b)!;
        Valid(i);
        Assert.Equal(1, Measure.Volume(i), 9);
        Assert.Equal(6, i.Faces.Length);
        Assert.Null(B.Intersection(a, Box(5, 5, 5, 6, 6, 6)));          // nothing in common
    }

    [Fact]
    public void Two_boxes_side_by_side_become_one_body_with_six_faces()
    {
        var u = B.Union([Box(0, 0, 0, 1, 1, 1), Box(1, 0, 0, 3, 1, 1)])!;
        Valid(u);
        Assert.Equal(3, Measure.Volume(u), 9);
        Assert.Equal(6, u.Faces.Length);                                // the shared face is gone, the coplanar sides merged
    }

    [Fact]
    public void A_round_hole_through_a_wall_leaves_a_face_with_a_hole()
    {
        var wall = Box(0, 0, 0, 4, 0.3, 3);
        // A horizontal bore, a 24-gon, through the wall's thickness.
        var bore = Prism.Extrude(Enumerable.Range(0, 24).Select(k => new Vec3(2 + 0.5 * Math.Cos(k * Math.PI / 12), -0.1, 1.5 + 0.5 * Math.Sin(k * Math.PI / 12))).ToArray(), null, new Vec3(0, 0.5, 0))!;
        var r = B.Difference(wall, [bore])!;
        Valid(r);
        var area = 0.5 * 24 * 0.25 * Math.Sin(2 * Math.PI / 24);          // the 24-gon's area
        Assert.Equal(4 * 0.3 * 3 - area * 0.3, Measure.Volume(r), 9);
        Assert.Contains(r.Faces, f => f.Holes.Length == 1);              // the wall's faces now carry the hole
    }
}
