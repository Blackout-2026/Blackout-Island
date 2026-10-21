using System;
using System.Collections.Generic;
using System.IO;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Step B1: the downloaded military hut becomes the bunker's entrance, and its materials style the rooms. The map is not changed:
    /// the hut is placed over the old facade, at D-Bunker's socket (0, 10, 48.2), and the old facade boxes keep their colliders but are no
    /// longer drawn. Re-runnable: materials, prefabs and the scene instance are updated in place.
    /// Models come from Art/Blender/Scripts/build_military_hut.py (SM_Hut_Entrance: the static front of the hut; SM_Hut_Props: wall props).
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.HutEntranceBuilder.BuildAll
    /// </summary>
    public static class HutEntranceBuilder
    {
        public const string EntranceModelPath = "Assets/Models/Custom/SM_Hut_Entrance.fbx";
        public const string PropsModelPath = "Assets/Models/Custom/SM_Hut_Props.fbx";
        public const string EntrancePrefabPath = "Assets/Prefabs/Environment/Hut_Entrance.prefab";
        public const string PropFolder = "Assets/Prefabs/Props";
        public const string MaterialFolder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string ContainerName = "EntranceHut";
        public const string InstanceName = "Hut_Entrance";

        /// <summary>D-Bunker's socket, 6 cm up so the pad's top is above the entrance apron (y 10.04) and the two do not flicker.</summary>
        public static readonly Vector3 HutPosition = new Vector3(0f, 10.06f, 48.2f);

        /// <summary>The downloaded material names, and the M_Hut_&lt;name&gt; material (and T_Hut_&lt;name&gt;_ maps) each one becomes.</summary>
        public static readonly (string Source, string Name)[] Materials =
        {
            ("Roof_map", "Roof"), ("Outside_Walls_map", "OutsideWalls"), ("Front_Entrance_map", "FrontEntrance"),
            ("Foundation_map", "Foundation"), ("Window_Frames_map", "WindowFrames"), ("Windows_map", "Windows"),
            ("Window_Glass_map", "WindowGlass"), ("Barricades_map", "Barricades"),
            ("Keypads_SecurityLight_Tracks_Brackets_mao", "Keypads"), ("Signs_map", "Signs"), ("Fuse_Box_map", "FuseBox"),
            ("Cameras_map", "Cameras"),
        };

        /// <summary>The parts of the hut that stop the player and agents (the rest is only drawn).</summary>
        public static readonly string[] SolidParts = { "Hut_Roof", "Hut_FrontWall", "Hut_Porch", "Hut_Bollards" };

        public static readonly (string Prefab, string Mesh)[] Props =
        {
            ("Prop_HutFuseBox", "Hut_FuseBox"), ("Prop_HutCamera", "Hut_Camera"), ("Prop_HutKeypad", "Hut_KeypadProp"),
            ("Prop_HutSignNotice", "Hut_SignNotice"), ("Prop_HutSignWarning", "Hut_SignWarning"),
        };

        /// <summary>Parts of the old EntranceExterior that stay drawn; every other box is hidden (its collider stays).</summary>
        private static readonly string[] KeptFacadeParts = { "Apron", "Retaining", "Pilaster", "Drain", "DoorTrim" };

        private const string UndoLabel = "Build Hut Entrance";

        public static string MaterialPath(string name) => MaterialFolder + "/M_Hut_" + name + ".mat";

        [MenuItem("Blackout/World/Build Hut Entrance")]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build Hut Entrance works outside Play mode only.");
            AssetDatabase.Refresh();
            var materials = BuildMaterials();
            BuildEntrancePrefab(materials);
            BuildPropPrefabs(materials);
            AssetDatabase.SaveAssets();
            PlaceEntrance();
        }

        /// <summary>
        /// Styles the bunker's rooms: Build Material Library gives the Concrete and ConcreteStained surfaces the hut's textures (Assets/Textures/Environment),
        /// which every wall, ceiling and floor of the lobby, ramp, security area and corridor already uses, then Dress Bunker places the hut's fuse boxes,
        /// cameras, keypad and signs from the dressing table in docs/level-layout.md.
        /// </summary>
        [MenuItem("Blackout/World/Style Bunker With Hut")]
        public static void StyleBunker()
        {
            MaterialLibraryBuilder.BuildMaterialLibrary();
            BunkerDresser.DressBunker();
        }

        /// <summary>M_Hut_* in Assets/Materials/Models, URP Lit, from T_Hut_*_BaseColor/_Normal/_MetalSmooth. A map missing on disk is left empty.</summary>
        public static Dictionary<string, Material> BuildMaterials()
        {
            var result = new Dictionary<string, Material>();
            foreach (var (source, name) in Materials)
            {
                string path = MaterialPath(name);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Hut_" + name };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.SetTexture("_BaseMap", LoadTexture(name, "BaseColor"));
                material.SetTexture("_BumpMap", LoadTexture(name, "Normal"));
                Texture2D metalSmooth = LoadTexture(name, "MetalSmooth");
                material.SetTexture("_MetallicGlossMap", metalSmooth);
                material.SetTexture("_OcclusionMap", metalSmooth);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 1f); // scales the map's alpha by 1
                BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
                EditorUtility.SetDirty(material);
                result[source] = material;
            }

            return result;
        }

        private static Texture2D LoadTexture(string name, string map)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/T_Hut_" + name + "_" + map + ".png");
        }

        /// <summary>Hut_Entrance.prefab: one child per mesh of SM_Hut_Entrance, with the hut materials, and mesh colliders on the solid parts.</summary>
        private static void BuildEntrancePrefab(Dictionary<string, Material> materials)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(EntranceModelPath);
            if (model == null) throw new InvalidOperationException(EntranceModelPath + " is missing; run Art/Blender/Scripts/build_military_hut.py.");
            WithPrefab(EntrancePrefabPath, InstanceName, root =>
            {
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                {
                    if (part == model.transform || !part.TryGetComponent(out MeshFilter filter)) continue;
                    GameObject go = AddMesh(root.transform, part, filter.sharedMesh, materials);
                    go.layer = PhysicsLayers.Level;
                    if (Array.IndexOf(SolidParts, part.name) >= 0) go.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticFlagRules.Environment);
                }

                root.layer = PhysicsLayers.Level;
            });
        }

        /// <summary>Prop_Hut*.prefab: one mesh with its materials; the pivot is the middle of the face that touches the wall, facing +Z.</summary>
        private static void BuildPropPrefabs(Dictionary<string, Material> materials)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(PropsModelPath);
            if (model == null) throw new InvalidOperationException(PropsModelPath + " is missing; run Art/Blender/Scripts/build_military_hut.py.");
            foreach (var (prefab, mesh) in Props)
            {
                Transform part = model.transform.Find(mesh);
                if (part == null) throw new InvalidOperationException(PropsModelPath + " has no " + mesh);
                WithPrefab(PropFolder + "/" + prefab + ".prefab", prefab, root =>
                {
                    GameObject go = AddMesh(root.transform, part, part.GetComponent<MeshFilter>().sharedMesh, materials);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticFlagRules.Environment);
                });
            }
        }

        private static GameObject AddMesh(Transform parent, Transform source, Mesh mesh, Dictionary<string, Material> materials)
        {
            var go = new GameObject(source.name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(source.localPosition, source.localRotation);
            go.transform.localScale = source.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material[] imported = source.GetComponent<MeshRenderer>().sharedMaterials;
            var assigned = new Material[imported.Length];
            for (int i = 0; i < imported.Length; i++)
            {
                if (!materials.TryGetValue(imported[i].name, out assigned[i]))
                    throw new InvalidOperationException(source.name + " uses " + imported[i].name + ", which has no M_Hut material.");
            }

            go.AddComponent<MeshRenderer>().sharedMaterials = assigned;
            return go;
        }

        /// <summary>Creates the prefab if it is missing, otherwise empties it; fills it with build; saves it. The root (and so its GUID and links) stays.</summary>
        private static void WithPrefab(string path, string rootName, Action<GameObject> build)
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            if (!File.Exists(path))
            {
                var seed = new GameObject(rootName);
                PrefabUtility.SaveAsPrefabAsset(seed, path);
                Object.DestroyImmediate(seed);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                build(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Puts Hut_Entrance in Environment under BunkerExterior/EntranceHut and hides the old facade boxes. A hidden box keeps its collider, so the
        /// walkable and blocked space does not change.
        /// </summary>
        public static void PlaceEntrance()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePrefabPath);
            Transform exterior = WorldSceneSkeleton.FindRoot(scene, "BunkerExterior").transform;
            Transform container = GeneratedObjects.FindOrCreateChild(exterior, ContainerName, UndoLabel);
            Transform instance = container.Find(InstanceName);
            if (instance == null)
            {
                var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                created.name = InstanceName;
                created.transform.SetParent(container, false);
                Undo.RegisterCreatedObjectUndo(created, UndoLabel);
                instance = created.transform;
            }

            instance.SetPositionAndRotation(HutPosition, Quaternion.identity);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);

            int hidden = HideOldFacade(exterior);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[World] Hut entrance placed at {HutPosition}; {hidden} old facade box(es) hidden, their colliders kept.");
        }

        /// <summary>Turns off the renderers of the old EntranceExterior boxes the hut replaces. Returns how many are hidden.</summary>
        public static int HideOldFacade(Transform exterior)
        {
            Transform old = exterior.Find("EntranceExterior");
            if (old == null) return 0;
            int hidden = 0;
            foreach (Transform box in old)
            {
                string part = box.name.StartsWith("EntranceExterior_", StringComparison.Ordinal) ? box.name.Substring("EntranceExterior_".Length) : box.name;
                if (IsKeptFacadePart(part) || !box.TryGetComponent(out MeshRenderer renderer)) continue;
                if (renderer.enabled)
                {
                    Undo.RecordObject(renderer, UndoLabel);
                    renderer.enabled = false;
                }

                hidden++;
            }

            return hidden;
        }

        public static bool IsKeptFacadePart(string part)
        {
            foreach (string kept in KeptFacadeParts)
                if (part.StartsWith(kept, StringComparison.Ordinal)) return true;
            return false;
        }

        // ------------------------------------------------------------------ review pictures

        private readonly struct View
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Target;

            public View(string name, Vector3 position, Vector3 target)
            {
                Name = name;
                Position = position;
                Target = target;
            }
        }

        /// <summary>
        /// Review pictures of the saved Environment scene, never saved: the compound approach, the porch, a three-quarter view, the lobby, the ramp,
        /// the security area and the corridor. Needs graphics, so run it without -nographics:
        /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod Blackout.Editor.HutEntranceBuilder.RenderPreview -previewOut "%TEMP%/hut-preview"
        /// </summary>
        public static void RenderPreview()
        {
            string folder = Argument("-previewOut") ?? Path.Combine(Path.GetTempPath(), "blackout-hut-preview");
            Directory.CreateDirectory(folder);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("The hut preview needs graphics; run without -nographics.");
            Scene scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            const int width = 1280, height = 720;
            var cameraObject = new GameObject("HutPreviewCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 70f;
            camera.aspect = (float)width / height;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 400f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask = ~0;
            data.volumeTrigger = camera.transform;
            // Lights the interior views: the saved scene is powered off by day, so a lamp follows the camera into the rooms.
            var lamp = new GameObject("HutPreviewLamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 14f;
            lamp.intensity = 6f;
            lamp.shadows = LightShadows.None;

            var views = new[]
            {
                new View("approach", new Vector3(0f, 11.65f, 33f), new Vector3(0f, 12.2f, 48.2f)),
                new View("porch", new Vector3(0f, 11.7f, 41.5f), new Vector3(0f, 11.4f, 48.2f)),
                new View("corner", new Vector3(-17f, 14f, 36f), new Vector3(0f, 12.5f, 52f)),
                new View("side", new Vector3(-20f, 12f, 58f), new Vector3(0f, 12.5f, 54f)),
                new View("lobby", new Vector3(0f, 11.65f, 51.6f), new Vector3(0f, 11.4f, 48.4f)),
                new View("lobby_door_side", new Vector3(3.5f, 11.65f, 51.4f), new Vector3(-1.5f, 11.2f, 48.4f)),
                new View("ramp", new Vector3(0f, 11.5f, 53f), new Vector3(0f, 7f, 66f)),
                new View("security", new Vector3(0f, 4.15f, 71f), new Vector3(5f, 4f, 78f)),
                new View("corridor", new Vector3(0f, 4.15f, 85f), new Vector3(0f, 4f, 112f)),
            };
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            try
            {
                foreach (View view in views)
                {
                    camera.transform.position = view.Position;
                    camera.transform.rotation = Quaternion.LookRotation(view.Target - view.Position, Vector3.up);
                    lamp.transform.position = view.Position + camera.transform.forward * 1.5f;
                    lamp.enabled = view.Position.y < 12f && view.Position.z > 50f;
                    camera.backgroundColor = RenderSettings.fogColor;
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    picture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    picture.Apply(false, false);
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(folder, view.Name + ".png"), picture.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(picture);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lamp.gameObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); // closes Environment without saving
            }

            Debug.Log("[World] Hut preview pictures: " + folder);
        }

        private static string Argument(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
