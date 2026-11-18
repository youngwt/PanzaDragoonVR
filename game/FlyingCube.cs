#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// One of the target cubes, tumbling as it flies. <see cref="CubeFlock"/> places it on its path and shows or hides it.
/// Its own mesh is the grey cube, the placeholder for <see cref="AssetIds.E03Flyer"/>; a model the asset folder
/// supplies flies in its place.
/// </summary>
public partial class FlyingCube : MeshInstance3D
{
    /// <summary>How fast the cube tumbles, in radians a second.</summary>
    [Export] public float TumbleRadiansPerSecond { get; set; } = 1.0f;

    private static readonly Vector3 TumbleAxis = new Vector3(0.3f, 1.0f, 0.2f).Normalized();

    private Mesh? _cube;
    private Node3D? _carried;

    public override void _Ready()
    {
        _cube = Mesh;
    }

    /// <summary>
    /// Flies <paramref name="model"/> in place of the grey cube, which is no longer drawn; or, with null,
    /// the grey cube again. A model carried before is freed: the asset folder is read again after extraction.
    /// </summary>
    public void Carry(Node3D? model)
    {
        if (_carried != null && IsInstanceValid(_carried))
        {
            _carried.QueueFree();
        }

        _carried = model;
        if (model == null)
        {
            Mesh = _cube;
            return;
        }

        AddChild(model);
        Mesh = null;
    }

    public override void _Process(double delta)
    {
        Rotate(TumbleAxis, TumbleRadiansPerSecond * (float)delta);
    }
}
