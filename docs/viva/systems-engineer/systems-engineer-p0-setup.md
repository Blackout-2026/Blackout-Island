# Viva notes: project setup (Systems Engineer, step P0)

PR #3 (merged into `develop` as 91d3014). Commits b542163, 02c1602, 2890402.

## What I built

- **The shared folder structure.** 24 new folders, each with a `.gitkeep`, plus 21 folder `.meta` files. Every role
  pulls the same folders and GUIDs. Folders that already existed were left alone.
- **Tags, layers and the collision matrix** (`ProjectSettings/TagManager.asset`, `DynamicsManager.asset`):
  - Layers: Player 6, Enemy 7, Level 8, Interactable 9, Hit 10, Ragdoll 11.
  - Tags: Enemy, Level, Interactable, Hit and Ragdoll (Player is built in).
  - Matrix: Enemy × Enemy off, and Ragdoll collides only with Level. Every other pair stays on.
- **`Blackout.Shared.asmdef`**: an empty assembly with no references, filled in Y1. Every role's assembly references
  it to reach the shared contracts.
- **Packages checked, not changed:** AI Navigation 2.0.14, Input System 1.20.0 and Test Framework 1.6.0 were already
  in `Packages/manifest.json`. Active Input Handling is the Input System package only.

## Why this design

- **One owner, one PR.** Folders, layers and settings exist once, in one merge. If two branches each create the same
  folder, Unity gives it two GUIDs, and Git then reports a conflict or keeps a duplicate (playbook risk 1).
- **Layer indices fixed early.** Prefabs save layer *numbers*. If two people had added "Enemy" at different indices,
  their prefabs would disagree silently.
- **Only the two matrix rules the playbook states.**
  - Agents don't body-collide, so they never shove each other in the 3 m corridor.
  - Ragdolls touch only the level, so a falling body never pushes the player or a door.
  Everything else stayed on until there was a reason to turn it off (Y7 audits it).
- **A shared assembly instead of Assembly-CSharp.** EditMode test assemblies can't reference Assembly-CSharp. A
  change in one role's code also no longer recompiles everybody's scripts.

## Alternatives rejected

- **Each role creates its own folders.** Rejected: duplicate GUIDs (above).
- **An Editor tool that applies the layers.** Rejected for P0. Two settings files are smaller to review than a tool,
  and nothing else would run it.

## Numbers

- 45 files in the folder commit.
- The matrix is 32 little-endian rows of 32 bits, written by a script and checked by decoding them back:
  - row Enemy = 0xFFFFF77F (bit 7 off)
  - row Ragdoll = 0x00000100 (only bit 8, Level)
- Unity loaded both files in batch mode without rewriting them.

## Known weaknesses

- The hand-written matrix hex is hard to read. Check it in Project Settings → Physics instead. Since Y7, 28
  `PhysicsLayersTests` fail if it changes.
- Hit was left colliding with everything, and nothing was put on Level. Y7 found both, reported them to the team,
  and, with the team's agreement, changed them: Hit now collides with nothing, and the level's colliders are on
  Level (`systems-engineer.md` section 3).
- Empty `.gitkeep` files stay until real content arrives.
