using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>One piece of the World Builder's greybox dock as it stands in the scene: a name and the world bounds of its renderer.</summary>
    public readonly struct GreyPart
    {
        public readonly string Name;
        public readonly Vector3 Centre;
        public readonly Vector3 Size;

        public GreyPart(string name, Vector3 centre, Vector3 size)
        {
            Name = name;
            Centre = centre;
            Size = size;
        }

        public float Top => Centre.y + Size.y * 0.5f;
    }

    /// <summary>Where one footbridge piece stands (world position of its origin) and how it is turned.</summary>
    public readonly struct DockPlacement
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly float Yaw;

        public DockPlacement(string name, Vector3 position, float yaw)
        {
            Name = name;
            Position = position;
            Yaw = yaw;
        }
    }

    /// <summary>What to place and which greybox renderers to hide. Pure data, made by <see cref="DockFootbridgePlan.Create"/>.</summary>
    public sealed class DockPlan
    {
        public bool HasDeck;
        public Vector3 DeckPosition;   // the deck's top face, centre
        public float DeckLength;
        public readonly List<DockPlacement> Panels = new List<DockPlacement>();
        public readonly List<DockPlacement> Posts = new List<DockPlacement>();
        public readonly List<string> Hide = new List<string>();
    }

    /// <summary>
    /// Works out the footbridge from the dock the World Builder built (BeachGreyboxBuilder): the deck box, the posts, and a rail box for every
    /// rail panel (the east side has a gap where the boat is moored, and short bollards there). Everything is read from what is in the
    /// scene, so no World Builder constant is needed and the dock can change without this code changing.
    /// </summary>
    public static class DockFootbridgePlan
    {
        /// <summary>A post taller than this above the deck gets a footbridge post; a shorter one is a mooring bollard and stays as it is.</summary>
        public const float TallPostAboveDeck = 0.5f;

        /// <summary>The name of a dock part without the "Dock_" prefix the scene's objects carry (Dock_Deck, Dock_Post_W00, Dock_Rail_E03).</summary>
        public static string Short(string name)
        {
            return name.StartsWith("Dock_") ? name.Substring("Dock_".Length) : name;
        }

        public static DockPlan Create(IReadOnlyList<GreyPart> parts)
        {
            var plan = new DockPlan();
            GreyPart? deck = null;
            foreach (GreyPart part in parts)
            {
                if (Short(part.Name) == "Deck")
                {
                    deck = part;
                }
            }

            if (deck == null)
            {
                return plan;
            }

            float deckTop = deck.Value.Top;
            plan.HasDeck = true;
            plan.DeckPosition = new Vector3(deck.Value.Centre.x, deckTop, deck.Value.Centre.z);
            plan.DeckLength = deck.Value.Size.z;
            plan.Hide.Add(deck.Value.Name);
            foreach (GreyPart part in parts)
            {
                string name = Short(part.Name);
                var position = new Vector3(part.Centre.x, deckTop, part.Centre.z);
                if (name.StartsWith("Post_"))
                {
                    if (part.Top > deckTop + TallPostAboveDeck)
                    {
                        plan.Posts.Add(new DockPlacement(part.Name, position, 0f));
                        plan.Hide.Add(part.Name);
                    }
                }
                else if (name == "Rail_End")
                {
                    plan.Panels.Add(new DockPlacement(part.Name, position, 90f));
                    plan.Hide.Add(part.Name);
                }
                else if (name.StartsWith("Rail_"))
                {
                    plan.Panels.Add(new DockPlacement(part.Name, position, 0f));
                    plan.Hide.Add(part.Name);
                }
            }

            return plan;
        }
    }

    /// <summary>
    /// The footbridge dock (docs/art-pipeline.md section 20): builds the materials and the prefabs Dock_Deck, Dock_RailPanel and Dock_RailPost
    /// from the three SM_Dock_* models, and dresses the World Builder's greybox dock with them. Dressing only hides the greybox renderers and
    /// adds the pieces under Beach/DockFootbridge; the greybox objects keep their colliders, so the walkable area, the rails you cannot step
    /// through and the gap at the boat are exactly as before. A rebuild of the dock by the World Builder re-enables nothing and removes
    /// nothing of this, so run Dress Dock As Footbridge again after one. Restore Greybox Dock undoes it.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;" -executeMethod Blackout.Editor.DockFootbridgeBuilder.BuildPrefabs -logFile -
    /// </summary>
    public static class DockFootbridgeBuilder
    {
        public const string PrefabFolder = "Assets/Prefabs/Models";
        public const string MaterialFolder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string VisualRootName = "DockFootbridge";
        private const string ModelName = "Model";

        public sealed class Piece
        {
            public readonly string Name;      // Deck, RailPanel, RailPost
            public Piece(string name) { Name = name; }
            public string FbxPath => $"Assets/Models/Custom/SM_Dock_{Name}.fbx";
            public string PrefabPath => $"{PrefabFolder}/Dock_{Name}.prefab";
        }

        public static readonly Piece[] Pieces = { new Piece("Deck"), new Piece("RailPanel"), new Piece("RailPost") };

        // material name, texture (T_<name>_BaseColor and _Normal), smoothness
        private static readonly (string Material, string Texture, float Smoothness)[] Materials =
        {
            ("M_Dock_Deck", "T_BridgeDeck", 0.12f),
            ("M_Dock_Paint", "T_DockPaint", 0.22f),
            ("M_Dock_Wood", "T_DockWood", 0.06f),
        };

        public static Piece Find(string name)
        {
            foreach (Piece piece in Pieces)
            {
                if (piece.Name == name)
                {
                    return piece;
                }
            }

            return null;
        }

        [MenuItem("Blackout/Art/Build Dock Footbridge Prefabs")]
        public static void BuildPrefabs()
        {
            ModelMaterials.EnsureFolder(PrefabFolder);
            ModelMaterials.EnsureFolder(MaterialFolder);
            var materials = new Dictionary<string, Material>();
            foreach (var (name, texture, smoothness) in Materials)
            {
                materials[name] = SaveMaterial(name, texture, smoothness);
            }

            foreach (Piece piece in Pieces)
            {
                BuildPrefab(piece, materials);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Dock] Dock_Deck, Dock_RailPanel and Dock_RailPost prefabs are in " + PrefabFolder
                + ". Open the Environment scene and run Blackout > Art > Dress Dock As Footbridge.");
        }

        private static void BuildPrefab(Piece piece, Dictionary<string, Material> materials)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(piece.FbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[Dock] {piece.FbxPath} not found, so {piece.PrefabPath} was not built.");
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(piece.PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(piece.PrefabPath) : new GameObject($"Dock_{piece.Name}");
            try
            {
                Transform model = root.transform.Find(ModelName);
                if (model == null || PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) != fbx)
                {
                    if (model != null)
                    {
                        Object.DestroyImmediate(model.gameObject);
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                    instance.name = ModelName;
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    model = instance.transform;
                }

                AssignMaterials(model.gameObject, materials);
                PrefabUtility.SaveAsPrefabAsset(root, piece.PrefabPath);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            Debug.Log($"[Dock] {(exists ? "Updated" : "Created")} {piece.PrefabPath}");
        }

        private static void AssignMaterials(GameObject instance, Dictionary<string, Material> materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                Renderer source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                Material[] imported = source != null ? source.sharedMaterials : null;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material named = imported != null && i < imported.Length && imported[i] != null ? imported[i] : slots[i];
                    string slotName = named != null ? named.name : string.Empty;
                    if (materials.TryGetValue(slotName, out Material replacement))
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[Dock] {renderer.name} slot {i} uses '{slotName}', which matches no M_Dock_* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }

        private static Material SaveMaterial(string name, string texture, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{texture}_BaseColor.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{texture}_Normal.png"));
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // ------------------------------------------------------------------ dressing the scene's dock

        [MenuItem("Blackout/Art/Dress Dock As Footbridge")]
        public static void DressDock()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Dock] Leave Play mode first: changes made while playing are lost.");
                return;
            }

            Transform dock = FindDock();
            if (dock == null)
            {
                Debug.LogWarning("[Dock] No dock (an object called Dock with a Dock_Deck child) in the loaded scenes. Loaded: " + OpenScenes()
                    + ". Open Assets/Scenes/Environment.unity (double-click it in the Project window), leave Play mode, and try again.");
                return;
            }

            var deckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Find("Deck").PrefabPath);
            var panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Find("RailPanel").PrefabPath);
            var postPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Find("RailPost").PrefabPath);
            if (deckPrefab == null || panelPrefab == null || postPrefab == null)
            {
                Debug.LogWarning("[Dock] The Dock_* prefabs are missing: run Blackout > Art > Build Dock Footbridge Prefabs first.");
                return;
            }

            var byName = new Dictionary<string, Renderer>();
            var parts = new List<GreyPart>();
            foreach (Renderer renderer in dock.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.transform.IsChildOf(dock) && !IsVisualChild(renderer.transform))
                {
                    byName[renderer.name] = renderer;
                    Bounds b = renderer.bounds;
                    parts.Add(new GreyPart(renderer.name, b.center, b.size));
                }
            }

            DockPlan plan = DockFootbridgePlan.Create(parts);
            if (!plan.HasDeck)
            {
                Debug.LogWarning("[Dock] The dock has no 'Deck' object, so nothing was changed.");
                return;
            }

            Transform parent = dock.parent;
            Transform existing = parent != null ? parent.Find(VisualRootName) : null;
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var root = new GameObject(VisualRootName);
            Undo.RegisterCreatedObjectUndo(root, "Dress Dock As Footbridge");
            root.layer = dock.gameObject.layer;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            var deck = (GameObject)PrefabUtility.InstantiatePrefab(deckPrefab, root.transform);
            deck.name = "Deck";
            deck.transform.position = plan.DeckPosition;
            float stretch = plan.DeckLength / 31f;
            if (Mathf.Abs(stretch - 1f) > 0.02f)
            {
                Debug.LogWarning($"[Dock] The dock deck is {plan.DeckLength:0.0} m long, not 31 m: the footbridge deck is stretched to fit.");
            }

            deck.transform.localScale = new Vector3(1f, 1f, stretch);
            foreach (DockPlacement p in plan.Panels)
            {
                Place(root.transform, panelPrefab, p);
            }

            foreach (DockPlacement p in plan.Posts)
            {
                Place(root.transform, postPrefab, p);
            }

            SetLayerRecursively(root, dock.gameObject.layer);
            foreach (string name in plan.Hide)
            {
                if (byName.TryGetValue(name, out Renderer renderer) && renderer.enabled)
                {
                    Undo.RecordObject(renderer, "Dress Dock As Footbridge");
                    renderer.enabled = false;
                }
            }

            EditorSceneManager.MarkSceneDirty(dock.gameObject.scene);
            Debug.Log($"[Dock] Footbridge: 1 deck, {plan.Panels.Count} rail panels, {plan.Posts.Count} posts placed under {VisualRootName}; "
                + $"{plan.Hide.Count} greybox renderers hidden (their colliders are untouched). Save the scene.");
        }

        [MenuItem("Blackout/Art/Restore Greybox Dock")]
        public static void RestoreGreybox()
        {
            Transform dock = FindDock();
            if (dock == null)
            {
                Debug.LogWarning("[Dock] No dock in the loaded scenes. Loaded: " + OpenScenes());
                return;
            }

            Transform parent = dock.parent;
            Transform visuals = parent != null ? parent.Find(VisualRootName) : null;
            if (visuals != null)
            {
                Undo.DestroyObjectImmediate(visuals.gameObject);
            }

            foreach (MeshRenderer renderer in dock.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderer.enabled)
                {
                    Undo.RecordObject(renderer, "Restore Greybox Dock");
                    renderer.enabled = true;
                }
            }

            EditorSceneManager.MarkSceneDirty(dock.gameObject.scene);
            Debug.Log("[Dock] The greybox dock is visible again and the footbridge pieces are removed.");
        }

        private static void Place(Transform root, GameObject prefab, DockPlacement placement)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = DockFootbridgePlan.Short(placement.Name);
            go.transform.position = placement.Position;
            go.transform.rotation = Quaternion.Euler(0f, placement.Yaw, 0f);
        }

        /// <summary>
        /// The dock: an object called "Dock" (active or not, in any loaded scene) that has the deck as a child. Looking through every loaded
        /// scene, and not only for an active "Beach", also finds it when the Environment scene is loaded next to Bootstrap.
        /// </summary>
        private static Transform FindDock()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "Dock" && (t.Find("Dock_Deck") != null || t.Find("Deck") != null))
                        {
                            return t;
                        }
                    }
                }
            }

            return null;
        }

        private static string OpenScenes()
        {
            var names = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                names.Add(SceneManager.GetSceneAt(i).name + (SceneManager.GetSceneAt(i).isLoaded ? string.Empty : " (not loaded)"));
            }

            return string.Join(", ", names);
        }

        private static bool IsVisualChild(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.name == VisualRootName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }
}
