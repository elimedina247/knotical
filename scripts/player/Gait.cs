using Godot;

namespace Knotical.Player;

[GlobalClass]
public partial class Gait : Node3D
{
    [Export(PropertyHint.Range, "0.1,1.5,0.01")] public float StrideLength { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.05,0.5,0.01")] public float StepTime { get; set; } = 0.11f;

    [Export(PropertyHint.Range, "0,0.4,0.005")] public float StepHeight { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "0,0.4,0.01")] public float LeadTime { get; set; } = 0.11f;

    [Export(PropertyHint.Range, "0.01,0.2,0.005")] public float FootLift { get; set; } = 0.055f;

    [Export(PropertyHint.Range, "1,1.3,0.01")] public float KneeSlack { get; set; } = 1.06f;

    [Export(PropertyHint.Range, "0,60,0.5")] public float DangleGravity { get; set; } = 18f;

    [Export(PropertyHint.Range, "0.01,1,0.01")] public float DangleDamping { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0.1,1,0.01")] public float DangleStiffness { get; set; } = 0.85f;

    [Export(PropertyHint.Range, "0.2,4,0.05")] public float KickRate { get; set; } = 1.9f;

    [Export(PropertyHint.Range, "0,0.4,0.005")] public float KickAmplitude { get; set; } = 0.16f;

    [Export(PropertyHint.Range, "0,1,0.01")] public float KickTrail { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0.1,2,0.05")] public float StrokeRate { get; set; } = 0.55f;

    private sealed class Leg
    {
        public Vector3 HipLocal;
        public MeshInstance3D Thigh;
        public MeshInstance3D Ball;
        public MeshInstance3D Shin;
        public MeshInstance3D Foot;
        public float RestThigh = 1f;
        public float RestShin = 1f;
        public float FootNose = -0.05f;
        public LimbChain Chain = new();
        public Node3D Deck;
        public Vector3 PlantLocal;
        public Vector3 PlantWorld;
        public bool HasPlant;
        public bool Stepping;
        public float StepBlend;
        public Vector3 StepFrom;
    }

    private static readonly Basis FootRoll = new(Vector3.Right, Mathf.Pi / 2f);

    private static readonly Vector3[] StrokeKeys =
    {
        new(0.10f, 0.95f, -0.42f),
        new(0.42f, 0.88f, -0.26f),
        new(0.36f, 0.72f, 0.04f),
        new(0.12f, 0.78f, -0.08f),
    };

    private static readonly float[] StrokeSpans = { 0.3f, 0.15f, 0.25f, 0.3f };

    private PlayerBody _body;
    private PlayerGrab _grab;
    private DangleArm _armLeft;
    private DangleArm _armRight;
    private readonly Leg[] _legs = new Leg[2];
    private readonly Godot.Collections.Array<Rid> _excludes = new();
    private float _span;
    private float _half;
    private float _kick;
    private float _stroke;

    public override void _Ready()
    {
        _body = GetParentOrNull<PlayerBody>();
        if (_body != null) _excludes.Add(_body.GetRid());

        _grab = _body?.GetNodeOrNull<PlayerGrab>("Grab");
        _armLeft = _body?.GetNodeOrNull<DangleArm>("ArmLeft");
        _armRight = _body?.GetNodeOrNull<DangleArm>("ArmRight");

        _legs[0] = Wire("LegLeft");
        _legs[1] = Wire("LegRight");

        float hip = _legs[0]?.HipLocal.Y ?? 0.5f;
        _span = Mathf.Max(hip - FootLift, 0.1f) * KneeSlack;
        _half = _span * 0.5f;

        foreach (Leg leg in _legs)
            leg?.Chain.Build(2, _span, GlobalPosition, Vector3.Down);
    }

    private Leg Wire(string name)
    {
        Node3D root = GetParentOrNull<Node3D>()?.GetNodeOrNull<Node3D>(name);
        if (root == null) return null;

        var leg = new Leg
        {
            HipLocal = root.Position,
            Thigh = root.GetNodeOrNull<MeshInstance3D>("Thigh"),
            Ball = root.GetNodeOrNull<MeshInstance3D>("Knee/Ball"),
            Shin = root.GetNodeOrNull<MeshInstance3D>("Knee/Shin"),
            Foot = root.GetNodeOrNull<MeshInstance3D>("Knee/Foot"),
        };

        if (leg.Thigh != null) leg.RestThigh = Mathf.Max(leg.Thigh.GetAabb().Size.Y, 1e-3f);
        if (leg.Shin != null) leg.RestShin = Mathf.Max(leg.Shin.GetAabb().Size.Y, 1e-3f);
        if (leg.Foot != null) leg.FootNose = leg.Foot.Position.Z;

        return leg;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_body == null) return;

        float dt = (float)delta;
        bool swimming = _body.IsSwimming;
        bool footing = _body.IsGrounded && !_body.IsDowned && !swimming;
        Basis facing = FlatFacing();
        Vector3 forward = -facing.Z;

        float effort = 0f;
        if (swimming)
        {
            effort = Mathf.Clamp(
                _body.PlanarVelocity.Length() / Mathf.Max(_body.SwimSpeed, 0.1f), 0f, 1f);
            _kick = Mathf.PosMod(
                _kick + Mathf.Tau * KickRate * (0.45f + 0.55f * effort) * dt, Mathf.Tau);
            _stroke = Mathf.PosMod(
                _stroke + Mathf.Tau * StrokeRate * (0.6f + 0.4f * effort) * dt, Mathf.Tau);

            if (HandsFree()) Breaststroke();
        }

        for (int i = 0; i < 2; i++)
        {
            Leg leg = _legs[i];
            if (leg == null) continue;

            Vector3 hip = _body.GlobalTransform * leg.HipLocal;

            if (footing) Stand(leg, _legs[1 - i], hip, forward, dt);
            else if (swimming) Flutter(leg, i, hip, forward, effort);
            else Dangle(leg, hip, dt);

            Vector3 knee = leg.Chain.Points[1];
            Vector3 ankle = leg.Chain.Points[2];

            Fit(leg.Thigh, hip, knee, leg.RestThigh);
            Fit(leg.Shin, knee, ankle, leg.RestShin);
            if (leg.Ball != null) leg.Ball.GlobalPosition = knee;

            if (leg.Foot == null) continue;

            if (swimming)
            {
                Basis shin = Aim(ankle - knee);
                var toes = new Basis(shin.X, -shin.Z, shin.Y);
                leg.Foot.GlobalTransform = new Transform3D(
                    toes * FootRoll,
                    ankle + shin.Y * -leg.FootNose);
            }
            else
            {
                leg.Foot.GlobalTransform = new Transform3D(
                    facing * FootRoll,
                    ankle + facing * new Vector3(0f, 0f, leg.FootNose));
            }
        }
    }

    private void Flutter(Leg leg, int index, Vector3 hip, Vector3 forward, float effort)
    {
        leg.HasPlant = false;
        leg.Stepping = false;

        Vector3 planar = _body.PlanarVelocity;
        Vector3 heading = planar.LengthSquared() > 0.04f ? planar.Normalized() : forward;
        Vector3 rest = (Vector3.Down - heading * (KickTrail * effort)).Normalized();
        float amplitude = KickAmplitude * (0.55f + 0.45f * effort);

        Vector3 ankle = hip
            + rest * (_span * 0.94f)
            + heading * (Mathf.Sin(_kick + index * Mathf.Pi) * amplitude);

        Pose(leg, hip, ankle, forward);
    }

    private bool HandsFree() => _grab == null || (!_grab.HandsEngaged && !_grab.Locked);

    private void Breaststroke()
    {
        Vector3 local = SampleStroke(_stroke / Mathf.Tau);
        Transform3D body = _body.GlobalTransform;

        _armLeft?.Grip(body * new Vector3(-local.X, local.Y, local.Z));
        _armRight?.Grip(body * local);
    }

    private static Vector3 SampleStroke(float t)
    {
        for (int i = 0; i < StrokeSpans.Length; i++)
        {
            if (t > StrokeSpans[i])
            {
                t -= StrokeSpans[i];
                continue;
            }

            Vector3 a = StrokeKeys[i];
            Vector3 b = StrokeKeys[(i + 1) % StrokeKeys.Length];
            return a.Lerp(b, Mathf.SmoothStep(0f, 1f, t / StrokeSpans[i]));
        }

        return StrokeKeys[0];
    }

    private void Stand(Leg leg, Leg other, Vector3 hip, Vector3 forward, float dt)
    {
        Vector3 ground = Probe(hip);
        Vector3 lead = _body.PlanarVelocity * LeadTime;
        if (lead.Length() > StrideLength) lead = lead.Normalized() * StrideLength;
        Vector3 home = ground + lead;

        if (!leg.HasPlant) Plant(leg, ground);

        Vector3 planted = PlantedWorld(leg);

        if (leg.Stepping)
        {
            leg.StepBlend = Mathf.Min(leg.StepBlend + dt / Mathf.Max(StepTime, 1e-3f), 1f);
            float t = Mathf.SmoothStep(0f, 1f, leg.StepBlend);
            Vector3 foot = leg.StepFrom.Lerp(home, t)
                + Vector3.Up * (StepHeight * Mathf.Sin(leg.StepBlend * Mathf.Pi));

            if (leg.StepBlend >= 1f)
            {
                leg.Stepping = false;
                Plant(leg, home);
                foot = home;
            }

            Pose(leg, hip, foot + Vector3.Up * FootLift, forward);
            return;
        }

        Vector3 error = home - planted;
        error -= Vector3.Up * error.Dot(Vector3.Up);
        float drift = error.Length();

        float speed = Mathf.Max(_body.PlanarVelocity.Length(), 0.5f);
        float near = StrideLength * 0.5f;
        float far = near + speed * StepTime * 1.3f;
        bool free = other == null || !other.Stepping;

        if ((free && drift > near) || drift > far)
        {
            leg.Stepping = true;
            leg.StepBlend = 0f;
            leg.StepFrom = planted;
        }

        Pose(leg, hip, planted + Vector3.Up * FootLift, forward);
    }

    private void Dangle(Leg leg, Vector3 hip, float dt)
    {
        leg.HasPlant = false;
        leg.Stepping = false;

        LimbChain chain = leg.Chain;
        chain.Gravity = DangleGravity;
        chain.Damping = DangleDamping;
        chain.Stiffness = DangleStiffness;
        chain.Step(dt, hip, false, Vector3.Zero);
    }

    private void Plant(Leg leg, Vector3 world)
    {
        leg.HasPlant = true;
        leg.PlantWorld = world;
        leg.Deck = _body.Deck;
        leg.PlantLocal = leg.Deck != null && IsInstanceValid(leg.Deck)
            ? leg.Deck.GlobalTransform.AffineInverse() * world
            : world;
    }

    private Vector3 PlantedWorld(Leg leg)
    {
        if (leg.Deck != null && IsInstanceValid(leg.Deck))
            leg.PlantWorld = leg.Deck.GlobalTransform * leg.PlantLocal;

        if (leg.Deck != _body.Deck) Plant(leg, leg.PlantWorld);

        return leg.PlantWorld;
    }

    private Vector3 Probe(Vector3 hip)
    {
        var query = PhysicsRayQueryParameters3D.Create(
            hip + Vector3.Up * 0.2f,
            hip + Vector3.Down * (_span + 0.35f),
            uint.MaxValue,
            _excludes);

        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0) return (Vector3)hit["position"];

        return hip + Vector3.Down * (_span / Mathf.Max(KneeSlack, 1f));
    }

    private void Pose(Leg leg, Vector3 hip, Vector3 ankle, Vector3 forward)
    {
        Vector3 knee = Bend(hip, ankle, forward);

        LimbChain chain = leg.Chain;
        chain.Previous[0] = chain.Points[0];
        chain.Previous[1] = chain.Points[1];
        chain.Previous[2] = chain.Points[2];
        chain.Points[0] = hip;
        chain.Points[1] = knee;
        chain.Points[2] = ankle;
    }

    private Vector3 Bend(Vector3 hip, Vector3 ankle, Vector3 forward)
    {
        Vector3 span = ankle - hip;
        float distance = span.Length();
        if (distance < 1e-4f) return hip + forward * _half;

        Vector3 direction = span / distance;
        float along = distance * 0.5f;

        if (distance >= _span) return hip + direction * along;

        float outward = Mathf.Sqrt(Mathf.Max(_half * _half - along * along, 0f));

        Vector3 pole = forward - direction * forward.Dot(direction);
        if (pole.LengthSquared() < 1e-6f) pole = Vector3.Up - direction * Vector3.Up.Dot(direction);
        if (pole.LengthSquared() < 1e-6f) pole = Vector3.Right;

        return hip + direction * along + pole.Normalized() * outward;
    }

    private Basis FlatFacing()
    {
        Vector3 forward = -_body.GlobalBasis.Z;
        forward -= Vector3.Up * forward.Y;
        forward = forward.LengthSquared() > 1e-6f ? forward.Normalized() : Vector3.Forward;
        return Basis.LookingAt(forward, Vector3.Up);
    }

    private static void Fit(MeshInstance3D mesh, Vector3 a, Vector3 b, float rest)
    {
        if (mesh == null) return;

        Vector3 span = b - a;
        float length = span.Length();
        if (length < 1e-5f) return;

        Basis aim = Aim(span);
        var stretched = new Basis(aim.X, aim.Y * (length / rest), aim.Z);
        mesh.GlobalTransform = new Transform3D(stretched, (a + b) * 0.5f);
    }

    private static Basis Aim(Vector3 direction)
    {
        Vector3 y = direction.Normalized();
        Vector3 reference = Mathf.Abs(y.Dot(Vector3.Forward)) > 0.99f ? Vector3.Right : Vector3.Forward;
        Vector3 x = reference.Cross(y).Normalized();
        return new Basis(x, y, x.Cross(y));
    }
}
