using System;
using System.IO;
using Blackout.Interaction;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Gives Door.prefab the downloaded cyberpunk double door (step D1): the M_DoorCyber material from the four T_DoorCyber maps, and a Model child
    /// (SM_Door_Cyberpunk: a static frame and two leaves) in place of the grey slab. All four doorways (D-Bunker, D-Security, D-Generator, D-Lab) use
    /// Door.prefab, so one rebuild restyles them all; their scene overrides (label, speed, noise, lock) are untouched.
    /// The two leaves are SlidingDoor's panel and mirrorPanel (they part towards -X and +X), and a thin kinematic Blocker keeps the doorway shut
    /// while the door is closed, as the slab's collider did. Re-runnable: the old slab and any earlier Model/Blocker are replaced.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.DoorVisualBuilder.Build
    /// </summary>
    public static class DoorVisualBuilder
    {
        public const string ModelPath = "Assets/Models/Custom/SM_Door_Cyberpunk.fbx";
        public const string MaterialName = "M_DoorCyber";
        public const string TexturePrefix = "T_DoorCyber_";
        public const string MaterialFolder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string FrameName = "DoorFrame";
        public const string LeafLeftName = "DoorLeaf_L";
        public const string LeafRightName = "DoorLeaf_R";

        private const string ModelChild = "Model";
        private const string BlockerChild = "Blocker";
        private const string LegacyPanelChild = "Panel";
        private const float GapWidth = 1.4f;
        private const float GapHeight = 2.3f;
        private const float LeafWidth = GapWidth * 0.5f;
        private const float SlideMargin = 0.05f;   // like the slab's: a little more than the leaf, so it is clear of the gap
        private const float BlockerThickness = 0.16f;

        [MenuItem("Blackout/Systems/Build Cyberpunk Doors")]
        public static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null || !File.Exists(InteractablePrefabBuilder.DoorPath))
            {
                Debug.LogError("[Blackout] Cyberpunk doors not built: " + ModelPath + " and " + InteractablePrefabBuilder.DoorPath + " must exist (run Blackout > Systems > Build Interactable Prefabs first).");
                return;
            }

            Material material = BuildMaterial();
            GameObject root = PrefabUtility.LoadPrefabContents(InteractablePrefabBuilder.DoorPath);
            try
            {
                Rebuild(root, model, material);
                PrefabUtility.SaveAsPrefabAsset(root, InteractablePrefabBuilder.DoorPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Blackout] Cyberpunk doors built: " + InteractablePrefabBuilder.DoorPath + " now shows " + ModelPath + ".");
        }

        /// <summary>
        /// M_DoorCyber in Assets/Materials/Models, URP Lit: base colour, normal, metallic (R) and smoothness (A), emission, and occlusion (G of the metal map).
        /// Created once; an existing material keeps its hand edits and only gets a map where its slot is empty.
        /// </summary>
        public static Material BuildMaterial()
        {
            AssetDatabase.Refresh();
            string path = MaterialFolder + "/" + MaterialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(MaterialFolder))
                {
                    AssetDatabase.CreateFolder("Assets/Materials", "Models");
                }

                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 1f); // scales the map's alpha by 1
                AssetDatabase.CreateAsset(material, path);
            }

            SetMissing(material, "_BaseMap", "BaseColor");
            SetMissing(material, "_BumpMap", "Normal");
            SetMissing(material, "_MetallicGlossMap", "MetalSmooth");
            SetMissing(material, "_OcclusionMap", "MetalSmooth");
            if (SetMissing(material, "_EmissionMap", "Emission"))
            {
                material.SetColor("_EmissionColor", Color.white);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            // URP Lit reads these maps only while their keywords are on (this assembly cannot call URP's BaseShaderGUI, so they are set here).
            EnableIfMapped(material, "_BumpMap", "_NORMALMAP");
            EnableIfMapped(material, "_MetallicGlossMap", "_METALLICSPECGLOSSMAP");
            EnableIfMapped(material, "_OcclusionMap", "_OCCLUSIONMAP");
            EnableIfMapped(material, "_EmissionMap", "_EMISSION");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnableIfMapped(Material material, string property, string keyword)
        {
            if (material.GetTexture(property) != null)
            {
                material.EnableKeyword(keyword);
            }
        }

        private static bool SetMissing(Material material, string property, string map)
        {
            if (material.GetTexture(property) != null)
            {
                return false;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + TexturePrefix + map + ".png");
            if (texture == null)
            {
                return false;
            }

            material.SetTexture(property, texture);
            return true;
        }

        private static void Rebuild(GameObject root, GameObject model, Material material)
        {
            DestroyChild(root.transform, LegacyPanelChild);
            DestroyChild(root.transform, ModelChild);
            DestroyChild(root.transform, BlockerChild);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = ModelChild;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }

            Transform leafRight = instance.transform.Find(LeafRightName);
            Transform leafLeft = instance.transform.Find(LeafLeftName);
            if (leafRight == null || leafLeft == null)
            {
                throw new System.InvalidOperationException(ModelPath + " must contain " + LeafLeftName + " and " + LeafRightName + "; rebuild it with Art/Blender/Scripts/build_cyber_door.py.");
            }

            // The blocker: thin, kinematic (so Assign Physics Layers leaves it on the Interactable layer, as it left the slab) and
            // switched off by SlidingDoor once the door is 80 % open.
            var blocker = new GameObject(BlockerChild);
            blocker.transform.SetParent(root.transform, false);
            blocker.transform.localPosition = new Vector3(0f, GapHeight * 0.5f, 0f);
            var box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(GapWidth + 0.2f, GapHeight + 0.05f, BlockerThickness);
            blocker.AddComponent<Rigidbody>().isKinematic = true;

            PlayerPrefabBuilder.SetLayerRecursively(root.transform, root.layer);

            // The lock lamp sits on the wall beside the frame, at eye height, where the frame no longer hides it.
            Transform lamp = root.transform.Find("LockLamp");
            if (lamp != null)
            {
                lamp.localPosition = new Vector3(GapWidth * 0.5f + 0.5f, 1.4f, 0.23f);
                lamp.localScale = new Vector3(0.08f, 0.3f, 0.05f);
            }

            var door = root.GetComponent<SlidingDoor>();
            PlayerPrefabBuilder.Assign(door, "panel", leafRight);
            PlayerPrefabBuilder.Assign(door, "mirrorPanel", leafLeft);
            PlayerPrefabBuilder.Assign(door, "blocker", box);
            var serialized = new SerializedObject(door);
            serialized.FindProperty("slideDistance").floatValue = LeafWidth + SlideMargin;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Review pictures of Door.prefab in a stand-in wall with the real 1.4 x 2.3 m gap, closed and open, from the south (-Z) and the north (+Z).
        /// Needs a graphics device, so run it without -nographics:
        /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod Blackout.Editor.DoorVisualBuilder.RenderPreview -previewOut "%TEMP%/door-preview"
        /// </summary>
        public static void RenderPreview()
        {
            string folder = Argument("-previewOut") ?? Path.Combine(Path.GetTempPath(), "blackout-door-preview");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(InteractablePrefabBuilder.DoorPath);
            Directory.CreateDirectory(folder);

            var temporary = new System.Collections.Generic.List<GameObject>();
            GameObject Make(string name, PrimitiveType type, Vector3 centre, Vector3 size)
            {
                GameObject go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.position = centre;
                go.transform.localScale = size;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                temporary.Add(go);
                return go;
            }

            Make("WallL", PrimitiveType.Cube, new Vector3(-2.7f, 1.5f, 0f), new Vector3(4f, 3f, 0.4f));
            Make("WallR", PrimitiveType.Cube, new Vector3(2.7f, 1.5f, 0f), new Vector3(4f, 3f, 0.4f));
            Make("Lintel", PrimitiveType.Cube, new Vector3(0f, 2.65f, 0f), new Vector3(1.4f, 0.7f, 0.4f));
            Make("Floor", PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 8f));
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            temporary.Add(camera.gameObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.fieldOfView = 40f;
            foreach (var (rotation, intensity) in new[] { (Quaternion.Euler(50f, -30f, 0f), 1.0f), (Quaternion.Euler(40f, 150f, 0f), 0.8f) })
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = intensity;
                light.shadows = LightShadows.None;
                light.transform.rotation = rotation;
                temporary.Add(light.gameObject);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.45f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            temporary.Add(instance);
            Transform right = instance.transform.Find(ModelChild + "/" + LeafRightName);
            Transform left = instance.transform.Find(ModelChild + "/" + LeafLeftName);
            var views = new (string name, Vector3 position, bool open)[]
            {
                ("south_closed", new Vector3(0f, 1.3f, -4.2f), false),
                ("south_closed_angle", new Vector3(2.6f, 1.6f, -3.0f), false),
                ("south_open", new Vector3(0f, 1.3f, -4.2f), true),
                ("north_closed", new Vector3(0f, 1.3f, 4.2f), false),
                ("north_open_angle", new Vector3(-2.6f, 1.6f, 3.0f), true),
            };
            const int size = 800;
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var picture = new Texture2D(size, size, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            Vector3 rightClosed = right.localPosition;
            Vector3 leftClosed = left.localPosition;
            foreach (var view in views)
            {
                float slide = view.open ? LeafWidth + SlideMargin : 0f;
                right.localPosition = rightClosed + Vector3.right * slide;
                left.localPosition = leftClosed + Vector3.left * slide;
                camera.transform.position = view.position;
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.2f, 0f) - view.position, Vector3.up);
                camera.Render();
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                picture.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, view.name + ".png"), picture.EncodeToPNG());
            }

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(picture);
            foreach (GameObject go in temporary)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            Debug.Log("[Blackout] Door preview pictures: " + folder);
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}
