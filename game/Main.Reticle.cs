#nullable enable
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The aiming reticle, in VR only: a ring the game draws itself, where the left controller's aim ray leaves a
/// sphere of <see cref="ReticlePlacement.Radius"/> around the head, facing the head and drawn over the scene.
/// It hides while the left controller is not tracked.
/// </summary>
public partial class Main
{
    private MeshInstance3D? _reticle;

    /// <summary>
    /// A ring on a quad, unlit and drawn without a depth test so the forest, the dragon and the cube never hide it.
    /// The frame-rate readout and the tuning list draw over it.
    /// </summary>
    private const string ReticleShader = """
        shader_type spatial;
        render_mode unshaded, depth_test_disabled, cull_disabled, shadows_disabled, blend_mix;

        void fragment() {
            float r = length(UV - vec2(0.5)) * 2.0;
            float edge = fwidth(r);
            float ring = smoothstep(0.62 - edge, 0.62 + edge, r) * (1.0 - smoothstep(0.92 - edge, 0.92 + edge, r));
            float centre = 1.0 - smoothstep(0.12 - edge, 0.12 + edge, r);
            // A dark band behind the white ring keeps it readable against the pale sky.
            float backing = smoothstep(0.52 - edge, 0.52 + edge, r) * (1.0 - smoothstep(1.0 - edge, 1.0, r));
            float shape = max(ring, centre);
            ALBEDO = vec3(shape);
            ALPHA = max(shape, backing * 0.6);
        }
        """;

    /// <summary>Builds the reticle, hidden until the left controller is tracked. Called only once OpenXR has started.</summary>
    private void StartReticle()
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = ReticleShader }, RenderPriority = 5 };
        _reticle = new MeshInstance3D
        {
            Name = "Reticle",
            Mesh = new QuadMesh { Size = new Vector2(ReticlePlacement.Size, ReticlePlacement.Size) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        AddChild(_reticle);
    }

    /// <summary>One frame of the reticle: placed from the left hand's aim and the head's position, or hidden.</summary>
    private void PlaceReticle()
    {
        if (_reticle == null)
        {
            return;
        }

        XRController3D hand = LeftHand;
        if (!hand.GetIsActive() || !hand.GetHasTrackingData())
        {
            _reticle.Visible = false;
            return;
        }

        Transform3D aim = hand.GlobalTransform;
        Vector3 forward = -aim.Basis.Z;
        Transform3D camera = Camera.GlobalTransform;
        Vector3 head = camera.Origin;
        ReticlePoint? point = ReticlePlacement.OnSphere(
            aim.Origin.X, aim.Origin.Y, aim.Origin.Z, forward.X, forward.Y, forward.Z, head.X, head.Y, head.Z);
        if (point is not { } p)
        {
            _reticle.Visible = false;
            return;
        }

        var at = new Vector3(p.X, p.Y, p.Z);
        // The quad's front is +Z: turn it to the head, keeping it upright to the head's own up where it can.
        Vector3 z = (head - at).Normalized();
        Vector3 x = camera.Basis.Y.Cross(z);
        if (x.LengthSquared() < 1e-6f)
        {
            x = camera.Basis.X;
        }

        x = x.Normalized();
        Vector3 y = z.Cross(x);
        _reticle.GlobalTransform = new Transform3D(new Basis(x, y, z), at);
        _reticle.Visible = true;
    }
}
