# Sector Cleanse: Idle

Minimalist 2D idle/incremental lane shooter (Unity, graybox prototype).

## Scripts so far

| Script | Purpose |
| --- | --- |
| `Assets/Scripts/Core/GameManager.cs` | Game loop state machine (`MainMenu → Playing → GameOver → MainMenu`), toggles scene roots, round money + persistent banked money. |
| `Assets/Scripts/Core/GameState.cs` | `GameState` enum and `RoundResult` struct. |
| `Assets/Scripts/Core/LaneSystem.cs` | Lane geometry (lane X positions, player line, spawn line). Draws gizmos in the Scene view. |
| `Assets/Scripts/Player/PlayerController.cs` | 3-lane movement: keyboard (A/D, ←/→), swipe, tap-to-lane. Raises `LaneChanged`. |
| `Assets/Scripts/Player/PlayerSquad.cs` | Soldier count = health. Ends the round at 0. |

## Graybox scene setup

1. **Camera**: Main Camera, Orthographic, size ~6, position `(0, 0, -10)`.
2. **GameManager**: empty GameObject → add `GameManager`.
3. **MenuRoot**: Canvas with a "Deploy" button. Button `OnClick` → `GameManager.StartRound`.
   Assign it to `GameManager.menuRoot`.
4. **GameplayRoot**: empty GameObject. Assign to `GameManager.gameplayRoot`. Under it:
   - **Lanes**: empty GameObject at `(0, 0, 0)` → add `LaneSystem` (3 lanes, spacing 2, player line −4, spawn line 6).
   - **Player**: a Sprite (Square) → add `PlayerController` and `PlayerSquad`.
5. *(Optional)* **GameOverRoot**: Canvas with "Squad wiped" text. Assign to `GameManager.gameOverRoot`.

Tip: tick `Auto Start Round` on the GameManager to skip the menu while testing.
Test the death loop without enemies via the `PlayerSquad` component's context menu
(⋮ → *Debug/Take 1 Damage*) in Play Mode.

Supports both the new Input System and the legacy Input Manager.

## Roadmap

1. ~~Core loop & 3-lane movement~~
2. Weapons, projectiles, enemies with HP, money on kill
3. Lane buffs / debuffs (Lowered Fire Rate, Compromised Position, …)
4. Enemy breach damage → squad loss → death (hooks already in `PlayerSquad`)
5. Start-menu meta-upgrades (DMG, fire rate, starting weapons, starting soldiers)
