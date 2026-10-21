# Soldier animations on all agents (AN1)

The imported Kevin Iglesias **Human Soldier Animations 2.0 FREE** package now
drives Security Patrol, Hunter, Tactical Guard and Commander. The 24 spawned
agents use their existing four gameplay prefabs. AI decisions, navigation,
combat timing and respawn rules still use their existing systems.

R1 subsequently replaces timed respawns with one cure-triggered wave outside
the lab. Player death restores the saved roster through
[checkpoint recovery](checkpoint-recovery.md), including fresh animation instances.

Run **Blackout > Agents > Apply Soldier Animations** after rebuilding a visual
or replacing an agent prefab. The tool fills the existing
`Assets/Animations/Agent_Humanoid.controller`, creates the sidearm override and
the two rigid-model avatars, and updates the four gameplay prefabs through
Unity's prefab APIs. Existing asset GUIDs, visual nesting, materials, weapon
drops, event channels and brain configuration are preserved. Package files
remain in their imported folder. The Hunter's FBX metadata changes to Humanoid;
its FBX contents stay unchanged, and its visual builder now keeps that rig type.

| Purpose | Clip under `Assets/Kevin Iglesias/Human Animations/Animations/Male/` |
| --- | --- |
| Idle | `Idles/HumanM@MilitaryIdle01.fbx` |
| Walk | `Movement/Walk/HumanM@Walk01_Forward.fbx` |
| Run | `Movement/Run/HumanM@Run01_Forward.fbx` |
| Rifle aim | `Combat/Rifle/HumanM@Rifle_Aim01.fbx` |
| Rifle shot | `Combat/Rifle/HumanM@Rifle_Aim01_Shoot01.fbx` |
| Sidearm aim | `Combat/Gun/HumanM@Gun_Aim01.fbx` |
| Sidearm shot | `Combat/Gun/HumanM@Gun_Aim01_Shoot01.fbx` |
| Damage | `Combat/HumanM@Damage01.fbx` |
| Death | `Combat/HumanM@Death01.fbx` |

Patrol and Hunter use their FBX importer's Humanoid avatars and rifle clips.
The existing robot and Commander consist of rigid meshes on named joints.
`AgentAvatarBuilder` maps those joints to Humanoid bones and builds avatar
assets from a T-pose. It restores the imported joint transforms first, so
rebuilding does not accumulate animation poses. Neither model is replaced or
unpacked. Commander and robot use a sidearm override, which fits a pistol or
single weapon pod. The robot's old procedural driver remains present but
disabled in its gameplay prefab; AgentRoot selects an enabled animation driver.

`AnimatorDriver` reads actual mover speed. The existing blend thresholds remain
**0 / 1.5 / 4 m/s** for idle/walk/run. Locomotion and Aim share the leg blend
tree, while an upper-body mask applies weapon aim and recoil. Therefore an
agent can move and aim together. Recoil follows the shooter's actual shot
counter, including its reaction delay and burst timing. A damage animation
temporarily takes priority over weapon posing; death blocks further hit
transitions and uses the existing **3.2 s delay + 1.6 s fade**. Respawning
creates a fresh prefab instance with live health and animation state.

Root motion is disabled: PathFollower continues moving and turning the
gameplay root. The animated Patrol/Hunter model rotation differs from their
static visual-prefab rotation because Humanoid playback supplies the facing
correction. Weapon grips are aligned in the sampled aim pose. The Commander's
imported pistol follows its right hand in LateUpdate without reparenting its
FBX objects. This makes the weapon and muzzle follow the animated pose.

Unity APIs used: [BuildHumanAvatar](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AvatarBuilder.BuildHumanAvatar.html),
[HumanDescription](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HumanDescription.html),
and [Animator.applyRootMotion](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Animator-applyRootMotion.html).

`SoldierAnimationTests` evaluates the real saved avatars and clips in EditMode,
including moving legs during aim, a fixed gameplay root and death priority.
`AgentAnimationPlayTests` uses Unity callbacks for all four prefabs, checks
actual-shot recoil, damage, death fade and fresh spawns. The existing lab
respawn test and 24-agent smoke test cover the live scene integration.

Verification on 2026-10-21: setup/import and repeat builds exited **0**;
both controllers, both generated avatars and all four prefabs were identical
after the repeat build. The selected agent and model EditMode checks passed
**311/311**, and animation lifecycle, lab respawn and scene smoke PlayMode
checks passed **3/3**, with Unity exit **0** for both test runs. Evidence is in
the system temporary folder: `blackout-AN1-editmode.log/.xml` and
`blackout-AN1-playmode.log/.xml`. The scene smoke report
`blackout-agent-stress-20261021-140928.txt` records 24 spawned/alive agents,
no exceptions, error logs, warnings or stuck events. These are selected checks,
not a whole-project test run or a full gameplay/visual sign-off.

`SoldierAnimationPreview.Render` writes Idle/Run/Aim/Death review frames to
the system temporary folder `blackout-AN1-preview`. This preview needs a graphics
device, so run it in batch mode without `-nographics`. Its image order is
Commander, Tactical Guard, Hunter, Patrol from left to right. It saves no scene.

Manual checks: play Bootstrap, watch every type idle, walk and run, draw fire
and check moving aim/recoil, damage and kill each type. Take the cure and check
the one outside-lab wave, clear the lab and verify the exit still opens. Die
and check checkpoint recovery restores the saved roster and visuals. Restart
and check there are no frozen or already-dead visuals. Check Console errors.

Limits: these are retargeted human clips, including on the robot's rigid parts.
Joint gaps, finger placement, left-hand support and weapon/shoulder clipping
still need an in-game visual check. There is no per-frame hand IK or pitch
aiming adjustment. The retained model proportions and animations are not an
exact weapon-grip match. Reload/grenade actions are not introduced by this step.

Viva: one shared controller drives four rig mappings from realised movement
and executor state. Root-motion movement was rejected because it would compete
with navigation. The 1.5/4 m/s thresholds reuse the existing mover defaults.
The known weakness is imperfect clip-to-model proportions and weapon fitting.
