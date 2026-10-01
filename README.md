# Sector Cleanse: Idle

Minimalist 2D idle/incremental lane shooter (Unity, graybox prototype).

## Scripts so far

| Script | Purpose |
| --- | --- |
| `Assets/Scripts/Core/GameManager.cs` | Game loop state machine (`MainMenu → Playing → GameOver → MainMenu`), toggles scene roots, round money + persistent banked money. |
| `Assets/Scripts/Core/GameState.cs` | `GameState` enum and `RoundResult` struct. |
| `Assets/Scripts/Core/LaneSystem.cs` | Lane geometry (lane X positions, player line, spawn line). Draws gizmos in the Scene view. |
| `Assets/Scripts/Player/PlayerController.cs` | Free horizontal movement across the 3 lanes: hold A/D or ←/→, or drag with mouse/finger. Tracks the current lane and raises `LaneChanged`. |
| `Assets/Scripts/Player/PlayerSquad.cs` | Soldier count = health. Ends the round at 0. |
| `Assets/Scripts/Editor/GrayboxSceneBuilder.cs` | Editor menu **Sector Cleanse → Build Graybox Scene**: generates the whole test scene in one click. |

## Graybox scene setup

In Unity: **Sector Cleanse → Build Graybox Scene** (top menu bar).

This generates `Assets/Scenes/Graybox.unity` with the camera, GameManager, lanes,
player, start menu (DEPLOY button) and game-over overlay already wired up, and adds
it to the build settings. Re-running it rebuilds the scene from scratch.

Then press **Play → DEPLOY** and move by holding A/D or the arrow keys, or by dragging with the mouse.
Test the death loop without enemies via the `PlayerSquad` component's context menu
(⋮ → *Debug/Take 1 Damage*) in Play Mode.

Supports both the new Input System and the legacy Input Manager.

## Roadmap

1. ~~Core loop & 3-lane movement~~
2. Weapons, projectiles, enemies with HP, money on kill
3. Lane buffs / debuffs (Lowered Fire Rate, Compromised Position, …)
4. Enemy breach damage → squad loss → death (hooks already in `PlayerSquad`)
5. Start-menu meta-upgrades (DMG, fire rate, starting weapons, starting soldiers)
