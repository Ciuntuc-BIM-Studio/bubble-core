using BubbleBim.Geometry;

namespace BubbleCore.Tests;

public class KernelTests
{
    static void Valid(Solid s) => Assert.Empty(Topology.Validate(s));

    [Fact]
    public void A_box_is_a_closed_solid_with_the_right_volume_and_area()
    {
        var b = Prism.Box(new Vec3(1, 2, 3), 2, 3, 4)!;
        Valid(b);
        Assert.Equal(24, Measure.Volume(b), 9);
        Assert.Equal(2 * (6 + 8 + 12), Measure.SurfaceArea(b), 9);
        Assert.Equal(6, b.Faces.Length);
        Assert.Equal(8, b.Vertices.Length);
    }

    [Fact]
    public void A_concave_footprint_extrudes_to_its_exact_volume()
    {
        // L shape: 4×4 minus 2×2 = 12 m², 3 m high.
        Vec2[] l = [new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 4), new(0, 4)];
        var s = Prism.ExtrudeFootprint(l, 0, 3)!;
        Valid(s);
        Assert.Equal(36, Measure.Volume(s), 9);
        Assert.Equal(12, Measure.TaggedArea(s, "top"), 9);
    }

    [Fact]
    public void The_winding_of_the_footprint_does_not_matter()
    {
        Vec2[] ccw = [new(0, 0), new(2, 0), new(2, 1), new(0, 1)];
        var a = Prism.ExtrudeFootprint(ccw, 0, 1)!;
        var b = Prism.ExtrudeFootprint(ccw.Reverse().ToArray(), 0, 1)!;
        Valid(a); Valid(b);
        Assert.True(Measure.SignedVolume(a) > 0);
        Assert.True(Measure.SignedVolume(b) > 0);
    }

    [Fact]
    public void A_profile_with_a_hole_extrudes_to_a_closed_solid_with_reveals()
    {
        // A 4×3 wall elevation with a 1×1 window, extruded 0.3 across.
        Vec3[] outer = [new(0, 0, 0), new(4, 0, 0), new(4, 0, 3), new(0, 0, 3)];
        Vec3[] hole = [new(1, 0, 1), new(2, 0, 1), new(2, 0, 2), new(1, 0, 2)];
        var s = Prism.Extrude(outer, [hole], new Vec3(0, 0.3, 0))!;
        Valid(s);
        Assert.Equal((12 - 1) * 0.3, Measure.Volume(s), 9);
        Assert.Equal(4, s.Faces.Count(f => f.Tag == "reveal"));
        Assert.Equal(11, Measure.TaggedArea(s, "start"), 9);
    }

    [Fact]
    public void Welding_merges_points_closer_than_the_tolerance()
    {
        var (v, remap) = SolidBuilder.Weld([new(0, 0, 0), new(0, 0, 1e-8), new(1, 0, 0)], Tolerance.Distance);
        Assert.Equal(2, v.Count);
        Assert.Equal(remap[0], remap[1]);
    }

    [Fact]
    public void An_open_shell_is_reported()
    {
        var b = Prism.Box(Vec3.Zero, 1, 1, 1)!;
        var open = b with { Faces = b.Faces.Skip(1).ToArray() };
        Assert.Contains(Topology.Validate(open), d => d.Code == "not_closed");
    }

    [Fact]
    public void A_mirror_transform_is_repaired_not_left_inside_out()
    {
        var b = Prism.Box(new Vec3(1, 1, 1), 1, 1, 1)!;
        var m = SolidBuilder.Transform(b, p => new Vec3(-p.X, p.Y, p.Z));
        Valid(m);
        Assert.Equal(1, Measure.SignedVolume(m), 9);
    }

    [Fact]
    public void Tessellation_gives_flat_shaded_triangles_and_each_edge_once()
    {
        var b = Prism.Box(Vec3.Zero, 1, 1, 1)!;
        var m = Tessellator.Tessellate(b);
        Assert.Equal(12 * 3, m.Indices.Length);
        Assert.Equal(24 * 3, m.Positions.Length);   // 4 per face, not shared
        Assert.Equal(12 * 6, m.Edges.Length);       // 12 edges, 2 points each
    }

    [Fact]
    public void A_swept_beam_and_a_cylinder_are_valid()
    {
        var beam = Prism.SweepBox(new Vec3(0, 0, 3), new Vec3(5, 2, 3.5), 0.2, 0.4)!;
        Valid(beam);
        Assert.Equal(0.2 * 0.4 * new Vec3(5, 2, 0.5).Length, Measure.Volume(beam), 9);
        var col = Prism.Cylinder(new Vec2(0, 0), 0, 0.2, 3, 24)!;
        Valid(col);
        Assert.InRange(Measure.Volume(col), Math.PI * 0.04 * 3 * 0.98, Math.PI * 0.04 * 3);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void A_face_with_holes_in_a_row_triangulates_to_its_area_whichever_way_it_faces(int side)
    {
        // A wall's face: a 10 × 3 m outline with a door notch, three windows one above the other in plan… in a row.
        var pts = new List<Vec3> { new(0, 0, 0), new(4, 0, 0), new(4, 0, 2.2), new(5, 0, 2.2), new(5, 0, 0), new(10, 0, 0), new(10, 0, 3), new(0, 0, 3) };
        int[] outer = [0, 1, 2, 3, 4, 5, 6, 7];
        var holes = new List<int[]>();
        foreach (var x0 in new[] { 0.5, 6.0, 8.0 })
        {
            var k = pts.Count;
            pts.AddRange([new(x0, 0, 0.9), new(x0, 0, 2.1), new(x0 + 1.2, 0, 2.1), new(x0 + 1.2, 0, 0.9)]);
            holes.Add([k, k + 1, k + 2, k + 3]);
        }
        var n = new Vec3(0, -side, 0);
        if (side < 0) { Array.Reverse(outer); holes = holes.Select(h => h.Reverse().ToArray()).ToList(); }
        var tris = Triangulator.Face(pts, outer, [.. holes], n);
        var area = tris.Sum(t => Vec3.Cross(pts[t.B] - pts[t.A], pts[t.C] - pts[t.A]).Length / 2);
        Assert.Equal(10 * 3 - 1 * 2.2 - 3 * 1.2 * 1.2, area, 9);
    }
}
