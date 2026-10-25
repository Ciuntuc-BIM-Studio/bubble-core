using BubbleBim.Geometry;
using BubbleBim.Step;

namespace BubbleCore.Tests;

/// <summary>A STEP file written, read back, and an IFC wall's geometry read from it.</summary>
public class StepTests
{
    [Fact]
    public void A_file_round_trips_through_a_stream_with_the_application_named()
    {
        var f = new StepWriter { Application = "Core tests" };
        var org = f.Add("IFCORGANIZATION", StepWriter.Null, StepWriter.S("Ciuntuc"), StepWriter.Null, StepWriter.Null, StepWriter.Null);
        var project = f.Add("IFCPROJECT", StepWriter.S(StepWriter.Guid("tests", "project")), StepWriter.Null, StepWriter.S("Test"), StepWriter.Null, StepWriter.Null, StepWriter.Null, StepWriter.Null, StepWriter.Null, StepWriter.Null);
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
        var a = StepWriter.Guid("project", "wall-1");
        Assert.Equal(a, StepWriter.Guid("project", "wall-1"));
        Assert.Equal(22, a.Length);
        Assert.NotEqual(a, StepWriter.Guid("project", "wall-2"));
        Assert.NotEqual(a, StepWriter.Guid("other", "wall-1"));
    }

    [Fact]
    public void An_extruded_wall_is_read_as_a_mesh_in_metres()
    {
        // Millimetres in the file: a 4000 × 300 × 2800 wall.
        var f = new StepWriter();
        var dir = f.Add("IFCDIRECTION", "(0.,0.,1.)");
        var origin = f.Add("IFCCARTESIANPOINT", "(0.,0.,0.)");
        var axis = f.Add("IFCAXIS2PLACEMENT3D", origin, StepWriter.Null, StepWriter.Null);
        var place = f.Add("IFCLOCALPLACEMENT", StepWriter.Null, axis);
        var pts = StepWriter.L(new[] { "(0.,0.)", "(4000.,0.)", "(4000.,300.)", "(0.,300.)" }.Select(p => f.Add("IFCCARTESIANPOINT", p)));
        var poly = f.Add("IFCPOLYLINE", pts);
        var profile = f.Add("IFCARBITRARYCLOSEDPROFILEDEF", StepWriter.E("AREA"), StepWriter.Null, poly);
        var solid = f.Add("IFCEXTRUDEDAREASOLID", profile, axis, dir, "2800.");
        var ctx = f.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", StepWriter.Null, StepWriter.S("Model"), "3", "1.E-05", axis, StepWriter.Null);
        var rep = f.Add("IFCSHAPEREPRESENTATION", ctx, StepWriter.S("Body"), StepWriter.S("SweptSolid"), StepWriter.L([solid]));
        var shape = f.Add("IFCPRODUCTDEFINITIONSHAPE", StepWriter.Null, StepWriter.Null, StepWriter.L([rep]));
        var wall = f.Add("IFCWALL", StepWriter.S(StepWriter.Guid("t", "w")), StepWriter.Null, StepWriter.S("W1"), StepWriter.Null, StepWriter.Null, place, shape, StepWriter.Null, StepWriter.Null);
        var mm = f.Add("IFCSIUNIT", StepWriter.Derived, StepWriter.E("LENGTHUNIT"), StepWriter.E("MILLI"), StepWriter.E("METRE"));
        var units = f.Add("IFCUNITASSIGNMENT", StepWriter.L([mm]));
        f.Add("IFCPROJECT", StepWriter.S(StepWriter.Guid("t", "p")), StepWriter.Null, StepWriter.S("P"), StepWriter.Null, StepWriter.Null, StepWriter.Null, StepWriter.Null, StepWriter.L([ctx]), units);
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
