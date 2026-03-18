# Game Mechanics Document - Maze Escape

## 1. Game Concept

A 3D third-person maze escape game. The player navigates through a maze to find a key, push it to a door, and reach the finish zone. Spike traps and patrolling guards serve as obstacles that can kill the player.

## 2. Player Movement

- **Script:** `PlayerMovement.cs`
- **Controls:** W/S (forward/backward), A/D (turn left/right)
- **Technical:** Uses `Rigidbody.velocity` for physics-based movement, preventing wall clipping issues.
- **Collision Detection:** Set to `Continuous` mode to ensure robust wall collision even at corners.

## 3. Camera

- **Script:** `CameraFollow.cs`
- A 3rd person camera that follows the player using `Vector3.Lerp` for smooth transitions.

## 4. Key Object

- **Physics:** Has a Rigidbody component — the player pushes it by walking into it.
- **Tag:** `Key` — used for collision detection with the door.
- The player must push the key through the maze towards the door.

## 5. Door

- **Script:** `DoorController.cs`
- **Collision Detection:** Uses `OnCollisionEnter` to detect collision with objects tagged as `Key`.
- When the key collides with the door, the asset's built-in `Door.OpenDoor()` function is called, causing the door to rotate open with animation and sound.
- After the door opens, the player must reach the Finish Zone to win.

## 6. Finish Zone

- **Script:** `FinishButton.cs`
- **Trigger Detection:** Uses `OnTriggerEnter` — when the player enters this zone, `GameManager.WinGame()` is called.
- The game is won and the Win Panel is displayed.

## 7. Spike Trap

- **Scripts:** `SpikeTrapDemo.cs` (Asset) + `KillZone.cs`
- **Coroutine:** `OpenCloseTrap()` — periodically raises and retracts spikes.
- Timing is configurable via `activeTime` and `inactiveTime` public variables.
- **Kill Mechanic:** Uses `OnTriggerStay` — if the player is standing on the trap while spikes are active, `GameManager.LoseGame()` is called.
- The player is only killed when standing on top; touching from the side does not trigger death.

## 8. Guard Patrol

- **Script:** `GuardPatrol.cs`
- **Coroutine:** `PatrolRoutine()` and `MoveToPoint()` — guards patrol back and forth between two waypoints.
- **Detection:** Uses `Vector3.Distance` to calculate distance to the player. If within `detectionRange`, `GameManager.LoseGame()` is called.
- **Gizmos:** Detection range is visualized as a yellow sphere in the Scene view.

## 9. Background Music

- **Script:** `MusicManager.cs`
- Uses `AudioSource` with `Play On Awake` and `Loop` enabled for continuous background music.
- **M key** toggles music on/off.

## 10. Restart GUI

- **Script:** `GameManager.cs`
- Uses a **singleton pattern** for global access.
- Displays `LosePanel` when the player dies, `WinPanel` when the player wins.
- `Time.timeScale = 0` pauses the game when a panel is shown.
- **Play Again:** Calls `RestartGame()` to reload the current scene.
- **Quit:** Calls `QuitGame()` to exit the application via `Application.Quit()`.

## 11. Visual Polish

- **Point Lights:** Placed near the key and door for atmospheric lighting.
- **Fog:** Enabled environmental fog for depth and atmosphere.
- **Ocean:** A large animated water plane surrounds the maze using `WaterAnimation.cs`, which scrolls the texture offset to simulate waves.
- **Flags:** A blue flag marks the starting point; two red flags mark the finish zone.

## 12. Techniques Used

| Technique           | Where Used                                   |
| ------------------- | -------------------------------------------- |
| Coroutine           | SpikeTrapDemo, GuardPatrol                   |
| Collision Detection | DoorController (OnCollisionEnter)            |
| Trigger Detection   | FinishButton, KillZone (OnTriggerEnter/Stay) |
| Rigidbody Physics   | Player, Key                                  |
| Unity Asset Store   | Character, Door, Spike Trap, Textures        |
| UI Canvas           | Win/Lose Panels, Buttons                     |
| AudioSource         | Background Music                             |
| Texture Animation   | WaterAnimation (Ocean waves)                 |

## 13. Asset Store Packages Used

- Easy Primitive People (Character model)
- Free Wood Door Pack (Door model and animation)
- AurynSky Dungeon Pack (Spike Trap)
- Game Buffs Free Stylized Textures (Wall/floor textures)
- Rust Key (Key model)
