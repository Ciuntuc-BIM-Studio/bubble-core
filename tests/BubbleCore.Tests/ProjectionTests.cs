using BubbleBim.Geometry;

namespace BubbleCore.Tests;

/// <summary>Drawings with hidden lines removed, and sections, on boxes whose answer is known.</summary>
public class ProjectionTests
{
    // Looking north (+Y) from y = −10: right is +X, up is +Z.
    static readonly ViewFrame North = ViewFrame.Horizontal(new Vec3(0, -10, 0), new Vec2(0, 1));

    static double Length(IEnumerable<Seg2> lines) => lines.Sum(l => (l.B - l.A).Length);

    [Fact]
    public void A_box_seen_from_the_front_is_its_front_rectangle_once()
    {
        var box = Prism.Box(new Vec3(0, 0, 1), 4, 2, 2)!;
        var (lines, cuts, _) = HiddenLines.Draw([(box, 0)], North);
        Assert.Empty(cuts);
        Assert.Equal(4, lines.Count);
        Assert.Equal(2 * 4 + 2 * 2, Length(lines), 9);
        Assert.Equal(-2, lines.Min(l => Math.Min(l.A.X, l.B.X)), 9);
        Assert.Equal(2, lines.Max(l => Math.Max(l.A.Y, l.B.Y)), 9);
    }

    [Fact]
    public void A_box_behind_another_shows_only_what_sticks_out()
    {
        var front = Prism.Box(new Vec3(0, 0, 1), 2, 1, 2)!;   // x −1..1, z 0..2
        var back = Prism.Box(new Vec3(1, 5, 1.5), 2, 1, 3)!;  // x 0..2, z 0..3, further away
        var (lines, _, _) = HiddenLines.Draw([(front, 0), (back, 1)], North);
        var behind = lines.Where(l => l.Owner == 1).ToList();
        // Visible of the back box: its right side (3), its top (2), and its left side above the front box (1);
        // its bottom from x 1 to 2 (1) — the rest of it is behind the front box.
        Assert.Equal(3 + 2 + 1 + 1, Length(behind), 9);
        Assert.Equal(2 * 2 + 2 * 2, Length(lines.Where(l => l.Owner == 0)), 9);
        // Nothing of the back box is drawn inside the front one.
        Assert.DoesNotContain(behind, l => { var m = (l.A + l.B) * 0.5; return m.X > -1 + 1e-6 && m.X < 1 - 1e-6 && m.Y > 1e-6 && m.Y < 2 - 1e-6; });
    }

    [Fact]
    public void A_section_cuts_the_element_and_draws_what_is_beyond()
    {
        // A wall with a doorway, cut across by a plane at y = 0 looking north.
        var wall = Prism.Extrude(
            [new(-3, -0.2, 0), new(3, -0.2, 0), new(3, -0.2, 3), new(-3, -0.2, 3)],
            [[new(-0.5, -0.2, 0.5), new(-0.5, -0.2, 2.5), new(0.5, -0.2, 2.5), new(0.5, -0.2, 0.5)]],
            new Vec3(0, 0.4, 0))!;
        var behind = Prism.Box(new Vec3(0, 4, 1), 1, 1, 2)!;
        var at = ViewFrame.Horizontal(new Vec3(0, 0, 0), new Vec2(0, 1));
        var (lines, cuts, _) = HiddenLines.Draw([(wall, 0), (behind, 1)], at, cut: true);

        var cut = Assert.Single(cuts);
        Assert.Equal(0, cut.Owner);
        // The cut is the wall's face with its hole: 6 × 3 minus 1 × 2, filled even-odd.
        Assert.Equal(2, cut.Loops.Count);
        Assert.Equal([18.0, 2.0], cut.Loops.Select(l => Math.Round(Math.Abs(Polygon2.SignedArea(l)), 9)).OrderDescending());
        // The box beyond is seen through the doorway only: its part inside x −0.5..0.5, z 0.5..2 (the rest is behind the cut wall).
        var seen = lines.Where(l => l.Owner == 1).ToList();
        Assert.NotEmpty(seen);
        Assert.All(seen, l => Assert.True(Math.Max(Math.Abs(l.A.X), Math.Abs(l.B.X)) <= 0.5 + 1e-9 && Math.Min(l.A.Y, l.B.Y) >= 0.5 - 1e-9, $"{l.A}–{l.B}"));
        // Nothing of the wall in front of the plane is drawn.
        var none = HiddenLines.Draw([(wall, 0)], ViewFrame.Horizontal(new Vec3(0, 1, 0), new Vec2(0, 1)), cut: true);
        Assert.Empty(none.Lines);
        Assert.Empty(none.Cuts);
    }

    [Fact]
    public void Two_walls_in_one_plane_are_one_facade_without_a_seam()
    {
        // Side by side, and one on top: their fronts are one plane at y = −0.15.
        var left = Prism.Box(new Vec3(-2, 0, 1.5), 4, 0.3, 3)!;
        var right = Prism.Box(new Vec3(2, 0, 1.5), 4, 0.3, 3)!;
        var above = Prism.Box(new Vec3(0, 0, 4.5), 8, 0.3, 3)!;
        var (lines, _, _) = HiddenLines.Draw([(left, 0), (right, 1), (above, 2)], North);
        // Only the outline of the 8 × 6 facade.
        Assert.Equal(2 * 8 + 2 * 6, Length(lines), 9);
        Assert.DoesNotContain(lines, l => Math.Abs(l.A.X) < 1e-9 && Math.Abs(l.B.X) < 1e-9);
        Assert.DoesNotContain(lines, l => Math.Abs(l.A.Y - 3) < 1e-9 && Math.Abs(l.B.Y - 3) < 1e-9);
    }

    [Fact]
    public void Hidden_edges_come_back_dashed_only_when_asked_and_never_the_seams()
    {
        var front = Prism.Box(new Vec3(0, 0, 1), 2, 1, 2)!;   // x −1..1, z 0..2
        var back = Prism.Box(new Vec3(1, 5, 1.5), 2, 1, 3)!;  // x 0..2, z 0..3
        Assert.Empty(HiddenLines.Draw([(front, 0), (back, 1)], North).Hidden);
        var (lines, _, hidden) = HiddenLines.Draw([(front, 0), (back, 1)], North, hidden: true);
        // The back box's outline behind the front one: its left side from z 0 to 2 (2) and its bottom from x 0 to 1 (1).
        Assert.Equal(2 + 1, Length(hidden.Where(h => h.Owner == 1)), 9);
        Assert.Equal(Length(lines), Length(HiddenLines.Draw([(front, 0), (back, 1)], North).Lines), 9);
        // Two walls in one plane: their seam is neither seen nor hidden.
        var left = Prism.Box(new Vec3(-2, 0, 1.5), 4, 0.3, 3)!;
        var right = Prism.Box(new Vec3(2, 0, 1.5), 4, 0.3, 3)!;
        Assert.DoesNotContain(HiddenLines.Draw([(left, 0), (right, 1)], North, hidden: true).Hidden, l => Math.Abs(l.A.X) < 1e-9 && Math.Abs(l.B.X) < 1e-9);
    }
}

