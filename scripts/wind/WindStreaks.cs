using Godot;
using OceanSystem = Knotical.Ocean.Ocean;

namespace Knotical.Weather;

[GlobalClass]
public partial class WindStreaks : MultiMeshInstance3D
{
	[Export] public float SpawnIntervalSeconds { get; set; } = 10f;

	[Export(PropertyHint.Range, "0,1,0.05")]
	public float SpawnJitter { get; set; } = 0.35f;

	[Export(PropertyHint.Range, "1,64,1")]
	public int MaxAlive { get; set; } = 8;

	[Export(PropertyHint.Range, "10,200,1")]
	public float Radius { get; set; } = 55f;

	[Export] public float HeightAbove { get; set; } = 12f;

	[Export] public float HeightBelow { get; set; } = 8f;

	[Export] public float MinAboveWater { get; set; } = 0.6f;

	[Export] public float StreakLength { get; set; } = 8f;

	[Export] public float StreakWidth { get; set; } = 0.12f;

	[Export] public float MinLifetime { get; set; } = 5f;

	[Export] public float MaxLifetime { get; set; } = 25f;

	[Export] public float FadeSeconds { get; set; } = 1.4f;

	[Export(PropertyHint.Range, "0,2,0.05")]
	public float DriftScale { get; set; } = 1f;

	[Export(PropertyHint.Range, "0,1,0.01")]
	public float Opacity { get; set; } = 0.45f;

	[Export] public float CalmSpeed { get; set; } = 1.5f;

	[Export] public float HeadingJitterDeg { get; set; } = 7f;

	[Export(PropertyHint.Range, "2,32,1")]
	public int Segments { get; set; } = 10;

	[Export] public Color Tint { get; set; } = new("#EAF6FF");

	[Export] public Shader StreakShader { get; set; }

	private Vector3[] _positions;
	private bool[] _alive;
	private float[] _ages;
	private float[] _lives;
	private float[] _phases;
	private float[] _lengthMuls;
	private float[] _headings;

	private MultiMesh _multiMesh;
	private ShaderMaterial _material;
	private readonly RandomNumberGenerator _rng = new();
	private float _spawnTimer;

	public override void _Ready()
	{
		_rng.Randomize();

		_material = new ShaderMaterial
		{
			Shader = StreakShader ?? GD.Load<Shader>("res://shaders/wind_streaks.gdshader")
		};

		_material.SetShaderParameter("tint", Tint);
		_material.SetShaderParameter("streak_width", StreakWidth);

		_multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			UseColors = true,
			UseCustomData = true,
			Mesh = BuildStrip(Segments)
		};

		_multiMesh.InstanceCount = MaxAlive;

		Multimesh = _multiMesh;
		MaterialOverride = _material;
		CastShadow = ShadowCastingSetting.Off;
		GIMode = GIModeEnum.Disabled;
		TopLevel = true;
		Transform = Transform3D.Identity;

		_positions = new Vector3[MaxAlive];
		_alive = new bool[MaxAlive];
		_ages = new float[MaxAlive];
		_lives = new float[MaxAlive];
		_phases = new float[MaxAlive];
		_lengthMuls = new float[MaxAlive];
		_headings = new float[MaxAlive];

		for (int i = 0; i < MaxAlive; i++) Retire(i);

		_spawnTimer = _rng.RandfRange(0f, SpawnIntervalSeconds);
	}

	public override void _Process(double delta)
	{
		Wind wind = Wind.Instance;
		Camera3D camera = GetViewport().GetCamera3D();

		if (wind == null || camera == null || _alive == null || _alive.Length == 0)
		{
			Visible = false;
			return;
		}

		Visible = true;

		float dt = (float)delta;
		Vector3 eye = camera.GlobalPosition;

		float speedGate = Mathf.SmoothStep(CalmSpeed, CalmSpeed + 3f, wind.Speed) * Opacity;

		_spawnTimer -= dt;
		if (_spawnTimer <= 0f)
		{
			if (speedGate > 0.01f) Spawn(eye, wind);
			_spawnTimer = SpawnIntervalSeconds
				* _rng.RandfRange(1f - SpawnJitter, 1f + SpawnJitter)
				* Mathf.Clamp(9f / Mathf.Max(wind.Speed, 0.5f), 0.6f, 2.5f);
		}

		Vector2 windVelocity = wind.Velocity * DriftScale;
		var drift = new Vector3(windVelocity.X, 0f, windVelocity.Y);

		float weatherTime = (float)wind.Time;
		float baseHeading = wind.DirectionRad;
		float span = Mathf.Clamp(StreakLength * wind.Speed / 8f, StreakLength * 0.45f, StreakLength * 2f);
		float fade = Mathf.Min(FadeSeconds, MinLifetime * 0.4f);
		float cullRadius = Radius * 1.15f;

		OceanSystem ocean = OceanSystem.Instance;

		for (int i = 0; i < _alive.Length; i++)
		{
			if (!_alive[i])
			{
				_multiMesh.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Zero, Vector3.Zero, Vector3.Zero), eye));
				_multiMesh.SetInstanceColor(i, new Color(1f, 1f, 1f, 0f));
				continue;
			}

			_ages[i] += dt;
			_positions[i] += drift * dt;
			_positions[i].Y += Mathf.Sin(weatherTime * 0.6f + _phases[i] * Mathf.Tau) * 0.6f * dt;

			float dx = _positions[i].X - eye.X;
			float dz = _positions[i].Z - eye.Z;
			float dy = _positions[i].Y - eye.Y;

			bool gone = _ages[i] >= _lives[i]
				|| dx * dx + dz * dz > cullRadius * cullRadius
				|| dy > HeightAbove * 1.6f
				|| dy < -HeightBelow * 1.6f;

			if (gone)
			{
				Retire(i);
				_multiMesh.SetInstanceTransform(i, new Transform3D(new Basis(Vector3.Zero, Vector3.Zero, Vector3.Zero), eye));
				_multiMesh.SetInstanceColor(i, new Color(1f, 1f, 1f, 0f));
				continue;
			}

			if (ocean != null)
			{
				float sea = ocean.GetRenderedHeight(new Vector2(_positions[i].X, _positions[i].Z));
				_positions[i].Y = Mathf.Max(_positions[i].Y, sea + MinAboveWater);
			}

			float remaining = _lives[i] - _ages[i];
			float lifeFade = Mathf.Clamp(Mathf.Min(_ages[i], remaining) / Mathf.Max(fade, 0.01f), 0f, 1f);

			float distance = Mathf.Sqrt(dx * dx + dz * dz);
			float radial = 1f - Mathf.SmoothStep(Radius * 0.7f, cullRadius, distance);

			float heading = baseHeading + _headings[i];
			var direction = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
			var side = new Vector3(-direction.Z, 0f, direction.X);
			var basis = new Basis(direction * span * _lengthMuls[i], Vector3.Up, side);

			_multiMesh.SetInstanceTransform(i, new Transform3D(basis, _positions[i]));
			_multiMesh.SetInstanceColor(i, new Color(1f, 1f, 1f, lifeFade * radial * speedGate));
			_multiMesh.SetInstanceCustomData(i, new Color(_phases[i], 0f, 0f, 0f));
		}

		float reach = cullRadius + StreakLength * 2f;
		float vertical = Mathf.Max(HeightAbove, HeightBelow) * 1.8f + StreakLength;

		CustomAabb = new Aabb(
			new Vector3(eye.X - reach, eye.Y - vertical, eye.Z - reach),
			new Vector3(reach * 2f, vertical * 2f, reach * 2f));
	}

	private void Spawn(Vector3 eye, Wind wind)
	{
		int index = -1;
		for (int i = 0; i < _alive.Length; i++)
		{
			if (!_alive[i]) { index = i; break; }
		}

		if (index < 0) return;

		Vector2 toward = wind.Direction;
		var across = new Vector2(-toward.Y, toward.X);

		Vector2 offset = toward * -Radius * _rng.RandfRange(0.7f, 0.95f)
			+ across * Radius * _rng.RandfRange(-0.75f, 0.75f);

		_positions[index] = new Vector3(
			eye.X + offset.X,
			eye.Y + _rng.RandfRange(-HeightBelow, HeightAbove),
			eye.Z + offset.Y);

		_alive[index] = true;
		_ages[index] = 0f;
		_lives[index] = Mathf.Clamp(
			2.4f * Radius / Mathf.Max(wind.Speed * DriftScale, 1f),
			MinLifetime,
			MaxLifetime);
		_phases[index] = _rng.Randf();
		_lengthMuls[index] = _rng.RandfRange(0.7f, 1.6f);
		_headings[index] = Mathf.DegToRad(_rng.RandfRange(-HeadingJitterDeg, HeadingJitterDeg));
	}

	private void Retire(int index)
	{
		_alive[index] = false;
		_ages[index] = 0f;
		_lives[index] = 0f;
	}

	private static ArrayMesh BuildStrip(int segments)
	{
		segments = Mathf.Max(segments, 2);

		int columns = segments + 1;
		var positions = new Vector3[columns * 2];
		var uvs = new Vector2[columns * 2];
		var indices = new int[segments * 6];

		for (int i = 0; i < columns; i++)
		{
			float t = (float)i / segments;

			positions[i * 2] = new Vector3(t - 0.5f, -0.5f, 0f);
			positions[i * 2 + 1] = new Vector3(t - 0.5f, 0.5f, 0f);
			uvs[i * 2] = new Vector2(t, 0f);
			uvs[i * 2 + 1] = new Vector2(t, 1f);
		}

		int k = 0;
		for (int i = 0; i < segments; i++)
		{
			int a = i * 2;
			int b = a + 1;
			int c = a + 2;
			int d = a + 3;

			indices[k++] = a; indices[k++] = b; indices[k++] = c;
			indices[k++] = c; indices[k++] = b; indices[k++] = d;
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = positions;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.CustomAabb = new Aabb(new Vector3(-0.7f, -1f, -1f), new Vector3(1.4f, 2f, 2f));

		return mesh;
	}
}
