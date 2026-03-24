# Pole Mechanic Design Spec

## Overview

A new stationary grid entity called "Pole" that characters can grab onto and orbit around. Poles add a movement mechanic where characters swing around poles using perpendicular inputs, and sprinting characters can use poles to reverse direction via a 180-degree momentum swing.

## 1. Pole Entity (`Pole.cs`)

**Pattern**: Standalone MonoBehaviour following the `Enemy.cs` static registry pattern.

- **Static registry**: `static List<Pole>`, `Pole.All`, `Pole.FindAtPosition(GridPosition)`. Register in `Awake()`, unregister in `OnDestroy()`.
- **Grid position**: `GridPosition` field, set from transform in `Awake()`.
- **Impassable**: Pole tiles block movement for both characters and enemies.
  - `Enemy.TryKnockback()`: Add `Pole.FindAtPosition()` check alongside the existing `WallManager.IsWall()` check so enemies cannot be knocked onto pole tiles.
  - Character `TryMove()`: Pole detection is handled explicitly as a higher-priority check before `CanMoveTo()` (see Section 2).
  - `UpdateSprinting()`: Add `Pole.FindAtPosition()` check at each cell boundary alongside the existing wall check. Head-on pole hit stops sprint and enters `HoldingPole` state. Perpendicular pole detection is a separate check (see Section 4).
  - `UpdateBraking()`: Add `Pole.FindAtPosition()` check alongside the existing wall check. Braking into a pole stops movement like a wall (no hold state).
- **No state machine**: Poles are purely passive. All interaction state lives on the character.
- **Exclusive occupancy**: Enemies and characters cannot occupy a pole tile. Spawning validation prevents initial overlap; knockback/launch checks prevent runtime overlap.

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

- **Detection ordering in `TryMove()`**: After the existing enemy check and before `CanMoveTo()`, add a pole check using `Pole.FindAtPosition(newPosition)`. If a pole is found, enter hold state and return `true` (movement was "handled"). The character does NOT move onto the pole tile. Instead:
  - Set `MoveState` to `HoldingPole`.
  - Set `HeldPole` to the detected pole.
  - Set `PoleDirection` to the movement direction (toward the pole).
  - Change pole color to character's color.
- When a sprinting character hits a pole head-on (sprint direction aligns with pole direction), sprint stops and character enters `HoldingPole` state (same as above, no 180-degree swing).
- When a braking character slides into a pole tile, treat it the same as a wall — stop braking at the tile before the pole. Do NOT enter `HoldingPole` (braking lacks intentional input toward the pole).

### Exiting hold state

- **Away input** (opposite to `PoleDirection`): Release pole, revert pole color, move normally in that direction. Return to `MoveState.Normal`.
- **Toward pole input**: No-op (TBD for future).

## 3. Orbit Mechanic

### 90-degree orbit (non-sprinting)

- **Trigger**: Perpendicular input while in `HoldingPole` state.
- **Behavior**: Character moves to the adjacent tile 90 degrees around the pole in the input direction.
  - Example: Character is left of pole (`PoleDirection = Right`). Input Up -> character moves to above the pole, `PoleDirection` updates to `Down`.
- **Stays in hold**: Character remains in `HoldingPole` state. Multiple orbits can be chained.
- **Blocked**: If destination tile is occupied (wall, enemy, pole, or the other character when separated), orbit does not execute.
- **Animation**: Smooth circular arc around the pole's world position, handled by a new `UpdateOrbitAnimation()` method in `GridCharacter` (separate from `UpdateVisualPosition()`). Arc radius = `cellSize` (1 unit). Angular speed derived from normal `moveSpeed` so perceived movement speed is consistent with grid movement.
- **Input during animation**: Input is blocked until the arc animation completes. The character's `IsMoving` flag remains `true` during the arc.

### 180-degree sprint orbit

- **Trigger**: While sprinting, at each cell boundary crossing, check perpendicular directions for poles. If the character taps a perpendicular direction toward a pole, initiate 180-degree sprint orbit.
  - A single tap is sufficient; the input does not need to be held.
- **Behavior**: Character swings 180 degrees around the pole and releases, now sprinting in the opposite direction on the other side.
  - Example: Sprinting right, tap up toward pole above -> swing from above-pole through right-of-pole to below-pole -> release sprinting left.
- **Sprint preserved**: Momentum is maintained; only direction reverses.
- **Animation**: Smooth circular arc (180 degrees), using `UpdateOrbitAnimation()`. Angular speed derived from current sprint speed for consistent perceived speed.
- **Input during animation**: Input is blocked until the arc completes. Sprint resumes automatically on completion.
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

**Input detection mechanism**: Add a `GetPerpendicularTapThisFrame(GridPosition sprintDir)` method to `CharacterInputHandler` that checks `Input.GetKeyDown` for the two perpendicular keys relative to the sprint direction. This is called from `UpdateSprinting()` at each cell boundary, bypassing the normal `GetMovementInput()` path (which is not called during sprint).

### Head-on sprint into pole

- Detected in `UpdateSprinting()` at cell boundary crossing (via the `Pole.FindAtPosition()` check added in Section 1).
- Sprint stops. Character enters `HoldingPole` state at the tile before the pole.
- From there, standard orbit/release inputs apply.

## 5. Joined Character Behavior

### Joined grab

- If characters are joined and one grabs a pole, they remain joined.
- The non-holding character follows the holder during orbits, maintaining adjacency.

### Joined orbit

- During 90-degree orbit: the follower moves to the tile the holder just vacated (the holder's previous position).
- During 180-degree sprint orbit: the follower occupies the same tile as the holder throughout the arc (they remain stacked as in normal joined sprint movement), and ends on the same tile as the holder on the exit side. On the first cell boundary crossing after the orbit, the follower naturally falls one tile behind as in normal joined sprint (no special transition needed).

### Separate characters

- Each character can independently grab and orbit their own pole.
- No special interaction between two separately-held poles.

## 6. CharacterLinkManager Integration

`CharacterLinkManager` already routes all input. In both `HandleJoinedInput()` and `HandleSeparateInput()`, before calling `TryMove()`, check if the character's `MoveState` is `HoldingPole`. If so, delegate to a new `HandlePoleInput(character, direction)` method that implements:

- **Away from pole** (opposite `PoleDirection`): Release pole, normal move.
- **Perpendicular to pole axis**: Execute orbit.
- **Toward pole**: No-op.

This keeps `CharacterLinkManager` as the single source of truth for input routing, consistent with how sprint/join/separate are handled.

Two characters cannot hold the same pole simultaneously. If a second character attempts to grab an already-held pole, the grab is blocked (treated as impassable wall).

## 7. Pole Spawning

### PoleSpawner

- Runs at scene start (or as a method on an existing manager).
- Spawns 3 poles at random grid positions.
- **Validation**: Each candidate position must have all 8 neighbors (cardinal + diagonal) free of:
  - Walls
  - Other poles
  - Enemy positions
  - Character start positions
- Retry with new random positions until 3 valid positions are found (max 100 attempts; log a warning if exhausted and spawn fewer poles).

## 8. Collision Summary

| Entity moving into pole | Result |
|---|---|
| Character (normal move) | Enter `HoldingPole` state, stay on current tile |
| Character (sprinting, head-on) | Sprint stops, enter `HoldingPole` state |
| Character (braking, slides into pole) | Stops at tile before pole (like a wall), does NOT enter hold |
| Character (sprinting, perpendicular tap) | 180-degree sprint orbit |
| Enemy (knocked back / launched) | Blocked, acts as wall |
| Other pole | N/A (poles are stationary) |

## 9. Future Considerations

- Defeated enemies becoming poles at their position (runtime spawn).
- Enemy parts acting as poles via a shared `IPoleGrabbable` interface.
- Input toward pole while holding (currently no-op, TBD).
