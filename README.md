# Knotical

> A co-op physics sailing game about delivering things that should not be on a boat.

Four friends, one small ship, and a cargo hold containing something enormous, fragile, and shaped
wrong. Sail it across an open ocean using nothing but a compass, a sandglass, and each other's
increasingly confident guesses about where you are.

There is no map UI. There is no waypoint marker. There is a horizon, a heading, and a rising
suspicion that you passed the island forty minutes ago.

---

## Design Pillars

**The cargo is the antagonist.**
Deliveries are real rigid bodies sitting on your deck. They slide when the ship rolls, they shift
your center of mass, they crush people, and — because the game is first person — they block the
helmsman's view. Every delivery is a hazard you agreed to carry.

**Navigation is a crew activity, not a UI element.**
Every instrument is a physical object somewhere on the ship. The chart is on a table below decks,
so reading it means leaving the helm and shouting corrections up through the hatch. Measuring speed
takes two people and a rope. The game's difficulty curve is how badly your position estimate drifts.

**Jank is the joke.**
Physics does the comedy. People get flung off the deck, cargo escapes over the rail, someone is
dangling from the rigging by one hand. The systems are built to produce stories, not to be fair.

**You can hold on, or you can be useful.**
Gripping occupies your hands. Every wave forces the same decision: secure yourself, or keep working.

---

## Core Loop

1. **Accept a delivery.** Something absurd. Lash it down badly.
2. **Plot a course.** Bearing, speed, time. Write it down or don't.
3. **Sail.** Storms, waterspouts, whirlpools, sharks, and reefs you did not sound for.
4. **Lose the cargo overboard.** Probably.
5. **Dive for it.** The bell goes down, the cargo comes up — if you can find the spot again.
6. **Arrive.** Somewhere. Ideally the right island.

---

## Key Systems

### Ocean

A sum of 4–8 Gerstner waves, evaluated as an **analytic function** — the same math runs in the
vertex shader and in C#. Buoyancy, foam, the dive bell, and swimming all query one function. The
ocean is a pure function of position and time, which means it needs no network syncing at all: sync
one float and every client renders the same sea.

### Buoyancy

Probe-point based. Six to twelve sample points on the hull, each applying Archimedes force and drag
proportional to its own submersion. Free pitch, roll, and heave; no special-case code.

### Sailing

Deliberately arcadey. A keel force that kills lateral velocity, a hand-authored thrust curve with an
upwind no-go zone, and a speed-scaled rudder. Players discover tacking on their own.

### Grip

A damped spring constraint between hand and anchor — not a joint. Three modes off one button:
planted (feet), held (hand), and latched (onto another player, which is how you get human chains off
the stern). Grips have a **break force**, so sea state is the difficulty setting. Stamina drains
proportional to load, not time.

### Diving Bell

The bell is the **cargo recovery system**, not a separate mode. When something goes over the side in
a storm it sinks, and getting it back means dead-reckoning your way to a spot you only half marked.
Water clarity is banded by depth — past the deepest band, it's gone for good.

### Camera

First person on a rolling deck is the worst case for motion sickness, so the ship's slow swell is
high-pass filtered out of the view while sharp impacts pass straight through. Damp the baseline,
unleash the events. When a player is airborne or ragdolled the filter is bypassed entirely.

---

## Gameplay / Tools

### Grapple Gun

Rope is no longer something you pick up and carry — it is fired. Every crew member has a grapple
gun: a line, a hook, and a winch. It replaces hand-thrown rope as the way players interact with
every rope in the game.

**Why a tool instead of hands.** Holding a rope means posing two arms against a moving target every
frame, and the animation never quite lands. A gun is one held prop with one muzzle. Aim, fire, and
the line does the rest — the hard part moves from character animation into rope physics, which is
where the interesting simulation already is.

**Reach.** A fired hook goes where a thrown coil cannot: the masthead, a passing hull, a rock
twenty metres up the cliff. Anchor and swing, and the deck stops being the only place you can
usefully stand.

**Winch.** The line reels in under power, which is one mechanic doing three jobs:

- **Lash** — hook the cargo, hook a cleat, reel until it stops sliding.
- **Retrieve** — hook something floating away and drag it back rather than swimming after it.
- **Ascend** — reel yourself toward the anchor instead of climbing hand over hand.

**Still costs your hands.** The pillar holds: the gun occupies a hand, and a loaded winch is a hand
you are not steering, hauling, or holding on with. Fire, or be useful.

---

## Look

Stylized and arcadey — closest reference is *Sail Forth*. Saturated, high-key, flat-shaded low-poly
geometry, no shadows cast on the water.

Water clarity is driven by a depth ramp, which does double duty: the color bands are readable as
depth contours, so the ocean is also the navigation chart. Shallow turquoise means reef, deep navy
means safe passage, and the black band means whatever you drop there is not coming back.

Reflection and opacity use **separate** Fresnel curves — the physical one for sky reflection, a
gentler capped one for transparency, so you can still see into the water at the grazing angles a
deck-level camera actually spends its time at.

---

## World

Handcrafted, not procedurally generated. Landmarks have to be memorable and — critically —
*shareable*, since directions between players only mean something if everyone's world is the same.
The mental map players build is the progression system.

Islands are authored meshes rather than sculpted terrain. Sea floor geometry exists only where it's
visible: shelves around islands and at dive sites. The whole archipelago fits inside ~20 km to stay
well within single-precision float comfort.

---

## Tech

- **Godot 4.7**, C# / .NET, Forward+ renderer
- **Jolt** physics, 90–120 Hz tick
- Host-authoritative networking — clients send input, never forces
- Steam lobbies and proximity voice (proximity voice is a core mechanic, not polish)

---

## Roadmap

**Vertical slice — one island to one island, one absurd object, two players, one storm.**

- [ ] Gerstner ocean renders and animates
- [ ] Sphere floats convincingly
- [ ] Boat floats, rocks, and doesn't explode
- [ ] Player stands on a moving deck without dying
- [ ] Wind and sail produce movement
- [ ] Second player joins
- [ ] Grip system
- [ ] Grapple gun: fire, anchor, winch
- [ ] Cargo rigid bodies + lashing
- [ ] Compass, sandglass, chart table
- [ ] One storm
- [ ] Dive bell

If shouting compass headings at each other across that gap is fun, the rest is content.

---

## References

Useful reading for anyone poking at the water code:

- Jacques Kerner, *Water interaction model for boats in video games*, Parts 1 & 2 — the best single
  resource on buoyancy and hull hydrodynamics for games.
- *GPU Gems* Ch. 1, *Effective Water Simulation from Physical Models* — the canonical Gerstner
  wave reference.
- Jerry Tessendorf, *Simulating Ocean Water* — for when the visuals get upgraded to FFT.
- Crest Ocean System (Unity, MIT) — well-designed buoyancy query API worth imitating.

---

## Building

Requires the **.NET build of Godot 4.7** and the .NET SDK. Open `project.godot` in the editor and
build from there.

---

*Knotical* — as in nautical, as in knots, as in the rope you should have tied better.
