# Unity port — implementation plan

Agreed 2026-09-11. Knotical moves from Godot 4.7 to Unity 6000.6.0f1 (URP) as a fresh
start. The Godot project is preserved at git tag `godot-final`.

## What carries over

| Godot file | Unity shape |
|---|---|
| `scripts/ocean/Ocean.cs` | Static class `Ocean` plus an `OceanClock` MonoBehaviour that advances time |
| `scripts/ocean/OceanSettings.cs` | `ScriptableObject`, same 33 fields, `System.Random` for the seed |
| `scripts/wind/Wind.cs`, `WindSettings.cs` | Static class plus `ScriptableObject`, same clock pattern |
| `scripts/boat/Buoyancy.cs` | MonoBehaviour on a `Rigidbody`; child transforms carrying an empty `Pontoon` component are the pontoons. Girardot's parameter set unchanged |

Copies of the four Godot sources sit in `port/godot/` until Phase 2 is done, then that
folder is deleted.

Everything else is dropped: rope solver, sail and rudder foils, both hull stacks,
procedural character, debug probes, Terrain3D worlds, Blender boat pipeline, toon
shaders. Sails, rudder, rope, grab, and the character are redesigned from scratch later.

Kept because they are engine-neutral: `README.md`, `docs/girardot-vs-knotical.md`,
`docs/weather-storms-plan.md`, `concept/`, `assets/` (audio clips, Girardot FFT
flipbooks, spray sheet).

## Decisions

- Unity project lives at the repo root on `main`.
- Networking is deferred until Phase 5 is done. Phases 2–5 use plain Unity physics; some
  rework is accepted when netcode arrives.
- Render pipeline is URP. Ocean rendering is our own HLSL shader driven by the same wave
  arrays the CPU uses, not Crest or a store asset, so physics equals what is drawn.
- Unity CLI (`%LOCALAPPDATA%\Unity\bin\unity.exe`, 1.0.0-beta.8) drives project
  creation, editor launch, batch runs, and tests.

## Porting notes

- `Vector4.Z` → `.z`; `Mathf.Tau` → `2f * Mathf.PI`.
- Godot `Mathf.SmoothStep(from, to, x)` is the GLSL threshold function. Unity's
  `Mathf.SmoothStep` is an interpolator. Write a local `SmoothStep01(edge0, edge1, x)`.
- `SeaDepthField` is gone; `Field()` returns 1 until terrain returns.
- Godot `RandomNumberGenerator` → `System.Random`. Seed 7 gives a different sea than in
  Godot. Fine.
- Command-line overrides (`--wind=`, `--seafile=`) move to a `Bootstrap` MonoBehaviour
  reading `Environment.GetCommandLineArgs()`.
- `ApplyForce(f, offset)` → `rb.AddForceAtPosition(f, worldPoint)` in `FixedUpdate`.
- The splash request in Buoyancy is dropped until VFX exists.

## Phases

### Phase 0 — repo turnover (one commit)

1. Tag `godot-final` on the last Godot commit.
2. Copy the four carry-over files to `port/godot/`.
3. Delete every Godot file.
4. Rewrite `CLAUDE.md` for Unity, replace `.gitignore` with Unity's, update the README
   tech section.

### Phase 1 — Unity skeleton

1. `unity projects create` on 6000.6.0f1 with the URP 3D template, at the repo root.
2. Add Input System and Cinemachine.
3. Folders: `Assets/Knotical/{Ocean,Wind,Buoyancy,Boat,Player,Shaders,Art}`,
   `Assets/ThirdParty/`.
4. One empty EditMode test passes under `unity test` before any real code lands.

### Phase 2 — carry-over code

Ocean, OceanSettings, Wind, WindSettings, Buoyancy, Pontoon. EditMode tests:
analytic normal vs finite difference; `GetFlow().y` vs `GetVerticalVelocity()`;
one-wave height at t = 0 against a hand-computed value.

### Phase 3 — ocean rendering

Done 2026-09-11. One camera-following grid whose vertex spacing grows quadratically
with distance (no LOD rings, so no cracks; far vertices swim slightly, hidden by the
short-wave distance fade). `Shaders/OceanWaves.hlsl` holds the wave maths and is
included by both the URP surface shader and `OceanProbe.compute`; the EditMode test
`WaveProbeTests` dispatches the compute kernel and compares against `Ocean.GetSurfacePoint`
and `Ocean.GetNormal` (tolerance 5 mm, 0.25°). `Editor/SceneBuilder.cs` (menu
Knotical → Build Main Scene, or `-executeMethod Knotical.Editor.SceneBuilder.BuildMain`)
regenerates `Scenes/Main.unity`, the settings assets, and the material. Whitecaps, foam
capture, cel banding, shore lines are not ported.

### Phase 4 — first floating boat

Done 2026-09-11, with a change: Kenney's ships are toy scale, so the placeholder is our
own hull. `tools/blender/build_boat.py` (Blender 5.1, headless) writes
`Art/Models/boat_placeholder.fbx` + `.json` (13 × 4.4 m, main deck 1.25 m, quarterdeck
2.05 m, mast, wheel). `Editor/BoatBuilder.cs` turns the JSON into
`Boat/BoatPlaceholder.prefab`: Rigidbody 4000 kg, the eight pontoons and Buoyancy numbers
tuned in Godot, convex hull/deck/quarterdeck/ramp colliders, wall and cabin boxes,
`BoatMotor` (Girardot's three numbers, WASD via Input System). `SceneBuilder.BuildMain`
places it with a `ChaseCamera`. `Tests/PlayMode/ShotTests` loads Main, runs 6 s of
physics, asserts the boat is afloat and upright, and writes `Logs/shot_main.png` — the
headless screenshot harness. Run with `unity test <repo> --mode PlayMode`.
Roadmap tick: "boat floats, rocks, and doesn't explode".

Regenerate the boat:

```
"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --python tools/blender/build_boat.py
Unity.exe -batchmode -projectPath <repo> -executeMethod Knotical.Editor.SceneBuilder.BuildMain -quit -logFile Logs/scenebuilder.log
```

### Phase 3b — ocean look

Agreed 2026-09-14, after the devlog Eli likes
(https://www.youtube.com/watch?v=uwU9ZaQ9PhY). Every effect is maths on the wave arrays
the shader already has; no store assets. The shader stays hand-written HLSL: the Cyanilux
sub-graphs the devlog uses only expose the main light to Shader Graph, and we call it
directly.

1. Two colours by depth. Sample the URP depth texture, blend shallow to deep over
   `_DepthFade` metres. The ocean moves to the Transparent queue so it is excluded from
   that texture. Until terrain exists only the boat hull shows the shallow tint.
2. Sky reflection through URP reflection sampling (skybox now, probes later), mixed in by
   fresnel. Planar reflections (a mirrored camera) are an optional later step once there
   is land to reflect.
3. Sun glint: Blinn-Phong on the wave normal, hardened by a smoothstep into a cartoon disc.
   The sun light is linked to the skybox so its disc sits where the light is.
4. Fake subsurface scattering: camera-facing-the-sun times height above sea level,
   normalised by significant wave height, lerped to a bright green-blue.
5. Foam, shader-side: crest (Gerstner Jacobian below a threshold, or height above one),
   scattered (two-octave value noise computed in the shader, no texture asset), edge
   (water depth under half a metre, which gives the boat a waterline).
6. Wake and contact foam. Done 2026-09-14: `FoamCapture` keeps a 1024 px, 768 m render
   texture centred on the main camera, fades it each frame (`FoamDecay.shader`), and an
   orthographic camera on the `FoamCapture` layer (index 30) stamps into it. `FoamEmitter`
   raycasts its colliders at the waterline from N angles and emits soft-disc particles
   (`FoamStamp.shader`) at a rate of ring plus wake-per-metre-per-second, so a still crate
   gets a ring and a moving hull leaves a trail. The ocean shader samples the texture in
   world space and draws it with the lace pattern. The boat prefab, three test crates and a
   placeholder rock carry emitters; the shot test now drives the boat for 10 s. Motor forces
   were cut to 8000 N / 6000 N m because the old 60000 / 40000 flipped the boat under way.
   Later the same day the foam was restyled after Eli's reference: one hard-cut mask from
   crest height, fold, the capture and the waterline, torn by fbm and the disc holes, drawn
   as flat white after lighting. `Shaders/Sky.shader` replaced the default skybox so a sun
   disc sits where the light is (`_KnoticalSunDir` pushed by `OceanSurface`); the light is
   at 16 degrees elevation, yaw 160, so it is in frame ahead of the boat.
7. Shoreline foam and shallow-water wave attenuation: a blurred top-down depth map of the
   level feeds both the shader and `Ocean.DepthField`. Lands with Phase 6.
8. Weather states: interpolating between `OceanSettings`, already planned in
   `docs/weather-storms-plan.md`.

Also 2026-09-14: `OceanSettings` drops the three Girardot bands for the devlog's single
parameter set (count, speed, direction, spread, distribution, min/max wavelength,
amplitude, steepness) plus the water colours, which `OceanSurface` pushes to the material
every frame. Editing the asset in Play mode rebuilds the sea live. Wave speed is packed
into `_OceanWaveB.w` as the angular frequency so CPU and GPU share it.

Done 2026-09-14 (second pass), after the first shader looked flat and generic: the Godot
`ocean.gdshader` look is ported instead of the devlog recipe alone. Nested snapped grids
(256 cells; 256/1024/4096/8192 m) replace the single quadratic grid and follow whichever
camera is rendering, so the Scene view shows the same sea. Short waves fade at 12 to 32
wavelengths (was 100 to 300, which aliased into moire). Girardot's FFT normal flipbook is
restored from `godot-final` into `Art/Textures/` (importer rule in
`Editor/TextureImportRules.cs`). Colour ramp bands, toon light, whitecaps from the Jacobian
with a time-offset trail, lace foam, waterline/shore lines from scene depth, glint, fog and a
post volume (Neutral tonemap, bloom, vignette, colour adjustments) are in. Debug views live on
`OceanSurface` (Grey/Normals/Height/Fold/Foam/Depth) and `ShotTests` writes one shot per view.
URP trap: the SSAO renderer feature makes `_CameraDepthTexture` come from the DepthNormals
prepass, so opaque shaders need a `DepthNormals` pass (`VertexColorLit` has one now).

Steps 1 to 5 are one shader change. `OceanWaves.hlsl` gains `OceanFrame`, which returns
the normal and the Jacobian; `OceanNormal` wraps it so `OceanProbe.compute` and
`WaveProbeTests` are untouched. Verify with the PlayMode shot.

### Phase 5 — standing on the deck

Kinematic `CharacterController` inheriting deck velocity; first-person camera with the
swell high-pass filter from the README. Roadmap tick: "player stands on a moving deck
without dying".

After Phase 5: choose netcode (FishNet + Steam transport is the default candidate), then
sails, then grab and rope, each as its own plan.

### Phase 6 — procedural levels

The next goal after 3b. Not planned yet. Shoreline foam and shallow-water attenuation
from 3b depend on it.
