using BubbleBim.Geometry;

namespace BubbleCore.Tests;

/// <summary>The caps that close solids cut in 3D, on boxes whose answer is known.</summary>
public class SectionCapTests
{
    static double Area(TriMesh m)
    {
        double a = 0;
        for (var i = 0; i + 2 < m.Indices.Length; i += 3)
        {
            Vec3 P(int k) => new(m.Positions[3 * k], m.Positions[3 * k + 1], m.Positions[3 * k + 2]);
            Vec3 p = P(m.Indices[i]), q = P(m.Indices[i + 1]), r = P(m.Indices[i + 2]);
            a += Vec3.Cross(q - p, r - p).Length / 2;
        }
        return a;
    }

    static double EdgeLength(TriMesh m)
    {
        double l = 0;
        for (var i = 0; i + 5 < m.Edges.Length; i += 6)
            l += new Vec3(m.Edges[i + 3] - m.Edges[i], m.Edges[i + 4] - m.Edges[i + 1], m.Edges[i + 5] - m.Edges[i + 2]).Length;
        return l;
    }

    /// <summary>Every triangle facing <paramref name="n"/> (wound counter-clockwise round it), and every normal it.</summary>
    static void Faces(TriMesh m, Vec3 n)
    {
        for (var i = 0; i + 2 < m.Indices.Length; i += 3)
        {
            Vec3 P(int k) => new(m.Positions[3 * k], m.Positions[3 * k + 1], m.Positions[3 * k + 2]);
            var c = Vec3.Cross(P(m.Indices[i + 1]) - P(m.Indices[i]), P(m.Indices[i + 2]) - P(m.Indices[i]));
            if (c.Length > 1e-12) Assert.True(Vec3.Dot(c, n) > 0);
        }
        for (var i = 0; i + 2 < m.Normals.Length; i += 3)
            Assert.Equal(1, Vec3.Dot(new Vec3(m.Normals[i], m.Normals[i + 1], m.Normals[i + 2]), n), 6);
    }

    [Fact]
    public void A_box_cut_across_is_closed_by_its_section()
    {
        // 2 × 2 × 3 box, the half with x < 0.5 kept: the cap is 2 × 3 at x = 0.5, facing +X (out of what is kept).
        var box = Prism.Box(new Vec3(0, 0, 1.5), 2, 2, 3)!;
        var caps = SectionCaps.Make([box], [ClipPlane.Through(new Vec3(0.5, 0, 0), new Vec3(-1, 0, 0))]);
        Assert.Equal(6, Area(caps[0]), 6);
        Faces(caps[0], new Vec3(1, 0, 0));
        Assert.All(Enumerable.Range(0, caps[0].Positions.Length / 3), k => Assert.Equal(0.5, caps[0].Positions[3 * k], 6));
        Assert.Equal(2 * (2 + 3), EdgeLength(caps[0]), 6);
    }

    [Fact]
    public void A_lift_moves_the_cap_into_what_is_kept()
    {
        var box = Prism.Box(new Vec3(0, 0, 1.5), 2, 2, 3)!;
        var caps = SectionCaps.Make([box], [ClipPlane.Through(new Vec3(0.5, 0, 0), new Vec3(-1, 0, 0))], lift: 0.001);
        Assert.All(Enumerable.Range(0, caps[0].Positions.Length / 3), k => Assert.Equal(0.499, caps[0].Positions[3 * k], 6));
    }

    [Fact]
    public void A_hole_through_the_solid_stays_open_in_its_cap()
    {
        // A 4 × 4 frame round a 2 × 2 hole, 3 high, cut at z = 1 (below kept): the cap is 16 − 4.
        var outer = new[] { new Vec3(-2, -2, 0), new Vec3(2, -2, 0), new Vec3(2, 2, 0), new Vec3(-2, 2, 0) };
        var hole = new[] { new Vec3(-1, -1, 0), new Vec3(-1, 1, 0), new Vec3(1, 1, 0), new Vec3(1, -1, 0) };
        var frame = Prism.Extrude(outer, [hole], new Vec3(0, 0, 3))!;
        var caps = SectionCaps.Make([frame], [ClipPlane.Through(new Vec3(0, 0, 1), new Vec3(0, 0, -1))]);
        Assert.Equal(12, Area(caps[0]), 6);
        Faces(caps[0], new Vec3(0, 0, 1));
        Assert.Equal(16 + 8, EdgeLength(caps[0]), 6);
    }

    [Fact]
    public void A_box_section_closes_each_face_where_it_cuts_and_they_meet_at_its_edges()
    {
        // A 2 × 2 × 2 cube from the origin, the section box keeping x ≤ 1 and y ≤ 1: two caps, each 1 × 2.
        var cube = Prism.Box(new Vec3(1, 1, 1), 2, 2, 2)!;
        var box = new[] { ClipPlane.Through(new Vec3(1, 0, 0), new Vec3(-1, 0, 0)), ClipPlane.Through(new Vec3(0, 1, 0), new Vec3(0, -1, 0)) };
        var caps = SectionCaps.Make([cube], box);
        Assert.Equal(4, Area(caps[0]), 6);
        // Each cap's outline is 1 + 2 + 1 + 2.
        Assert.Equal(12, EdgeLength(caps[0]), 6);
        // Nothing of the caps lies past either plane.
        for (var k = 0; k < caps[0].Positions.Length / 3; k++)
        {
            Assert.True(caps[0].Positions[3 * k] <= 1 + 1e-9);
            Assert.True(caps[0].Positions[3 * k + 1] <= 1 + 1e-9);
        }
    }

    [Fact]
    public void A_slab_between_a_section_and_its_depth_is_capped_on_both()
    {
        var wall = Prism.Box(new Vec3(0, 0, 1.5), 0.3, 10, 3)!;
        var planes = new[] { ClipPlane.Through(new Vec3(0, -1, 0), new Vec3(0, 1, 0)), ClipPlane.Through(new Vec3(0, 2, 0), new Vec3(0, -1, 0)) };
        var caps = SectionCaps.Make([wall], planes);
        Assert.Equal(2 * 0.3 * 3, Area(caps[0]), 6);
    }

    [Fact]
    public void A_solid_the_planes_do_not_cross_has_no_caps()
    {
        var plane = new[] { ClipPlane.Through(new Vec3(5, 0, 0), new Vec3(-1, 0, 0)) };
        var kept = Prism.Box(new Vec3(0, 0, 0.5), 1, 1, 1)!;
        var gone = Prism.Box(new Vec3(10, 0, 0.5), 1, 1, 1)!;
        var caps = SectionCaps.Make([kept, gone], plane);
        Assert.Empty(caps[0].Indices);
        Assert.Empty(caps[1].Indices);
    }

    [Fact]
    public void A_solid_wholly_outside_the_box_has_no_caps_where_another_plane_crosses_it()
    {
        // Crossed by x = 0, but past y ≤ 1: the box keeps none of it.
        var far = Prism.Box(new Vec3(0, 5, 0.5), 2, 1, 1)!;
        var box = new[] { ClipPlane.Through(Vec3.Zero, new Vec3(-1, 0, 0)), ClipPlane.Through(new Vec3(0, 1, 0), new Vec3(0, -1, 0)) };
        Assert.Empty(SectionCaps.Make([far], box)[0].Indices);
    }

    [Fact]
    public void Two_solids_of_one_material_cut_together_read_as_one()
    {
        // Two boxes side by side along y (a wall's core in two storeys' pieces), cut at x = 0: the line between them goes when they join by one key.
        var a = Prism.Box(new Vec3(0, 0.5, 1), 1, 1, 2)!;
        var b = Prism.Box(new Vec3(0, 1.5, 1), 1, 1, 2)!;
        var plane = new[] { ClipPlane.Through(Vec3.Zero, new Vec3(-1, 0, 0)) };
        var apart = SectionCaps.Make([a, b], plane, ["brick", "concrete"]);
        Assert.Equal(6, EdgeLength(apart[0]), 6);
        var joined = SectionCaps.Make([a, b], plane, ["brick", "brick"]);
        Assert.Equal(4, EdgeLength(joined[0]), 6);
        Assert.Equal(4, EdgeLength(joined[1]), 6);
        Assert.Equal(2, Area(joined[0]), 6);
    }
}
