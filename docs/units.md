# Units and standard sizes

One Unity unit is one metre. Everything is sized from the player.

## Player

| Thing | Size |
|---|---|
| Height | 1.80 m |
| Capsule radius | 0.40 m |
| Eye height | 1.65 m |
| Max step rise (ramps, not stairs) | 0.25 m |
| Walk speed | 3 m/s |

## Architecture

| Thing | Size |
|---|---|
| Doorway | 0.9 m wide, 2.1 m tall |
| Interior headroom | 2.2 m |
| Deck rail / bulwark top above deck | 1.05 m |
| Ladder rung spacing | 0.30 m |
| Cargo crate | 1.2 m cube |

## Boats

Sizes follow from the player: a room under a raised deck needs 2.2 m of headroom plus deck
thickness, so a sterncastle deck sits about 2.5 m above the main deck.

| Class | Length | Width | Crew |
|---|---|---|---|
| Small | 9 m | 3 m | 1 to 2 |
| Medium (first boat) | 18 m | 5.5 m | 2 to 4 |
| Large | 30 m | 8 m | 4+ |

## Boat definition

`tools/blender/build_boat.py` is the source of truth for a boat. It writes the mesh and a JSON
definition next to it, and `Editor/BoatBuilder.cs` derives everything physical from that
definition: colliders, mass and centre of mass from the underwater volume, pontoon positions
from the hull sections, and named attachment points (wheel, rudder, mast, anchor, stern) that
gameplay code looks up by name under the prefab's `Attach` child. Nothing about the boat's
physics is typed into Unity by hand.

Blender coordinates are x right, y forward, z up. Unity is x right, y up, z forward. The
builder swaps y and z when reading the definition.
