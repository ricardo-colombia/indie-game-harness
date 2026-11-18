---
name: gamedev-platformer
description: Architecture recipe and measurable feel targets for a 2D or 2.5D platformer (controller, jump, camera, enemies, collectibles, level flow, save). Use when the game is a platformer, before writing the controller, and when jumping "feels off".
---
Genre recipe on top of the CLAUDE.md code contract. **Rules in plain C# classes, MonoBehaviours only wire them** — that is what makes this testable.

## Shape of the code (one system per card)
| System | Plain C# (EditMode-tested) | Thin MonoBehaviour / asset |
|---|---|---|
| Motor | `PlatformerMotor.Step(ref State, Input, dt)`: velocity, gravity, grounded, coyote, jump buffer, variable jump, slopes via a ground-hit struct | applies the result with a `CharacterController` or kinematic Rigidbody + capsule casts; for 2.5D pin `z` to the play plane every frame |
| Tuning | `MotorConfig` ScriptableObject: every number below | one asset per character, so enemies/hero share the motor |
| Input | action map → `Input` struct (move, jumpPressed/Held, dash…) | PlayerInput; touch = on-screen stick/buttons mapped to the same actions |
| Camera | `CameraRig.Step`: look-ahead, vertical dead zone, bounds clamp | Cinemachine or a ~40-line follower; never parented to the player |
| Enemies | state machine `Patrol→Chase→Attack→Hurt→Dead`, targeting from data | same motor or a simple mover; hit/hurt via an interface (`IDamageable`) |
| Collectibles / interactions | `Pickup` event + counter model, `IInteractable` | trigger colliders; counters exposed as properties for probes |
| Level flow | `LevelCatalog` data (id, scene, spawn, goal, checkpoints), `StartLevel(string)` public | loader + fade |
| Save | `SaveData` (versioned JSON, atomic write), pure serialisation tests | `SaveService` writes to `Application.persistentDataPath` |
| Audio / UI | events in, audio/UI out (`Jumped`, `Landed`, `Hurt`, `Collected`) | listeners; the motor never calls audio |

## Feel numbers to start from (all in `MotorConfig`; the director tunes them)
coyote time 0.10 s · jump buffer 0.12 s · jump height 2.5–3.5 × character height with 0.35–0.45 s to apex · variable jump (release → cut upward velocity ×0.5) · fall gravity 1.5–2× rise gravity · terminal fall speed ≈ 1.5× jump launch speed · ground accel 0.06–0.1 s to top speed, decel faster than accel · air control ≥ 70 % of ground · camera look-ahead ≈ 1.5 body-widths in move direction · hurt invulnerability 1.0 s with knockback ≤ 0.3 s of lost control.

## Lock it with scenarios (the feel contract)
Probes read the motor's state (`Player › PlatformerMotorBehaviour › Velocity.y`, `› IsGrounded`, `› Position.y`); steps press jump with `set` on `<Keyboard>/space`. Thresholds worth locking once the director likes it: `player.y.max` (jump height), `player.speed.max` (run speed), time-to-apex, `coyote jump succeeds 0.08 s after leaving the ledge` (a scenario that walks off and jumps late), `frame.ms.p95`. Add `scenarios/platformer_feel.json` as a card with `allow_paths`.

## Order of work (playable first)
Greybox level + motor + camera → play it with `gamedev-playtest` → collectible + one enemy + hurt/respawn → checkpoint/level flow → save → only then art/animation (`gamedev-animation-set`) and juice. A platformer with no good jump is not rescued by assets.
