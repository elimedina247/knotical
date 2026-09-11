# Overnight 2026-09-08 — toon look, new boat, whitecaps

Nothing is committed. Everything below is in the working tree on top of Eli's own
uncommitted work (GrappleGun, PlayerGrab, SplashEmitter, MapOverlay, DeckCargo, Ocean,
FoamCapture untouched; OceanSurface.cs and BoatHull.cs carry both).

## What changed, by task

| Task | Files | Revert by |
|---|---|---|
| Whitecaps, foam trail, hull cut-out, shore lines, cel water, blue palette | `shaders/ocean.gdshader`, `scripts/ocean/OceanSurface.cs` (wind/sun uniforms), `scripts/boat/SailController.cs` + `scripts/boat/BoatHull.cs` (GetSurfaceMask now: halfLen, halfBeam, inset, ring), `scripts/style/GamePalette.cs` | `git checkout` those files |
| Cel shading + ink outlines | `shaders/toon.gdshader`, `shaders/deck_toon.gdshader`, `shaders/ink_outline.gdshader`, `scripts/style/InkOutline.cs`, `materials/*.tres`, `shaders/sail.gdshader` (light()), `shaders/sky.gdshader` (hard clouds), InkOutline nodes in `playground.tscn` + `ocean.tscn` | delete the new files, checkout the shaders/scenes |
| Terrain flat colours | `shaders/terrain_toon.gdshader`, `playground_map.tscn` (`shader_override_enabled = true`) | set `shader_override_enabled = false` |
| Props/character on toon | `dock.tscn`, `cargo_crate*.tscn`, `rope_coil.tscn`, `character.tscn` (StandardMaterial → ShaderMaterial toon) | checkout |
| New boat | `tools/build_boat.py` (Blender), `tools/build_boat_scene.py`, `models/boat_toon.glb/.json`, `boat_toon.tscn`, `scripts/style/ToonPaint.cs`, `scripts/boat/IDeckBody.cs` (+BowWorld/HullLength/BeamWidth), `scripts/boat/BoatWake.cs` (any IDeckBody), `playground.tscn` (instances boat_toon at the dock), `probe_sail.tscn` (points at boat_toon) | swap the playground instance back to `boat_custom.tscn` |
| Probes | `probe_shots.tscn` + `tools/shot_probe.gd` (screenshots), `probe_deck.tscn` + `tools/deck_probe.gd` (deck collision), `probe_fold.tscn` + `scripts/debug/FoldProbe.cs` (fold stats), `scripts/debug/SailProbe.cs` (deploys sail) | delete |

## Numbers

- Fold ceiling with the never-fold pinch: max 0.49, p99 0.27, p999 0.34. Old trigger needed 0.49.
  New trigger: `cap_threshold_calm` 0.30 → `cap_threshold_gale` 0.15 across `wind_speed` 0..16.
- Polar at wind 9 (probe_sail, gusts off): run 7.7, broad reach 6.3–6.9, beam 4.4–5.0,
  close reach 2.7–3.4, irons 0.3 m/s (creeps forward), heel ≤ 12.4°.
- Boat: 13 × 4.4 m, draft 1.5, deck 1.25 above water, quarterdeck +0.8 via 35° ramp,
  helm 2 m of standing room behind the wheel, gangway gaps midships line up with the dock
  (0.15 m step).

## Knobs worth touching first

- Water: `cap_threshold_*`, `trail_opacity`, `surface_foam_opacity` (the lace network),
  `sand_visibility` / `min_opacity` (see-through vs opaque), `shore_*` lines, `band_softness`.
  `debug_view = 1` paints fold / cap / trail as red / green / blue.
- Outlines: InkOutline node → Thickness, DepthSensitivity, NormalSensitivity, FadeEnd.
- Cel bands: `lit_threshold`, `half_band` on toon.gdshader / terrain_toon / deck_toon.
- Boat shape: constants at the top of `tools/build_boat.py`; rebuild with the three commands in
  the memory note (Blender → build_boat_scene.py → `--import`).

## Known gaps / next session

- Ship look pass together (as agreed): rigging, more trim, figurehead, transom nameplate,
  crow's nest, lantern glow at night, cabin door/portholes.
- Boat is not moored; it drifts slowly off the dock in wind. A cleat rope or an anchor is needed.
- Sky clouds are harder but still blobby; a proper WW cloud shape pass is pending.
- Character is only recoloured (toon skin + outline), not redesigned toward the PEAK refs.
- ocean.tscn's big ships got the new hull cut-out and outlines but no look pass.
