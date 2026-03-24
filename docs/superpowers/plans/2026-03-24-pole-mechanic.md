# Pole Mechanic Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Pole" grid entity that characters can grab and orbit around, including 90-degree manual orbits and 180-degree sprint-momentum swings.

**Architecture:** Pole is a standalone MonoBehaviour with a static registry (same pattern as Enemy). Hold state lives on GridCharacter via a new MoveState value. All pole-related input routing goes through CharacterLinkManager via a new HandlePoleInput method. Orbit animation uses a circular arc path in a new UpdateOrbitAnimation method.

**Tech Stack:** Unity 6000.3, C#

**Spec:** `docs/superpowers/specs/2026-03-24-pole-mechanic-design.md`

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/Scripts/Environment/Pole.cs` | Pole entity: static registry, grid position, visual color changes |
| Create | `Assets/Scripts/Environment/PoleSpawner.cs` | Spawns 3 poles at validated random positions on scene start |
| Modify | `Assets/Scripts/Characters/GridCharacter.cs` | Add `HoldingPole` to MoveState, hold fields, orbit animation, pole checks in sprint/brake |
| Modify | `Assets/Scripts/Core/CharacterLinkManager.cs` | Add `HandlePoleInput()`, pole-aware input routing for joined/separate |
| Modify | `Assets/Scripts/Input/CharacterInputHandler.cs` | Add `GetPerpendicularTapThisFrame()` for sprint pole detection |
| Modify | `Assets/Scripts/Enemies/Enemy.cs` | Add pole check in `TryKnockback()` |

---

## Task 1: Create Pole Entity with Static Registry

**Files:**
- Create: `Assets/Scripts/Environment/Pole.cs`

- [ ] **Step 1: Create the Pole script**

```csharp
using UnityEngine;
using System.Collections.Generic;
using DogAndRobot.Core;

namespace DogAndRobot.Environment
{
public class Pole : MonoBehaviour
{
    static readonly List<Pole> _allPoles = new List<Pole>();
    public static IReadOnlyList<Pole> All => _allPoles;

    [SerializeField] GridPosition _gridPosition;
    [SerializeField] SpriteRenderer _spriteRenderer;

    static readonly Color DefaultColor = new Color(0.3f, 0.3f, 0.3f, 1f); // dark gray

    public GridPosition GridPosition => _gridPosition;

    public static Pole FindAtPosition(GridPosition pos)
    {
        for (int i = 0; i < _allPoles.Count; i++)
        {
            if (_allPoles[i]._gridPosition.Equals(pos))
                return _allPoles[i];
        }
        return null;
    }

    void Awake()
    {
        _allPoles.Add(this);
        float cellSize = SettingsManager.Instance.settings.cellSize;
        _gridPosition = GridPosition.FromWorldPosition(transform.position, cellSize);
        transform.position = _gridPosition.ToWorldPosition(cellSize);

        if (_spriteRenderer == null)
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _spriteRenderer.color = DefaultColor;
    }

    void OnDestroy()
    {
        _allPoles.Remove(this);
    }

    public void SetHolderColor(Color color)
    {
        _spriteRenderer.color = color;
    }

    public void ResetColor()
    {
        _spriteRenderer.color = DefaultColor;
    }
}
} // namespace DogAndRobot.Environment
```

**Note:** All files that reference `Pole` will need `using DogAndRobot.Environment;` added to their imports. This includes `GridCharacter.cs`, `CharacterLinkManager.cs`, `Enemy.cs`, and `PoleSpawner.cs`.

- [ ] **Step 2: Create a circle sprite for the pole**

In Unity Editor (or via script): create a prefab `Pole` with a SpriteRenderer using the built-in circle sprite (`Knob` or a circle from the sprite assets). Set sorting order so it renders below characters. Scale to fill one tile (1x1 world units).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Environment/Pole.cs"
git commit -m "feat: add Pole entity with static registry and color feedback"
```

---

## Task 2: Block Movement Through Poles

**Files:**
- Modify: `Assets/Scripts/Enemies/Enemy.cs:303-314` (TryKnockback)

**Note:** Poles are NOT added to `CanMoveTo()`. Instead, each code path handles poles explicitly: `TryMove()` (Task 3), `UpdateSprinting()` (Task 6), `UpdateBraking()` (Task 6). This gives each path fine-grained control over behavior (e.g., sprint enters hold, brake does not).

- [ ] **Step 1: Add pole check to Enemy.TryKnockback**

In `Enemy.cs` at line 308, alongside the existing `WallManager.IsWall()` check, add a pole check:

```csharp
// In TryKnockback, where it checks WallManager.IsWall() (preserve existing null guard pattern):
if ((WallManager.Instance != null && WallManager.Instance.IsWall(newPosition)) || Pole.FindAtPosition(newPosition) != null)
{
    // blocked
    return false;
}
```

- [ ] **Step 2: Verify enemies treat poles as walls during knockback**

Place a pole adjacent to an enemy in the scene. Hit the enemy to trigger knockback toward the pole. Confirm the enemy does not move onto the pole tile.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Enemies/Enemy.cs"
git commit -m "feat: block enemy knockback onto pole tiles"
```

---

## Task 3: Add HoldingPole State to GridCharacter

**Files:**
- Modify: `Assets/Scripts/Characters/GridCharacter.cs:12` (MoveState enum)
- Modify: `Assets/Scripts/Characters/GridCharacter.cs:115-173` (TryMove)

- [ ] **Step 1: Add HoldingPole to MoveState enum**

At `GridCharacter.cs` line 12, add `HoldingPole` to the `MoveState` enum:

```csharp
public enum MoveState { Normal, Sprinting, Braking, HoldingPole }
```

- [ ] **Step 2: Add hold state fields**

Add fields to `GridCharacter`:

```csharp
Pole _heldPole;
GridPosition _poleDirection; // direction from character TO pole

public Pole HeldPole => _heldPole;
public GridPosition PoleDirection => _poleDirection;
// Note: use existing `SprintState` property (line 62) to check MoveState externally
```

- [ ] **Step 3: Add pole detection in TryMove**

In `TryMove()`, after the enemy check (line ~160) and before the `CanMoveTo()` call (line ~163), insert a pole check:

```csharp
// After enemy handling, before CanMoveTo:
Pole pole = Pole.FindAtPosition(newPosition);
if (pole != null)
{
    // Don't move onto the pole — enter hold state
    EnterPoleHold(pole, direction);
    return true;
}
```

- [ ] **Step 4: Implement EnterPoleHold and ReleasePole methods**

```csharp
public void EnterPoleHold(Pole pole, GridPosition directionToPole)
{
    _sprintState = MoveState.HoldingPole;
    _heldPole = pole;
    _poleDirection = directionToPole;
    pole.SetHolderColor(GetHolderColor());
}

public void ReleasePole()
{
    if (_heldPole != null)
    {
        _heldPole.ResetColor();
        _heldPole = null;
    }
    _poleDirection = GridPosition.Zero;
    _sprintState = MoveState.Normal;
}

Color GetHolderColor()
{
    return GetDamageType() == DamageType.Dog
        ? new Color(1f, 0.831f, 0.631f, 1f)    // #FFD4A1
        : new Color(0.631f, 0.655f, 1f, 1f);    // #A1A7FF
}
```

- [ ] **Step 5: Test pole grab**

Enter play mode. Move a character into a pole. Confirm:
- Character stays on adjacent tile (does not move onto pole)
- Pole changes color to the character's color
- Character's MoveState is HoldingPole

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: add HoldingPole state and pole grab on TryMove"
```

---

## Task 4: Pole Input Routing in CharacterLinkManager

**Files:**
- Modify: `Assets/Scripts/Core/CharacterLinkManager.cs:311` (HandleJoinedInput)
- Modify: `Assets/Scripts/Core/CharacterLinkManager.cs:433` (HandleSeparateInput)

- [ ] **Step 1: Add HandlePoleInput method**

```csharp
bool HandlePoleInput(GridCharacter character, GridPosition direction)
{
    GridPosition poleDir = character.PoleDirection;

    // Away from pole — release and move normally
    if (direction.x == -poleDir.x && direction.y == -poleDir.y)
    {
        character.ReleasePole();
        character.TryMove(direction);
        return true;
    }

    // Toward pole — no-op
    if (direction.Equals(poleDir))
        return true;

    // Perpendicular — orbit
    GridPosition newPos = character.HeldPole.GridPosition + direction;

    // Check blocked (wall, enemy, pole, or other character when separated)
    if (WallManager.Instance.IsWall(newPos) ||
        Enemy.FindAtPosition(newPos) != null ||
        Pole.FindAtPosition(newPos) != null)
        return true; // blocked, consume input

    if (!_isJoined)
    {
        // When separated, also block if other character is at destination
        GridCharacter other = (character == _robot) ? (GridCharacter)_dog : _robot;
        if (other.GridPosition.Equals(newPos))
            return true;
    }

    // Execute orbit
    GridPosition prevPos = character.GridPosition;
    character.StartOrbit(newPos, character.HeldPole);

    // Joined follower moves to holder's previous position
    if (_isJoined)
    {
        GridCharacter follower = (character == _robot) ? (GridCharacter)_dog : _robot;
        follower.AnimateMoveTo(prevPos);
    }

    return true;
}
```

- [ ] **Step 2: Intercept in HandleJoinedInput**

At the start of `HandleJoinedInput()` (line ~311), before existing movement logic, add:

```csharp
// Check if either character is holding a pole
if (_robot.SprintState == MoveState.HoldingPole && robotInput != GridPosition.Zero)
{
    HandlePoleInput(_robot, robotInput);
    return;
}
if (_dog.SprintState == MoveState.HoldingPole && dogInput != GridPosition.Zero)
{
    HandlePoleInput(_dog, dogInput);
    return;
}
```

- [ ] **Step 3: Intercept in HandleSeparateInput**

At the start of `HandleSeparateInput()` (line ~433), add similar checks for each character independently:

```csharp
if (_robot.SprintState == MoveState.HoldingPole && robotInput != GridPosition.Zero)
    HandlePoleInput(_robot, robotInput);
else if (robotInput != GridPosition.Zero)
    // existing robot movement logic...

if (_dog.SprintState == MoveState.HoldingPole && dogInput != GridPosition.Zero)
    HandlePoleInput(_dog, dogInput);
else if (dogInput != GridPosition.Zero)
    // existing dog movement logic...
```

- [ ] **Step 4: Block same-pole double grab**

In `CharacterLinkManager`, add a check before pole grab can occur. Since `CharacterLinkManager` has references to both characters, add a helper:

```csharp
bool IsPoleHeldByOther(GridCharacter character, Pole pole)
{
    GridCharacter other = (character == _robot) ? (GridCharacter)_dog : _robot;
    return other.HeldPole == pole;
}
```

Then in `HandlePoleInput`, before allowing orbit, and in the pre-TryMove checks, verify the pole isn't already held. Also update `GridCharacter.TryMove()` pole detection to accept an optional check:

```csharp
// In GridCharacter.TryMove(), the pole block added in Task 3:
Pole pole = Pole.FindAtPosition(newPosition);
if (pole != null)
{
    // Pole grab is handled — but double-grab blocking is checked
    // by CharacterLinkManager before this is reached.
    EnterPoleHold(pole, direction);
    return true;
}
```

Add a pre-check in `HandleJoinedInput` and `HandleSeparateInput`: before calling `TryMove`, if the target has a pole held by the other character, block the move (return without calling `TryMove`).

- [ ] **Step 5: Test input routing**

In play mode with a character holding a pole:
- Press away from pole → character releases and moves away. Pole reverts to gray.
- Press toward pole → nothing happens.
- Press perpendicular → character moves to adjacent tile around pole (for now, linear move — orbit animation is Task 5).
- Joined: follower moves to holder's previous position.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Core/CharacterLinkManager.cs" "Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: add pole input routing with orbit, release, and joined follower"
```

---

## Task 5: Orbit Animation (Circular Arc)

**Files:**
- Modify: `Assets/Scripts/Characters/GridCharacter.cs`

- [ ] **Step 1: Add orbit animation state fields**

```csharp
// Orbit animation
bool _isOrbiting;
Vector3 _orbitCenter;      // pole world position
float _orbitStartAngle;    // radians
float _orbitEndAngle;      // radians
float _orbitProgress;      // 0 to 1
float _orbitDuration;      // seconds
GridPosition _orbitTargetGridPos;
```

- [ ] **Step 2: Implement StartOrbit method**

```csharp
public void StartOrbit(GridPosition targetGridPos, Pole pole, float speedOverride = -1f)
{
    float cellSize = SettingsManager.Instance.settings.cellSize;
    _orbitCenter = pole.GridPosition.ToWorldPosition(cellSize);

    Vector3 startWorld = _gridPosition.ToWorldPosition(cellSize);
    Vector3 endWorld = targetGridPos.ToWorldPosition(cellSize);

    Vector3 startOffset = startWorld - _orbitCenter;
    Vector3 endOffset = endWorld - _orbitCenter;

    _orbitStartAngle = Mathf.Atan2(startOffset.y, startOffset.x);
    _orbitEndAngle = Mathf.Atan2(endOffset.y, endOffset.x);

    // Determine shortest arc direction
    float angleDiff = _orbitEndAngle - _orbitStartAngle;
    if (angleDiff > Mathf.PI) angleDiff -= 2f * Mathf.PI;
    if (angleDiff < -Mathf.PI) angleDiff += 2f * Mathf.PI;
    _orbitEndAngle = _orbitStartAngle + angleDiff;

    // Duration based on arc length and speed
    float arcLength = Mathf.Abs(angleDiff) * cellSize; // radius = cellSize
    float speed = speedOverride > 0 ? speedOverride
        : SettingsManager.Instance.settings.moveSpeed;
    _orbitDuration = arcLength / speed;

    _orbitTargetGridPos = targetGridPos;
    _orbitProgress = 0f;
    _isOrbiting = true;
    IsMoving = true;

    // Update grid position immediately
    _gridPosition = targetGridPos;

    // Update pole direction
    _poleDirection = pole.GridPosition - targetGridPos;
}
```

- [ ] **Step 3: Implement UpdateOrbitAnimation**

Called from `Update()`:

```csharp
void UpdateOrbitAnimation()
{
    if (!_isOrbiting) return;

    float cellSize = SettingsManager.Instance.settings.cellSize;
    _orbitProgress += Time.deltaTime / _orbitDuration;

    if (_orbitProgress >= 1f)
    {
        _orbitProgress = 1f;
        _isOrbiting = false;
        IsMoving = false;
        transform.position = _orbitTargetGridPos.ToWorldPosition(cellSize) + _visualOffset;
        return;
    }

    // Smooth interpolation
    float t = _orbitProgress; // can add easing here if desired
    float angle = Mathf.Lerp(_orbitStartAngle, _orbitEndAngle, t);
    float radius = cellSize;

    Vector3 pos = _orbitCenter + new Vector3(
        Mathf.Cos(angle) * radius,
        Mathf.Sin(angle) * radius,
        0f
    );
    transform.position = pos + _visualOffset;
}
```

- [ ] **Step 4: Hook UpdateOrbitAnimation into Update**

In `GridCharacter.Update()`, add a call to `UpdateOrbitAnimation()` early (before `UpdateVisualPosition`). When `_isOrbiting` is true, skip the normal `UpdateVisualPosition` logic:

```csharp
if (_isOrbiting)
{
    UpdateOrbitAnimation();
    return; // skip normal visual update
}
```

- [ ] **Step 5: Test orbit animation**

Hold a pole and press perpendicular. Confirm the character follows a smooth circular arc around the pole (not a straight line). Verify:
- Arc takes a consistent amount of time relative to normal movement speed
- Character ends on the correct tile
- Input is blocked during the arc (IsMoving is true)

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: add circular arc orbit animation for pole holds"
```

---

## Task 6: Sprint Head-On and Brake Pole Detection

**Files:**
- Modify: `Assets/Scripts/Characters/GridCharacter.cs:281-381` (UpdateSprinting)
- Modify: `Assets/Scripts/Characters/GridCharacter.cs:383-476` (UpdateBraking)

- [ ] **Step 1: Add pole check in UpdateSprinting wall detection**

At `GridCharacter.cs` line ~310, where the wall check occurs during sprint, add pole detection:

```csharp
// Existing: if (!CanMoveTo(newGridPos))
// Add pole check:
Pole sprintPole = Pole.FindAtPosition(newGridPos);
if (!CanMoveTo(newGridPos) || sprintPole != null)
{
    if (sprintPole != null)
    {
        // Head-on sprint into pole — stop and enter hold
        StopSprintImmediate();
        EnterPoleHold(sprintPole, _sprintDirection);
    }
    else
    {
        // Existing wall hit logic
    }
    return;
}
```

- [ ] **Step 2: Add pole check in UpdateBraking**

At `GridCharacter.cs` line ~400, where the wall check occurs during braking, add:

```csharp
// Alongside existing wall check:
if (WallManager.Instance.IsWall(newGridPos) || Pole.FindAtPosition(newGridPos) != null)
{
    // Stop braking — treat pole same as wall (no hold state)
    // ... existing brake-stop logic
}
```

- [ ] **Step 3: Test sprint into pole head-on**

Sprint directly at a pole. Confirm:
- Sprint stops at the tile before the pole
- Character enters HoldingPole state
- Pole changes to character's color

- [ ] **Step 4: Test brake into pole**

Sprint, release key to brake, slide into a pole. Confirm:
- Character stops at the tile before the pole
- Character does NOT enter HoldingPole (stays Normal)

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: add pole detection in sprint and brake code paths"
```

---

## Task 7: Perpendicular Sprint Pole Grab (180-Degree Orbit)

**Files:**
- Modify: `Assets/Scripts/Input/CharacterInputHandler.cs`
- Modify: `Assets/Scripts/Characters/GridCharacter.cs:281-381` (UpdateSprinting)

- [ ] **Step 1: Add GetPerpendicularTapThisFrame to CharacterInputHandler**

```csharp
public GridPosition GetPerpendicularTapThisFrame(GridPosition sprintDir)
{
    // Determine perpendicular keys based on sprint direction
    if (sprintDir.x != 0)
    {
        // Sprinting horizontally — perpendicular is up/down
        if (Input.GetKeyDown(_upKey)) return GridPosition.Up;
        if (Input.GetKeyDown(_downKey)) return GridPosition.Down;
    }
    else if (sprintDir.y != 0)
    {
        // Sprinting vertically — perpendicular is left/right
        if (Input.GetKeyDown(_leftKey)) return GridPosition.Left;
        if (Input.GetKeyDown(_rightKey)) return GridPosition.Right;
    }
    return GridPosition.Zero;
}
```

- [ ] **Step 2: Add perpendicular pole detection in CharacterLinkManager sprint updates**

`GridCharacter` has no input handler reference — input handlers live on `CharacterLinkManager` (`_robotInput`, `_dogInput`). The perpendicular pole check must be called from `CharacterLinkManager.HandleJoinedSprintUpdate()` (line ~562) and `HandleSeparateSprintUpdate()` (line ~633), which already have access to the character's input handler.

Add a new method to `CharacterLinkManager`:

```csharp
bool TrySprintPoleGrab(GridCharacter character, CharacterInputHandler input)
{
    if (character.SprintState != MoveState.Sprinting) return false;

    GridPosition perpTap = input.GetPerpendicularTapThisFrame(character.SprintDirection);
    if (perpTap == GridPosition.Zero) return false;

    GridPosition perpPolePos = character.GridPosition + perpTap;
    Pole perpPole = Pole.FindAtPosition(perpPolePos);
    if (perpPole == null) return false;

    // Calculate opposite side destination
    GridPosition exitPos = perpPolePos + perpTap; // opposite side of pole
    if (!WallManager.Instance.IsWall(exitPos) &&
        Enemy.FindAtPosition(exitPos) == null &&
        Pole.FindAtPosition(exitPos) == null)
    {
        // 180-degree sprint orbit
        character.StartSprintOrbit(perpPole, perpTap, exitPos);
        return true;
    }
    else
    {
        // Blocked — fall back to normal hold
        character.StopSprintImmediate();
        character.EnterPoleHold(perpPole, perpTap);
        return true;
    }
}
```

Call `TrySprintPoleGrab(character, input)` at the start of each sprint update method. If it returns true, return early.

**Note:** `SprintDirection` must be exposed as a public property on `GridCharacter` if not already (add `public GridPosition SprintDirection => _sprintDirection;`).

- [ ] **Step 3: Implement StartSprintOrbit**

```csharp
public void StartSprintOrbit(Pole pole, GridPosition grabDirection, GridPosition exitPos)
{
    float currentSpeed = _sprintSpeed; // preserve sprint speed
    GridPosition exitSprintDir = new GridPosition(-_sprintDirection.x, -_sprintDirection.y);

    // Stop sprint state temporarily for orbit
    _sprintState = MoveState.HoldingPole;
    _heldPole = pole;
    pole.SetHolderColor(GetHolderColor());

    // Start 180-degree orbit with sprint speed
    StartOrbit(exitPos, pole, currentSpeed);

    // Store post-orbit sprint info
    _postOrbitSprintDirection = exitSprintDir;
    _postOrbitSprintSpeed = currentSpeed;
    _isSprintOrbit = true;
}
```

- [ ] **Step 4: Resume sprint after orbit completes**

In `UpdateOrbitAnimation()`, when orbit completes (`_orbitProgress >= 1f`), check if this was a sprint orbit:

```csharp
if (_orbitProgress >= 1f)
{
    _orbitProgress = 1f;
    _isOrbiting = false;

    float cellSize = SettingsManager.Instance.settings.cellSize;
    transform.position = _orbitTargetGridPos.ToWorldPosition(cellSize);

    if (_isSprintOrbit)
    {
        // Resume sprint in opposite direction
        _isSprintOrbit = false;
        ReleasePole();
        _sprintDirection = _postOrbitSprintDirection;
        _sprintSpeed = _postOrbitSprintSpeed;
        _sprintState = MoveState.Sprinting;
        IsMoving = false; // sprint manages its own movement
    }
    else
    {
        IsMoving = false;
    }
    return;
}
```

- [ ] **Step 5: Add sprint orbit fields**

```csharp
bool _isSprintOrbit;
GridPosition _postOrbitSprintDirection;
float _postOrbitSprintSpeed;
```

- [ ] **Step 6: Test 180-degree sprint orbit**

Sprint horizontally past a pole that is one tile above. Tap up as you pass. Confirm:
- Character swings 180 degrees around the pole
- Exits sprinting in the opposite direction on the other side
- Sprint speed is maintained
- Pole flashes character color during swing then reverts

- [ ] **Step 7: Test blocked sprint orbit fallback**

Place a wall on the opposite side of a pole. Sprint and tap toward the pole. Confirm:
- Character stops sprinting
- Enters HoldingPole state on the grab side
- Does not crash or teleport

- [ ] **Step 8: Commit**

```bash
git add "Assets/Scripts/Input/CharacterInputHandler.cs" "Assets/Scripts/Characters/GridCharacter.cs"
git commit -m "feat: add 180-degree sprint orbit with perpendicular pole grab"
```

---

## Task 8: Joined Character Sprint Orbit Support

**Files:**
- Modify: `Assets/Scripts/Core/CharacterLinkManager.cs`

- [ ] **Step 1: Handle joined sprint orbit in CharacterLinkManager**

In `HandleJoinedSprintUpdate()` (line ~562), the existing code syncs the follower's position to the leader each frame (e.g. `dog.transform.position = robot.transform.position` at line ~615). When the leader is orbiting (`_isOrbiting` is true), this same sync continues to work — the follower's transform is set to the leader's transform, so they stay stacked during the arc.

Add an explicit check: when the leader's orbit completes (leader `_isOrbiting` transitions from true to false after a sprint orbit), teleport the follower to the leader's grid position:

```csharp
// In HandleJoinedSprintUpdate, after the existing position sync:
GridCharacter leader = (_joinedMovementLeader == DamageType.Robot)
    ? (GridCharacter)_robot : _dog;
GridCharacter follower = (leader == _robot)
    ? (GridCharacter)_dog : _robot;

if (leader.IsOrbiting)
{
    // During orbit, follower stays stacked with leader
    follower.transform.position = leader.transform.position;
    return; // skip normal sprint update while orbiting
}

// After orbit completes, sync follower grid position
// (follower naturally falls behind on next cell boundary crossing)
```

**Note:** Expose `IsOrbiting` as a public property on `GridCharacter`: `public bool IsOrbiting => _isOrbiting;`

- [ ] **Step 2: Handle joined 90-degree orbit follower**

Already handled in Task 4's `HandlePoleInput` — follower `AnimateMoveTo` to holder's previous position. Verify this works with joined characters.

- [ ] **Step 3: Test joined sprint orbit**

Join characters, sprint, and grab a perpendicular pole. Confirm:
- Both characters swing around together
- After orbit, sprint resumes with both characters
- Follower falls one tile behind on next cell crossing

- [ ] **Step 4: Test joined 90-degree orbit**

Join characters, grab a pole, orbit perpendicular. Confirm:
- Holder orbits around pole
- Follower moves to holder's previous position
- Characters remain joined

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/Core/CharacterLinkManager.cs"
git commit -m "feat: add joined character support for pole orbits"
```

---

## Task 9: Pole Spawner

**Files:**
- Create: `Assets/Scripts/Environment/PoleSpawner.cs`

- [ ] **Step 1: Create PoleSpawner script**

Follow the existing `EnemySpawner` pattern (uses `WallManager.Instance.GetInteriorBounds()` and `FindAtPosition()` checks):

```csharp
using UnityEngine;
using DogAndRobot.Core;
using DogAndRobot.Environment;
using DogAndRobot.Enemies;

namespace DogAndRobot.Environment
{
public class PoleSpawner : MonoBehaviour
{
    [SerializeField] GameObject _polePrefab;
    [SerializeField] int _poleCount = 3;
    [SerializeField] int _maxAttempts = 100;
    [SerializeField] Transform[] _characterStartTransforms; // assign Robot & Dog transforms

    void Start()
    {
        SpawnPoles();
    }

    void SpawnPoles()
    {
        var bounds = WallManager.Instance.GetInteriorBounds();
        int spawned = 0;
        int attempts = 0;

        while (spawned < _poleCount && attempts < _maxAttempts)
        {
            attempts++;
            int x = Random.Range(bounds.xMin, bounds.xMax + 1);
            int y = Random.Range(bounds.yMin, bounds.yMax + 1);
            var pos = new GridPosition(x, y);

            if (!IsValidPolePosition(pos))
                continue;

            float cellSize = SettingsManager.Instance.settings.cellSize;
            Instantiate(_polePrefab, pos.ToWorldPosition(cellSize), Quaternion.identity);
            spawned++;
        }

        if (spawned < _poleCount)
            Debug.LogWarning($"PoleSpawner: Only spawned {spawned}/{_poleCount} poles after {_maxAttempts} attempts");
    }

    bool IsValidPolePosition(GridPosition pos)
    {
        // Check the tile itself
        if (WallManager.Instance.IsWall(pos)) return false;
        if (Enemy.FindAtPosition(pos) != null) return false;
        if (Pole.FindAtPosition(pos) != null) return false;

        // Check character start positions
        float cellSize = SettingsManager.Instance.settings.cellSize;
        foreach (var t in _characterStartTransforms)
        {
            if (t != null && GridPosition.FromWorldPosition(t.position, cellSize).Equals(pos))
                return false;
        }

        // Check all 8 neighbors (cardinal + diagonal)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var neighbor = new GridPosition(pos.x + dx, pos.y + dy);
                if (WallManager.Instance.IsWall(neighbor)) return false;
                if (Pole.FindAtPosition(neighbor) != null) return false;
                if (Enemy.FindAtPosition(neighbor) != null) return false;
            }
        }

        return true;
    }
}
} // namespace DogAndRobot.Environment
```

- [ ] **Step 2: Set up in scene**

- Create an empty GameObject "PoleSpawner" in the scene
- Attach `PoleSpawner` component
- Create a Pole prefab (GameObject with SpriteRenderer using circle sprite, `Pole` script attached)
- Assign prefab to `_polePrefab` field

- [ ] **Step 3: Test spawning**

Enter play mode. Confirm:
- 3 poles appear at random positions
- No pole is adjacent to a wall (all 8 neighbors free)
- No two poles are adjacent to each other
- Poles display as dark gray circles

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Environment/PoleSpawner.cs"
git commit -m "feat: add PoleSpawner with validated random placement"
```

---

## Task 10: Integration Testing & Polish

- [ ] **Step 1: Full integration playtest**

Test the following scenarios:

| Scenario | Expected |
|----------|----------|
| Walk into pole | Hold state, pole turns character color |
| Press away while holding | Release, move away, pole reverts gray |
| Press perpendicular while holding | 90-degree orbit arc |
| Chain multiple orbits | Full 360 around pole |
| Sprint head-on into pole | Sprint stops, enter hold |
| Sprint past pole, tap perpendicular | 180-degree swing, exit sprinting opposite |
| Brake into pole | Stop like wall, no hold |
| Joined walk into pole | Both characters stay joined, holder grabs |
| Joined orbit | Holder arcs, follower moves to previous tile |
| Joined sprint orbit | Both swing together, resume sprint |
| Separated, both grab different poles | Independent orbits |
| Separated, second tries to grab held pole | Blocked |
| Enemy knocked toward pole | Blocked like wall |
| Enemy launched toward pole | Blocked like wall |

- [ ] **Step 2: Fix any issues found**

Address bugs discovered during integration testing.

- [ ] **Step 3: Final commit**

```bash
git add -A
git commit -m "feat: pole mechanic integration fixes and polish"
```
