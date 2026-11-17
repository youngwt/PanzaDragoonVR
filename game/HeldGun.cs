#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// Holds the rider's gun in the left hand. This node is a child of the left controller, at its aim point, so the
/// gun goes wherever the controller goes. A gun's origin is its grip, the point the hand holds, as the asset
/// contract's Models section says, and is put at the aim point. Models point their barrel along +Z, as glTF faces
/// a model's front, and the controller aims along its -Z, so this node is turned 180 degrees about Y to point a
/// model's barrel along the aim, at the reticle, as <see cref="RiddenDragon"/> turns the dragon to face the flight.
/// Its own shapes are the grey placeholder gun for <see cref="AssetIds.RiderGun"/>, built in the same model axes so
/// that a hand-made model tests the same convention; a model the asset folder supplies is held in their place.
/// The tuning file's <c>gun_pitch</c> tilts it about the grip (<see cref="Pitch"/>), and the reticle follows its barrel.
/// </summary>
public partial class HeldGun : Node3D
{
    private static readonly Color BodyGrey = new(0.5f, 0.5f, 0.5f);

    /// <summary>A paler grey, so the muzzle, and so the front, can be told from the back at a glance.</summary>
    private static readonly Color MuzzleGrey = new(0.82f, 0.82f, 0.82f);

    private Node3D? _placeholder;
    private Node3D? _carried;

    public override void _Ready()
    {
        Pitch(0);
        _placeholder = Placeholder();
        AddChild(_placeholder);
    }

    /// <summary>
    /// Tilts the gun about its grip by <paramref name="degrees"/> from the controller's aim, positive raising the muzzle,
    /// on top of the half turn about Y. A turn about the model's +X lowers +Z, so the muzzle rises for a negative one.
    /// </summary>
    public void Pitch(float degrees) =>
        Transform = new Transform3D(new Basis(Vector3.Up, Mathf.Pi) * new Basis(Vector3.Right, -Mathf.DegToRad(degrees)), Vector3.Zero);

    /// <summary>The way the barrel points, in the scene: the model's +Z.</summary>
    public Vector3 Barrel => GlobalBasis.Z.Normalized();

    /// <summary>
    /// Holds <paramref name="model"/> in place of the placeholder gun, which is no longer drawn; or, with null,
    /// the placeholder again. A model held before is freed: the asset folder is read again after extraction.
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
            SetPlaceholderShown(true);
            return;
        }

        AddChild(model);
        SetPlaceholderShown(false);
    }

    private void SetPlaceholderShown(bool shown)
    {
        if (_placeholder != null)
        {
            _placeholder.Visible = shown;
        }
    }

    /// <summary>
    /// The placeholder gun in model axes: +Y up, its barrel along +Z and its origin at the grip, where the barrel
    /// meets the top of the handle. A barrel about 0.25 m long and 3.5 cm thick on the line through the origin,
    /// starting a little behind it, ending in a paler muzzle, and a handle hanging to about 11.5 cm below the origin.
    /// Boxes of the author's own, nothing from the disc.
    /// </summary>
    private static Node3D Placeholder()
    {
        var body = new StandardMaterial3D { AlbedoColor = BodyGrey };
        var muzzle = new StandardMaterial3D { AlbedoColor = MuzzleGrey };

        var gun = new Node3D { Name = "Placeholder" };
        gun.AddChild(Box("Barrel", new Vector3(0.035f, 0.035f, 0.22f), new Vector3(0, 0, 0.08f), body));
        gun.AddChild(Box("Muzzle", new Vector3(0.04f, 0.04f, 0.03f), new Vector3(0, 0, 0.205f), muzzle));
        gun.AddChild(Box("Grip", new Vector3(0.03f, 0.1f, 0.04f), new Vector3(0, -0.065f, -0.01f), body));
        return gun;
    }

    private static MeshInstance3D Box(string name, Vector3 size, Vector3 centre, Material material) => new()
    {
        Name = name,
        Mesh = new BoxMesh { Size = size },
        MaterialOverride = material,
        Position = centre,
    };
}
