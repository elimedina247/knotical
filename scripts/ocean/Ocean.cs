using Godot;
using Knotical.Weather;

namespace Knotical.Ocean;

/// <summary>
/// The single source of truth for the sea surface.
///
/// The wave field is a pure function of world position, time, and sea state, which is why
/// it needs almost no networking: sync <see cref="Time"/> and the two sea-state scalars,
/// and every client renders and simulates an identical ocean. This class owns that
/// function on the CPU; ocean.gdshader runs the same maths on the GPU. If you change one,
/// change the other.
///
/// Register as an autoload named "Ocean", after "Wind".
/// </summary>
public partial class Ocean : Node
{
	/// <summary>Half-extent of the playable world in metres. Coordinates run -6000..+6000.</summary>
	public const float WorldHalfExtent = 6000f;

	private const float Gravity = 9.81f;

	public static Ocean Instance { get; private set; }

	[Export] public OceanSettings Settings { get; set; }

	/// <summary>
	/// Seconds of wave time. Advanced locally, but authoritative on the host once
	/// networking lands — clients adopt the host's value rather than their own clock.
	/// </summary>
	public double Time { get; private set; }

	/// <summary>
	/// Current significant wave height in metres. Chases the wind rather than tracking it:
	/// see <see cref="OceanSettings.DevelopmentLagSeconds"/>.
	/// </summary>
	public float SignificantHeight { get; private set; }

	/// <summary>Current spectral peak wavelength in metres. Also lagged.</summary>
	public float PeakWavelength { get; private set; }

	/// <summary>
	/// Σ k·a over the live spectrum. Pushed to the shader, which needs it to normalise
	/// Gerstner displacement without dividing by per-wave amplitude.
	/// </summary>
	public float SteepnessNormaliser { get; private set; }

	private Vector4[] _waves = System.Array.Empty<Vector4>();
	private float[] _phases = System.Array.Empty<float>();

	/// <summary>xy = direction, z = amplitude, w = wavelength. Read by OceanSurface.</summary>
	/// <remarks>
	/// Amplitudes are rewritten every frame from the wind; directions, wavelengths, and
	/// phases never change after <see cref="Rebuild"/>. Callers may cache the array
	/// reference but must not cache the values.
	///
	/// The shader additionally fades short waves out with distance from the camera, to
	/// stop the far field aliasing on a coarse mesh. That fade is deliberately not
	/// mirrored here: it is a rendering concern, it is view-dependent, and everything
	/// that samples the CPU side is close enough to the camera for it to be inactive.
	/// </remarks>
	public Vector4[] Waves => _waves;

	/// <summary>Per-wave phase offset in radians, parallel to <see cref="Waves"/>.</summary>
	public float[] Phases => _phases;

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
		UpdateSeaState((float)delta);
	}

	/// <summary>
	/// Rebuilds the wave skeleton after editing <see cref="Settings"/>, and snaps the sea
	/// state straight to whatever the wind currently justifies rather than growing into it.
	/// </summary>
	public void Rebuild()
	{
		Settings.BuildSkeleton(out _waves, out _phases);

		float windSpeed = Wind.Instance?.Speed ?? 0f;
		SignificantHeight = Settings.DevelopedHeight(windSpeed);
		PeakWavelength = Settings.DevelopedPeakWavelength(windSpeed);

		SolveAmplitudes();
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

	/// <summary>
	/// Overrides the lagged sea state. The lag is deterministic given the same wind
	/// history, but a client joining mid-session has no history to integrate, so the host
	/// hands over where the sea has actually got to.
	/// </summary>
	public void SetSeaState(float significantHeight, float peakWavelength)
	{
		SignificantHeight = significantHeight;
		PeakWavelength = peakWavelength;
		SolveAmplitudes();
	}

	/// <summary>
	/// Eases the sea toward what the current wind would eventually build, then re-solves
	/// amplitudes. The lag is the point: gusts arrive in seconds, water takes far longer,
	/// so without it every puff visibly inflates the surface.
	/// </summary>
	private void UpdateSeaState(float delta)
	{
		Wind wind = Wind.Instance;
		if (wind == null) return;

		float targetHeight = Settings.DevelopedHeight(wind.Speed);
		float targetPeak = Settings.DevelopedPeakWavelength(wind.Speed);

		// Frame-rate independent exponential approach.
		float lag = Mathf.Max(Settings.DevelopmentLagSeconds, 0.001f);
		float blend = 1f - Mathf.Exp(-delta / lag);

		SignificantHeight = Mathf.Lerp(SignificantHeight, targetHeight, blend);
		PeakWavelength = Mathf.Lerp(PeakWavelength, targetPeak, blend);

		SolveAmplitudes();
	}

	private void SolveAmplitudes()
	{
		float windDir = Wind.Instance?.DirectionRad ?? 0f;
		SteepnessNormaliser = Settings.SolveAmplitudes(_waves, windDir, SignificantHeight, PeakWavelength);
	}

	/// <summary>
	/// Surface height at a world XZ position.
	///
	/// Note this samples the undisplaced height field — it ignores the Gerstner
	/// horizontal pinch the shader applies, so in choppy water the value is off by
	/// roughly the displacement magnitude. That error is well under the size of a hull
	/// and buoyancy probes average it out, so it is not worth inverting yet. Revisit if
	/// Steepness ever goes high enough that boats visibly float above the crests.
	/// </summary>
	public float GetHeight(Vector2 worldXZ)
	{
		float y = 0f;
		float t = (float)Time;

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			y += w.Z * Mathf.Sin(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
		}

		return y;
	}

	public float GetHeight(Vector3 worldPos) => GetHeight(new Vector2(worldPos.X, worldPos.Z));

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

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);
			float phase = k * dir.Dot(worldXZ) - omega * t + _phases[i];

			float s = Mathf.Sin(phase);
			float c = Mathf.Cos(phase) * w.Z * k;

			dx += c * dir.X;
			dz += c * dir.Y;

			float qak = q * w.Z * k;
			jxx += qak * dir.X * dir.X * s;
			jxz += qak * dir.X * dir.Y * s;
			jzz += qak * dir.Y * dir.Y * s;
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

		for (int i = 0; i < _waves.Length; i++)
		{
			Vector4 w = _waves[i];
			var dir = new Vector2(w.X, w.Y);
			float k = Mathf.Tau / w.W;
			float omega = Mathf.Sqrt(Gravity * k);

			v += -w.Z * omega * Mathf.Cos(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
		}

		return v;
	}
}
