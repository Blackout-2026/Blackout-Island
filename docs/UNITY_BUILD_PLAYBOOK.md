# Blackout Island: The Cure — Unity Build Playbook

Reference: the working web prototype in `prototype/`. Target: Unity 6000.3.25f1 (URP, C#).
Deadline: Wednesday 21 Oct 2026. Put this file in `docs/` so every agent can read it.

---

## 0. Read this first

**How to use it**
1. Start each step on its own short-lived branch from the latest `develop`: `git checkout develop`, `git pull`, `git checkout -b feature/<role>-<step>-<short-name>` (for example `feature/wb-w3-bunker-blockout`). Commit as you go, push at least daily, merge into `develop` by PR when the step is tested, then delete the branch. No long-lived personal branches.
2. Start every agent session by pasting the **Master Prompt** (section 5), then **one step prompt at a time**.
3. The agent works in small commits. After each one it stops and gives you a title, message and file list. Tick only those files in GitHub Desktop, commit, and reply `continue`. After the last commit, open the PR and start the next step on a new branch.
4. Close the Unity Editor before an agent runs a batch-mode command. Batch mode cannot open a project that is already open.
5. Create `commit-log.local.md` in the repo root and add that name to `.git/info/exclude` (a file inside your own `.git` folder). GitHub Desktop then ignores it, so it never appears as a change and never gets committed. The agent reads it at the start of each session and updates it after every commit.

**What an agent can and cannot do**
- It can write C# scripts, Editor tools that build scenes, materials and prefabs, tests, and docs. It can run Unity in batch mode to compile, run EditMode tests and execute Editor tools, and it can read the console output.
- It cannot see the Editor, judge how something looks, sculpt, paint, model in Blender, or press Bake reliably. Those are your jobs, and your viva marks depend on them.
- It must never claim something works unless it ran it and showed you the output.

**Assessment note.** You are marked individually on your Git history and a viva where you explain your own code, workflow and optimisation choices. Check your module's rules on AI assistance. Whatever the agent writes for you, you must be able to open, explain and defend. Every step ends with a 5-line viva summary for that reason. Read it.

---

## 1. Re-baselined schedule (18 days left, not 4 weeks)

| Phase | Dates | Goal | Gate to move on |
| --- | --- | --- | --- |
| 0 Contracts | Sat 3 – Sun 4 Oct | Reference notes, shared contracts, layout spec | Contracts merged to `develop` |
| 1 Blockout | Mon 5 – Fri 9 Oct | Walkable greybox + NavMesh, player, weapons, doors, hero models drafted and imported, agents moving with placeholder logic | Layout freeze v1 by Wed 7 Oct |
| 2 Content | Sat 10 – Wed 14 Oct | Real materials and lighting, finished models, full AI logic, event chain wired | Mission playable start to finish once (ugly is fine) |
| 3 Integration | Thu 15 – Sat 17 Oct | All agents together, balance, bug fixing | 3 clean full runs, no console errors |
| 4 Freeze | Sun 18 – Wed 21 Oct | Feature freeze Sun 18, optimise, record demo 19–20, viva prep, tag `v1.0-submission` by Tue 20 night | Submitted with a day of buffer |

**Check now:** the module's "Week 13 – Logic & Asset Prototype" checkpoint requires custom models drafted and imported and player interaction working. It probably falls in Phase 1. Confirm the exact date with your lecturer.

**Scope: cut from the bottom first**

| Must | Should | Could |
| --- | --- | --- |
| Full mission loop, generator event chain, cure alert chain | Audio, HUD polish | Sniper scope zoom |
| 3 weapons, health, respawn | Post-processing | Rain, minimap, flashlight, crouch |
| 4 agents, 4 different architectures | Dense forest | Water shader, footstep surfaces |
| 2 custom Blender models, UVs, imported | Extra props | Pause menu settings |
| Door + barricade, NavMesh, powered/blackout lighting | Particles | Debug overlay beyond gizmos |

---

## 2. Ownership (one owner per file, this prevents conflicts)

| Role | Owns |
| --- | --- |
| **World Builder** (+ Security Patrol AI) | `Assets/Environment`, `Assets/Materials/Environment`, `Assets/Textures/Environment`, `Assets/Editor/WorldBuilder`, `Assets/Scripts/Environment`, `Assets/Scripts/AI/SecurityPatrol`, `Assets/ScriptableObjects/Layout`, `Assets/Scenes/Environment.unity`, `Shared/Marker.cs`, `docs/level-layout.md` |
| **Systems Engineer** (+ Hunter AI) | `Assets/Scripts/PlayerAndInteraction`, `Assets/Scripts/Shared` (events, health, damage), `Assets/ScriptableObjects/Events` and `Weapons`, `Assets/Prefabs/Player` and `Interactables`, `Assets/Scripts/AI/Hunter`, `Assets/Scenes/Interactables.unity` and `Bootstrap.unity`, **`ProjectSettings/` and `Packages/manifest.json` (tags, layers, physics, packages)** |
| **Core Developer** (+ Tactical Guard AI) | `Art/Blender/*.blend` (outside `Assets`), `Assets/Models/Custom`, `Assets/Materials/Models`, `Assets/Textures/Models`, `Assets/Prefabs/Models` (visual-only prefabs), `Assets/Prefabs/Props`, `Assets/Scripts/AI/TacticalGuard`, `docs/art-pipeline.md` |
| **Agent Controller** (+ Commander AI) | `Assets/Scripts/Agents`, `Assets/Scripts/Shared/Agents`, `Assets/Animations`, `Assets/Prefabs/Agents`, `Assets/Scripts/AI/Commander`, `Assets/Scenes/Agents.unity` |

Rules: every shared file has its owner in a header comment and only the owner edits it. Need a change in someone else's file? Ask the owner. Never edit another person's scene. Per-role credits go in `docs/credits/<role>.md`, per-role viva notes in `docs/viva/<role>.md`.

**Who blocks whom**

| Blocked task | Needs first |
| --- | --- |
| Everything | P0 setup PR (folders, layers, packages), then Y1 contracts |
| Agent spawning, patrol, cover | Markers and NavMesh (W3, W4) |
| Doors, generator, cure, barricade | Sockets and markers (W3) |
| Blackout lighting | `GeneratorDisabled` channel (Y1) |
| Hunter hearing | `NoiseEvent` channel (Y1) and doors (Y5) |
| Commander alert | `CureTaken` channel (Y1) and cure pickup (Y5) |
| Robot movement and animation | Robot prefab (C3) and path mover (A2) |

---

### 2b. Conflict traps (read before your first commit)

One owner per file is not enough. These are where the team will actually collide:

1. **Duplicate folders and assets.** If two people create the same new folder or the same ScriptableObject, Unity generates a different GUID in each branch. Git then reports a conflict, or both assets survive and scripts reference the wrong one. Only the P0 PR creates shared folders, only Y1 creates the event channel assets, everyone else pulls them. If a channel looks missing, pull `develop`; never re-create it.
2. **ProjectSettings and packages.** Layers, tags, the physics matrix, input settings and `Packages/manifest.json` belong to the Systems Engineer. Need a package or a layer? Ask in the group chat.
3. **Scenes you don't own.** Never save a scene you don't own, even if you only opened it to look. When Unity asks to save, click Don't Save. Test through Bootstrap.
4. **Lighting.** Only the World Builder bakes. Lightmap files are binary and cannot be merged. Never click Generate Lighting, and keep Auto Generate off.
5. **Binary art.** `.blend`, `.fbx` and textures: only their owner edits them.
6. **Prefabs.** Visual prefabs (Core Developer, `Prefabs/Models`) are nested inside gameplay prefabs (Systems Engineer `Interactables`, Agent Controller `Agents`). Nest them; do not edit them.
7. **Additive scenes.** Unity cannot store a reference from an object in one scene to an object in another, so code finds Markers at runtime with `FindObjectsByType<Marker>`, never through Inspector fields across scenes. Fog, skybox and ambient light come from the active scene, so MissionDirector must make Environment the active scene after loading, or the lighting will look wrong and people will blame the World Builder.
8. **Check before committing.** The changed-files list in GitHub Desktop must contain only your owned paths and their `.meta` files. Anything else is usually an accidental save or reimport: discard it and tell the owner. A script can do this check for you:
```
Write tools/check-ownership.ps1 taking a role name. It reads the owned paths for that role from section 2
of docs/UNITY_BUILD_PLAYBOOK.md (hard-code them in the script), runs git diff --name-only against develop
plus untracked files, and prints every changed file outside the owned paths, exiting non-zero if any.
```
9. **A broken `develop`.** If a merge leaves red errors in the console, fix it within the hour or revert your PR. Everyone else is working from it.

---

## 3. Shared contracts (agreed shapes, frozen Mon 5 Oct)

The owner may extend them but must never break them. Namespace `Blackout.Shared`, assembly `Blackout.Shared`.

- `DamageInfo` { amount, point, direction, source, type } and `IDamageable` { IsAlive, TakeDamage(DamageInfo) }
- `Health` MonoBehaviour: max/current HP, invulnerability timer, `OnDamaged`, `OnDied`
- Event channels (ScriptableObjects in `Assets/ScriptableObjects/Events`): `VoidEventChannel` for GeneratorDisabled, CureTaken, PlayerDied, PlayerRespawned, MissionComplete; `NoiseEventChannel` carrying `NoiseEvent` { position, loudness, NoiseType: Footstep/Sprint/Gunshot/Melee/Door/Generator, source }; `StringEventChannel` for ObjectiveChanged
- `Marker` MonoBehaviour { MarkerType type, string id, string owner }. Types: PlayerStart, Checkpoint, AgentSpawn, Waypoint, CoverPoint, DoorSocket, GeneratorSocket, CureSocket, BoatExit, HealthPickup
- `AgentIntent` { moveTarget, MoveSpeed (Stop/Walk/Run), lookTarget, aim, fire }
- `IAgentContext` { own Transform, Health, Blackboard, player Transform, helpers to query Markers }, handed to a brain at Initialize (owner: Agent Controller)
- `IAgentBrain` { Initialize(IAgentContext), Tick(dt) }: decides and returns an intent, never touches Transform, NavMesh or Animator
- `IDebugInfo` { GetDebugText() }, implemented by every brain for the shared debug overlay (owner: Agent Controller)
- `IAgentMover` { SetDestination, Stop, HasArrived, RemainingDistance, CurrentPath (Vector3[]) }
- `Directive` { DirectiveType: Converge/SearchZone/Regroup, position, radius } and `IDirectiveReceiver` { OnDirective }
- `Blackboard` (held by one MonoBehaviour): lastKnownPlayerPos, alertLevel, generatorDisabled, cureTaken, registered agents and their state strings

Each owner puts runtime scripts in their own assembly definition (for example `Blackout.AI.SecurityPatrol`) that references `Blackout.Shared`, plus a Tests assembly. That is what makes EditMode tests possible.

---

## 4. Web prototype → Unity translation

| Prototype (three.js) | Unity |
| --- | --- |
| Procedural world code | Re-runnable Editor tools that build the scene, then hand-edited in the Editor |
| Canvas-generated textures | CC0 PBR textures (ambientCG, Poly Haven), imported at 1024 (2048 for hero items) |
| MeshStandardMaterial | URP/Lit |
| Shadows, ACES, bloom, vignette, grain | URP shadow settings + Volume profiles |
| InstancedMesh forest | Terrain trees and detail objects with LODs |
| WebAudio synthesis | AudioClips (CC0) on spatial AudioSources |
| Grid A* | NavMesh (AI Navigation package) producing path arrays |
| Event bus | ScriptableObject event channels |
| F1 debug overlay | Gizmos and an OnGUI overlay |
| Teleport/debug hooks | Editor menu `Blackout/Debug` |

The prototype is the reference for layout, numbers, look and feel. Do not port the JavaScript line by line.

---

## 5. MASTER PROMPT (paste at the start of every agent session)

```
You are helping me build "Blackout Island: The Cure", a first-person shooter, in Unity 6000.3.25f1
(URP, C#). Four students each own one role and are marked individually on Git history and a viva, so
keep changes small, readable and explainable.

REFERENCE: a working web prototype lives in prototype/ (read-only, never modify it). Use it for intent,
numbers, layout and look; port the design, not the JavaScript. Screenshots: prototype/screenshots/.
Role notes extracted from it: docs/reference/<role>.md. Team plan and contracts: docs/UNITY_BUILD_PLAYBOOK.md.

MY ROLE: <World Builder | Systems Engineer | Core Developer | Agent Controller>.
I own only the paths listed for my role in the playbook (section 2). Never edit files outside them.
If I need a change elsewhere, stop and tell me exactly what to ask the owner.

RULES
- Create new files freely. Never rename, move or delete existing assets or .meta files with file
  commands; use AssetDatabase APIs inside an Editor script. After adding files, run Unity in batch
  mode once so .meta files are generated, and list them so I commit them.
- Editor tools go in Assets/Editor/<Role>/, are re-runnable, deterministic (fixed seeds), and never
  delete content they did not generate.
- Namespaces Blackout.<Area>. Put runtime code in my assembly definition referencing Blackout.Shared.
  Do not edit the shared contracts unless I am their owner.
- AI decision code must be decoupled from visuals: brains return an AgentIntent; a separate mover and
  animation layer executes it. No third-party AI frameworks; write the FSM/behaviour tree/utility/GOAP
  code by hand so I can defend it in a viva.
- Unity 6 APIs: use Rigidbody.linearVelocity (not velocity), FindFirstObjectByType (not
  FindObjectOfType), and the input system the project is configured for (check Project Settings >
  Player > Active Input Handling). No deprecated APIs.
- No per-frame allocations in hot paths; cache references; use pooling for bullets, tracers and FX.
- Commands. Unity: C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe (if missing, ask me).
  Project path contains spaces and a comma, so always quote it. Close the Editor first. Examples:
    Unity.exe -batchmode -nographics -quit -projectPath "<repo>" -executeMethod Blackout.Editor.<Class>.<Method> -logFile -
    Unity.exe -batchmode -nographics -projectPath "<repo>" -runTests -testPlatform EditMode -testResults "%TEMP%\blackout-tests.xml" -logFile -
  Write logs and test results outside the repo.
- Git: you may run read-only commands (status, diff, log, branch --show-current). Never commit, push,
  checkout, switch, reset, stash, amend or rebase. I create branches and make every commit myself.
- Never include files outside my owned paths in a commit.
- If something is ambiguous, choose the simplest option, say what you chose, and continue.
- If a step needs assets only I can supply (textures, models, audio), create clearly named
  placeholders and list exactly what I must replace.

COMMIT WORKFLOW (follow exactly)
BRANCH: at the start of a step run git branch --show-current. If it is main or develop, stop and tell me
to create a branch named feature/<role>-<step>-<short-name> (for example feature/wb-w3-bunker-blockout).
Never create or switch branches yourself.
PLAN: state the step as 3-6 small commits (a title for each), then start commit 1 without waiting.
SMALL COMMITS: each one is a single logical change that leaves the project compiling with no new console
errors (for example: 1 tool code + its tests, 2 generated asset or scene, 3 docs). No unrelated files.
Include the .meta file for every asset. Order them so each commit works on its own.
AFTER EACH COMMIT-SIZED CHUNK: stop, touch no files, and post exactly this:
  COMMIT <n> of <N>
  Title: <type>(<area>): <imperative summary, 72 characters max> (<step id>)
  Message: <2-5 short bullet lines: what changed and why>
  Stage exactly these files: <full list, including .meta files>
  Do not stage: <every other file git status shows, or "nothing">
  Check: <what to look at in Unity or the Console to confirm this chunk works>
  Output: <real output of any batch run or tests for this chunk; say plainly if you could not run Unity>
  Then wait. I will commit manually and reply "continue".
  (type is one of feat, fix, docs, test, chore, refactor)
ON "continue": run git log -1 --stat and git status (read-only). If my commit differs from what you
proposed, or files you changed are still uncommitted, tell me and stop until it is resolved. Otherwise
start the next chunk.
AFTER THE LAST COMMIT OF A STEP, post: (1) a PR title and short description; (2) a 5-line viva summary:
what it does, why this design, one alternative rejected, one number or parameter and its justification,
one known weakness; (3) what I should click or test in the Editor before opening the PR. Then stop.
EDITOR: before a batch run, ask me to confirm the Editor is closed, unless I already said it is closed
and have not said I reopened it.

PROJECT LOG: commit-log.local.md in the repo root is my local memory for you. Never stage or commit it.
AT SESSION START read, in this order: docs/reference/<role>.md, docs/level-layout.md (if it exists),
then the "Current state" section and the last 2 entries of commit-log.local.md. If the log disagrees
with the code or the docs, the code and docs win; tell me about the mismatch.
ON "continue", once my commit is confirmed: append one entry (title, branch, date, decisions, what was
verified and how, open items for other owners, 5-line viva summary), max 15 lines, no file-stat tables.
Then rewrite the "Current state" section at the top: what exists, key decisions and numbers,
conventions (for example how prototype coordinates map to Unity), and handoffs I have not sent yet.
Write each handoff as a one-line message addressed to the owner of the file concerned, so I can send
it. Write only what you actually ran or read.
```

---

## 6. PHASE 0 — reference extraction (everyone, Sat 3 Oct, about 1 hour)

Each role extracts its own slice so the four notes files never conflict. Commit `docs/reference/<role>.md`.

**P0 — Setup PR (Systems Engineer only; everyone else pulls it before creating any folder, layer, package or asset)**
```
STEP P0 — Setup PR.
Branch feature/sys-p0-setup from develop. 1) Create these folders, each with a .gitkeep, then run Unity in
batch mode so .meta files are generated: Assets/Editor/{WorldBuilder,SystemsEngineer,CoreDeveloper,
AgentController}; Assets/ScriptableObjects/{Events,Layout,Weapons,Agents}; Assets/Scripts/{Environment,
Shared,Shared/Agents,Agents}; Assets/Scripts/AI/{SecurityPatrol,Hunter,TacticalGuard,Commander};
Assets/Prefabs/{Player,Interactables,Props,Models,Agents}; Assets/Materials/{Environment,Models};
Assets/Textures/{Environment,Models}; Assets/Environment; Art/Blender; docs/{reference,viva,credits}.
2) In Packages/manifest.json add the AI Navigation package and confirm the Test Framework and the Input
System are present; make no other package changes. 3) In ProjectSettings define the tags and layers
Player, Enemy, Level, Interactable, Hit, Ragdoll and set the physics collision matrix (agents do not
body-collide with each other; Ragdoll collides only with Level). 4) Create an empty assembly definition
Blackout.Shared in Assets/Scripts/Shared. Touch nothing else. Print the list of changed files so I can
verify the PR contains only these.
```

**World Builder**
```
STEP W0 — Reference extraction.
Read prototype/README.md, prototype/screenshots/, and every file in prototype/src that builds the world,
materials, lighting, sky/ocean, fog and ambient audio. Write docs/reference/world-builder.md with:
(1) a level table: each zone with centre, size, floor height, connections, in metres (state the
prototype's unit assumption); (2) exact coordinates of generator, cure, doors, player start,
checkpoints, boat/exit, cover points, patrol waypoints, agent spawns, pickups; (3) the material and
colour palette with hex values per surface; (4) lighting values for powered vs blackout: moon/hemi
light, fog colours and densities, emergency light colours, post-processing values; (5) vegetation
counts and distribution; (6) what the prototype fakes that Unity needs a real solution for.
Write no Unity code. Do not modify prototype/.
```

**Systems Engineer**
```
STEP Y0 — Reference extraction.
Read prototype/README.md and the prototype files for player, weapons, physics, doors, crate, mission
flow, HUD and audio events. Write docs/reference/systems-engineer.md with: player speeds, jump,
sprint, crouch, head bob values; each weapon's damage, range, cooldown, spread, falloff, switch time,
recoil; health numbers; interaction ranges and hold times; door and crate behaviour; the mission
phase list with objective strings; respawn rules; noise event types and loudness values; HUD
elements. Numbers only from the code; mark anything guessed. No Unity code. Do not modify prototype/.
```

**Core Developer**
```
STEP C0 — Reference extraction.
Read prototype/README.md, screenshots and the prototype files that build models (generator, security
robot, humanoids, weapon viewmodels, props, cure chamber). Write docs/reference/core-developer.md
describing each model as a build brief: silhouette, proportions in metres, parts list, materials and
colours, emissive parts, moving parts, and approximate detail level; list the props needed with
counts. Include the two required hero models (generator, security robot) with front/side/top
dimensions. No Unity or Blender code. Do not modify prototype/.
```

**Agent Controller**
```
STEP A0 — Reference extraction.
Read prototype/README.md and the prototype files for navigation, agents, the four AI classes,
procedural animation, the blackboard and the debug overlay. Write docs/reference/agent-controller.md
with: movement speeds, acceleration, turn rates, separation/stuck handling; per-agent stats (HP,
vision, hearing, attack range, damage, reaction delay, aim error); each AI's states/nodes/actions and
transition conditions; the Commander goals/actions/preconditions/effects; the blackboard fields; the
alert buff multipliers; animation states and timings. Mark anything guessed. No Unity code.
Do not modify prototype/.
```

---

## 7. WORLD BUILDER

### Graphics: level, lighting, texturing, NavMesh

**W1 — Layout spec and scene skeleton** (Phase 0)
```
STEP W1 — Layout spec and scene skeleton.
Read docs/reference/world-builder.md. 1) Write docs/level-layout.md: a table of zones (Beach, Forest,
BunkerEntrance, SecurityArea, Corridor, GeneratorRoom, Laboratory, EscapeRoute) with centre, size (m),
floor height and door connections, at real scale (player 1.8 m, corridor 3 m wide, ceiling 3 m,
door 1.4 x 2.3 m). 2) Create Assets/ScriptableObjects/Layout/BunkerLayout.cs (rooms: name, position,
size, door openings) and an asset filled from the table. 3) Write Editor menu Blackout/World/Create
Scene Skeleton that creates Assets/Scenes/Environment.unity if missing and adds empty roots: Terrain,
Beach, Forest, BunkerExterior, Bunker (child per room), Markers, Lighting, Props, Volumes. Re-runnable,
never deletes existing content. No geometry yet.
```
You: run the menu item, check the hierarchy, tell the team `level-layout.md` is ready.

**W2 — Terrain and beach blockout**
```
STEP W2 — Terrain and beach.
Using the island shape and heightmap parameters in docs/reference/world-builder.md, write Editor tool
Blackout/World/Build Terrain: create a Terrain in Environment.unity (size from the reference, default
200 x 200 m, heightmapResolution 513 or lower) with a beach-to-forest slope, a flat pad for the bunker
exterior and a flat beach strip with the boat berth. Fixed seed, re-runnable. Add boundary cliffs or
invisible colliders so the player cannot leave the island. Add a placeholder water plane (URP Lit,
transparent), a greybox boat (primitives) and dock posts under Beach. Save TerrainData in
Assets/Environment/Terrain/.
```
You: walk it with the player controller once Y2 lands; adjust the sculpt by hand.

**W3 — Bunker blockout and markers**
```
STEP W3 — Bunker blockout and markers.
Write Editor tool Blackout/World/Build Bunker Blockout that reads BunkerLayout.asset and builds each
room from cubes (floor, ceiling, walls with door gaps) under Bunker/<Room>, with colliders, static
flags and a distinct placeholder material per room. No door geometry (Systems Engineer owns doors):
put a Marker of type DoorSocket in every gap. Under Markers create, from docs/level-layout.md and the
reference: PlayerStart, Checkpoint_* (beach, forest, bunker entrance, generator room), AgentSpawn_*
and Waypoint_* per agent type (Hunter x2, SecurityPatrol x3, TacticalGuard x2, Commander x1),
CoverPoint_* (at least 4 across corridor and generator room), GeneratorSocket, CureSocket,
HealthPickup_*, BoatExit. Leave space in the corridor for the barricade crate. Re-runnable: rebuild
only generated children.
```
Done when: every marker exists with the exact names, rooms connect, nothing blocks the route.

**W4 — NavMesh and Layout Freeze v1** (gate for everyone, target Wed 7 Oct)
```
STEP W4 — NavMesh and validator.
Confirm the AI Navigation package is already in Packages/manifest.json (added in P0). If it is not,
stop and tell me; do not edit manifest.json. Write Editor tool Blackout/World/Bake NavMesh: add/update a
NavMeshSurface on an Environment root with agent radius 0.4, height 1.8, step height 0.4, max slope 40
(these must match docs/level-layout.md and the Agent Controller's mover) and bake. Write Blackout/World/
Validate Layout: every Waypoint/AgentSpawn/CoverPoint/PlayerStart/BoatExit marker is within 1 m of the
NavMesh (NavMesh.SamplePosition); a path exists PlayerStart -> CureSocket -> BoatExit
(NavMesh.CalculatePath); door gaps are at least 1.2 m wide. Print a pass/fail table to the Console.
```
You: click Bake, run Validate, open PR "Layout freeze v1" into `develop`, announce it. After this, geometry changes need a heads-up in the group chat.

**W5 — Material and texture library**
```
STEP W5 — Material library.
Read the palette in docs/reference/world-builder.md. I will place CC0 PBR texture sets in
Assets/Textures/Environment/<Surface>/ (BaseColor, Normal, Roughness/Metallic/AO). Write Editor tool
Blackout/World/Build Material Library: for each surface (Sand, Grass, ForestFloor, Rock, Concrete,
ConcreteStained, MetalPlate, Rust, Tile, HazardStripe, Glass, Fabric) create a URP/Lit material in
Assets/Materials/Environment with maps assigned and tiling set, falling back to a flat colour from the
palette if textures are missing. Add an import pass: max size 1024 (2048 only for names ending _Hero),
compression on, mipmaps on, normal maps flagged, correct sRGB. Add Blackout/World/Apply Materials to
assign materials to blockout objects by room and surface name. Record every texture's name, URL and
licence in docs/credits/world-builder.md.
```
You: download the textures (ambientCG / Poly Haven), keep a consistent look, check it against the prototype screenshots.

**W6 — Terrain painting and vegetation**
```
STEP W6 — Terrain painting and vegetation.
Write Blackout/World/Paint Terrain And Forest: paint terrain layers (Sand, Grass, ForestFloor, Rock)
by height and slope, then scatter trees, bushes, rocks and grass with TerrainData tree/detail
prototypes, using counts and clearings from the reference. Keep clearings near the Hunter waypoints
and a readable route from the beach to the bunker. Tree prefabs are in Assets/Environment/Vegetation
(free low-poly pine/palm, LOD groups). State the visible-triangle budget you chose and report the
real numbers. Fixed seed, re-runnable.
```

**W7 — Powered lighting**
```
STEP W7 — Powered lighting.
Read the lighting section of the reference. Write Blackout/World/Setup Lighting: night ambient/skybox
colours, one directional moon light, fog per the reference, URP shadow settings suited to a medium
quality preset. Under Lighting/PoweredLights create fluorescent lights per room (Mixed mode, colours
and intensities from the reference); add a FlickerLight script (Assets/Scripts/Environment) used on a
few. Mark static environment geometry Contribute GI + Batching Static + Occluder/Occludee Static.
Add reflection probes per room and a Light Probe Group (or Adaptive Probe Volume) covering the bunker
and the forest path. Make sure the Lighting Settings asset has Auto Generate off. Do NOT bake in batch mode: tell me the exact settings to use in Window >
Rendering > Lighting (lightmapper, resolution, samples, denoiser).
```
You: click Generate Lighting, check the look against the prototype's pre-blackout screenshots.

**W8 — Blackout lighting state** (needs Y1)
```
STEP W8 — Blackout state.
Write Assets/Scripts/Environment/PowerStateLighting.cs. It references the GeneratorDisabled
VoidEventChannel (Blackout.Shared) and a LightingStateProfile ScriptableObject (powered vs blackout:
fog colour/density, ambient, directional intensity, emergency light settings). On the event: powered
lights die one by one over about 1.5 s (staggered), red emergency lights fade in, a rotating alarm
light starts, fog transitions over about 2 s, emissive materials on screens and lamps switch off.
Add ContextMenu 'Trigger Blackout' and 'Reset' for testing; reset on PlayerRespawned only when the
generator is not disabled. Emergency and alarm lights go under Lighting/BlackoutLights and must be
Realtime or Mixed (baked lights cannot change). Event-driven only: no reference to the generator
object. EditMode test for the profile interpolation.
```
Done when: 'Trigger Blackout' changes the mood dramatically. This is the best visual beat in the demo.

**W9 — Post-processing and atmosphere**
```
STEP W9 — Post-processing and atmosphere.
Create URP Volume profiles in Assets/Environment/Volumes: Powered and Blackout, matching the
prototype's tonemapping, bloom, vignette, film grain and colour adjustments. Extend
PowerStateLighting (do not rewrite it) to blend the global volume weight between them. Add an Editor
tool that builds particle systems: vent steam, corridor dust, drifting beach fog, optional light
rain (off by default). Add a QualityPreset script (low/medium/high) that toggles shadow distance,
post-processing and particle counts.
```

**W10 — Interior set dressing and final bake** (needs C4)
```
STEP W10 — Set dressing and final bake.
Use props from Assets/Prefabs/Props (Core Developer) plus CC0 kit pieces I add. Write Blackout/World/
Dress Bunker: place props per room from a table in docs/level-layout.md (crates, pipes, cables,
monitors, desks, signs, lab equipment), keeping a 1.2 m clear walking path and leaving marker
positions free; skip any prop whose prefab is missing. Then run Bake NavMesh and Validate Layout and
set static flags. List which objects must stay dynamic (doors, barricade, pickups).
```
You: dress the key rooms by hand for the hero shots, then Generate Lighting again.

**W11 — Optimisation with numbers** (this is your 20-mark justification)
```
STEP W11 — Optimisation report.
Write Blackout/World/Scene Report: triangles and vertices of the Environment scene, material count,
lights by mode, top-10 textures with sizes, renderer counts, static vs dynamic objects; append a dated
table to docs/viva/world-builder-optimisation.md. Then apply and measure one change at a time:
SRP Batcher compatibility check, static batching, occlusion culling (generate data or give me the
steps), vegetation LODs, per-object lightmap resolution, texture compression, shadow distance. After
each change re-run the report and add a before/after row with what changed and why. Do not touch
gameplay objects.
```

**W12 — Capture and integration**
```
STEP W12 — Screenshots and fixes.
Write Blackout/World/Capture Screenshots: moves a camera to named viewpoints (Beach, Forest,
BunkerEntrance, Corridor, GeneratorRoom powered, GeneratorRoom blackout, Lab) and saves PNGs to a
folder outside the repo (print the path) for the demo video and viva. Then fix the layout and lighting
bugs listed in docs/playtest-log.md that belong to my role.
```

### Intelligent Systems: Security Patrol (Finite State Machine)

**S1 — Structure**
```
STEP S1 — Security Patrol structure.
Read docs/reference/agent-controller.md (Security Patrol section). In Assets/Scripts/AI/SecurityPatrol
create assembly Blackout.AI.SecurityPatrol (references Blackout.Shared) and a Tests assembly.
Implement SecurityPatrolBrain : MonoBehaviour, IAgentBrain as a hand-written finite state machine:
abstract State with Enter/Tick/Exit; states Patrol, Investigate, Pursue, Attack, Search; one transition
table; a SecurityPatrolConfig ScriptableObject (vision range/angle, hearing range, attack range,
speeds, search time, reaction delay, aim error). The brain only returns an AgentIntent each tick.
```

**S2 — Perception**
```
STEP S2 — Perception.
Add a pure-C# Perception class: vision cone with range/angle, a line-of-sight raycast against level
geometry, and hearing via NoiseEventChannel (loudness vs distance). Output: canSeePlayer,
lastKnownPosition, heardNoise. Keep it testable (inputs are positions and a raycast interface).
EditMode tests: inside/outside the cone, blocked by a wall, noise below/above threshold.
```

**S3 — State logic**
```
STEP S3 — States.
Implement the transitions: Patrol (walk Waypoint markers owned by this agent) -> Investigate (heard
noise) -> Pursue (saw player) -> Attack (in range with line of sight, fire with reaction delay and aim
error) -> Search (lost player: go to last known position, look around, give up after searchTime) ->
Patrol. Add the alert multipliers from the reference when generatorDisabled/cureTaken are set on the
Blackboard (faster, wider detection). Tests for each transition with fake perception input.
```

**S4 — Debug and tuning**
```
STEP S4 — Debug, tuning, viva notes.
Implement IDebugInfo (state name, last transition reason) for the shared debug overlay, and scene
gizmos for the vision cone and waypoints. Write docs/viva/security-patrol.md: why an FSM suits a patrol
role, what its limits are against a behaviour tree/utility/GOAP, each config value with its
justification, and the transition table.
```

---

## 8. SYSTEMS ENGINEER

### Graphics: player interaction physics

**Y1 — Shared contracts** (first task in the project, Sat 3 – Sun 4 Oct)
```
STEP Y1 — Shared assembly and contracts.
Create Assets/Scripts/Shared with assembly Blackout.Shared and implement the contracts in section 3 of
docs/UNITY_BUILD_PLAYBOOK.md: DamageInfo, IDamageable, Health; VoidEventChannel, NoiseEventChannel
(NoiseEvent, NoiseType), StringEventChannel plus a GameEventListener component with UnityEvent;
the channel assets in Assets/ScriptableObjects/Events; Marker + MarkerType; and the agent contracts
(AgentIntent, MoveSpeed, IAgentContext, IAgentBrain, IAgentMover, IDebugInfo, Directive,
IDirectiveReceiver, Blackboard) as
interfaces/data only, headers naming their owner (Agent Controller for agent contracts, World
Builder for Marker). EditMode tests for Health (damage, death, invulnerability) and channels.
Keep it small: this must merge to develop first.
```
You: open the PR immediately and tell the team. Everyone waits for this.

**Y2 — Player controller**
```
STEP Y2 — FPS controller.
Using docs/reference/systems-engineer.md, build the player prefab in Assets/Prefabs/Player:
CharacterController-based movement (walk, sprint, jump, gravity, slopes/steps), mouse look with cursor
lock, head bob, landing shake, camera, weapon holder. Footsteps and sprinting publish NoiseEvents
scaled by speed. Use the configured input system with an actions asset. Include a Blackout/Debug menu
to teleport the player to any Checkpoint marker. Expose speeds in a ScriptableObject. Tests for the
movement-state logic that can be separated from MonoBehaviour.
```

**Y3 — Weapons**
```
STEP Y3 — Weapons.
WeaponData ScriptableObject (damage, range, cooldown, spread, falloff start, pellets, recoil, switch
time). Three weapons from the reference: short-range gun (fast, spread, falloff beyond 10 m), sniper
(slow, no spread), melee (cone/overlap in front of the player, no ammo). One WeaponController handles
switching (1/2/3 and wheel), firing, cooldowns; HitscanWeapon and MeleeWeapon implement a common
interface. Hit feedback: hit-marker event, impact FX, pooled tracers (LineRenderer), muzzle flash,
recoil kick, enemy hit-flash hook via IDamageable. Firing and melee publish NoiseEvents. Placeholder
viewmodels; list what to replace. Tests: damage falloff, cooldown, melee cone.
```

**Y4 — Death, respawn, HUD**
```
STEP Y4 — Respawn and HUD.
PlayerHealth: 100 HP, no regen, 1.5 s invulnerability after respawn, publishes PlayerDied and
PlayerRespawned; respawn at the last reached Checkpoint marker with full health, keeping generator
and cure state. uGUI HUD in the player prefab: health bar, weapon slots 1/2/3 with the active one
highlighted, crosshair, hit-marker, objective text (StringEventChannel), interaction prompt, alert
banner, damage-direction flash. No hard game-over screen.
```

**Y5 — Interactables**
```
STEP Y5 — Interactables.
Interactable base class (camera raycast, 3 m, prompt text) and prefabs in Assets/Prefabs/Interactables
using the sockets (DoorSocket, GeneratorSocket, CureSocket, HealthPickup markers): sliding Door (open
by player and by agents, locked state, publishes Door NoiseEvents), GeneratorInteractable (a prefab that
nests the Core Developer's Generator_Visual and never edits it; hold E for 2 s with a progress ring, then raises GeneratorDisabled and publishes a Generator noise), CurePickup
(raises CureTaken), HealthPickup, Barricade crate (Rigidbody with drag and mass, NavMeshObstacle with
carving so agents path around it). Placement tool Blackout/Systems/Place Interactables that
instantiates prefabs at the sockets and never moves existing ones. Spark/hum audio hooks for the
generator. Tests for the hold-to-interact timer.
```
Done when: the generator holds, fires the event, and a listener on `GeneratorDisabled` proves it.

**Y6 — Mission director and Bootstrap**
```
STEP Y6 — Mission flow.
Create Assets/Scenes/Bootstrap.unity that additively loads Environment, Interactables and Agents, then makes Environment the active scene
(fog, skybox and ambient light come from the active scene).
MissionDirector (state machine): Beach -> Forest -> BunkerEntrance -> Corridor -> GeneratorRoom ->
Lab -> Escape -> Complete. It sets objective text per phase, tracks checkpoints, detects the win
condition (CureTaken and player at BoatExit), and shows a Mission Complete screen with time, kills,
deaths and a Restart button. Add Blackout/Debug menu items to jump to each phase. Tests for the phase
transitions.
```

**Y7 — Stress, layers, physics optimisation**
```
STEP Y7 — Physics hygiene.
Verify the physics layers and collision matrix created in P0 (Player, Enemy, Level, Interactable, Hit,
Ragdoll) and tell the team before changing them; make weapons raycast only the right layers, and check for null references and missed hits in
a scripted run (Blackout/Debug/Stress Test). Write docs/viva/systems-engineer.md covering the event
channel decoupling decision, the weapon system design, and the physics optimisation choices with
numbers.
```

### Intelligent Systems: Hunter (behaviour tree + sound propagation)

**H1 — Behaviour tree core**
```
STEP H1 — Behaviour tree.
In Assets/Scripts/AI/Hunter create assembly Blackout.AI.Hunter and a Tests assembly. Write a small
behaviour-tree core by hand: Node base with Running/Success/Failure, Selector, Sequence, Condition,
Action, Inverter, Cooldown decorator, and a shared per-agent Blackboard of local values. No
packages. Tests for each composite's semantics.
```

**H2 — Hearing and sound propagation**
```
STEP H2 — Sound propagation.
Implement HearingSystem consuming NoiseEventChannel. A noise has a loudness; perceived loudness falls
with distance and is reduced per wall crossed (count occluders with raycasts between source and
listener). The Hunter keeps the strongest recent noise with a timestamp and decays it. Expose
perceivedLoudness, noiseDirection, timeSinceNoise. Document the attenuation formula and constants in
the config ScriptableObject. Tests: open space vs one wall vs two walls, decay.
```

**H3 — Tree design and tactics**
```
STEP H3 — The Hunter tree.
HunterBrain : IAgentBrain builds the tree: Selector[ Attack (see player in range, fire with reaction
delay and aim error), Pursue (see player), Investigate (loud noise: move to source), CautiousApproach
(gunfire heard: advance using cover/bushes, slower), Search (last known position), Idle/Roam
(forest waypoints) ]. Different reactions to gunfire vs footsteps vs sprinting. Camouflage is a
visibility multiplier on the player's detection when standing still (config value). Returns an
AgentIntent only. Tests with a fake blackboard for the key branches.
```

**H4 — Debug and viva**
```
STEP H4 — Debug and viva notes.
IDebugInfo for the active tree branch, scene gizmos for noise radii and the current target, and
docs/viva/hunter.md: why a behaviour tree beats a flat FSM for this agent, why sound-based sensing
rather than a detection radius, the attenuation formula with each constant justified, and one known
weakness.
```

---

## 9. CORE DEVELOPER

### Graphics: custom 3D models (Blender)

You model, unwrap and texture the two required hero models yourself in Blender; that is what the rubric checks (custom models, topology, UV mapping) and what the viva asks about. The agent can write Blender Python helpers for repetitive chores and the Unity import and prefab setup, not your topology or UVs.

**C1 — Art pipeline**
```
STEP C1 — Art pipeline doc and import tooling.
Read docs/reference/core-developer.md. Write docs/art-pipeline.md: units (1 Blender unit = 1 m),
apply transforms, forward/up axes for FBX export to Unity 6, naming (SM_ for static meshes, M_ for
materials, T_ for textures), pivot rules, triangle budgets (generator, robot, props: propose numbers
with a justification), texture sizes, the folder rules (.blend in Art/Blender outside Assets, FBX in
Assets/Models/Custom), and a checklist before export. Then write an AssetPostprocessor in
Assets/Editor/CoreDeveloper that applies the right import settings to anything in Models/Custom
(scale factor, normals, tangents, no baked cameras/lights, materials mode) and prints the triangle
count of each imported mesh.
```

**C2 — Generator hero model** (draft imported by about Wed 8 Oct)
```
STEP C2 — Generator prefab.
I will model and unwrap the generator in Blender and export SM_Generator.fbx. Create the Unity side:
Assets/Prefabs/Models/Generator_Visual.prefab (visuals and sockets only, no interaction scripts; the
Systems Engineer nests it in their interactable prefab) from the FBX with URP/Lit materials from my textures
(M_Generator_*), a collider, an emissive core material, child empties for: InteractPoint, SparkVFX,
HumAudioSource. Write a small GeneratorVisuals script that listens to GeneratorDisabled and switches
the core material to off, stops the hum and plays sparks, with a ContextMenu to test. If my textures are
missing, assign flat placeholder colours and list what to replace.
```

**C3 — Security robot hero model** (draft imported by about Wed 8 Oct)
```
STEP C3 — Robot prefab.
I will model the robot (segmented body, visor with emissive eye, hydraulics, weapon) as separate mesh
parts with pivots at the joints, and export SM_SecurityRobot.fbx. Build Assets/Prefabs/Models/SecurityRobot_Visual.prefab: visual only (mesh parts hierarchy, emissive
eye material, WeaponSocket and MuzzlePoint empties); no colliders, Health or scripts, because the Agent
Controller nests it inside their agent prefab. Note the joint
pivots and the part names the Agent Controller needs for procedural animation, in docs/art-pipeline.md.
```

**C4 — Prop kit and cure chamber**
```
STEP C4 — Props.
From the prop list in docs/reference/core-developer.md, list which props I should model low-poly in
Blender (cure chamber, lab table, crate/barricade, monitor, desk, barrel) and which I should take from
CC0 packs (pipes, cables, signs, vents). Create the Unity prefabs in Assets/Prefabs/Props with colliders
and correct pivots, hazard/sign decals as quads with materials, and a Props palette Editor window
listing every prop with its triangle count. Weapon viewmodels: use free low-poly weapon meshes, wire
them as prefabs for the Systems Engineer, and credit them in docs/credits/core-developer.md.
```

**C5 — LODs and optimisation numbers**
```
STEP C5 — LODs and numbers.
Add LOD groups where the triangle count warrants it, set up shared materials/atlases to cut draw calls,
and write Blackout/Art/Model Report listing each custom model's triangles, UV channels, texture sizes
and material count into docs/viva/core-developer.md with before/after rows for every reduction you
make. Explain each topology decision (edge flow, where quads were kept, n-gons removed).
```

### Intelligent Systems: Tactical Guard (Utility AI)

**T1 — Utility framework**
```
STEP T1 — Utility core.
In Assets/Scripts/AI/TacticalGuard create assembly Blackout.AI.TacticalGuard and Tests. Write a utility
AI by hand: Consideration (input value, response curve: linear/quadratic/logistic/inverse, clamped
0..1), UtilityAction (list of considerations, combined by multiplication with the compensation factor
for the number of considerations, plus a weight), and a selector that picks the highest score with a
small hysteresis bonus for the current action so it does not flicker. Tests for the curves and the
selector.
```

**T2 — Considerations and actions**
```
STEP T2 — Guard actions.
Actions: Attack, MoveToCover, Reposition, HoldPost. Considerations: distance to player, line of sight,
cover availability (nearest unused CoverPoint markers, distance from the player and from the post),
distance from the guarded post, own health, threat level (from the Blackboard and recent damage).
TacticalGuardBrain : IAgentBrain returns an AgentIntent for the winning action. All weights and curve
parameters live in a config ScriptableObject. Cover points are claimed so two guards do not pick the
same one. Tests with fake inputs for each action winning in the situation it should.
```

**T3 — Debug, tuning and viva**
```
STEP T3 — Score overlay and justification.
IDebugInfo that lists every action's score each tick (for the shared overlay) and a gizmo for the
chosen cover point. Write docs/viva/tactical-guard.md with a table of every weight and curve
parameter, the reasoning for each number, an example scoring calculation by hand for one situation,
and why utility AI fits a guard better than an FSM or a behaviour tree.
```

---

## 10. AGENT CONTROLLER

### Graphics: agent animation and movement

**A1 — Agent contracts and base prefab** (Phase 0, after Y1)
```
STEP A1 — Agent base.
Using the contracts from section 3 (already in Blackout.Shared from Y1), create in Assets/Scripts/
Agents: AgentRoot (Health, brain slot, mover, animator driver, shooter), AgentContext given to
brains, the Blackboard holder MonoBehaviour, and AgentRegistry (agents register, the Blackboard
exposes their state strings). A base prefab Assets/Prefabs/Agents/AgentBase.prefab with a capsule
collider on the Enemy layer. Brains only return AgentIntent; AgentRoot applies it through the mover,
the animator driver and the shooter. Agent prefabs for each type nest the visual prefabs from Assets/Prefabs/Models (Core Developer) or the
free humanoid models and never edit them. EditMode tests for intent application with a fake mover.
```

**A2 — Path array and smooth mover**
```
STEP A2 — Path follower.
PathProvider: NavMesh.CalculatePath to a Vector3[] corner array (this is the path array the brains'
destinations become). PathFollower : IAgentMover consumes the array and moves the agent with
acceleration and deceleration, a turn-rate-limited rotation, corner smoothing and arrival slowing,
staying on the NavMesh (NavMesh.SamplePosition clamp). Speeds, acceleration and turn rate come from the
reference. Replans when the destination changes or the path is blocked (barricade moved). Gizmos draw
the path. Tests for corner advancement and arrival.
```

**A3 — Animation**
```
STEP A3 — Animation.
Humanoid agents: an Animator Controller with parameters Speed, Aiming, Hit, Dead and a blend tree
(idle/walk/run) plus aim, hit-react and death states, using free humanoid animations I supply (list the
exact clip names expected). AnimatorDriver reads the mover's velocity and the intent. Robot: a
ProceduralRobotAnimator that rotates the joint parts (names in docs/art-pipeline.md) for walk, run,
aim and collapse. Death: collapse then fade/disable after a delay. Everything is driven by the mover
and Health events, never by the brains.
```

**A4 — Steering extras**
```
STEP A4 — Separation and stuck handling.
Separation steering so agents do not stack, stuck detection (no progress for N seconds -> replan, then
nudge), door awareness (agents trigger doors on their route via the Door interface owned by the
Systems Engineer), and a shared AgentShooter (hitscan with reaction delay, spread/aim error, damage via
IDamageable, tracer, Gunshot NoiseEvent) that the brains use through AgentIntent.fire. Tests for
separation and stuck logic.
```

**A5 — Spawning and difficulty**
```
STEP A5 — Spawner.
AgentSpawner in the Agents scene reads AgentSpawn markers and instantiates Hunter x2, SecurityPatrol
x3, TacticalGuard x2, Commander x1 with their brain components, waypoints from Waypoint markers owned
by each agent, and stats from a DifficultyConfig ScriptableObject (HP, damage, reaction delay, aim
error). Respawn all agents on Restart. Scripted test run: all eight agents active for 5 minutes,
report average frame time, null reference errors and stuck counts.
```

### Intelligent Systems: Commander (GOAP + blackboard)

**G1 — Planner**
```
STEP G1 — GOAP planner.
In Assets/Scripts/AI/Commander create assembly Blackout.AI.Commander and Tests. Write a GOAP planner by
hand: WorldState as a dictionary of bool/int keys, GoapAction (preconditions, effects, cost),
GoapGoal (desired state, priority), and an A* search over world states that returns the cheapest action
sequence with a heuristic of unmet goal keys. Tests: plans found, cheapest chosen, unreachable goal
returns empty, replan when state changes.
```

**G2 — Commander brain**
```
STEP G2 — Commander.
CommanderBrain : IAgentBrain: keeps its world state in sync with the Blackboard (playerLocated,
cureTaken, generatorDisabled, agentsAlive, squadAssembled, playerNeutralized). Goals PlayerLocated and
PlayerNeutralized. Actions OrderConverge, OrderSearchZone, OrderRegroup, each with preconditions,
effects and costs from the config ScriptableObject; executing an action sends Directives through
IDirectiveReceiver to the other three brains. It does not need to shoot. CureTaken triggers an
immediate replan and the alert buff from the reference. If the Commander dies, no more directives
(agents keep the last one).
```

**G3 — Coordination contract with the other brains**
```
STEP G3 — Directive handling.
Provide each of the other brains' owners (Security Patrol, Hunter, Tactical Guard) with a tiny,
documented DirectiveHandling sample in docs/viva/commander.md showing how OnDirective should map to
their own states/actions without coupling to the Commander. Add a test harness scene-free (fake
receivers) showing three receivers reacting to Converge, SearchZone and Regroup.
```

**G4 — Debug and viva**
```
STEP G4 — Debug and viva notes.
IDebugInfo showing the current goal, the plan (action list with costs) and the blackboard values, and
one scene gizmo per directive. Write docs/viva/commander.md: why GOAP rather than an FSM here, how
costs were chosen (with a worked example plan), why a shared blackboard instead of agents referencing
each other, and one known weakness.
```

### Shared debug overlay (Agent Controller)

```
STEP A6 — Debug overlay.
Create AgentDebugOverlay (OnGUI, toggled with F1): for each registered agent show type, HP, IDebugInfo
text, current path length and speed; plus the Blackboard values. Draw all paths and noise radii as
gizmos when enabled. Strip it from release builds with a scripting define.
```

---

## 11. INTEGRATION (everyone, Phases 3–4)

**Daily**
- Morning: `git checkout develop` and `git pull`, then open the project and fix any errors before working. If a step runs longer than two days, merge `develop` into your step branch each morning. Don't build one step branch on top of another: merge the first one before starting the next.
- Evening: small PR into `develop`, one teammate approves. Post one line in the group chat on what you touched.
- `develop` -> `main` only at checkpoints, after a team playtest.

**Full-run test (all four, every day from Wed 14 Oct)**
Beach -> forest (Hunter) -> bunker door -> corridor (Security Patrol, barricade) -> generator room (Tactical Guard) -> hold E on generator (blackout cascade) -> lab -> cure (alert) -> fight back -> boat. Log every bug in `docs/playtest-log.md` with an owner. No new features after Sun 18 Oct.

**Prompt for any bug you own**
```
BUG (follow the commit workflow). Role: <role>. Symptom: <what happened and where>. Repro: <steps>. Expected: <expected>.
Find the cause by reading only my owned files plus the Console output. Reproduce it with a test or
an Editor tool first if you can, then fix the smallest thing. If the cause is in someone else's file,
stop and tell me exactly what to send them.
```

**Build and demo**
- One person builds a Windows standalone (File > Build Profiles), tested on a second machine. Keep the build out of Git.
- 3-minute demo plan: boat arrival (~20 s), forest and Hunter (~40 s), bunker fight (~50 s), generator blackout cascade (~30 s), cure and alert (~30 s), escape (~10 s). Use the Capture Screenshots tool for the title cards.
- Tag the submission commit `v1.0-submission` by Tue 20 Oct night.

**Viva prep (each person, Mon 19 – Tue 20 Oct)**
Open your own agent's main file in under 10 seconds. Explain your architecture against the other three. Justify one optimisation or design choice with a number from your `docs/viva/<role>.md`. Run each other's mock vivas.
