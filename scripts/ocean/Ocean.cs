using Godot;

namespace Knotical.Ocean;

/// <summary>
/// The single source of truth for the sea surface.
///
/// The wave field is a pure function of world position, time, and a handful of authored
/// numbers, which is why it needs almost no networking: sync <see cref="Time"/> and every
/// client renders and simulates an identical ocean. Every wave is derived from
/// <see cref="OceanSettings.Seed"/> in three bands (swell, medium, chop); local sea state
/// comes from the seabed via <see cref="SeaDepthField"/>, baked identically on every
/// machine. This class owns that function on the CPU; ocean.gdshader runs the same maths
/// on the GPU. If you change one, change the other.
///
/// There is exactly one surface: what physics feels is what the shader draws.
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

	private const float Gravity = 9.81f;

	public static Ocean Instance { get; private set; }

	[Export] public OceanSettings Settings { get; set; }

	/// <summary>
	/// Seconds of wave time. Advanced locally, but authoritative on the host once
	/// networking lands — clients adopt the host's value rather than their own clock.
	/// </summary>
	public double Time { get; private set; }

	/// <summary>Significant wave height in metres at field 1, for shading, HUDs, and audio.</summary>
	public float SignificantHeight { get; private set; }

	/// <summary>Amplitude-weighted dominant wavelength in metres, for HUD display.</summary>
	public float PeakWavelength { get; private set; }

	/// <summary>Bumped on every rebuild so uniform pushers know to re-send the wave set.</summary>
	public int Version { get; private set; }

	private Vector4[] _waves = System.Array.Empty<Vector4>();
	private float[] _steepness = System.Array.Empty<float>();
	private float[] _phases = System.Array.Empty<float>();
	private float[] _depthResponse = System.Array.Empty<float>();
	private SeaDepthField _depth;

	/// <summary>xy = unit direction, z = amplitude (m), w = wavelength (m).</summary>
	/// <remarks>
	/// Bands are contiguous: [swell | medium | chop]. Static between rebuilds. The shader
	/// additionally fades short waves out with distance from the camera, to stop the far
	/// field aliasing on a coarse mesh. That fade is deliberately not mirrored here: it is
	/// a rendering concern, it is view-dependent, and everything that samples the CPU side
	/// is close enough to the camera for it to be inactive.
	/// </remarks>
	public Vector4[] Waves => _waves;

	/// <summary>Per-wave Gerstner steepness 0..1, parallel to <see cref="Waves"/>.</summary>
	public float[] Steepness => _steepness;

	/// <summary>Per-wave phase offset in radians, parallel to <see cref="Waves"/>.</summary>
	public float[] Phases => _phases;

	/// <summary>Per-wave exponent on the local field: 1 = full response, 0 = immune.</summary>
	public float[] DepthResponse => _depthResponse;

	/// <summary>Seabed-derived sea-state field, or null when no terrain is baked.</summary>
	public SeaDepthField DepthField => _depth;

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
	}

	/// <summary>Re-derives every wave from the seed after editing <see cref="Settings"/>.</summary>
	public void Rebuild()
	{
		Settings.Build(out _waves, out _steepness, out _phases, out _depthResponse);

		float squareSum = 0f;
		float weighted = 0f;

		for (int i = 0; i < _waves.Length; i++)
		{
			float a2 = _waves[i].Z * _waves[i].Z;
			squareSum += a2;
			weighted += a2 * _waves[i].W;
		}

		SignificantHeight = 4f * Mathf.Sqrt(squareSum * 0.5f);
		PeakWavelength = squareSum > 0f ? weighted / squareSum : 0f;
		Version++;
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

	/// <summary>
	/// Local sea-state multiplier at a world XZ position, from the baked seabed field.
	/// 1 in baseline-depth water, below 1 over shallows, above 1 in sculpted deeps.
	/// Each wave responds by pow(field, its band's DepthResponse).
	/// </summary>
	public float Field(Vector2 worldXZ) => _depth != null ? _depth.Sample(worldXZ) : 1f;

	private float WaveScale(float field, int i) =>
		field == 1f ? 1f : Mathf.Pow(field, _depthResponse[i]);

	/// <summary>
	/// Per-wave horizontal pinch amplitude. The GPU-Gems normalisation — steepness
	/// divided by k and the wave count — keeps the summed displacement below the
	/// self-intersection limit; the pinch follows the field down in calm water but is
	/// capped at its baseline in big water, so giants grow taller and rounder, never
	/// folded through themselves.
	/// </summary>
	private float PinchAmplitude(int i, float scale) =>
		_steepness[i] / (Mathf.Tau / _waves[i].W * _waves.Length) * Mathf.Min(scale, 1f);

	/// <summary>
	/// Surface height at a world XZ position — the same surface the shader draws.
	///
	/// Samples at the pinch-inverted position, so the sharp Gerstner crests on screen
	/// are also the crests physics feels. Four fixed-point iterations; convergence
	/// slows as summed steepness approaches 1, and near-limit chop needs the margin.
	/// </summary>
	public float GetHeight(Vector2 worldXZ)
	{
		float field = Field(worldXZ);
		Vector2 p = Undisplace(worldXZ, field);
		float y = 0f;
		float t = (float)Time;

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			y += w.Z * WaveScale(field, i) * Mathf.Sin(k * dir.Dot(p) - omega * t + _phases[i]);
		}

		return SeaLevel + y;
	}

	public float GetHeight(Vector3 worldPos) => GetHeight(new Vector2(worldPos.X, worldPos.Z));

	public float GetRenderedHeight(Vector2 worldXZ) => GetHeight(worldXZ);

	public float GetRenderedHeight(Vector3 worldPos) => GetHeight(new Vector2(worldPos.X, worldPos.Z));

	private Vector2 Undisplace(Vector2 worldXZ, float field)
	{
		float t = (float)Time;
		Vector2 p = worldXZ;

		for (int iter = 0; iter < 4; iter++)
		{
			Vector2 pinch = Vector2.Zero;

			for (int i = 0; i < _waves.Length; i++)
			{
				Vector4 w = _waves[i];
				var dir = new Vector2(w.X, w.Y);
				float k = Mathf.Tau / w.W;
				float omega = Mathf.Sqrt(Gravity * k);

				pinch += dir * (PinchAmplitude(i, WaveScale(field, i))
					* Mathf.Cos(k * dir.Dot(p) - omega * t + _phases[i]));
			}

			p = worldXZ - pinch;
		}

		return p;
	}

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
		float field = Field(worldXZ);

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float scale = WaveScale(field, i);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);
			float phase = k * dir.Dot(worldXZ) - omega * t + _phases[i];

			float s = Mathf.Sin(phase);
			float c = Mathf.Cos(phase) * w.Z * scale * k;

			dx += c * dir.X;
			dz += c * dir.Y;

			float qak = PinchAmplitude(i, scale) * k * s;
			jxx += qak * dir.X * dir.X;
			jxz += qak * dir.X * dir.Y;
			jzz += qak * dir.Y * dir.Y;
		}

		var tangentX = new Vector3(1f - jxx, dx, -jxz);
		var tangentZ = new Vector3(-jxz, dz, 1f - jzz);

		return tangentZ.Cross(tangentX).Normalized();
	}

	public Vector3 GetNormal(Vector3 worldPos) => GetNormal(new Vector2(worldPos.X, worldPos.Z));

	/// <summary>
	/// Vertical velocity of the surface. Useful for drag that should not fight a wave
	/// lifting a hull, and for spray thresholds.
	/// </summary>
	public float GetVerticalVelocity(Vector2 worldXZ)
	{
		float v = 0f;
		float t = (float)Time;
		float field = Field(worldXZ);

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			v += -w.Z * WaveScale(field, i) * omega
				* Mathf.Cos(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
		}

		return v;
	}

	public float GetRenderedVerticalVelocity(Vector2 worldXZ) => GetVerticalVelocity(worldXZ);

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
		float field = Field(flat);

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);
			float phase = k * dir.Dot(flat) - omega * t + _phases[i];
			float amplitude = w.Z * WaveScale(field, i) * omega
				* Mathf.Exp(k * Mathf.Min(worldPos.Y - SeaLevel, 0f));
			float swing = Mathf.Sin(phase);

			flow.X += dir.X * amplitude * swing;
			flow.Z += dir.Y * amplitude * swing;
			flow.Y -= amplitude * Mathf.Cos(phase);
		}

		return flow;
	}
}
