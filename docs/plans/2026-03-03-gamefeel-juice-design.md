# Gamefeel Juice Design

## Goal

Add snappy, punchy feedback to movement and combat so every action feels satisfying. Two layers: a core transform-based system (Layer A) and an optional particle/post-processing layer (Layer C) built on top.

## Architecture

**GameFeelManager** - singleton MonoBehaviour in the scene. Provides static methods any script can call:

```csharp
GameFeelManager.HitStop(duration);
GameFeelManager.ScreenShake(duration, intensity);
GameFeelManager.Squash(transform, duration, direction);
```

Effects run as coroutines internally. Camera reference owned by the manager for screen shake.

**GameFeelSettings** - ScriptableObject holding all timing/intensity values. Tunable without code changes.

## Layer A: Transform-Based Juice

### 1. Movement - Overshoot & Settle

Replace the current lerp in `GridCharacter.UpdateVisualPosition`. The character overshoots the target tile by ~0.08 units in the movement direction, then snaps back. Total duration ~0.1s. Uses a custom easing curve (sharp ease-out with overshoot).

### 2. Successful Hit

Triggered from `Enemy.TryTakeHit` on correct damage type. All effects oriented to the attack direction:

- **Hitstop**: freeze all movement for ~50ms (pause coroutines / flag-based, not Time.timeScale to avoid global side effects)
- **Attacker squash**: compress along the attack axis, stretch on the perpendicular axis, snap back (~0.1s)
- **Enemy stretch**: stretch along the knockback direction, snap back as it moves (~0.1s)
- **Enemy flash**: SpriteRenderer color goes white for 1-2 frames, then restores
- **Health bar segment pop**: the removed segment scales up ~1.5x briefly before being destroyed
- **Screen shake**: subtle, ~0.03 units for ~0.1s

### 3. Wrong Hit (Blocked)

Triggered from `Enemy.OnWrongHit`:

- **Enemy shake**: rapid small oscillation along the attack axis for ~0.15s (stays in place)
- **Health bar reject wobble**: entire health bar container does a quick horizontal shake
- **Attacker recoil**: slides back ~0.15 units opposite to attack direction, then returns

### 4. Enemy Death

Triggered from `Enemy.Die`:

- **Scale punch to zero**: enemy squishes down along both axes over ~0.15s
- **Screen shake**: slightly stronger, ~0.05 units for ~0.15s
- **Health bar scatter**: any remaining segments scale to zero quickly

### Direction-Aware Effects

All squash/stretch/recoil effects use the attack direction to determine axes:
- Attack along X (left/right): squash on X, stretch on Y
- Attack along Y (up/down): squash on Y, stretch on X

## Layer C: Particles + Post-Processing (Optional)

Additive on top of Layer A. All Layer A effects still run.

### Particles

- **Hit particles**: small burst of colored squares (matching damage type color - orange for Dog, blue for Robot) at impact point, flying outward in hit direction
- **Death particles**: larger burst in all directions when enemy is defeated
- **Wrong hit particles**: a few sparks/flashes at the contact point

### Post-Processing

- **Chromatic aberration pulse**: brief RGB split on successful hits, slightly stronger on death. Requires URP post-processing volume in scene.

### Time Effects

- **Time-scale punch on death**: brief 0.7x slowdown for ~0.1s then snap back to 1x. More dramatic than hitstop. Only on enemy death.

## Integration Points

| Trigger | Where in code | Effects |
|---------|--------------|---------|
| Character moves | `GridCharacter.UpdateVisualPosition` | Overshoot & settle |
| Correct hit | `Enemy.TryTakeHit` (success path) | Hitstop, squash, stretch, flash, health pop, screen shake, [particles, chromatic aberration] |
| Wrong hit | `Enemy.OnWrongHit` | Enemy shake, bar wobble, attacker recoil, [spark particles] |
| Enemy death | `Enemy.Die` | Scale to zero, screen shake, bar scatter, [death particles, chromatic aberration, time-scale punch] |

## Files to Create

- `Assets/Scripts/Core/GameFeelManager.cs` - singleton, effect coroutines
- `Assets/ScriptableObjects/GameFeelSettings.asset` - tuning values

## Files to Modify

- `Assets/Scripts/Characters/GridCharacter.cs` - movement overshoot
- `Assets/Scripts/Enemies/Enemy.cs` - hook juice calls into hit/death/wrong-hit flows
