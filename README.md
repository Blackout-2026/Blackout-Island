# Blackout Island: The Cure

A Unity first-person stealth/combat game built for **SE3032 (Graphics & Visualization)** and **SE3062 (Intelligent Systems)**. The player lands on the island by boat, infiltrates the forest and bunker, disables the generator, takes the cure from the laboratory and escapes while four AI agents hunt them.

## Unity setup

| Setting | Value |
| --- | --- |
| Unity version | **6000.3.25f1** (Unity 6.3 LTS). All four installs must match exactly. |
| Render pipeline | URP 17.3.0 |
| Asset Serialization | Force Text |
| Version Control Mode | Visible Meta Files |

The Editor and version control settings are saved in `ProjectSettings/`.

### Open the project

The repository contains the required `Assets`, `Packages`, and `ProjectSettings` folders. The project was initialized from a 3D URP template and validated with Unity Editor 6000.3.25f1.

Clone the repository, then in Unity Hub choose **Add → Add project from disk** and select the repository folder. Use the Editor version above.

Empty asset folders contain `.gitkeep` files so Git retains the folders alongside their `.meta` files.

## Repository structure

```
Assets/
├── Scenes/                        # Bootstrap + additive scenes (see below)
├── Scripts/
│   ├── AI/
│   ├── PlayerAndInteraction/
│   └── Agents/
├── Models/
│   └── Custom/
├── Materials/
├── Textures/
├── Animations/
├── Prefabs/
└── Environment/
docs/                              # Development guide
```

### Scene split

To avoid merge conflicts, work is split into additive scenes that a small `Bootstrap.unity` loads together. Never have two people editing the same `.unity` file.

| Scene | Owner |
| --- | --- |
| `Environment.unity` | World Builder |
| `Interactables.unity` | Systems Engineer |
| `Agents.unity` | Agent Controller + AI |
| `Bootstrap.unity` | Loads the three above |

## Git workflow

- `main` is protected: merges only via a reviewed PR (one teammate's approval required).
- One long-lived branch per person: `feature/world-builder`, `feature/systems-engineer`, `feature/core-developer`, `feature/agent-controller`.
- Large binary assets (`.fbx`, `.blend`, `.png`, `.psd`, `.wav`, `.mp3`, `.mp4`, `.tga`) are stored with **Git LFS**. Run `git lfs install` once after cloning.

## Team

| Student | GV role (SE3032) | IS agent (SE3062) | Architecture |
| --- | --- | --- | --- |
| 1 | World Builder | Security Patrol | Finite State Machine |
| 2 | Systems Engineer | Hunter | Behavior Tree + sound propagation |
| 3 | Core Developer | Tactical Guard | Utility AI |
| 4 | Agent Controller | Commander | GOAP / shared blackboard |



