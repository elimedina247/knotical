using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Renders the sea as a set of nested grids that follow the camera.
///
/// Each level is a uniform grid snapped to its own cell size. Snapping is what stops
/// vertices sliding through the wave field as you sail — without it the whole surface
/// crawls. Levels overlap rather than tiling edge-to-edge: because displacement is a
/// pure function of world XZ, two grids agree wherever they overlap, so there are no
/// seams to stitch. Each outer level discards fragments over the region its finer
/// neighbour covers, so a coarse grid is simply never drawn where a fine one exists;
/// the small vertical stagger only prevents z-fighting in the thin hand-over ring.
/// </summary>
[GlobalClass]
public partial class OceanSurface : Node3D
{
	/// <summary>Cells per side, per level. Same for every level; only the extent changes.</summary>
	[Export(PropertyHint.Range, "16,512,16")]
	public int CellsPerLevel { get; set; } = 256;

	/// <summary>
	/// Half-extent of each level in metres, innermost first. Cell size is
	/// 2 * extent / CellsPerLevel — so 256 m over 256 cells gives 2 m cells.
	/// </summary>
	[Export]
	public float[] LevelExtents { get; set; } = { 256f, 1024f, 4096f, 8192f };

	[Export] public Shader OceanShader { get; set; }

	/// <summary>
	/// Authored wave spectrum. Handed to the Ocean autoload on ready, so the inspector
	/// on this node is the one place the sea gets tuned. Leave null for defaults.
	/// </summary>
	[Export] public OceanSettings Settings { get; set; }

	[Export] public Knotical.Style.GamePalette Palette { get; set; }

	/// <summary>
	/// Re-applies <see cref="Settings"/> while the game is running. Band heights re-solve
	/// every frame, but the skeleton — counts, wavelengths, directions — only rebuilds
	/// through here.
	/// </summary>
	[Export] public bool RebuildNow { get => false; set { if (value) Ocean.Instance?.SetSettings(Settings); } }

	private readonly System.Collections.Generic.List<MeshInstance3D> _levels = new();
	private ShaderMaterial _material;
	private Camera3D _camera;
	private bool _skeletonPushed;

	// Reused every frame. Godot maps untyped arrays onto fixed-size shader array uniforms
	// the same way a GDScript array literal does, and rebuilding them each frame would
	// allocate 48 boxed values per frame for nothing.
	private readonly Godot.Collections.Array _packedWaves = new();
	private readonly Godot.Collections.Array _packedPhases = new();

	private const int MaxWakeBows = 4;
	private const int MaxHullMasks = 4;

	private readonly Godot.Collections.Array _packedWakePoints = new();
	private readonly Godot.Collections.Array _packedWakeBows = new();
	private readonly Godot.Collections.Array _packedMaskFrames = new();
	private readonly Godot.Collections.Array _packedMaskExtents = new();
	private readonly Godot.Collections.Array _packedSeaSources = new();
	private readonly Godot.Collections.Array _packedSeaFalloffs = new();

	public override void _Ready()
	{
		Ocean.Instance?.SetSettings(Settings);
		Palette ??= new Knotical.Style.GamePalette();

		_material = new ShaderMaterial
		{
			Shader = OceanShader ?? GD.Load<Shader>("res://shaders/ocean.gdshader")
		};

		// The shader declares fixed-size arrays, so always send a full set with the
		// unused tail zeroed rather than a short array.
		_packedWaves.Resize(OceanSettings.MaxWaves);
		_packedPhases.Resize(OceanSettings.MaxWaves);
		_packedWakePoints.Resize(Knotical.Boat.BoatWake.MaxTrailPoints);
		_packedWakeBows.Resize(MaxWakeBows);
		_packedMaskFrames.Resize(MaxHullMasks);
		_packedMaskExtents.Resize(MaxHullMasks);
		_packedSeaSources.Resize(Ocean.MaxSeaSources);
		_packedSeaFalloffs.Resize(Ocean.MaxSeaSources);

		for (int i = 0; i < LevelExtents.Length; i++)
		{
			var instance = new MeshInstance3D
			{
				Name = $"OceanLevel{i}",
				Mesh = BuildGrid(LevelExtents[i], CellsPerLevel),
				MaterialOverride = _material,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};

			AddChild(instance);
			instance.SetInstanceShaderParameter("inner_extent", i == 0 ? 0f : LevelExtents[i - 1] * 0.95f);
			_levels.Add(instance);
		}

		PushWaveUniforms();
	}

	public override void _Process(double delta)
	{
		// Ocean is an autoload and so normally ready first, but PushWaveUniforms retries
		// the skeleton rather than silently rendering a flat plane if scene order ever
		// changes.
		PushWaveUniforms();

		_camera ??= GetViewport().GetCamera3D();
		if (_camera == null) return;

		Vector3 eye = _camera.GlobalPosition;

		// Snap each level independently to its own cell size.
		for (int i = 0; i < _levels.Count; i++)
		{
			float cell = LevelExtents[i] * 2f / CellsPerLevel;
			float x = Mathf.Floor(eye.X / cell) * cell;
			float z = Mathf.Floor(eye.Z / cell) * cell;

			_levels[i].GlobalPosition = new Vector3(x, Ocean.SeaLevel - 0.05f * i, z);
		}

		_material.SetShaderParameter("wave_time", (float)(Ocean.Instance?.Time ?? 0.0));

		float submerged = 0f;
		if (Ocean.Instance != null && eye.Y < Ocean.Instance.GetRenderedHeight(new Vector2(eye.X, eye.Z)))
		{
			submerged = 1f;
		}

		_material.SetShaderParameter("camera_submerged", submerged);

		PushWakeUniforms();
		PushHullMasks();
		PushSeaSources();
	}

	private void PushSeaSources()
	{
		Ocean ocean = Ocean.Instance;
		if (ocean == null) return;

		for (int i = 0; i < Ocean.MaxSeaSources; i++)
		{
			_packedSeaSources[i] = i < ocean.SeaSourceCount ? ocean.SeaSources[i] : Vector4.Zero;
			_packedSeaFalloffs[i] = i < ocean.SeaSourceCount ? ocean.SeaFalloffs[i] : 0f;
		}

		_material.SetShaderParameter("sea_sources", _packedSeaSources);
		_material.SetShaderParameter("sea_falloffs", _packedSeaFalloffs);
		_material.SetShaderParameter("sea_source_count", ocean.SeaSourceCount);
		_material.SetShaderParameter("sea_scale_max", ocean.Settings.MaxSeaScale);

		SeaDepthField depth = ocean.DepthField;
		bool baked = depth != null && depth.Baked;

		_material.SetShaderParameter("sea_depth_enabled", baked ? 1f : 0f);

		if (baked)
		{
			_material.SetShaderParameter("sea_depth_map", depth.Texture);
			_material.SetShaderParameter("sea_depth_extent", depth.HalfExtent);
		}

		Knotical.Vfx.FoamCapture foam = Knotical.Vfx.FoamCapture.Instance;
		if (foam?.Texture != null)
		{
			_material.SetShaderParameter("foam_capture", foam.Texture);
			_material.SetShaderParameter("foam_capture_extent", foam.HalfExtent);
			_material.SetShaderParameter("foam_capture_strength", foam.Strength);
		}
	}

	private void PushHullMasks()
	{
		int count = 0;

		foreach (Knotical.Boat.BoatHull hull in Knotical.Boat.BoatHull.Active)
		{
			if (count >= MaxHullMasks) break;
			if (!IsInstanceValid(hull) || !hull.IsInsideTree()) continue;

			hull.GetSurfaceMask(out Vector4 frame, out Vector4 extents);
			_packedMaskFrames[count] = frame;
			_packedMaskExtents[count] = extents;
			count++;
		}

		int maskCount = count;
		for (; count < MaxHullMasks; count++)
		{
			_packedMaskFrames[count] = Vector4.Zero;
			_packedMaskExtents[count] = Vector4.Zero;
		}

		_material.SetShaderParameter("hull_masks_a", _packedMaskFrames);
		_material.SetShaderParameter("hull_masks_b", _packedMaskExtents);
		_material.SetShaderParameter("hull_mask_count", maskCount);
	}

	private void PushWakeUniforms()
	{
		int points = 0;
		int bows = 0;
		float width = 12f;
		float hullLength = 30f;
		float life = 22f;
		float spread = 0.5f;
		float strength = 0.9f;

		foreach (Knotical.Boat.BoatWake wake in Knotical.Boat.BoatWake.Active)
		{
			if (bows >= MaxWakeBows) break;

			_packedWakeBows[bows++] = wake.Bow;
			width = wake.TrailWidth;
			hullLength = wake.HullLength;
			life = wake.TrailLife;
			spread = wake.SpreadRate;
			strength = wake.Strength;

			System.Collections.Generic.IReadOnlyList<Vector4> trail = wake.TrailPoints;
			for (int i = trail.Count - 1; i >= 0 && points < Knotical.Boat.BoatWake.MaxTrailPoints; i--)
			{
				_packedWakePoints[points++] = trail[i];
			}
		}

		int pointCount = points;
		for (; points < Knotical.Boat.BoatWake.MaxTrailPoints; points++) _packedWakePoints[points] = Vector4.Zero;

		int bowCount = bows;
		for (; bows < MaxWakeBows; bows++) _packedWakeBows[bows] = Vector4.Zero;

		_material.SetShaderParameter("wake_points", _packedWakePoints);
		_material.SetShaderParameter("wake_point_count", pointCount);
		_material.SetShaderParameter("wake_bows", _packedWakeBows);
		_material.SetShaderParameter("wake_bow_count", bowCount);
		_material.SetShaderParameter("wake_width", width);
		_material.SetShaderParameter("wake_hull_length", hullLength);
		_material.SetShaderParameter("wake_life", life);
		_material.SetShaderParameter("wake_spread", spread);
		_material.SetShaderParameter("wake_strength", strength);
	}

	/// <summary>
	/// Ships the live spectrum to the shader. Safe to call every frame — the unchanging
	/// half (phases, count) is only pushed once, and the rest is 24 vectors.
	/// </summary>
	public void PushWaveUniforms()
	{
		Ocean ocean = Ocean.Instance;
		if (ocean == null || _material == null) return;

		if (!_skeletonPushed)
		{
			for (int i = 0; i < OceanSettings.MaxWaves; i++)
			{
				_packedPhases[i] = i < ocean.Phases.Length ? ocean.Phases[i] : 0f;
			}

			_material.SetShaderParameter("wave_phase", _packedPhases);
			_material.SetShaderParameter("wave_count", ocean.Waves.Length);
			_skeletonPushed = true;
		}

		for (int i = 0; i < OceanSettings.MaxWaves; i++)
		{
			_packedWaves[i] = i < ocean.Waves.Length ? ocean.Waves[i] : Vector4.Zero;
		}

		_material.SetShaderParameter("waves", _packedWaves);
		_material.SetShaderParameter("ka_sum", ocean.SteepnessNormaliser);
		_material.SetShaderParameter("steepness", ocean.Settings.Steepness);
		_material.SetShaderParameter("significant_height", ocean.SignificantHeight);
		PushPalette();

		if (Knotical.Sky.DayCycle.Instance != null)
		{
			_material.SetShaderParameter("sky_color", Knotical.Sky.DayCycle.Instance.WaterHorizonColor);
		}
	}

	private void PushPalette()
	{
		Color[] ramp = Palette.WaterRamp;
		if (ramp == null || ramp.Length == 0) return;

		int count = Mathf.Min(ramp.Length, 8);
		var packed = new Godot.Collections.Array();
		for (int i = 0; i < 8; i++)
		{
			Color c = ramp[Mathf.Min(i, count - 1)].SrgbToLinear();
			packed.Add(new Vector3(c.R, c.G, c.B));
		}

		_material.SetShaderParameter("water_ramp", packed);
		_material.SetShaderParameter("water_ramp_size", count);
		_material.SetShaderParameter("foam_color", Palette.Foam);
	}

	/// <summary>Call after rebuilding the skeleton so phases and count are re-sent.</summary>
	public void InvalidateSkeleton() => _skeletonPushed = false;

	/// <summary>
	/// Flat grid centred on the origin. The vertex shader supplies all displacement,
	/// so the custom AABB has to be inflated by hand or Godot culls the mesh the moment
	/// its flat bounds leave the frustum.
	/// </summary>
	private static ArrayMesh BuildGrid(float halfExtent, int cells)
	{
		int verts = cells + 1;
		float step = halfExtent * 2f / cells;

		var positions = new Vector3[verts * verts];
		var normals = new Vector3[verts * verts];
		var indices = new int[cells * cells * 6];

		for (int z = 0; z < verts; z++)
		{
			for (int x = 0; x < verts; x++)
			{
				int i = z * verts + x;
				positions[i] = new Vector3(-halfExtent + x * step, 0f, -halfExtent + z * step);
				normals[i] = Vector3.Up;
			}
		}

		int t = 0;
		for (int z = 0; z < cells; z++)
		{
			for (int x = 0; x < cells; x++)
			{
				int tl = z * verts + x;
				int tr = tl + 1;
				int bl = tl + verts;
				int br = bl + 1;

				indices[t++] = tl; indices[t++] = bl; indices[t++] = tr;
				indices[t++] = tr; indices[t++] = bl; indices[t++] = br;
			}
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = positions;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		mesh.CustomAabb = new Aabb(
			new Vector3(-halfExtent, -60f, -halfExtent),
			new Vector3(halfExtent * 2f, 120f, halfExtent * 2f));

		return mesh;
	}
}
