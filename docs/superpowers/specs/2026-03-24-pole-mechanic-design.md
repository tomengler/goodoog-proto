# Pole Mechanic Design Spec

## Overview

A new stationary grid entity called "Pole" that characters can grab onto and orbit around. Poles add a movement mechanic where characters swing around poles using perpendicular inputs, and sprinting characters can use poles to reverse direction via a 180-degree momentum swing.

## 1. Pole Entity (`Pole.cs`)

**Pattern**: Standalone MonoBehaviour following the `Enemy.cs` static registry pattern.

- **Static registry**: `static List<Pole>`, `Pole.All`, `Pole.FindAtPosition(GridPosition)`. Register in `Awake()`, unregister in `OnDestroy()`.
- **Grid position**: `GridPosition` field, set from transform in `Awake()`.
- **Impassable**: Pole tiles block movement. `GridCharacter.CanMoveTo()` checks `Pole.FindAtPosition()` in addition to walls.
- **No state machine**: Poles are purely passive. All interaction state lives on the character.

### Visuals

- Dark gray circle sprite, full tile width and height.
- When a character grabs the pole, the sprite color changes to the holder's color:
  - Dog: `#FFD4A1` (orange)
  - Robot: `#A1A7FF` (blue)
- On release, reverts to dark gray.
- If joined characters grab a pole, use the grabbing character's color.

## 2. Character Hold State

### GridCharacter changes

- **New `MoveState` value**: `HoldingPole`.
- **New fields**:
  - `HeldPole`: Reference to the `Pole` being held.
  - `PoleDirection`: `GridPosition` direction from character to pole (e.g., `GridPosition.Right` if pole is to the character's right).

### Entering hold state

- When `TryMove()` detects a `Pole` at the target position, the character does NOT move onto the pole tile. Instead:
  - Set `MoveState` to `HoldingPole`.
  - Set `HeldPole` to the detected pole.
  - Set `PoleDirection` to the movement direction (toward the pole).
  - Change pole color to character's color.
- When a sprinting character hits a pole head-on (sprint direction aligns with pole direction), sprint stops and character enters `HoldingPole` state (same as above, no 180-degree swing).

### Exiting hold state

- **Away input** (opposite to `PoleDirection`): Release pole, revert pole color, move normally in that direction. Return to `MoveState.Normal`.
- **Toward pole input**: No-op (TBD for future).

## 3. Orbit Mechanic

### 90-degree orbit (non-sprinting)

- **Trigger**: Perpendicular input while in `HoldingPole` state.
- **Behavior**: Character moves to the adjacent tile 90 degrees around the pole in the input direction.
  - Example: Character is left of pole (`PoleDirection = Right`). Input Up -> character moves to above the pole, `PoleDirection` updates to `Down`.
- **Stays in hold**: Character remains in `HoldingPole` state. Multiple orbits can be chained.
- **Blocked**: If destination tile is occupied (wall, enemy, pole), orbit does not execute.
- **Animation**: Smooth circular arc around the pole's world position. Arc radius = `cellSize` (1 unit). Angular speed derived from normal `moveSpeed` so perceived movement speed is consistent with grid movement.

### 180-degree sprint orbit

- **Trigger**: While sprinting, at each cell boundary crossing, check perpendicular directions for poles. If the character taps a perpendicular direction toward a pole, initiate 180-degree sprint orbit.
  - A single tap is sufficient; the input does not need to be held.
- **Behavior**: Character swings 180 degrees around the pole and releases, now sprinting in the opposite direction on the other side.
  - Example: Sprinting right, tap up toward pole above -> swing from above-pole through right-of-pole to below-pole -> release sprinting left.
- **Sprint preserved**: Momentum is maintained; only direction reverses.
- **Animation**: Smooth circular arc (180 degrees). Angular speed derived from current sprint speed for consistent perceived speed.
- **Blocked destination**: If the tile on the opposite side of the pole is occupied, the sprint orbit fails. Character stops sprinting and enters normal `HoldingPole` state instead.

### Direction mapping for orbits

For a 90-degree orbit, given the character's current `PoleDirection` and the perpendicular input:

| Current PoleDirection | Input | New position (relative to pole) | New PoleDirection |
|---|---|---|---|
| Right (pole is right) | Up | Above pole | Down |
| Right | Down | Below pole | Up |
| Left | Up | Above pole | Down |
| Left | Down | Below pole | Up |
| Up | Left | Left of pole | Right |
| Up | Right | Right of pole | Left |
| Down | Left | Left of pole | Right |
| Down | Right | Right of pole | Left |

The new position is calculated as: `pole.GridPosition + input direction`.
The new `PoleDirection` is: `pole.GridPosition - new character position` (direction from character to pole).

## 4. Sprint Pole Grab Detection

### Detection during sprint

In `GridCharacter.UpdateSprinting()`, at each cell boundary crossing (where we already check for walls and enemies):

1. Check perpendicular directions (both sides of sprint direction) for poles via `Pole.FindAtPosition()`.
2. Check if the character tapped a perpendicular input this frame toward one of those poles.
3. If yes: initiate 180-degree sprint orbit.

### Head-on sprint into pole

- Pole is impassable, so sprinting directly into a pole behaves like hitting a wall.
- Sprint stops. Character enters `HoldingPole` state at the tile before the pole.
- From there, standard orbit/release inputs apply.

## 5. Joined Character Behavior

### Joined grab

- If characters are joined and one grabs a pole, they remain joined.
- The non-holding character follows the holder during orbits, maintaining adjacency.

### Joined orbit

- During 90-degree orbit: the follower moves to stay adjacent to the holder's new position.
- During 180-degree sprint orbit: the follower mirrors the arc movement.

### Separate characters

- Each character can independently grab and orbit their own pole.
- No special interaction between two separately-held poles.

## 6. CharacterLinkManager Integration

`CharacterLinkManager` already routes all input. When a character is in `HoldingPole` state, input routing changes:

- **Away from pole** (opposite `PoleDirection`): Release pole, normal move.
- **Perpendicular to pole axis**: Execute orbit.
- **Toward pole**: No-op.

This applies to both joined and separate input handling paths.

## 7. Pole Spawning

### PoleSpawner

- Runs at scene start (or as a method on an existing manager).
- Spawns 3 poles at random grid positions.
- **Validation**: Each candidate position must have all 8 neighbors (cardinal + diagonal) free of:
  - Walls
  - Other poles
  - Enemy positions
  - Character start positions
- Retry with new random positions until 3 valid positions are found.

## 8. Collision Summary

| Entity moving into pole | Result |
|---|---|
| Character (normal move) | Enter `HoldingPole` state, stay on current tile |
| Character (sprinting, head-on) | Sprint stops, enter `HoldingPole` state |
| Character (sprinting, perpendicular tap) | 180-degree sprint orbit |
| Enemy (knocked back / launched) | Blocked, acts as wall |
| Other pole | N/A (poles are stationary) |

## 9. Future Considerations

- Defeated enemies becoming poles at their position (runtime spawn).
- Enemy parts acting as poles via a shared `IPoleGrabbable` interface.
- Input toward pole while holding (currently no-op, TBD).
