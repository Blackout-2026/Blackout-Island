# Credits (World Builder)

Third-party assets used by the World Builder (`Assets/Textures/Environment`, `Assets/Environment`). Every texture
must have a row here before it is committed. Use CC0 sources only (ambientCG, Poly Haven) unless the team agrees
otherwise.

## Textures

No textures have been added yet. All 12 environment materials use the flat palette colour from
`docs/level-layout.md` ("Materials (W5)"). Fill in a surface's row when you add its files.

| Surface | Files (in `Assets/Textures/Environment/<Surface>/`) | Source asset | URL | Licence | Author |
| --- | --- | --- | --- | --- | --- |
| Sand | none yet (flat #847860) | — | — | — | — |
| Grass | none yet (flat #2C3A1D) | — | — | — | — |
| ForestFloor | none yet (flat #32281A) | — | — | — | — |
| Rock | none yet (flat #43423D) | — | — | — | — |
| Concrete | none yet (flat #72726B) | — | — | — | — |
| ConcreteStained | none yet (flat #5C5E5A) | — | — | — | — |
| MetalPlate | none yet (flat #5A5E60) | — | — | — | — |
| Rust | none yet (flat #64361C) | — | — | — | — |
| Tile | none yet (flat #AAB3B4) | — | — | — | — |
| HazardStripe | none yet (flat #D9A521) | — | — | — | — |
| Glass | none yet (flat #9FC9D0, alpha 0.12) | — | — | — | — |
| Fabric | none yet (flat #7D7158) | — | — | — | — |

Generated files are not credited separately: `<Surface>_MetallicSmoothness.png` is made by Build Material Library
from that surface's Roughness and Metallic maps, so it has the same source and licence.

## How to add a texture set

1. Download the 1K or 2K PNG/JPG version from ambientCG or Poly Haven. Both are CC0 1.0.
2. Rename the files `<Surface>_<Map>` and put them in `Assets/Textures/Environment/<Surface>/`:

   | Map | ambientCG file ends with | Poly Haven file contains |
   | --- | --- | --- |
   | BaseColor | `_Color` | `_diff` |
   | Normal | `_NormalGL` (not `_NormalDX`) | `_nor_gl` |
   | Roughness | `_Roughness` | `_rough` |
   | Metallic | `_Metalness` | `_metal` |
   | AO | `_AmbientOcclusion` | `_ao` |

   Skip displacement and packed `_arm` files. Add `_Hero` at the end of the name only for a 2048 texture.
3. Run **Blackout/World/Build Material Library** and check that the Console lists the surface as "textured".
4. Fill in the surface's row: the file names, the asset's name or ID, its page URL, "CC0 1.0", and the author
   shown on the page.

## Vegetation models (W6)

No third-party models have been added yet. The terrain is painted with generated placeholders
(`Assets/Environment/Vegetation/Placeholder`, made by **Blackout/World/Build Vegetation Placeholders** from the
prototype's own geometry), which need no credit. Fill in a row when you add a real model.

| Prefab (in `Assets/Environment/Vegetation/`) | Used now | Source asset | URL | Licence | Author |
| --- | --- | --- | --- | --- | --- |
| Pine.prefab | Pine_Placeholder (108 / 46 / 23 triangles) | — | — | — | — |
| Palm.prefab | Palm_Placeholder (712 / 230 / 96) | — | — | — | — |
| Rock.prefab, RockSmall.prefab | Rock_Placeholder, RockSmall_Placeholder (180 / 80 / 20) | — | — | — | — |
| Bush.prefab | Bush_Placeholder (160) | — | — | — | — |
| Grass.prefab | Grass_Placeholder (6) | — | — | — | — |

## How to add a vegetation model

1. Use a CC0 low-poly pack (for example Kenney or Quaternius, both CC0 1.0). Put its files in
   `Assets/Environment/Vegetation/<Pack>/`. FBX files go through Git LFS.
2. In `Assets/Environment/Vegetation/`, make a prefab (or prefab variant) named exactly as in the table above, for
   example `Pine.prefab`. Its pivot is at the foot of the trunk, 1 unit = 1 m, about 10 m tall for a pine and 8 m for a
   palm at scale 1, with URP materials.
   - Trees and rocks: a LOD Group on the root with 3 levels of at most 1,200 / 400 / 120 triangles (the budget in
     `docs/level-layout.md`), and a CapsuleCollider round the trunk (pine radius 0.3, height 10, centre y 4; palm
     radius 0.25, height 8, centre y 3; RockSmall has no collider). Terrain trees only use capsule colliders.
   - Bush and Grass: one MeshFilter and MeshRenderer on the root, no collider, a material with GPU instancing on.
3. Run **Paint Terrain And Forest**, then **Bake NavMesh** and **Validate Layout**. The Console lists the prefab
   without "(placeholder)" and prints the new triangle numbers.
4. Fill in the row: the asset's name, its page URL, "CC0 1.0", and the author.
