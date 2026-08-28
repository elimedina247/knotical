using Godot;
using Knotical.Debug;

namespace Knotical.Rigging;

[GlobalClass]
public partial class Rope : Node3D
{
	private struct Wrap
	{
		public Node3D Body;
		public Vector3 Local;
		public Vector3 Point;
		public Vector3 Winding;
		public int Age;
		public int Pin;
	}

	[Export(PropertyHint.Range, "3,16,1")]
	public int Sides { get; set; } = 8;

	[Export(PropertyHint.Range, "1,8,1")]
	public int Smoothing { get; set; } = 3;

	[Export(PropertyHint.Range, "0,6,1")]
	public int StrandCount { get; set; } = 3;

	[Export(PropertyHint.Range, "0,0.4,0.01")]
	public float StrandDepth { get; set; } = 0.14f;

	[Export(PropertyHint.Range, "0,20,0.5")]
	public float StrandTwist { get; set; } = 9f;

	[Export(PropertyHint.Range, "20,120,10")]
	public float StepHz { get; set; } = 60f;

	[Export(PropertyHint.Range, "8,64,1")]
	public int Beads { get; set; } = 24;

	[Export(PropertyHint.Range, "0.01,0.1,0.005")]
	public float Radius { get; set; } = 0.03f;

	[Export(PropertyHint.Range, "0,40,0.5")]
	public float Gravity { get; set; } = 12f;

	[Export(PropertyHint.Range, "0.01,1,0.01")]
	public float Damping { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "2,20,1")]
	public int Iterations { get; set; } = 10;

	[Export(PropertyHint.Range, "0.05,1,0.01")]
	public float Bias { get; set; } = 0.25f;

	[Export(PropertyHint.Range, "0.2,20,0.1")]
	public float MaxSnapSpeed { get; set; } = 1.2f;

	[Export(PropertyHint.Range, "0,10,0.1")]
	public float SwayDamping { get; set; } = 1.5f;

	[Export(PropertyHint.Range, "200,50000,100")]
	public float TensileStrength { get; set; } = 4000f;

	[Export] public bool Breaks { get; set; } = true;

	[Export(PropertyHint.Range, "1,20,0.5")]
	public float PullStrength { get; set; } = 8f;

	[Export] public bool PullScalesWithPlayers { get; set; } = true;

	[Export(PropertyHint.Range, "0,3,0.05")]
	public float WalkGive { get; set; } = 1.2f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float HeldLever { get; set; } = 0.35f;

	[Export(PropertyHint.Layers3DPhysics)]
	public uint GroundMask { get; set; } = 15;

	[Export(PropertyHint.Range, "0,24,1")]
	public int MaxWraps { get; set; } = 16;

	private Vector3[] _points;
	private Vector3[] _previous;
	private Vector3[] _trail;
	private float _step;
	private int _windTick;
	private Vector3[] _windAt;
	private bool _asleep;
	private bool _dirty = true;
	private bool _pushed;
	private int _still;
	private float _restLength;
	private Vector3 _restFrom;
	private Vector3 _restTo;
	private Vector3[] _spine;
	private float[] _arcs;
	private float[] _ringCos;
	private float[] _ringSin;
	private float[] _ringAngle;
	private Vector3[] _normals;
	private Vector3[] _binormals;
	private bool[] _locked;
	private Vector3[] _anchors;
	private float[] _rest;
	private Vector3[] _nodes;
	private int[] _nodeIndex;
	private float[] _spans;
	private Node3D _bodyA;
	private Node3D _bodyB;
	private Vector3 _localA;
	private Vector3 _localB;
	private bool _boundA;
	private bool _boundB;
	private bool _held;
	private Vector3 _hand;
	private RigidBody3D _holder;
	private float _length = 2f;
	private float _tension;
	private bool _taut;
	private float _straight;
	private int _overload;
	private MeshInstance3D _skin;
	private ImmediateMesh _mesh;
	private StandardMaterial3D _material;
	private SphereShape3D _probe;
	private PhysicsShapeQueryParameters3D _probeQuery;
	private readonly System.Collections.Generic.List<Wrap> _wraps = new();
	private readonly Godot.Collections.Array<Rid> _excludes = new();

	public static int BeadsFor(float capacity) =>
		Mathf.Clamp(Mathf.RoundToInt(capacity * 2f), 12, 64);

	public float WorkingLength
	{
		get => _length;
		set
		{
			float want = Mathf.Max(value, 0.5f);
			if (Mathf.Abs(want - _length) > 1e-4f) Wake();
			_length = want;
		}
	}

	public void Wake()
	{
		_asleep = false;
		_still = 0;
	}

	public float Tension => _tension;

	public bool Taut => _taut;

	public bool Held => _held;

	public bool BoundStart => _boundA;

	public bool BoundEnd => _boundB;

	public int WrapCount => _wraps.Count;

	public Vector3 WrapAt(int i) => WrapPoint(i);

	public int BeadCount => _points?.Length ?? 0;

	public Vector3 BeadAt(int i) => _points[i];

	public override void _Ready()
	{
		AddToGroup("Ropes");

		_mesh = new ImmediateMesh();
		_material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.72f, 0.6f, 0.42f),
			Roughness = 1f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};

		_skin = new MeshInstance3D
		{
			Name = "Skin",
			Mesh = _mesh,
			TopLevel = true,
			Visible = false,
		};

		AddChild(_skin);
		_skin.GlobalTransform = Transform3D.Identity;

		_probe = new SphereShape3D { Radius = Radius };
		_probeQuery = new PhysicsShapeQueryParameters3D
		{
			Shape = _probe,
			Margin = 0.004f,
		};
	}

	public void BindStart(Node3D body, Vector3 worldPoint)
	{
		Wake();
		_bodyA = body;
		_localA = body.GlobalTransform.AffineInverse() * worldPoint;
		_boundA = true;
	}

	public void BindEnd(Node3D body, Vector3 worldPoint)
	{
		Wake();
		_bodyB = body;
		_localB = body.GlobalTransform.AffineInverse() * worldPoint;
		_boundB = true;
		ReleaseHold();
	}

	public void Hold(Vector3 hand, RigidBody3D holder)
	{
		if (hand.DistanceSquaredTo(_hand) > 1e-8f) Wake();
		_hand = hand;
		_held = true;
		_boundB = false;
		_bodyB = null;

		if (_holder == holder) return;

		_holder = holder;
		_excludes.Clear();
		if (holder != null) _excludes.Add(holder.GetRid());
	}

	public void ReleaseHold()
	{
		Wake();
		_held = false;
		_holder = null;
		_excludes.Clear();
	}

	public void TakeEnd(bool endB, Vector3 hand, RigidBody3D holder)
	{
		if (!endB) Reverse();

		_boundB = false;
		_bodyB = null;
		Hold(hand, holder);
	}

	public Node3D EndBody(bool endB) => endB ? _bodyB : _bodyA;

	public bool TryStart(out Vector3 point)
	{
		if (_boundA && IsInstanceValid(_bodyA))
		{
			point = _bodyA.GlobalTransform * _localA;
			return true;
		}

		point = _points != null ? _points[0] : GlobalPosition;
		return false;
	}

	public Vector3 EndPoint(bool endB)
	{
		if (_points != null) return endB ? _points[^1] : _points[0];
		return endB && _held ? _hand : GlobalPosition;
	}

	public Vector3 PointFromEnd(float back)
	{
		if (_points == null) return _held ? _hand : GlobalPosition;

		float remaining = back;
		for (int i = _points.Length - 1; i > 0; i--)
		{
			Vector3 span = _points[i - 1] - _points[i];
			float step = span.Length();
			if (step >= remaining && step > 1e-5f)
				return _points[i] + span * (remaining / step);
			remaining -= step;
		}

		return _points[0];
	}

	private void Reverse()
	{
		if (_points != null)
		{
			System.Array.Reverse(_points);
			System.Array.Reverse(_previous);
		}

		_wraps.Reverse();

		(_bodyA, _bodyB) = (_bodyB, _bodyA);
		(_localA, _localB) = (_localB, _localA);
		(_boundA, _boundB) = (_boundB, _boundA);
	}

	public override void _PhysicsProcess(double delta)
	{
		_step += (float)delta;

		float budget = 1f / Mathf.Max(StepHz, 1f);
		if (_step < budget) return;

		float dt = Mathf.Min(_step, budget * 4f);
		_step = 0f;
		Validate();

		bool haveA = _boundA;
		Vector3 from = haveA ? _bodyA.GlobalTransform * _localA : Vector3.Zero;

		bool haveB = _boundB || _held;
		Vector3 to = _boundB ? _bodyB.GlobalTransform * _localB : _hand;

		if (!haveA && !haveB)
		{
			_taut = false;
			_wraps.Clear();
			Decay(dt);
			return;
		}

		if (_points == null) Seed(from, to, haveA, haveB);
		if (!haveA) from = _points[0];
		if (!haveB) to = _points[^1];

		if (Settled(from, to)) return;

		PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;

		if (haveA && haveB) { long _p = RopeProfile.Start(); Route(space, from, to); RopeProfile.Stop(0, _p); }
		else _wraps.Clear();

		{ long _p = RopeProfile.Start(); Constrain(dt, haveA, haveB, from, to); RopeProfile.Stop(4, _p); }
		Simulate(dt, space, haveA, from, haveB, to);
	}

	private void Validate()
	{
		if (_boundA && !IsInstanceValid(_bodyA))
		{
			_boundA = false;
			_bodyA = null;
		}

		if (_boundB && !IsInstanceValid(_bodyB))
		{
			_boundB = false;
			_bodyB = null;
		}

		if (_held && (_holder == null || !IsInstanceValid(_holder))) ReleaseHold();
	}

	private void Seed(Vector3 from, Vector3 to, bool haveA, bool haveB)
	{
		int count = Mathf.Max(Beads, 4);
		_points = new Vector3[count];
		_previous = new Vector3[count];

		for (int i = 0; i < count; i++)
		{
			float t = (float)i / (count - 1);

			if (haveA && haveB) _points[i] = from.Lerp(to, t);
			else if (haveA) _points[i] = from + Vector3.Down * (_length * t * 0.5f);
			else _points[i] = to + Vector3.Down * (_length * (1f - t) * 0.5f);

			_previous[i] = _points[i];
		}
	}

	private void Decay(float dt)
	{
		_tension = Mathf.Lerp(_tension, 0f, Mathf.Min(dt * 10f, 1f));
		_overload = 0;
	}

	private void Route(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 to)
	{
		for (int i = _wraps.Count - 1; i >= 0; i--)
		{
			if (_wraps[i].Body != null && !IsInstanceValid(_wraps[i].Body))
			{
				_wraps.RemoveAt(i);
				continue;
			}

			Wrap aged = _wraps[i];
			aged.Age++;
			_wraps[i] = aged;
		}

		Unwind(space, true);
		Unwind(space, false);

		if ((++_windTick & 3) == 0) Wind(space);
	}

	private void Unwind(PhysicsDirectSpaceState3D space, bool endB)
	{
		if (_points == null) return;

		while (_wraps.Count > 0)
		{
			int index = endB ? _wraps.Count - 1 : 0;
			Wrap wrap = _wraps[index];
			if (wrap.Age < 3) break;

			Vector3 pivot = WrapPoint(index);
			int pin = Mathf.Clamp(wrap.Pin, 2, _points.Length - 3);
			Vector3 beadA = _points[pin - 2] - pivot;
			Vector3 beadB = _points[pin + 2] - pivot;

			if (beadA.LengthSquared() < 9e-4f || beadB.LengthSquared() < 9e-4f) break;

			float bead = Turn(beadA, beadB, wrap.Winding);
			bool straight = beadA.Normalized().Dot(beadB.Normalized()) < -0.9f;
			bool release = bead < -0.15f
				|| (straight && Clear(space, _points[pin - 2], _points[pin + 2], out _, out _, out _));

			if (!release) break;
			_wraps.RemoveAt(index);
		}
	}

	private static float Turn(Vector3 inner, Vector3 outward, Vector3 winding)
	{
		Vector3 cross = inner.Cross(outward);
		return cross.LengthSquared() < 1e-10f ? 0f : cross.Normalized().Dot(winding);
	}

	private void Wind(PhysicsDirectSpaceState3D space)
	{
		if (_points == null) return;

		int last = _points.Length - 1;
		bool fresh = _windAt == null || _windAt.Length != _points.Length;
		if (fresh) _windAt = new Vector3[_points.Length];

		for (int i = 0; i < last && _wraps.Count < MaxWraps; i++)
		{
			if (!fresh
				&& _points[i].DistanceSquaredTo(_windAt[i]) < 4e-6f
				&& _points[i + 1].DistanceSquaredTo(_windAt[i + 1]) < 4e-6f) continue;

			if (Clear(space, _points[i], _points[i + 1], out Vector3 point, out Vector3 normal, out Node3D body)) continue;
			if (normal == Vector3.Zero) continue;

			Vector3 position = point + normal * (Radius + 0.02f);

			bool near = false;
			for (int w = 0; w < _wraps.Count && !near; w++)
				near = position.DistanceTo(WrapPoint(w)) < 0.12f;
			if (near) continue;

			Vector3 armA = _points[Mathf.Max(i - 1, 0)] - position;
			Vector3 armB = _points[Mathf.Min(i + 2, last)] - position;
			Vector3 winding = armA.Cross(armB);
			if (winding.LengthSquared() < 1e-10f) continue;

			var wrap = new Wrap
			{
				Body = body,
				Local = body != null ? body.GlobalTransform.AffineInverse() * position : position,
				Point = position,
				Winding = winding.Normalized(),
				Pin = i,
			};

			int at = 0;
			while (at < _wraps.Count && _wraps[at].Pin <= i) at++;

			_wraps.Insert(at, wrap);
		}

		System.Array.Copy(_points, _windAt, _points.Length);
	}

	private bool Clear(
		PhysicsDirectSpaceState3D space,
		Vector3 a,
		Vector3 b,
		out Vector3 point,
		out Vector3 normal,
		out Node3D body)
	{
		point = Vector3.Zero;
		normal = Vector3.Up;
		body = null;

		Vector3 span = b - a;
		float distance = span.Length();
		if (distance < 0.08f) return true;

		Vector3 start = a + span * (0.04f / distance);
		var query = PhysicsRayQueryParameters3D.Create(start, b, GroundMask, _excludes);
		query.HitFromInside = true;
		Godot.Collections.Dictionary hit = space.IntersectRay(query);
		if (hit.Count == 0) return true;

		point = (Vector3)hit["position"];
		normal = (Vector3)hit["normal"];
		body = hit["collider"].As<Node3D>();

		if (normal.LengthSquared() < 1e-6f)
		{
			normal = Vector3.Zero;
			if (point.DistanceTo(start) > 0.02f) return true;
		}

		return false;
	}

	private Vector3 WrapPoint(int i)
	{
		Wrap wrap = _wraps[i];
		return wrap.Body != null && IsInstanceValid(wrap.Body) ? wrap.Body.GlobalTransform * wrap.Local : wrap.Point;
	}

	private float PathLength(Vector3 from, Vector3 to)
	{
		Vector3 previous = from;
		float total = 0f;

		for (int i = 0; i < _wraps.Count; i++)
		{
			Vector3 point = WrapPoint(i);
			total += previous.DistanceTo(point);
			previous = point;
		}

		return total + previous.DistanceTo(to);
	}

	private void Constrain(float dt, bool haveA, bool haveB, Vector3 from, Vector3 to)
	{
		_taut = false;

		if (!haveA || !haveB)
		{
			_straight = 0f;
			Decay(dt);
			return;
		}

		float span = Mathf.Max(_length, 1e-3f);
		float distance = PathLength(from, to);
		_straight = Mathf.Clamp((distance - span * 0.97f) / (span * 0.03f), 0f, 1f);

		float error = distance - _length;

		if (error <= 0f || distance < 1e-4f)
		{
			Decay(dt);
			return;
		}

		_taut = true;

		Vector3 leadA = (_wraps.Count > 0 ? WrapPoint(0) : to) - from;
		Vector3 leadB = to - (_wraps.Count > 0 ? WrapPoint(_wraps.Count - 1) : from);

		if (leadA.LengthSquared() < 1e-8f || leadB.LengthSquared() < 1e-8f)
		{
			Decay(dt);
			return;
		}

		Vector3 dirA = leadA.Normalized();
		Vector3 dirB = leadB.Normalized();
		var rigidA = _bodyA as RigidBody3D;
		RigidBody3D rigidB = _held ? _holder : _bodyB as RigidBody3D;
		Vector3 holdAt = _held && rigidB != null ? Center(rigidB).Lerp(to, HeldLever) : to;

		if (!_held && _bodyA == _bodyB)
		{
			Decay(dt);
			return;
		}

		float grip = _held ? Grip() : 1f;
		float invA = rigidA != null && rigidA.Mass > 0f ? 1f / rigidA.Mass : 0f;
		float invB = rigidB != null && rigidB.Mass > 0f ? 1f / (rigidB.Mass * grip) : 0f;
		float invSum = invA + invB;

		if (invSum <= 0f)
		{
			Decay(dt);
			return;
		}

		Vector3 velocityA = Velocity(rigidA, from);
		Vector3 velocityB = Velocity(rigidB, to);
		float stretch = velocityB.Dot(dirB) - velocityA.Dot(dirA);

		if (SwayDamping > 0f)
		{
			Vector3 sway = velocityB - dirB * velocityB.Dot(dirB) - (velocityA - dirA * velocityA.Dot(dirA));
			Vector3 brake = sway * (Mathf.Min(SwayDamping * dt, 1f) / invSum);
			rigidA?.ApplyImpulse(brake, from - Center(rigidA));
			rigidB?.ApplyImpulse(-brake / grip, holdAt - Center(rigidB));
		}

		float give = Mathf.Min(_length * 0.01f, 0.06f);
		float slop = _held ? WalkGive : 0f;
		float want = -Mathf.Min(Mathf.Max(error - give, 0f) * Bias / dt, MaxSnapSpeed);
		float excess = stretch - want - slop;

		if (excess <= 0f)
		{
			Decay(dt);
			return;
		}

		float impulse = excess / invSum;
		rigidA?.ApplyImpulse(dirA * impulse, from - Center(rigidA));
		rigidB?.ApplyImpulse(dirB * (-impulse / grip), holdAt - Center(rigidB));

		_tension = Mathf.Lerp(_tension, impulse / (dt * grip), Mathf.Min(dt * 10f, 1f));

		if (!Breaks || _tension < TensileStrength)
		{
			_overload = 0;
			return;
		}

		_overload++;
		if (_overload >= 4) Snap();
	}

	private float Grip()
	{
		float strength = PullStrength;

		if (PullScalesWithPlayers)
			strength /= Mathf.Max(GetTree().GetNodesInGroup("Players").Count, 1);

		return Mathf.Max(strength, 1f);
	}

	private void Snap()
	{
		GD.Print($"{Name}: rope parted at {_tension:0} N");

		_overload = 0;
		_tension = 0f;
		_taut = false;

		int last = _points.Length - 1;
		int cut = last / 2;

		if (cut >= 2 && _boundA)
		{
			var debris = new Rope
			{
				Beads = cut + 1,
				Radius = Radius,
				Gravity = Gravity,
				Damping = Damping,
				Iterations = Iterations,
				Bias = Bias,
				MaxSnapSpeed = MaxSnapSpeed,
				TensileStrength = TensileStrength,
				Breaks = Breaks,
				PullStrength = PullStrength,
				PullScalesWithPlayers = PullScalesWithPlayers,
				GroundMask = GroundMask,
				MaxWraps = MaxWraps,
				WorkingLength = _length * cut / last,
				_points = _points[..(cut + 1)],
				_previous = _previous[..(cut + 1)],
				_bodyA = _bodyA,
				_localA = _localA,
				_boundA = true,
			};

			GetParent().AddChild(debris);
		}

		_points = _points[cut..];
		_previous = _previous[cut..];
		_length = Mathf.Max(_length * (last - cut) / last, 0.5f);
		_wraps.Clear();
		_boundA = false;
		_bodyA = null;
	}

	private bool Settled(Vector3 from, Vector3 to)
	{
		if (!_asleep) return false;

		if (from.DistanceSquaredTo(_restFrom) > 1e-6f
			|| to.DistanceSquaredTo(_restTo) > 1e-6f
			|| Mathf.Abs(_length - _restLength) > 1e-4f)
		{
			Wake();
			return false;
		}

		return true;
	}

	private void Rest(Vector3 from, Vector3 to)
	{
		float drift = 0f;

		for (int i = 0; i < _points.Length; i++)
			drift = Mathf.Max(drift, _points[i].DistanceSquaredTo(_trail[i]));

		if (drift > 4e-6f)
		{
			_still = 0;
			_dirty = true;
			return;
		}

		if (++_still < 30) return;

		_asleep = true;
		_restFrom = from;
		_restTo = to;
		_restLength = _length;
	}

	private void Simulate(float dt, PhysicsDirectSpaceState3D space, bool pinA, Vector3 from, bool pinB, Vector3 to)
	{
		int last = _points.Length - 1;
		float retain = Mathf.Pow(Mathf.Clamp(Damping, 0.001f, 1f), dt);
		Vector3 fall = Vector3.Down * (Gravity * dt * dt);

		if (_trail == null || _trail.Length != _points.Length) _trail = new Vector3[_points.Length];
		System.Array.Copy(_points, _trail, _points.Length);

		Shape(pinA, from, pinB, to);

		for (int i = 0; i <= last; i++)
		{
			Vector3 velocity = (_points[i] - _previous[i]) * retain;
			_previous[i] = _points[i];
			_points[i] += velocity + fall;
		}

		{ long _p = RopeProfile.Start(); Relax(Iterations); RopeProfile.Stop(1, _p); }

		if (_straight > 0f && _wraps.Count == 0) Straighten(0.85f * _straight, from, to);

		{ long _p = RopeProfile.Start(); Collide(space); RopeProfile.Stop(2, _p); }
		{ long _p = RopeProfile.Start(); Depenetrate(space); RopeProfile.Stop(3, _p); }
		Relax(Mathf.Max(Iterations / 3, 2));
		if (_pushed) Depenetrate(space);

		Rest(from, to);
	}

	private void Shape(bool pinA, Vector3 from, bool pinB, Vector3 to)
	{
		int count = _points.Length;
		int last = count - 1;

		if (_locked == null || _locked.Length != count)
		{
			_locked = new bool[count];
			_anchors = new Vector3[count];
			_rest = new float[Mathf.Max(last, 1)];
		}

		System.Array.Clear(_locked, 0, count);

		int wraps = Mathf.Min(_wraps.Count, Mathf.Max(last - 1, 0));
		int nodes = wraps + 2;

		if (_nodes == null || _nodes.Length < nodes)
		{
			_nodes = new Vector3[nodes + 4];
			_nodeIndex = new int[nodes + 4];
			_spans = new float[nodes + 4];
		}

		_nodes[0] = from;
		for (int i = 0; i < wraps; i++) _nodes[i + 1] = WrapPoint(i);
		_nodes[nodes - 1] = to;

		float total = 0f;
		for (int i = 0; i < nodes - 1; i++)
		{
			_spans[i] = _nodes[i].DistanceTo(_nodes[i + 1]);
			total += _spans[i];
		}

		_nodeIndex[0] = 0;
		_nodeIndex[nodes - 1] = last;

		float travelled = 0f;
		for (int i = 1; i < nodes - 1; i++)
		{
			travelled += _spans[i - 1];
			int index = total > 1e-4f
				? Mathf.RoundToInt(last * travelled / total)
				: i * last / (nodes - 1);
			_nodeIndex[i] = Mathf.Clamp(index, _nodeIndex[i - 1] + 1, last - (nodes - 1 - i));
		}

		for (int i = 0; i < nodes - 1; i++)
		{
			int links = _nodeIndex[i + 1] - _nodeIndex[i];
			if (links <= 0) continue;

			float share = total > 1e-4f ? _length * _spans[i] / total : _length / (nodes - 1);
			float rest = Mathf.Max(share / links, 1e-4f);
			for (int k = _nodeIndex[i]; k < _nodeIndex[i + 1]; k++) _rest[k] = rest;
		}

		if (pinA)
		{
			_locked[0] = true;
			_anchors[0] = from;
		}

		if (pinB)
		{
			_locked[last] = true;
			_anchors[last] = to;
		}

		for (int i = 1; i < nodes - 1; i++)
		{
			_locked[_nodeIndex[i]] = true;
			_anchors[_nodeIndex[i]] = _nodes[i];

			Wrap wrap = _wraps[i - 1];
			wrap.Pin = _nodeIndex[i];
			_wraps[i - 1] = wrap;
		}
	}

	private void Relax(int iterations)
	{
		int last = _points.Length - 1;

		for (int pass = 0; pass < iterations; pass++)
		{
			Pin();

			for (int i = 0; i < last; i++)
			{
				bool lockLow = _locked[i];
				bool lockHigh = _locked[i + 1];
				if (lockLow && lockHigh) continue;

				Vector3 link = _points[i + 1] - _points[i];
				float distance = link.Length();
				if (distance < 1e-5f) continue;

				Vector3 push = link * ((distance - _rest[i]) / distance);
				if (lockLow) _points[i + 1] -= push;
				else if (lockHigh) _points[i] += push;
				else
				{
					_points[i] += push * 0.5f;
					_points[i + 1] -= push * 0.5f;
				}
			}
		}

		Pin();
	}

	private void Pin()
	{
		for (int i = 0; i < _points.Length; i++)
			if (_locked[i]) _points[i] = _anchors[i];
	}

	private void Collide(PhysicsDirectSpaceState3D space)
	{
		int last = _points.Length - 1;

		for (int i = 0; i <= last; i++)
		{
			if (_locked[i]) continue;

			Vector3 from = _trail[i];
			Vector3 motion = _points[i] - from;
			float length = motion.Length();
			if (length < 1e-3f) continue;

			Vector3 direction = motion / length;
			var query = PhysicsRayQueryParameters3D.Create(
				from,
				from + direction * (length + Radius),
				GroundMask,
				_excludes);

			Godot.Collections.Dictionary hit = space.IntersectRay(query);
			if (hit.Count == 0) continue;

			Vector3 normal = (Vector3)hit["normal"];
			if (normal.LengthSquared() < 1e-6f) continue;

			Vector3 surface = (Vector3)hit["position"] + normal * Radius;
			Vector3 slide = motion - normal * motion.Dot(normal);

			_points[i] = surface;
			_previous[i] = surface - slide * 0.35f;
		}
	}

	private void Depenetrate(PhysicsDirectSpaceState3D space)
	{
		_pushed = false;
		_probe.Radius = Radius;
		_probeQuery.CollisionMask = GroundMask;
		_probeQuery.Exclude = _excludes;

		int last = _points.Length - 1;

		for (int i = 0; i <= last; i++)
		{
			if (_locked[i]) continue;

			_probeQuery.Transform = new Transform3D(Basis.Identity, _points[i]);
			Godot.Collections.Dictionary rest = space.GetRestInfo(_probeQuery);
			if (rest.Count == 0) continue;

			Vector3 normal = (Vector3)rest["normal"];
			if (normal.LengthSquared() < 1e-6f) continue;

			Vector3 contact = (Vector3)rest["point"];
			float depth = Radius - normal.Dot(_points[i] - contact);
			if (depth <= 0f) continue;

			Vector3 motion = _points[i] - _previous[i];
			Vector3 slide = motion - normal * motion.Dot(normal);

			_points[i] += normal * depth;
			_previous[i] = _points[i] - slide * 0.35f;
			_pushed = true;
		}
	}

	private void Straighten(float amount, Vector3 from, Vector3 to)
	{
		int last = _points.Length - 1;

		for (int i = 1; i < last; i++)
		{
			Vector3 line = from.Lerp(to, (float)i / last);
			_points[i] = _points[i].Lerp(line, amount);
			_previous[i] = _points[i];
		}
	}

	public override void _Process(double delta)
	{
		if (_points == null || _points.Length < 2)
		{
			if (_skin != null) _skin.Visible = false;
			return;
		}

		_skin.Visible = true;
		if (!_dirty) return;

		_dirty = false;
		{ long _p = RopeProfile.Start(); BuildTube(); RopeProfile.Stop(5, _p); }
	}

	private void BuildTube()
	{
		int count = Spine();
		if (count < 2) return;
		RopeProfile.Count(6, count);
		RopeProfile.Count(7, (count - 1) * Mathf.Clamp(Sides, 3, 16) * 6);

		if (_normals == null || _normals.Length < count)
		{
			_normals = new Vector3[count];
			_binormals = new Vector3[count];
			_arcs = new float[count];
		}

		Vector3 normal = Vector3.Right;
		float arc = 0f;

		for (int i = 0; i < count; i++)
		{
			Vector3 ahead = _spine[Mathf.Min(i + 1, count - 1)] - _spine[Mathf.Max(i - 1, 0)];
			Vector3 tangent = ahead.LengthSquared() > 1e-8f ? ahead.Normalized() : Vector3.Up;

			normal -= tangent * normal.Dot(tangent);
			if (normal.LengthSquared() < 1e-6f) normal = tangent.Cross(Vector3.Up);
			if (normal.LengthSquared() < 1e-6f) normal = tangent.Cross(Vector3.Right);
			normal = normal.Normalized();

			if (i > 0) arc += _spine[i].DistanceTo(_spine[i - 1]);

			_normals[i] = normal;
			_binormals[i] = tangent.Cross(normal);
			_arcs[i] = arc;
		}

		int sides = Mathf.Clamp(Sides, 3, 16);
		Ring(sides);

		_mesh.ClearSurfaces();
		_mesh.SurfaceBegin(Godot.Mesh.PrimitiveType.Triangles, _material);

		for (int i = 0; i < count - 1; i++)
		{
			for (int s = 0; s < sides; s++)
			{
				int n = (s + 1) % sides;

				Strand(i, s, out Vector3 a, out Vector3 dirA);
				Strand(i, n, out Vector3 b, out Vector3 dirB);
				Strand(i + 1, s, out Vector3 c, out Vector3 dirC);
				Strand(i + 1, n, out Vector3 d, out Vector3 dirD);

				_mesh.SurfaceSetNormal(dirA);
				_mesh.SurfaceAddVertex(a);
				_mesh.SurfaceSetNormal(dirC);
				_mesh.SurfaceAddVertex(c);
				_mesh.SurfaceSetNormal(dirD);
				_mesh.SurfaceAddVertex(d);

				_mesh.SurfaceSetNormal(dirA);
				_mesh.SurfaceAddVertex(a);
				_mesh.SurfaceSetNormal(dirD);
				_mesh.SurfaceAddVertex(d);
				_mesh.SurfaceSetNormal(dirB);
				_mesh.SurfaceAddVertex(b);
			}
		}

		_mesh.SurfaceEnd();
	}

	private void Strand(int i, int s, out Vector3 vertex, out Vector3 direction)
	{
		direction = _normals[i] * _ringCos[s] + _binormals[i] * _ringSin[s];
		float ripple = 1f + StrandDepth * Mathf.Cos(StrandCount * (_ringAngle[s] + _arcs[i] * StrandTwist));
		vertex = _spine[i] + direction * (Radius * ripple);
	}

	private void Ring(int sides)
	{
		if (_ringCos != null && _ringCos.Length == sides) return;

		_ringCos = new float[sides];
		_ringSin = new float[sides];
		_ringAngle = new float[sides];

		for (int s = 0; s < sides; s++)
		{
			float angle = Mathf.Tau * s / sides;
			_ringAngle[s] = angle;
			_ringCos[s] = Mathf.Cos(angle);
			_ringSin[s] = Mathf.Sin(angle);
		}
	}

	private int Spine()
	{
		int beads = _points.Length;
		int last = beads - 1;
		if (last < 1) return 0;

		float link = _length / last;
		int steps = Mathf.Clamp(Mathf.RoundToInt(link / 0.35f), 1, Mathf.Max(Smoothing, 1));

		int count = steps > 1 ? last * steps + 1 : beads;
		if (_spine == null || _spine.Length < count) _spine = new Vector3[count];

		if (steps <= 1)
		{
			System.Array.Copy(_points, _spine, beads);
			return beads;
		}

		int at = 0;

		for (int i = 0; i < last; i++)
		{
			Vector3 p0 = _points[Mathf.Max(i - 1, 0)];
			Vector3 p1 = _points[i];
			Vector3 p2 = _points[i + 1];
			Vector3 p3 = _points[Mathf.Min(i + 2, last)];

			for (int s = 0; s < steps; s++)
				_spine[at++] = Catmull(p0, p1, p2, p3, (float)s / steps);
		}

		_spine[at++] = _points[last];
		return at;
	}

	private static Vector3 Catmull(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t;
		float t3 = t2 * t;

		return 0.5f * (2f * p1
			+ (p2 - p0) * t
			+ (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
			+ (3f * p1 - p0 - 3f * p2 + p3) * t3);
	}

	private static Vector3 Velocity(RigidBody3D body, Vector3 at)
	{
		if (body == null) return Vector3.Zero;
		return body.LinearVelocity + body.AngularVelocity.Cross(at - Center(body));
	}

	private static Vector3 Center(RigidBody3D body) =>
		body.CenterOfMassMode == RigidBody3D.CenterOfMassModeEnum.Custom
			? body.GlobalTransform * body.CenterOfMass
			: body.GlobalPosition;
}
