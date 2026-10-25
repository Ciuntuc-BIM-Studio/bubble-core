using BubbleBim.Geometry;
using BubbleBim.Step;

namespace BubbleCore.Tests;

/// <summary>A STEP file written, read back, and an IFC wall's geometry read from it.</summary>
public class StepTests
{
    [Fact]
    public void A_file_round_trips_through_a_stream_with_the_application_named()
    {
        var f = new Step { Application = "Core tests" };
        var org = f.Add("IFCORGANIZATION", Step.Null, Step.S("Ciuntuc"), Step.Null, Step.Null, Step.Null);
        var project = f.Add("IFCPROJECT", Step.S(Step.Guid("tests", "project")), Step.Null, Step.S("Test"), Step.Null, Step.Null, Step.Null, Step.Null, Step.Null, Step.Null);
        using var ms = new MemoryStream();
        f.Write(ms, "test.ifc", "ViewDefinition [ReferenceView_V1.2]");
        var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("FILE_NAME('test.ifc'", text);
        Assert.Contains("'Core tests','Core tests'", text);
        Assert.Contains("FILE_SCHEMA(('IFC4'))", text);
        var read = StepFile.Read(ms.ToArray());
        Assert.Equal("IFC4", read.Schema);
        Assert.Equal(int.Parse(project[1..]), read.OfType("IFCPROJECT").Single().Id);
        Assert.Equal("Ciuntuc", read.OfType("IFCORGANIZATION").Single()[1]);
        Assert.Equal(org, $"#{read.OfType("IFCORGANIZATION").Single().Id}");
    }

    [Fact]
    public void Guids_are_stable_and_22_characters()
    {
        var a = Step.Guid("project", "wall-1");
        Assert.Equal(a, Step.Guid("project", "wall-1"));
        Assert.Equal(22, a.Length);
        Assert.NotEqual(a, Step.Guid("project", "wall-2"));
        Assert.NotEqual(a, Step.Guid("other", "wall-1"));
    }

    [Fact]
    public void An_extruded_wall_is_read_as_a_mesh_in_metres()
    {
        // Millimetres in the file: a 4000 × 300 × 2800 wall.
        var f = new Step();
        var dir = f.Add("IFCDIRECTION", "(0.,0.,1.)");
        var origin = f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var axis = f.Add("IFCAXIS2PLACEMENT3D", origin, Step.Null, Step.Null);
        var place = f.Add("IFCLOCALPLACEMENT", Step.Null, axis);
        var pts = Step.L(new[] { "(0.,0.)", "(4000.,0.)", "(4000.,300.)", "(0.,300.)" }.Select(p => f.Add("IFCCARTESIANPOINT", p)));
        var poly = f.Add("IFCPOLYLINE", pts);
        var profile = f.Add("IFCARBITRARYCLOSEDPROFILEDEF", Step.E("AREA"), Step.Null, poly);
        var solid = f.Add("IFCEXTRUDEDAREASOLID", profile, axis, dir, "2800.");
        var ctx = f.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", Step.Null, Step.S("Model"), "3", "1.E-05", axis, Step.Null);
        var rep = f.Add("IFCSHAPEREPRESENTATION", ctx, Step.S("Body"), Step.S("SweptSolid"), Step.L([solid]));
        var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", Step.Null, Step.Null, Step.L([rep]));
        var wall = f.Add("IFCWALL", Step.S(Step.Guid("t", "w")), Step.Null, Step.S("W1"), Step.Null, Step.Null, place, shape, Step.Null, Step.Null);
        var mm = f.Add("IFCSIUNIT", Step.Derived, Step.E("LENGTHUNIT"), Step.E("MILLI"), Step.E("METRE"));
        var units = f.Add("IFCUNITASSIGNMENT", Step.L([mm]));
        f.Add("IFCPROJECT", Step.S(Step.Guid("t", "p")), Step.Null, Step.S("P"), Step.Null, Step.Null, Step.Null, Step.Null, Step.L([ctx]), units);
        using var ms = new MemoryStream();
        f.Write(ms, "wall.ifc", "test");

        var model = new IfcModel(StepFile.Read(ms.ToArray()));
        Assert.Equal(0.001, model.Scale, 9);
        var product = Assert.Single(model.Products, p => p.Class == "IfcWall");
        Assert.Equal(int.Parse(wall[1..]), product.Id);
        var (body, openings) = model.Shape(product.Id);
        Assert.Empty(openings);
        var mesh = Assert.Single(body).Mesh;
        Assert.True(mesh.Closed());
        Assert.Equal(4.0 * 0.3 * 2.8, Volume(mesh), 6);
    }

    /// <summary>The volume enclosed by a closed triangle mesh (the divergence theorem).</summary>
    static double Volume(MeshBuilder m)
    {
        double v = 0;
        foreach (var (a, b, c) in m.T) v += Vec3.Dot(m.V[a], Vec3.Cross(m.V[b], m.V[c]));
        return Math.Abs(v) / 6;
    }
}
