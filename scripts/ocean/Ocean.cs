using Godot;

namespace Knotical.Ocean;

/// <summary>
/// The single source of truth for the sea surface.
///
/// The wave field is a pure function of world position, time, and authored settings,
/// which is why it needs almost no networking: sync <see cref="Time"/> and every client
/// renders and simulates an identical ocean. Wind never touches the waves — calm and
/// rough regions are authored, derived from island shallows and <see cref="SeaZone"/>
/// nodes. This class owns that function on the CPU; ocean.gdshader runs the same maths
/// on the GPU. If you change one, change the other.
///
/// Register as an autoload named "Ocean".
/// </summary>
public partial class Ocean : Node
{
	/// <summary>Half-extent of the playable world in metres. Coordinates run -6000..+6000.</summary>
	public const float WorldHalfExtent = 6000f;

	/// <summary>
	/// World Y of still water. Non-zero so terrain heightmaps, which start at zero and
	/// cannot be moved off the origin, read as the deep sea floor: sculpting up from 0
	/// climbs toward the surface, and land is anything above this.
	/// </summary>
	public const float SeaLevel = 30f;

	/// <summary>Array size for the sea-scale field uniforms. Must match ocean.gdshader.</summary>
	public const int MaxSeaSources = 16;

	private const float Gravity = 9.81f;

	public static Ocean Instance { get; private set; }

	[Export] public OceanSettings Settings { get; set; }

	/// <summary>
	/// Seconds of wave time. Advanced locally, but authoritative on the host once
	/// networking lands — clients adopt the host's value rather than their own clock.
	/// </summary>
	public double Time { get; private set; }

	/// <summary>Combined significant wave height in metres, for shading and audio.</summary>
	public float SignificantHeight { get; private set; }

	/// <summary>Medium-band peak wavelength in metres, for HUD display.</summary>
	public float PeakWavelength { get; private set; }

	/// <summary>
	/// Σ k·a over the live spectrum. Pushed to the shader, which needs it to normalise
	/// Gerstner displacement without dividing by per-wave amplitude.
	/// </summary>
	public float SteepnessNormaliser { get; private set; }

	private Vector4[] _waves = System.Array.Empty<Vector4>();
	private float[] _phases = System.Array.Empty<float>();
	private float[] _physicsWeights = System.Array.Empty<float>();
	private int _physicsCount;

	private readonly Vector4[] _seaSources = new Vector4[MaxSeaSources];
	private readonly float[] _seaFalloffs = new float[MaxSeaSources];
	private SeaDepthField _depth;

	/// <summary>xy = direction, z = amplitude, w = wavelength. Read by OceanSurface.</summary>
	/// <remarks>
	/// Bands are contiguous: [swell | medium | chop]. Amplitudes are static between
	/// rebuilds. The shader additionally fades short waves out with distance from the
	/// camera, to stop the far field aliasing on a coarse mesh. That fade is deliberately
	/// not mirrored here: it is a rendering concern, it is view-dependent, and everything
	/// that samples the CPU side is close enough to the camera for it to be inactive.
	/// </remarks>
	public Vector4[] Waves => _waves;

	/// <summary>Per-wave phase offset in radians, parallel to <see cref="Waves"/>.</summary>
	public float[] Phases => _phases;

	public int PhysicsWaveCount => _physicsCount;

	public float[] PhysicsWeights => _physicsWeights;

	public Vector4[] SeaSources => _seaSources;

	public float[] SeaFalloffs => _seaFalloffs;

	public int SeaSourceCount { get; private set; }

	/// <summary>Seabed-derived calm field, or null when no terrain is baked.</summary>
	public SeaDepthField DepthField => _depth;

	/// <summary>
	/// Live swell multiplier from the set envelope, roughly 1±SwellSetDepth. Slow
	/// incommensurable oscillators, so bigger sets arrive occasionally and never on a
	/// visible cycle. Pure function of <see cref="Time"/>, like everything else here.
	/// </summary>
	public float SetEnvelope { get; private set; } = 1f;

	private static readonly float[] SetPeriods = { 91f, 212f, 337f };
	private static readonly float[] SetWeights = { 1f, 0.6f, 0.35f };

	private const double PlasticConjugate = 0.7548776662466927;

	public override void _EnterTree()
	{
		Instance = this;
		Settings ??= new OceanSettings();
		Rebuild();
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

	public override void _Process(double delta)
	{
		Time += delta;
		RefreshSeaSources();
		Breathe();
	}

	private void Breathe()
	{
		float t = (float)Time;
		float sum = 0f;
		float total = 0f;

		for (int i = 0; i < SetPeriods.Length; i++)
		{
			float phase = (float)(Mathf.Tau * Frac((i + 1) * PlasticConjugate));
			sum += SetWeights[i] * Mathf.Sin(Mathf.Tau * t / SetPeriods[i] + phase);
			total += SetWeights[i];
		}

		SetEnvelope = Mathf.Max(0.1f, 1f + Settings.SwellSetDepth * sum / total);
		SteepnessNormaliser = Settings.SolveAmplitudes(_waves, SetEnvelope);
	}

	private static double Frac(double v) => v - System.Math.Floor(v);

	/// <summary>Rebuilds the wave skeleton and amplitudes after editing <see cref="Settings"/>.</summary>
	public void Rebuild()
	{
		Settings.BuildSkeleton(out _waves, out _phases);

		int swell = Mathf.Min(Settings.ClampedSwellCount, _waves.Length);
		int medium = Mathf.Min(swell + Settings.ClampedMediumCount, _waves.Length);
		_physicsCount = Mathf.Min(Settings.PhysicsWaveCount, _waves.Length);
		_physicsWeights = new float[_physicsCount];

		for (int i = 0; i < _physicsCount; i++)
		{
			_physicsWeights[i] = i < swell ? Settings.SwellPhysicsWeight
				: i < medium ? Settings.MediumPhysicsWeight
				: Settings.ChopPhysicsWeight;
		}

		SignificantHeight = Settings.CombinedHeight;
		PeakWavelength = Settings.MediumPeakWavelength;
		SteepnessNormaliser = Settings.SolveAmplitudes(_waves);
	}

	/// <summary>
	/// Adopts a settings resource and rebuilds. Autoload exports are awkward to edit in
	/// the inspector, so OceanSurface owns the authored resource and hands it over here.
	/// </summary>
	public void SetSettings(OceanSettings settings)
	{
		if (settings == null) return;
		Settings = settings;
		Rebuild();
	}

	/// <summary>Overrides wave time. Used by clients to adopt the host's clock.</summary>
	public void SetTime(double time) => Time = time;

	public void SetDepthField(SeaDepthField field) => _depth = field;

	private void RefreshSeaSources()
	{
		int count = 0;

		foreach (SeaZone zone in SeaZone.Active)
		{
			if (count >= MaxSeaSources) break;
			if (!IsInstanceValid(zone) || !zone.IsInsideTree()) continue;

			Vector3 pos = zone.GlobalPosition;
			_seaSources[count] = new Vector4(pos.X, pos.Z, zone.Radius, zone.Intensity);
			_seaFalloffs[count] = zone.Falloff;
			count++;
		}

		SeaSourceCount = count;
	}

	/// <summary>
	/// Local sea-state multiplier at a world XZ position. The seabed sets the baseline —
	/// 1 in open water, falling toward calm over shallows — and SeaZone nodes override it
	/// locally, above 1 for deliberately rough water. Mirrored by sea_scale() in
	/// ocean.gdshader and map_overlay.gdshader.
	/// </summary>
	public float SeaScale(Vector2 worldXZ)
	{
		float s = _depth != null ? _depth.Sample(worldXZ) : 1f;

		for (int i = 0; i < SeaSourceCount; i++)
		{
			Vector4 src = _seaSources[i];
			float dist = new Vector2(src.X, src.Y).DistanceTo(worldXZ);
			float w = 1f - Mathf.SmoothStep(src.Z, src.Z + Mathf.Max(_seaFalloffs[i], 0.01f), dist);
			s = Mathf.Lerp(s, src.W, w);
		}

		return Mathf.Clamp(s, 0f, Settings.MaxSeaScale);
	}

	/// <summary>
	/// Surface height felt by physics: swell and medium bands at their physics weights.
	///
	/// Samples at the pinch-inverted position, so the sharp Gerstner crests the shader
	/// draws are also the crests hulls feel. Two fixed-point iterations; at legal
	/// steepness the remaining error is centimetres.
	/// </summary>
	public float GetHeight(Vector2 worldXZ)
	{
		float sea = SeaScale(worldXZ);
		Vector2 p = Undisplace(worldXZ, sea, _physicsCount, _physicsWeights);
		float y = 0f;
		float t = (float)Time;

		for (int i = 0; i < _physicsCount; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			y += w.Z * _physicsWeights[i] * Mathf.Sin(k * dir.Dot(p) - omega * t + _phases[i]);
		}

		return SeaLevel + y * sea;
	}

	private Vector2 Undisplace(Vector2 worldXZ, float sea, int count, float[] weights)
	{
		float q = Settings.Steepness / Mathf.Max(SteepnessNormaliser, 0.0001f);
		float t = (float)Time;
		Vector2 p = worldXZ;

		for (int iter = 0; iter < 2; iter++)
		{
			Vector2 pinch = Vector2.Zero;

			for (int i = 0; i < count; i++)
			{
				Vector4 w = _waves[i];
				var dir = new Vector2(w.X, w.Y);
				float amp = w.Z * (weights != null ? weights[i] : 1f) * sea;
				float k = Mathf.Tau / w.W;
				float omega = Mathf.Sqrt(Gravity * k);

				pinch += dir * (q * amp * Mathf.Cos(k * dir.Dot(p) - omega * t + _phases[i]));
			}

			p = worldXZ - pinch;
		}

		return p;
	}

	public float GetHeight(Vector3 worldPos) => GetHeight(new Vector2(worldPos.X, worldPos.Z));

	/// <summary>
	/// Surface height as drawn: every band at full amplitude. For cosmetic consumers —
	/// cameras, audio, spray, swimmers — that must agree with the rendered surface.
	/// </summary>
	public float GetRenderedHeight(Vector2 worldXZ)
	{
		float sea = SeaScale(worldXZ);
		Vector2 p = Undisplace(worldXZ, sea, _waves.Length, null);
		float y = 0f;
		float t = (float)Time;

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			y += w.Z * Mathf.Sin(k * dir.Dot(p) - omega * t + _phases[i]);
		}

		return SeaLevel + y * sea;
	}

	public float GetRenderedHeight(Vector3 worldPos) => GetRenderedHeight(new Vector2(worldPos.X, worldPos.Z));

	/// <summary>
	/// Analytic surface normal. Derived from the wave derivatives rather than sampled
	/// neighbours, so it stays exact no matter how coarse the mesh is.
	/// </summary>
	public Vector3 GetNormal(Vector2 worldXZ)
	{
		float dx = 0f;
		float dz = 0f;
		float jxx = 0f;
		float jxz = 0f;
		float jzz = 0f;
		float t = (float)Time;
		float q = Settings.Steepness / Mathf.Max(SteepnessNormaliser, 0.0001f);
		float sea = SeaScale(worldXZ);

		for (int i = 0; i < _physicsCount; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float amp = w.Z * _physicsWeights[i];
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);
			float phase = k * dir.Dot(worldXZ) - omega * t + _phases[i];

			float s = Mathf.Sin(phase);
			float c = Mathf.Cos(phase) * amp * k;

			dx += c * dir.X;
			dz += c * dir.Y;

			float qak = q * amp * k;
			jxx += qak * dir.X * dir.X * s;
			jxz += qak * dir.X * dir.Y * s;
			jzz += qak * dir.Y * dir.Y * s;
		}

		dx *= sea;
		dz *= sea;
		jxx *= sea;
		jxz *= sea;
		jzz *= sea;

		var tangentX = new Vector3(1f - jxx, dx, -jxz);
		var tangentZ = new Vector3(-jxz, dz, 1f - jzz);

		return tangentZ.Cross(tangentX).Normalized();
	}

	public Vector3 GetNormal(Vector3 worldPos) => GetNormal(new Vector2(worldPos.X, worldPos.Z));

	/// <summary>
	/// Vertical velocity of the physics surface. Useful for drag that should not fight a
	/// wave lifting a hull, and for spray thresholds.
	/// </summary>
	public float GetVerticalVelocity(Vector2 worldXZ)
	{
		float v = 0f;
		float t = (float)Time;

		for (int i = 0; i < _physicsCount; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			v += -w.Z * _physicsWeights[i] * omega * Mathf.Cos(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
		}

		return v * SeaScale(worldXZ);
	}

	public float GetRenderedVerticalVelocity(Vector2 worldXZ)
	{
		float v = 0f;
		float t = (float)Time;

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			v += -w.Z * omega * Mathf.Cos(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
		}

		return v * SeaScale(worldXZ);
	}

	/// <summary>
	/// Full orbital velocity of the water at a point, horizontal included, falling off with
	/// depth as exp(k*y). Drag should be measured against this rather than against still
	/// water, or a hull fights a current that is not there.
	/// </summary>
	public Vector3 GetFlow(Vector3 worldPos)
	{
		var flat = new Vector2(worldPos.X, worldPos.Z);
		var flow = Vector3.Zero;
		float t = (float)Time;

		for (int i = 0; i < _physicsCount; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);
			float phase = k * dir.Dot(flat) - omega * t + _phases[i];
			float amplitude = w.Z * _physicsWeights[i] * omega * Mathf.Exp(k * Mathf.Min(worldPos.Y - SeaLevel, 0f));
			float swing = Mathf.Sin(phase);

			flow.X += dir.X * amplitude * swing;
			flow.Z += dir.Y * amplitude * swing;
			flow.Y -= amplitude * Mathf.Cos(phase);
		}

		return flow * SeaScale(flat);
	}
}
