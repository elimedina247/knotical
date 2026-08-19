# Girardot's boat sim vs ours — the differences, in plain English

Source: the three UE5 example projects in `C:\Users\elime\Documents\ghislaine`
(Advanced Boat Sim, Jan 2024; Niagara Buoyancy System, Jul 2025; Foam System,
Aug 2025). The `.uasset` files are binary, but their name tables expose every
parameter, variable, and graph comment, so the structure below is read from the
actual files, not guessed from the videos.

## How his water works

The whole ocean is **eleven numbers in one shared parameter collection**
(`MC_GersnterWaves`): `Waves` (how many), `Seed`, `MinLength`/`MaxLength`,
`MinAmplitude`/`MaxAmplitude`, `MinSteepness`/`MaxSteepness`, `Spread`,
`Distribution`, and a `Wind` direction vector. Every individual wave is
*derived* from the seed inside those ranges. Change the seed, get a new sea.
There is no weather simulation, no wind coupling, no calm or rough regions —
the sea is the same everywhere and only changes when a human edits a number.

The same collection is read by the water material (GPU rendering) and mirrored
into Niagara (`FX_Col_GerstnerWaves`) for physics sampling. One definition,
evaluated in two places. **What physics feels is exactly what the eye sees** —
there is no separate "physics sea".

He needs a GPU readback (Niagara particles sample the wave and report heights
back to the CPU) because UE materials can't be queried from gameplay code. We
never needed that machinery: our ocean is analytic, so the CPU can just compute
the same function the shader draws. That part of his design stays UE-only.

## How his buoyancy works

One reusable component, `BP_Buoyancy_Component`, floats anything. A thing that
wants to float implements a two-function interface: *give me your pontoon
positions* (a handful of points under the hull) and *give me your physics body*.
That's it — his boat, crate, and plank all float through the identical code.

Per pontoon, per frame:

- **Depth** below the water surface → normalized 0–1 by the pontoon `Radius`.
- **Buoyancy**: an upward force scaled by submersion and a single
  `Coefficient` ("overall buoyancy multiplier — prefer increasing the radius
  first"), clamped by `MaxForce`.
- **Vertical damping**: two coefficients (`DampingFactor1/2`), one linear in
  vertical speed, one quadratic — this is what kills the endless bobbing.
- **Planar drag** (sideways/forward water resistance): two coefficients
  (`DragCoefficient/2`) blended up to `MaxDragSpeed`, applied only while the
  pontoon is wet and moving.
- **Angular drag**: one number, applied while wet.

Extras: `ApplyWaveNormal` (fit a plane through the pontoons' water-surface
points and tilt the body to match — used by the crate so it rides wave slopes),
`SnapToWaterOnActivation` (teleport the body onto the surface over a few
iterations so it doesn't spawn mid-air), `bDrawDebug` (the wireframe pontoon
spheres), and events (`BuoyancyStarted/Applied/Stopped`) that feed splashes and
controller rumble.

## How his boat works

`BP_Pawn` is tiny. Three tuning numbers: `PropellerStrength`, `RudderStrength`,
`MaxSpeed`. Two rules, quoted from his own graph comments: *"move
forward/backward only if speed limit isn't reached (or going reverse) and if
motor is submerged"* and *"turn only if boat has forward/backward velocity"*.
Thrust is a force applied at the motor's position; steering is a torque. The
boat floats because the buoyancy component floats it — the pawn adds nothing
about water. No polar curves, no keel model, no capsize logic.

## How his foam works

A top-down capture writes boat foam into a render target (`RT_Foam`), a
composite material fades it each frame, and the water material samples it back.
Our `FoamCapture.cs` + `foam_capture` uniform already copy this design.

## Where ours differed (before the refactor)

| His | Ours (was) |
|---|---|
| One wave band, ~11 authored numbers, seed-derived waves | Three bands (swell/medium/chop), ~30 knobs, amplitudes re-solved from a spectrum model every frame |
| Physics sea == rendered sea | Physics felt a *weighted* subset (`SwellPhysicsWeight`, `MediumPhysicsWeight`…), so hulls and visuals could disagree |
| Sea identical everywhere; changes only when a human edits it | Sea varied by place (terrain-baked calm-shallows field, `SeaZone` overrides) and by time (slow "set" breathing envelope) |
| One generic buoyancy component for boat, crate, plank | A generic `Buoyancy.cs` for cargo, but the boat used a separate ~850-line `BoatHull` monolith with its own probe model |
| Boat = three numbers + two rules | Motor was an `IFoil` routed through `SailingRig`'s force loop, on top of the full sailing hull |
| Debug pontoon spheres + force lines + HUD body list | Same (`BuoyancyGizmos`) — already matched |
| Persistent foam render target | Same (`FoamCapture`) — already matched |

## What the refactor changed

- `OceanSettings` is now his eleven numbers: `WaveCount`, `Seed`,
  `MinWavelength`/`MaxWavelength`, `MinAmplitude`/`MaxAmplitude`,
  `MinSteepness`/`MaxSteepness`, `DirectionDeg`, `SpreadDeg`, `Distribution`.
  Waves derive from the seed once at rebuild; nothing re-solves per frame.
- One sea for everyone: `GetHeight` *is* the rendered surface. The physics
  weights, the set envelope, `SeaDepthField`, and `SeaZone` are deleted.
  (`GetRenderedHeight` remains as an alias so callers didn't churn.)
- `Buoyancy.cs` is rewritten to his pontoon model: `Radius`, `Coefficient`,
  `MaxForce`, `DampingFactor1`/`2`, `DragCoefficient`/`2`, `MaxDragSpeed`,
  `AngularDrag`, `ApplyWaveNormal`, `SnapToWaterOnActivation`. Splash events
  and gizmo telemetry kept. Placement rule learned in testing: pontoons go at
  the body's vertical middle with a radius about half the body height, and
  `Coefficient` sets ride depth — small spheres at the bottom flip the body
  the moment a wave fully covers them.
- The skiff no longer uses `BoatHull`. `boat_skiff.tscn` is a plain
  `RigidBody3D` + `Buoyancy` pontoons + `BoatController.cs`
  (`PropellerStrength`, `RudderStrength`, `MaxSpeed`, his two rules).
- `BoatHull`/`SailingRig` and the big sailing boats are untouched and still
  sail `ocean.tscn`; they now feel the full rendered sea like everything else.

Deliberately not ported: the Niagara readback pipeline (unnecessary — our CPU
already evaluates the exact rendered wave function).

## Update — his FFT flipbook and the splash rework (2026-08-18)

His fine water detail and better-looking splashes both came from textures, so:

- His baked FFT flipbooks were extracted from the UE 4.27 flipbook project
  (`GhisFFTFlipbook_UEProject427_19March2023`) straight out of the `.uasset`
  files (source PNGs live inside zlib-chunked bulk data; UE 5.x assets use
  Oodle instead, which is why the 5.3 splash textures could not be pulled).
  Now in `assets/textures/ghis/`: `fft_water_normals_8x8.png` (wired into
  `ocean.gdshader` as the detail-normal layer, replacing the procedural noise;
  sampled at two world scales with frame interpolation, his packing R=up,
  G/B=horizontal), plus `fft_water_height_8x8.png` and `fft_water_crest_8x8.png`
  extracted and ready for later use (micro-displacement, foam mask).
- Splashes were blank white quads — that was the whole problem. Now each splash
  fires a few large billboard plumes using a 2×2 spray-variant sheet
  (`assets/textures/spray_sheet_2x2.png`, procedurally generated — overwrite the
  file with his `T_WaterSplashBig_2X2` export to reskin) plus a burst of
  textured droplets; bow spray uses the sheet too.
- Screen-space reflections enabled in both scene environments; water roughness
  lowered (0.62 → 0.24 base) so the sun glints off the detail normals.
- License note: Girardot's license permits commercial indie use but requires
  'Ghislain GIRARDOT' in the game credits.

## Update — bands and zones on top of the core (2026-08-18, later)

Two deliberate extensions beyond his demo, both layered over the unchanged core:
the single seeded wave list became **three seeded bands** (swell / medium / chop,
same derivation per band, one Seed, ≤24 waves), and a **seabed-baked zone field**
scales them by place — each band with its own response (swell full, chop barely),
so deep sculpted water grows huge rolling swell without growing the jittery chop.
Sculpting the terrain *is* authoring the ocean states: ≤5 m deep = calm, 30 m =
baseline, ≥55 m = 1.6× seas. Physics still equals the rendered surface everywhere;
the whole ocean still syncs over the network as a single clock value.
`ocean_settings_storm.tres` is the big-sea preset (`--seafile=` loads presets in
headless runs).
