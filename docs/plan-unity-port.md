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

Camera-following clipmap mesh; URP HLSL vertex shader reading the wave arrays via
`Shader.SetGlobalVectorArray`; displacement and analytic normal only, flat colour.
A `WaveProbe` port reads one displaced vertex back and compares it to `GetHeight`.
Whitecaps, foam capture, cel banding, shore lines are not ported.

### Phase 4 — first floating boat

Kenney Pirate Kit ship (CC0) as a `Rigidbody` with 4–6 pontoons, driven by Girardot's
three-number motor (propeller strength, rudder strength, max speed). Pontoon gizmos.
Roadmap tick: "boat floats, rocks, and doesn't explode".

### Phase 5 — standing on the deck

Kinematic `CharacterController` inheriting deck velocity; first-person camera with the
swell high-pass filter from the README. Roadmap tick: "player stands on a moving deck
without dying".

After Phase 5: choose netcode (FishNet + Steam transport is the default candidate), then
sails, then grab and rope, each as its own plan.
