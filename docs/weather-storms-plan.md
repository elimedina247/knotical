# Weather & storms — plan

Agreed 2026-08-18. **Nothing here is implemented yet** — this is the design to pick
up from. Read `docs/girardot-vs-knotical.md` first for how the ocean got here.

## The idea in one line

A storm does not generate waves. It is a moving multiplier that raises the waves
that are already there — plus the wind, plus the sky. One number, `s`, from 0 to 1.

## What already exists

- **Ocean** (`scripts/ocean/Ocean.cs`, `OceanSettings.cs`) — three seeded bands
  (swell / medium / chop), every wave derived from one `Seed`, static between
  rebuilds. Physics equals the rendered surface.
- **Depth field** (`scripts/ocean/SeaDepthField.cs`) — bakes the Terrain3D seabed
  into a sea-state multiplier: 0.15 in shallows, 1.0 at baseline depth, up to 1.6
  in sculpted deeps. CPU and GPU sample the same texture. Each band already has a
  `DepthResponse` (swell 1.0, medium 0.6, chop 0.15).
- **Wind** (`scripts/wind/Wind.cs`) — pure function of time. Speed is base × summed
  sines (47/17.1/6.3/2.9 s gusts); direction is base + slower sines (137/61/23.3 s
  veer, meaning the direction wanders). **Global** — same at every point in the
  world. Drives sails, wind streaks, foam drift. Never touches the waves.
- Everything above is a pure function of position and time, which is why the whole
  ocean syncs over the network as a single clock value. **Storms must preserve this.**

## The one hard rule

Wave **amplitude and steepness may vary freely** in space and time.
Wave **wavelength, direction, and phase may not change**, ever.

The phase term is `k·x − ωt`. Changing `k` at a point kilometres from the origin
shifts the phase by thousands of radians, so the entire sea visibly slides and
shimmers. Storms therefore scale existing waves and never re-generate the wave set.

*Optional, only if longer storm waves are ever wanted:* author the swell band to
already contain 400–600 m waves sitting near zero amplitude in calm water, and let
the storm raise them. Same skeleton, no phase break. Skip unless the look demands it.

## Storm intensity

```
s(x, z, t) = shape(distance from moving centre) × lifecycle(t) × depthGate(x, z)
```

- **shape** — soft radial falloff from the storm centre; centre drifts over time.
- **lifecycle** — ramp up, hold, fade out.
- **depthGate** — reuse the baked field. A storm drifting toward the coast dies over
  the shallows. This is what makes harbours safe and "run for shallow water" a real
  escape rather than a scripted one.

### Percent chance without breaking determinism

Do **not** roll dice per frame. Divide weather time into epochs (~20 min). For each
epoch and each of a few storm slots, hash the world seed with the epoch index to get:
storm exists (the percentage), start position, radius, drift, peak strength, ramp
timing. A 30% chance really is 30% of epochs, but every client derives the identical
storm from the clock alone — nothing to replicate.

Depth enters twice: **spawn eligibility** (candidate centres rejected unless the
field there is above a threshold, so storms only form over deeps) and **continuous
gating** (above).

## What a storm does

| System | Effect |
|---|---|
| Waves | Per-band amplitude gain. Start ~swell ×6, medium ×3, chop ×1.5 — everything grows, chop least, so it is huge without being nauseating |
| Steepness | Raise modestly. This is the one value that can push the surface toward folding; amplitude is free, steepness is not |
| Wind | Speed rises with `s`; optionally rotate direction around the storm centre |
| Sky | Cloud cover, darkness, light energy — `SkyController` / `DayCycle` consume the same `s` |
| Map (M) | Draws storms nearly free — the overlay already samples the same field |

Waves and wind are both functions of `s`. **Neither is a function of the other** —
that keeps the architecture intact while giving the correlation players expect.

## Wind has to become spatial

Storms are local; wind currently is not. `Wind.GetVelocity(Vector2 worldXZ)` already
exists and ignores its argument — that is the seam. Give it a real implementation and
move every caller onto it (sails, `WindStreaks`, `DebugWindHud`, anything reading
`Wind.Instance.Velocity` / `.Speed` directly). Until this lands, two players in
different places would share one wind, which breaks co-op.

## Build order

1. **One manually-triggered storm** — place it, ramp it with a debug key, no
   randomness. Feel tuning is the expensive part and is miserable under random
   spawns. Get "how big is thrilling vs unplayable" answered here.
2. Depth gate on storm intensity.
3. Hash-based epoch schedule (the percent chance). ~an hour once the feel is right.
4. Spatial wind migration.
5. Sky, audio, map presentation.

## Dials to expect

| Name | Rough start | What it does |
|---|---|---|
| `StormChancePerEpoch` | 0.3 | Percentage, evaluated per epoch |
| `StormEpochMinutes` | 20 | How often the dice are rolled |
| `StormRadius` | 800–2000 m | Size of the weather cell |
| `StormDrift` | 3–8 m/s | How fast it crosses the map (world is 12 km) |
| `StormRampMinutes` | 3 up / 5 hold / 4 down | Lifecycle |
| `StormMinField` | ~1.2 | Depth threshold for a storm to form at all |
| `SwellStormGain` | 6 | Per-band amplitude multiplier at full storm |
| `MediumStormGain` | 3 | |
| `ChopStormGain` | 1.5 | Raise for violence, lower for comfort |

## Open questions

- How big is too big? Measure with the boats, not by eye — the existing headless
  probe pattern (`--quit-after`, read the trace) is the tool.
- Chop gain in storms: bigger seas were *more* comfortable than the everyday sea in
  the last measurement, because the size lived in slow swell. Raising chop is what
  reintroduces the queasiness.
- Should storms be visible on the map from the start, or discovered by sailing?
- Do storms need to affect anything besides waves/wind/sky — cargo, rope, crew?

## Files this will touch

- New: a `Storms.cs` (or `Weather.cs`) autoload owning the schedule and `s`.
- `scripts/ocean/Ocean.cs` — fold `s` into the per-wave scale alongside the field.
- `shaders/ocean.gdshader`, `shaders/map_overlay.gdshader` — mirror it, same as the
  field is mirrored. **CPU and GPU must match or boats float off the drawn surface.**
- `scripts/ocean/OceanSettings.cs` — per-band storm gains.
- `scripts/wind/Wind.cs` — spatial `GetVelocity`, storm-driven speed/direction.
- `scripts/debug/DebugWindHud.cs` — show `s` next to the existing `field` readout.
