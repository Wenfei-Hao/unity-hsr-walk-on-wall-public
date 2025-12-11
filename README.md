# Walk-On-Wall Locomotion & Enemy AI Prototype

A small Unity 3D prototype exploring **curved-surface locomotion**, **local-up camera control**, and **plane-aware enemy AI** with a simple, readable codebase.

The demo shows a player character walking from floor to wall to spherical surfaces while enemies patrol on their own planes, detect the player via a basic perception model, and visualize their internal state using world-space UI (question / exclamation marks with a filling alert bar).

---

## ✨ Key Features

- **Curved-Surface Locomotion (Fake Gravity)**
  - Player walks smoothly across floor, walls, and spheres.
  - Gravity direction is computed from surface normals via raycasts.
  - Player’s `transform.up` is continuously aligned to the current surface, avoiding pops or sudden flips.

- **Local-Up Third-Person Camera**
  - Orbit camera that always uses the **player’s local up** as the up direction.
  - Stable view when transitioning between surfaces (no camera flips, minimal jitter).
  - Mouse-driven yaw/pitch and scroll-wheel zoom.

- **Plane-Aware Enemy AI (NavMesh-Based)**
  - Enemies use Unity’s `NavMeshAgent` to patrol and chase, but only on their own plane.
  - Simple perception model: distance + field-of-view (FOV) cone.
  - Enemies start chasing only when they **see** the player; if the player runs too far away, they return to their spawn point.

- **World-Space Alert UI (Question / Exclamation)**
  - Each enemy has a world-space Canvas with a two-layer UI:
    - Bottom: static white/gray question mark.
    - Top: yellow filling bar that grows from bottom to top as suspicion rises.
  - When the bar fills, the icon switches to a red exclamation mark and the enemy starts chasing.
  - Icons always face the camera (billboard effect).

- **Reusable Controllers**
  - `PlayerControl`: self-contained curved-surface locomotion + camera controller.
  - `EnemyController`: reusable enemy AI with:
    - Detection, loss-of-sight, and return-to-origin behavior.
    - Configurable distances, FOV, and UI references.
  - A simplified “flat-world” version of `EnemyController` can be dropped into other projects that do not use wall-walking.

---

## 🧰 Tech Stack

- **Engine:** Unity 2022 LTS (tested on a recent 2022.x LTS build)
- **Language:** C#
- **AI / Navigation:** Unity NavMesh / AI Navigation
- **Animation:** Unity Animator (state machine with `IsWalk` / `IsRun` / `IsChase` booleans)
- **UI:** Unity UI (World Space Canvas, `Image` with Filled mode)
- **Math:** Linear algebra (vector dot/cross products), quaternions for smooth rotation

---

## 🎮 Controls (Demo Scene)

Typical demo controls (you can adjust in your Input settings):

- **Movement:** `W / A / S / D`
- **Camera Orbit:** Move mouse while holding right mouse button (or always-on, depending on setup)
- **Zoom:** Mouse scroll wheel
- **Quit Play Mode:** Standard Unity editor controls

---

## 🧠 How It Works (High-Level)

### 1. Curved-Surface Locomotion & Fake Gravity

- A raycast is fired from the player downwards along `-transform.up` to find the surface underfoot.
- The surface normal becomes the **local up** direction.
- The player’s rotation is adjusted by aligning its up vector to this normal using a quaternion:
  - `Quaternion.FromToRotation(oldUp, normal)` and `Quaternion.Slerp` for smoothing.
- Gravity is applied along the **negative local up** instead of `Vector3.down`, so the character “sticks” to whatever surface they are standing on.

### 2. Local-Up Camera

- Camera position is computed as an orbit around the player, in **spherical coordinates** relative to the player’s current orientation.
- The camera target always uses `player.transform.up` as the up vector:
  - `followCameraPos.LookAt(player.position, player.transform.up);`
- This keeps the horizon visually stable from the player’s perspective, even when walking on walls or spheres.

### 3. Enemy AI & Plane-Aware Chasing

- Each enemy uses a `NavMeshAgent` baked on its own plane.
- Detection uses:
  - **Distance threshold** (`detectDistance`)
  - **Field-of-view**: via `Vector3.Dot(forward, directionToPlayer)` and a cosine threshold.
- When suspicious, an internal **alarm value** is charged over time while the player remains visible; it decays when the player leaves the cone.
- Once the alarm reaches 1.0, the enemy transitions into “alert” state and chases the player.
- If the player runs far enough (`loseDistance`), the enemy stops chasing and returns to its initial position, switching back to idle animation.

### 4. Alert UI (Question / Exclamation Marks)

- A world-space `Canvas` is attached above the enemy’s head.
- Two layered `Image` components:
  - `QuestionBaseImage`: white/gray question mark, always fully visible during suspicious phase.
  - `SignImage`: yellow bar using `Image.Type = Filled (Vertical)` to visualize alarm progress.
- When alarm fills:
  - The icon changes to a red exclamation mark sprite.
- Both images are toggled/updated from the `EnemyController` and rotated each frame to face the camera.

---

## 🚀 Getting Started

1. **Clone the repository**

2. **Open in Unity**

   * Open Unity Hub.
   * Add the cloned folder as a project.
   * Use a Unity 2022 LTS version (or later compatible version).

3. **Open the demo scene**

   * In the Unity Project window, open the sample scene under `Scenes/` (e.g., `Scenes/DemoWalkOnWall.unity`).

4. **Press Play**

   * Use the controls described above to walk onto walls and spheres and observe enemy behavior.

---

## 🔁 Reusing Components in Your Own Project

* **Player Movement (Curved Surfaces)**

  * Add `PlayerControl` to a root object with:

    * A `Rigidbody` (with `useGravity = false` and rotation constraints).
    * A collider (e.g., `CapsuleCollider`).
    * A child model with an `Animator` and appropriate movement animations.
  * Ensure your surfaces have colliders to receive raycasts.

* **Enemy AI & Alert UI**

  * Create an `EnemyRoot` with:

    * `NavMeshAgent`
    * `EnemyController`
    * A child `EnemyModel` with `Animator` and `IsChase` bool.
    * A `SignRoot` with a world-space Canvas and the two-layer images.
  * Tag your player as `"Player"` or drag the reference into the `EnemyController.player` field.
  * Bake a NavMesh for the enemy’s plane/area.

* **Flat-World Variant**

  * For standard, gravity-down projects, you can use the “flat” version of `EnemyController` which removes all curved-surface assumptions and only relies on NavMesh + FOV + distance.

---

## 📌 Status & Future Work

This repository is intended as a **learning / prototype project**, not a polished game. Possible extensions include:

* Curved-surface pathfinding for enemies (instead of restricting them to planes).
* More complex perception models (hearing, memory, group coordination).
* Camera assist features (collision avoidance, dynamic framing).
* Tooling to visualize local frames and surface geometry directly in the editor.

Feel free to fork, experiment, and adapt the controllers to your own gameplay ideas.
