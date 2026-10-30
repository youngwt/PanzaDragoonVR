#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// Holds the dragon the player rides, fixed under the player at the seat while the forest scrolls past.
/// The seat is put below the head, less the tuning file's offsets, when the head is first tracked and after a recentre, and stays there between.
/// Its origin is the seat: a model's origin is put there, as the asset contract's Models section says.
/// The tuning file's offsets move the dragon from there the other way to the player, and its scale turns about the seat.
/// Models face +Z, as glTF does, and the game flies towards -Z, so this node is turned 180 degrees about Y
/// to point a model's front the way of flight.
/// Its own shapes are the grey placeholder dragon for <see cref="AssetIds.DragonRide"/>, built in the same
/// model axes so that a hand-made model tests the same convention; a model the asset folder supplies sits
/// at the seat in their place.
/// </summary>
public partial class RiddenDragon : Node3D
{
    private static readonly Color BodyGrey = new(0.5f, 0.5f, 0.5f);

    /// <summary>A paler grey, so the head, and so the front, can be told from the tail at a glance.</summary>
    private static readonly Color HeadGrey = new(0.82f, 0.82f, 0.82f);

    private Node3D? _placeholder;
    private Node3D? _carried;
    private float _sceneHeight;
    private float _headZ;
    private float _forward;

    public override void _Ready()
    {
        // The turn lives here rather than in the scene, so the convention cannot be lost from the scene.
        Basis = new Basis(Vector3.Up, Mathf.Pi);
        _sceneHeight = Position.Y;
        _placeholder = Placeholder();
        AddChild(_placeholder);
    }

    /// <summary>Puts the seat directly below a head at this world X and Z, with the forward offset; its height stays as it is.</summary>
    public void SitUnder(float headX, float headZ)
    {
        _headZ = headZ;
        GlobalPosition = new Vector3(headX, GlobalPosition.Y, headZ + _forward);
    }

    /// <summary>
    /// Moves the player <paramref name="forward"/> metres towards the head and <paramref name="height"/> metres
    /// up the dragon, by moving the dragon the other way (the game flies towards -Z), and scales it about the seat.
    /// </summary>
    public void Adjust(float forward, float height, float scale)
    {
        _forward = forward;
        Basis = new Basis(Vector3.Up, Mathf.Pi).Scaled(Vector3.One * scale);
        Position = new Vector3(Position.X, _sceneHeight - height, _headZ + forward);
    }

    /// <summary>
    /// Shows <paramref name="model"/> at the seat in place of the placeholder dragon, which is no longer drawn;
    /// or, with null, the placeholder again. A model carried before is freed: the asset folder is read again
    /// after extraction.
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
    /// The placeholder dragon in model axes: +Y up, its front towards +Z, and its origin on top of its back,
    /// half-way along. A body about 4 m long and 1 m wide, a head on a short neck about 2.8 to 3.8 m in front
    /// of the seat, a thinner tail reaching about 3 m behind the body and flat wings about 3 m long either side.
    /// The head's top is about 0.45 m above the seat, so it passes under the board. Boxes of the author's own,
    /// nothing from the disc.
    /// </summary>
    private static Node3D Placeholder()
    {
        var body = new StandardMaterial3D { AlbedoColor = BodyGrey };
        var head = new StandardMaterial3D { AlbedoColor = HeadGrey };

        var dragon = new Node3D { Name = "Placeholder" };
        dragon.AddChild(Box("Body", new Vector3(1.0f, 0.6f, 4.0f), new Vector3(0, -0.3f, 0), body));
        dragon.AddChild(Box("Neck", new Vector3(0.4f, 0.4f, 1.0f), new Vector3(0, -0.15f, 2.4f), body));
        dragon.AddChild(Box("Head", new Vector3(0.5f, 0.5f, 1.0f), new Vector3(0, 0.2f, 3.3f), head));
        dragon.AddChild(Box("Tail", new Vector3(0.3f, 0.25f, 3.0f), new Vector3(0, -0.3f, -3.4f), body));
        dragon.AddChild(Box("LeftWing", new Vector3(3.0f, 0.05f, 1.5f), new Vector3(2.0f, -0.1f, 0.3f), body));
        dragon.AddChild(Box("RightWing", new Vector3(3.0f, 0.05f, 1.5f), new Vector3(-2.0f, -0.1f, 0.3f), body));
        return dragon;
    }

    private static MeshInstance3D Box(string name, Vector3 size, Vector3 centre, Material material) => new()
    {
        Name = name,
        Mesh = new BoxMesh { Size = size },
        MaterialOverride = material,
        Position = centre,
    };
}
