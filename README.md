# Sector Cleanse: Idle

Minimalist 2D idle/incremental lane shooter (Unity, graybox prototype).

## Scripts so far

| Script | Purpose |
| --- | --- |
| `Assets/Scripts/Core/GameManager.cs` | Game loop state machine (`MainMenu → Playing → GameOver → MainMenu`), toggles scene roots, round money + persistent banked money. |
| `Assets/Scripts/Core/GameState.cs` | `GameState` enum and `RoundResult` struct. |
| `Assets/Scripts/Core/LaneSystem.cs` | Lane geometry (lane X positions, player line, spawn line). Draws gizmos in the Scene view. |
| `Assets/Scripts/Player/PlayerController.cs` | Free horizontal movement across the 3 lanes: hold A/D or ←/→, or drag with mouse/finger. Tracks the current lane and raises `LaneChanged`. |
| `Assets/Scripts/Player/Barracks.cs` | Permanent soldiers by tier (T0–T100, DMG/HP = 5^tier). Max 5 per tier in reserve; 5 reserve soldiers MERGE into 1 of the next tier; up to 10 deployed on the front line. Saved. |
| `Assets/Scripts/Player/PlayerSquad.cs` | The run's squad: player (commander) + deployed soldiers, each with HP = 5^tier. Breach damage hits the weakest soldier first and overflows; the round ends when the player falls. |
| `Assets/Scripts/Player/SquadFormation.cs` | Draws each soldier below the green player, labelled and tinted by tier; the Weapon fires one bullet per soldier with that soldier's damage. |
| `Assets/Scripts/Combat/Weapon.cs` | Auto-fires bullets upward. Base damage / fire rate (meta-upgrades) × per-round multipliers (buffs/debuffs). |
| `Assets/Scripts/Combat/Bullet.cs` | Flies up, damages the first enemy it overlaps (swept check, no physics). |
| `Assets/Scripts/Combat/Enemy.cs` | Moves down its lane showing HP. Killed → pays $1 × wave number. Reaches the bottom in the player's lane → deals its remaining HP to the squad (weakest soldier first). |
| `Assets/Scripts/Combat/EnemySpawner.cs` | Spawns enemies in random lanes (every 0.4 s → 0.15 s over 5 min, never stacked in a lane); HP ramps up over the round. |
| `Assets/Scripts/UI/HudView.cs` | In-round HUD in two side columns: bank (unchanged until the round ends), round earnings, time on the left; soldiers, damage, fire rate, bullet speed on the right. |
| `Assets/Scripts/Meta/UpgradeShop.cs` | Permanent upgrades bought with banked money (saved): Damage (+1 per bullet, $40 +$40/level), Fire Rate (+0.5 shots/s, $30 +$30/level, max 20). |
| `Assets/Scripts/Core/GameManager.cs` (waves) | 30 s waves; every 5th wave reached is a checkpoint new runs start from; highscores (best wave, best run money). |
| `Assets/Scripts/Core/RunSave.cs` | Saved run (time/wave, round money, surviving squad, player position). Autosaved every 3 s and when the app is paused/closed or the pause button is pressed. |
| `Assets/Scripts/Core/NumberFormat.cs` | Compact big-number display (12.3K, 4.5M, 1.23e15). |
| `Assets/Scripts/UI/BarracksView.cs` | Barracks screen: recruit T0, and per tier DEPLOY / RETURN / MERGE. |
| `Assets/Scripts/UI/UpgradeButtonView.cs` | Reusable shop button: name, owned level, price; greys out when unaffordable. |
| `Assets/Scripts/UI/MenuView.cs` | Start menu: bank total and the stats you deploy with. |
| `Assets/Scripts/UI/GameOverView.cs` | Game-over summary: money earned, survival time, new bank total. |
| `Assets/Scripts/Editor/GrayboxSceneBuilder.cs` | Editor menu **Sector Cleanse → Build Graybox Scene**: generates the whole test scene in one click. |

## Graybox scene setup

In Unity: **Sector Cleanse → Build Graybox Scene** (top menu bar).

This generates `Assets/Scenes/Graybox.unity` with the camera, GameManager, lanes,
player, start menu (DEPLOY button) and game-over overlay already wired up, and adds
it to the build settings. Re-running it rebuilds the scene from scratch.

Then press **Play → DEPLOY** and move by holding A/D or the arrow keys, or by dragging with the mouse.
The weapon fires automatically; line up under enemies to shoot them, and dodge (or kill) anything about to
reach your lane. Tuning values live on the **Player** (Weapon, PlayerSquad) and **EnemySpawner** components.
Debug helpers (right-click the component header in the Inspector): **GameManager → Debug/Add $100 to bank**,
**UpgradeShop → Debug/Reset all upgrades**, **Barracks → Debug/Add 5 T0 to reserve**, **Barracks → Debug/Reset barracks**, **GameManager → Debug/Reset checkpoint and highscores**.

Saved runs: closing the app / stopping Play mode / pressing the in-game **II** button saves the run. The menu
then shows **CONTINUE** (resume at the same wave with the surviving squad) and **ABANDON RUN** (banks its money).

Test the death loop without enemies via the `PlayerSquad` component's context menu
(⋮ → *Debug/Take 1 Damage*) in Play Mode.

Supports both the new Input System and the legacy Input Manager.

## Roadmap

1. ~~Core loop & 3-lane movement~~
2. ~~Weapons, projectiles, enemies with HP, money on kill, enemy breach damage → death~~
3. ~~Squad visuals: recruited soldiers shown and adding firepower~~ (recruiting comes with buffs)
4. Lane buffs / debuffs (Lowered Fire Rate, Compromised Position, …)
5. Start-menu meta-upgrades: ~~starting soldiers~~, ~~DMG~~, ~~fire rate~~, starting weapons
