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
