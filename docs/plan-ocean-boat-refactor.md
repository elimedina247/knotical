# Ocean & boat refactor + splash/foam plan

Working plan for restructuring the ocean into three bands, componentizing buoyancy, adding a
simple debug boat, and layering in Girardot-style splash and foam presentation. Written to be
picked up by any future session with no other context. Reference video series: Ghislain
Girardot, "Advanced Boat Simulation" parts 1–3 (UE5/Niagara). We are NOT porting his GPU
readback pipeline — our ocean is analytic on CPU and GPU, so all his readback machinery is
unnecessary here. We are borrowing: pontoon debug visuals (part 1), splash system ideas
(part 3), persistent foam capture (part 2).

## Ground rules (do not skip)

- **Ask Eli before any build or run** (`dotnet build`, Godot CLI, anything that compiles or
  launches). Once granted for a task, iterate freely for that task. Godot console binary path
  is in CLAUDE.md. `dotnet build` before every headless run or the assembly is stale.
- **Eli owns git.** Never commit or push unprompted.
- **No code comments.** None. Names and structure carry meaning; explanation goes in chat.
- **`ocean.tscn` instance overrides beat code defaults silently.** Before concluding an
  exported value has no effect, grep `ocean.tscn` for it. This has burned a full session.
- **CPU/GPU mirror:** `Ocean.cs` and `shaders/ocean.gdshader` implement the same wave maths.
  A change to the wave function on one side is a change to both, or buoyancy stops matching
  what is drawn. (Physics *weighting* of bands is CPU-only and exempt — see Phase 1.)
- **Measure, don't predict.** Physics claims get verified with a headless probe run
  (`--quit-after`, read the trace). Predictions have been wrong in this project before.
- **Ask when gameplay design is ambiguous** — one short question listing options. Open
  questions are listed at the bottom; resolve them with Eli at the phase that needs them.
- Eli does not sail: gloss nautical terms in chat replies, plain word where one exists.

## Current state (verified 2026-08-17)

- `scripts/ocean/Ocean.cs` — autoload, sum of up to 24 Gerstner waves, two bands:
  swell (default 6 waves, 40–260 m) and chop (default 10, 4–22 m). **All CPU physics queries
  (`GetHeight`, `GetNormal`, `GetVerticalVelocity`, `GetFlow`) loop only `_swellCount` —
  chop is already shader-only.** Sea state (significant height, peak wavelength) chases wind
  with a 45 s exponential lag. Amplitudes re-solved from wind every frame.
- `scripts/ocean/OceanSettings.cs` — band definitions, spectrum solve, wind→sea formulas.
- `scripts/ocean/OceanSurface.cs` — nested camera-following grids, pushes wave uniforms,
  wake trail uniforms, hull masks. Note line ~122: camera-underwater check uses `GetHeight`.
- `shaders/ocean.gdshader` — renders ALL waves (`wave_count` loop), distance-fades short
  ones. Already has: wake trail foam (`wake_points`), hull masks, crest foam, detail normals.
- `scripts/boat/BoatHull.cs` — monolith (~850 lines): RigidBody3D with 12 weighted probes
  (2×5 stations + bow/stern), per-probe buoyancy + heave/slam damping + fwd/lat drag against
  `GetFlow`, `SlopePush` normal tilt, keel righting, wet-scaled roll/pitch/yaw damping,
  capsize state machine, AND the sailing model (Polar drive curve, no-go gate, HeelTorque,
  RudderAuthority, foil collection). Sails/rudder are separate nodes via `IFoil`.
- `scripts/wind/Wind.cs` — autoload, incommensurable-oscillator gusts/veer,
  `AccumulatedDrift` (useful for foam drift later).
- `scripts/ocean/WaveProbe.cs` — debug marker riding `GetHeight`/`GetNormal`; the CPU/GPU
  agreement check.
- `scripts/boat/DeckCargo.cs` — crates follow decks; they do NOT float. No buoyancy.
- `scripts/boat/BoatWake.cs` + shader wake uniforms — existing foam trail system.
- `scripts/debug/MapOverlay.cs` — the debug-toggle idiom to copy: `_Input`, physical key
  (`Key.M`), `SetInputAsHandled`, `Visible` flip.
- Scenes: `ocean.tscn` instances `boat_2.tscn` (working tuned boat), `boat_3.tscn` (bigger,
  in progress), `cargo_crate*.tscn`, `rope_coil.tscn`.
- Memory note `two-band-ocean-and-probe-hull.md` describes the two-band split — update it
  when Phase 1 lands.

## Progress log

- 2026-08-17: **Phase 1 implemented** (not yet verified in a run), expanded beyond the
  original scope at Eli's direction: wind is now FULLY decoupled from the sea, and calm
  vs crazy water is authored spatially. Three bands with per-wave physics weights landed
  as planned, plus: `Ocean.SeaScale(xz)` — a local sea-state multiplier built from island
  shallows (auto-gathered `Island*` CylinderMesh nodes → calm) and new `SeaZone` nodes
  (any intensity, for storm corridors or glass patches). Contrary to the Phase 1 text
  below, the shaders DID change: `sea_scale()` is mirrored in `ocean.gdshader` and
  `map_overlay.gdshader` (the M map now shows the authored field, physics-weighted).
  `GetRenderedHeight`/`GetRenderedVerticalVelocity` added; cosmetic callers re-pointed;
  hull/rudder/sail stay on physics queries. Removed: `SetSeaState`, the 45 s development
  lag, all `Developed*` wind formulas — a joining network client now needs only
  `Ocean.Time`. Amplitudes are static between `Rebuild()` calls; runtime settings edits
  need an explicit `Rebuild()`. `--seacap=X` now clamps the three band heights.
  Phase 2 shrinks to just the slow set envelope; its wind-independence bullets are done.

## Phase order and why

1. Three-band ocean (small, self-contained, unlocks everything else)
2. Independent dramatic swell (the actual feel goal)
3. BuoyancyComponent + simple dev boat + debug gizmos (test bed for tuning)
4. Extract SailingRig from BoatHull (behavior-preserving)
5. Splash particles (needs phase 3's probe events)
6. Persistent foam capture (needs phase 5's splashes)
7. Retune + consolidate + memory updates

Phases 1–2 and 3 are independent enough to swap if convenient. 5 needs 3. 6 needs 5.

---

## Phase 1 — Three-band ocean with per-band physics weights

Goal: swell / medium / chop instead of swell / chop, with graded physics influence
(swell 1.0, medium ~0.35 default, chop 0.0) instead of the current binary cutoff.

Changes:

- `OceanSettings.cs`: three band definitions. Reuse the existing per-band property pattern.
  Suggested budget within `MaxWaves = 24`: swell 3, medium 6, chop 10 (19 total, headroom
  left). Wavelength defaults: swell 150–420 m, medium = current swell band (40–260 m),
  chop unchanged (4–22 m). Add `MediumPhysicsWeight` (0–1, default 0.35) and
  `SwellPhysicsWeight` (default 1). `BuildSkeleton` writes bands contiguously:
  [swell | medium | chop]. Amplitude solve: swell gets its own target height (Phase 2 makes
  it independent; in Phase 1 keep it wind-driven so the sea looks the same), medium keeps the
  current spectral solve, chop unchanged.
- `Ocean.cs`: replace `_swellCount` with `_physicsCount` (= swell + medium) plus a parallel
  `_physicsWeight[]` per wave. Every CPU query (`GetHeight`, `GetNormal`,
  `GetVerticalVelocity`, `GetFlow`) multiplies amplitude by its weight. Add
  `GetRenderedHeight(Vector2)` that sums ALL bands at full weight — the shader-truth height.
- Callers to re-point at `GetRenderedHeight`: `OceanSurface` camera-submerged check,
  `WaveProbe` (add an exported toggle: rendered vs physics height — rendered is the CPU/GPU
  agreement check, physics shows what hulls feel). `Rudder.Submersion`, `Sail.Windage`,
  audio scripts: leave on physics-weighted `GetHeight` (they should track hull motion).
- `ocean.gdshader`: **no change required.** The shader already renders all waves; band
  weighting is physics-only. The existing documented tolerance (probes average out
  render-vs-physics mismatch) now covers the medium band at partial weight — acceptable
  while medium amplitudes stay moderate; if hulls visibly hover in medium seas, raise
  `MediumPhysicsWeight` rather than adding shader complexity.
- **Migration trap:** the tuned `OceanSettings` resource lives embedded in `ocean.tscn` (or a
  `.tres`). Renamed/added exported properties silently drop stale overrides and revert to
  C# defaults. Grep `ocean.tscn` for `SwellCount|ChopCount|Wavelength|Steepness` etc.,
  carry every tuned value into the new band layout by hand, and diff the visible result.

Verify (ask permission first): headless run of a probe scene printing `GetHeight` and
`GetRenderedHeight` at fixed points/times before vs after — physics track should match the
old two-band values with medium at weight 1.0 and the new swell band zeroed; then confirm
defaults. Visual check: sea looks unchanged with default settings.

## Phase 2 — Independent dramatic swell

Goal: big, slow swell that exists even in light wind, with occasional larger "sets", while
medium waves stay wind-driven. Wind must not equal roughness.

Changes, all in `OceanSettings.cs`/`Ocean.cs` (no shader work — amplitudes are already
pushed to the GPU every frame, so the envelope propagates for free):

- Swell band gets: `SwellBaseHeight` (m, wind-independent floor, suggest 1.2),
  `SwellWindCoupling` (0–1, suggest 0.25 — fraction of wind-driven height added on top),
  its own fixed direction (already exists: `SwellDirectionDeg`), and NO development lag
  (or a much longer one, ~180 s) since it represents distant weather.
- Set envelope: deterministic slow modulation of swell amplitude,
  `envelope = 1 + SetDepth * oscSum(t)`, where `oscSum` is 2–3 incommensurable sine periods
  (suggest 90 s / 210 s / 330 s, weights 1 / 0.6 / 0.35, normalized) — same pattern as
  `Wind.WeightedOscillator`. `SetDepth` ~0.5 → swell breathes between roughly 0.6× and
  1.6×, with rare alignments producing the dramatic sets. Pure function of `Ocean.Time`:
  deterministic, network-free, matches the whole architecture. (Decision was made against
  host-triggered rogue-wave events; do not reintroduce them.)
- Keep the feel constraints from memory `boat-physics-design-goals.md`: dead run fastest,
  wind never flips the boat, waves must still tilt the deck enough that cargo slides.

Verify: headless hull trace (BoatHull `--trace=1`) in light wind — tilt and bow motion
should show slow large-period movement instead of near-flat water; in strong wind, medium
band grows but jitter stays bounded (chop is visual-only). Watch `bowg` (bow g-force) in the
trace: dramatic ≠ violent; sets should raise pitch amplitude, not slam acceleration.

## Phase 3 — BuoyancyComponent, simple dev boat, debug gizmos

Goal: the screenshot. A minimal boat floating on 6 probes with wireframe-sphere pontoons,
force lines, and a HUD listing buoyant bodies — plus a reusable component so crates and
planks can float too.

New files:

- `scripts/boat/Buoyancy.cs` — `Node3D`, child of any `RigidBody3D`. Finds probe positions
  from its `Marker3D` children (names `Probe*`); probes are plain transforms, exactly the
  target architecture. Exports: `TotalDisplacedVolume` (or derive from parent mass /
  water density × a `Displacement` ratio like BoatHull's), per-probe span, `HeaveDamping`,
  `SlamDrag`, `ForwardDrag`, `LateralDrag`, `SlopePush`, `RightingGain`,
  `RollDamping`/`PitchDamping`. Physics: copy the per-probe force model from
  `BoatHull.Probe()` (depth vs `Ocean.GetHeight`, buoyancy along `SlopePush`-tilted normal,
  damping/drag against `Ocean.GetFlow`) — it is tuned and correct. Apply forces in
  `_PhysicsProcess` via `body.ApplyForce(force, worldPos - body.GlobalPosition)`; a child
  node cannot hook `_IntegrateForces`, and ApplyForce from `_PhysicsProcess` is fine at this
  scale. Keep a static `Active` registry (same pattern as `BoatHull.Active`) for the HUD and
  gizmos. Expose per-probe telemetry for the gizmos: world position, submersion 0–1, last
  applied force.
- `boat_dev.tscn` — `RigidBody3D` + `Buoyancy` + 6 probe markers (bow L/R, mid L/R,
  stern L/R) + a simple hull mesh + one convex `CollisionShape3D`. See open question 2 for
  the hull's look. Mass ~ a small skiff (500–1500 kg). No sails, no rudder, no deck logic.
- Optionally (open question 3): add `Buoyancy` with 1 probe to `cargo_crate.tscn` /
  4 probes to a plank scene so loose cargo floats like his demo. `DeckCargo` deck-following
  is unaffected (different mechanism, both are forces on the same body).
- `scripts/debug/BuoyancyGizmos.cs` — one autoload-or-scene node drawing all `Active`
  components with a single `MeshInstance3D` + `ImmediateMesh` rebuilt each frame
  (lines only, ≤ a few hundred segments — cheap). Per probe: wireframe sphere (3 orthogonal
  rings, 24 segments each), radius from probe span, color by submersion (orange dry →
  saturated when wet, matching the reference screenshot); yellow line from probe along the
  last applied force, length `force / (body.Mass * 9.81) * K` so a fully loaded pontoon
  reads ~1 boat-length. Material: `StandardMaterial3D` unshaded, vertex color, no depth
  test. HUD: `CanvasLayer` + `Label`, top-left, `"Buoyant Pontoons: N"` then one line per
  active body name — same as the screenshot. Toggle: copy `MapOverlay.cs`'s `_Input`
  physical-key pattern; key is open question 1. Gizmos node lives in `ocean.tscn`, hidden
  by default.

Verify: run `ocean.tscn` (ask first) — dev boat floats level and rides swell slowly, gizmo
spheres sit on the rendered surface (this doubles as a CPU/GPU sync check), force lines
pulse as waves pass, crates bob if question 3 is yes. Big boats unaffected.

## Phase 4 — Extract SailingRig from BoatHull

Goal: BoatHull keeps water physics; a child `SailingRig` node owns wind physics. Zero
behavior change, verified by trace diff.

- New `scripts/boat/SailingRig.cs` (`Node3D`, child of BoatHull). Move from BoatHull: foil
  collection (`RefreshRig`/`CollectFoils`/`_foils`), the foil force loop, `Polar` and its
  five drive exports, `HeelTorque` + `HeelLever`/`MaxHeelDegrees`, `RudderAuthority`,
  steer-torque telemetry. Keep in BoatHull: probes/buoyancy, all drag, wave-wall drag,
  `KeelLever`, damping, capsize, `MaxAcceleration` clamps, gravity.
- Contract: BoatHull calls `rig.Accumulate(state, level, _com, _linear, _angular,
  out force, out torque, out canvasTelemetry)` inside `_IntegrateForces` so force
  application order and the trace stay identical. BoatHull keeps `RigForce` property fed
  from the rig for `SailSound`/others (grep consumers before moving anything public:
  `RigForce`, `DeckAcceleration`, `FoilCount`, `SubmergedVolume`).
- `boat_2.tscn`/`boat_3.tscn`: insert the `SailingRig` node; sails/booms/rudder stay where
  they are in the tree (`CollectFoils` walks from BoatHull root today — have the rig walk
  the same root so no scene reparenting is needed).
- The cmdline tuning args in `_Ready` that map to moved properties must keep working
  (route them through to the rig) — headless comparison scripts depend on them.

Verify: two headless runs, same wind args, before vs after — `--trace=1` lines should match
field-for-field within float noise. Any drift means force order changed; fix, don't retune.

Deferred decision (open question 4): whether BoatHull's internal probe code later gets
replaced by the shared `Buoyancy` component. Default: leave the tuned hull alone.

## Phase 5 — Splash presentation

Goal: Girardot part-3-style impact splashes and bow spray, driven entirely by CPU data we
already have — no readback, no screen-space tricks.

- Event source: in `Buoyancy.cs` (and mirrored minimally in `BoatHull.Probe`), detect probe
  dry→wet transitions and record entry speed (`relY` at crossing). Raise a
  `SplashRequested(worldPos, entrySpeed, probeArea)` signal / static event when entry speed
  exceeds ~1.5 m/s. Bow spray source: BoatHull already computes bow clearance in `Log` —
  promote stem position + headway [forward speed] to properties the emitter can poll.
- New `scripts/vfx/SplashEmitter.cs` + `GPUParticles3D` (one node per boat, reused):
  one-shot `Emitting` bursts positioned at the event point, `Amount` scaled by entry speed,
  capped concurrent bursts (~4). Water surface height at spawn comes from
  `Ocean.GetRenderedHeight` — exact, same frame.
- New `shaders/splash_particles.gdshader` (particle process shader): ballistic droplets +
  gravity; kill (or fade) a particle when its Y drops below the wave surface. The shader
  computes surface height with the same wave loop as `ocean.gdshader` — pass the same
  `waves[24]`/`wave_phase`/`wave_time` uniforms (swell+medium indices are enough; chop is
  too fine to matter for kill height). This is the payoff of the analytic ocean: particles
  interact with the exact rendered surface with zero readback. Draw pass: quad +
  soft-circle texture, color from `GamePalette.Foam`, unshaded, slight additive.
- Bow spray: second `GPUParticles3D`, continuous, gated by headway > ~3 m/s and bow
  clearance < ~1 m, strength scaling with speed².
- On droplet death at the surface, feed Phase 6: emit into the foam capture (see below) —
  design the death path with that hook in mind (sub-emitter or just splash events also
  stamping foam directly).

Verify: visual, in-editor run. Watch frame time with several boats + crates splashing;
particle counts are the lever.

## Phase 6 — Persistent foam capture

Goal: Girardot part-2 equivalent — splashes and wakes leave foam patches on the water that
linger, drift, and fade. Godot mapping: a world-space accumulation texture, not Niagara.

- New `scripts/vfx/FoamCapture.cs` owning a `SubViewport` (suggest 1024×1024, covering a
  ~512 m square centred on the camera, snapped to whole texels exactly like
  `OceanSurface`'s grid snapping — unsnapped, the foam crawls when the camera moves).
  Orthographic top-down camera, dedicated cull layer.
- Feedback fade: full-rect quad in the viewport multiplying last frame's texture by
  `exp(-dt / FoamLife)` (frame-rate independent; suggest FoamLife ~8 s). When the region
  re-centres, offset the previous-frame sample by the texel delta so foam stays world-fixed.
- Writers into the viewport: splash events stamp soft white blobs (small quads on the foam
  layer, sized by entry speed); optionally bow spray dabs continuously. Existing
  `BoatWake` trail keeps working as-is in Phase 6; folding it into the capture texture is a
  later consolidation (flag it, don't do two things at once).
- Reader: `ocean.gdshader` gains `uniform sampler2D foam_capture` + a `vec4` region
  (centre XZ, half-extent, strength), sampled in world space and added into the existing
  foam term (`surface_foam`/`crest_foam` blend already exists — reuse its compositing).
  Push the uniform from `FoamCapture` via `OceanSurface`'s material (same pattern as
  `PushWakeUniforms`).
- Drift: cheap and pretty — offset the sample position by a small multiple of
  `Wind.AccumulatedDrift` delta so foam slides downwind without simulating advection.

Verify: visual — a crate dropped from height leaves a fading patch; a slamming bow leaves
foam that trails downwind; no crawling when sailing (snapping correct); GPU cost of one
1024² viewport is negligible but confirm.

## Phase 7 — Retune and consolidate

- Tune the three bands against the feel goal (slow dramatic movement, occasional big sets,
  no wind jitter) using the dev boat + gizmos as the test bed. Tuning happens in
  `ocean.tscn`'s settings resource — remember overrides outrank code defaults.
- Update memory `two-band-ocean-and-probe-hull.md` → three bands + weights; add a memory
  for the foam/splash architecture if it deviated from this plan.
- Optional, per open question 4: A/B the big boats on shared `Buoyancy` vs internal probes
  (headless trace comparison), migrate only if identical or better.
- Optional: fold `BoatWake` into the foam capture texture and delete its shader uniforms.

---

## Open questions for Eli (ask at the phase that needs them, one at a time)

1. **Gizmo toggle key** — MapOverlay uses M. Suggest B (buoyancy) for the pontoon
   gizmos + HUD. (Phase 3)
2. **Dev boat look** — plain box/raft (fastest, clearly a dev object) vs a tiny hull via the
   existing parametric `HullForm` (prettier, free) — which? (Phase 3)
3. **Should loose cargo float?** Crates/planks with their own pontoons like the reference
   screenshot, or dev boat only for now? (Phase 3)
4. **Migrate boat_2/boat_3 onto the shared Buoyancy component** or leave their tuned
   internal probes permanently? Default: leave them. (Phase 7)
5. **Splash art direction** — billboarded soft sprites only, or sprites + a few mesh chunks
   for big impacts (his look)? Affects Phase 5 scope. (Phase 5)

## Acceptance summary

| Phase | Done when |
|---|---|
| 1 | Sea renders identically with defaults; physics queries weight bands; rendered-height callers re-pointed; ocean.tscn values migrated |
| 2 | Light wind still shows slow swell with periodic bigger sets; trace shows slow pitch, bounded bow g |
| 3 | Dev boat + (crates) float convincingly; gizmos match screenshot: wireframe pontoons, force lines, HUD list |
| 4 | Trace before/after identical; BoatHull ~sailing-free; scenes updated |
| 5 | Slams and bow spray produce splashes that die on the analytic surface; no readback anywhere |
| 6 | Splashes leave world-fixed fading foam patches that drift downwind; no crawl |
| 7 | Feel target met; memories updated; optional consolidations decided |
