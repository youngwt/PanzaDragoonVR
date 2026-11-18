#nullable enable
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The six target cubes, corkscrewing round the player's origin on the paths <see cref="CubeFlight"/> gives them,
/// each tumbling as it goes and hidden while destroyed. Each is a grey cube, the placeholder for
/// <see cref="AssetIds.E03Flyer"/>, built from the scene's box mesh and material; when the asset folder supplies the
/// model, each cube carries its own copy in place of the grey cube.
/// </summary>
public partial class CubeFlock : Node3D
{
    /// <summary>The grey cube's mesh, the author's own.</summary>
    [Export] public Mesh? CubeMesh { get; set; }

    /// <summary>The grey cube's material, the author's own.</summary>
    [Export] public Material? CubeMaterial { get; set; }

    private readonly CubeFlight _flight = new();
    private readonly FlyingCube[] _cubes = new FlyingCube[CubeFlight.Count];

    public override void _Ready()
    {
        for (int i = 0; i < CubeFlight.Count; i++)
        {
            _cubes[i] = new FlyingCube { Name = $"Cube{i}", Mesh = CubeMesh, MaterialOverride = CubeMaterial };
            AddChild(_cubes[i]);
        }

        Place();
    }

    public override void _Process(double delta)
    {
        _flight.Advance(delta);
        Place();
    }

    /// <summary>
    /// Flies <paramref name="model"/> in place of the grey cubes: the first cube carries it and each other cube a
    /// copy of it. With null, all six are grey cubes again. Models carried before are freed.
    /// </summary>
    public void Carry(Node3D? model)
    {
        _cubes[0].Carry(model);
        for (int i = 1; i < CubeFlight.Count; i++)
        {
            _cubes[i].Carry(model == null ? null : (Node3D)model.Duplicate());
        }
    }

    private void Place()
    {
        for (int i = 0; i < CubeFlight.Count; i++)
        {
            CubePoint point = _flight.Position(i);
            _cubes[i].Position = new Vector3(point.X, point.Y, point.Z);
            _cubes[i].Visible = _flight.IsPresent(i);
        }
    }
}
