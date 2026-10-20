using System;
using System.IO;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Replaces only the generated boat geometry, preserving its transform, lantern and exit marker.
    /// The old parts are disabled, not removed. Collision uses boxes, not the detailed net/oar meshes.</summary>
    public static class RowboatPlacement
    {
        public const string VisualName = "RowboatVisual";
        public const float VisualHeight = -0.28f; // Keel just below sea level; floorboard tops at world y0.1675.
        private const string Label = "Replace Boat With Rowboat";

        [MenuItem("Blackout/World/Replace Boat With Rowboat")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Replace the boat outside Play mode.");
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            Transform boat = WorldSceneSkeleton.FindRoot(scene, "Beach")?.transform.Find("Boat");
            if (boat == null) throw new InvalidOperationException("Build the beach before placing the rowboat.");
            Apply(boat);
            // Reattach the existing dock moorings to this hull and retain the lantern/flicker link.
            BeachDressingBuilder.Build();
            Debug.Log("[World] Rowboat placed at the existing boat transform; old generated hull/cabin/fittings disabled. "
                + "Floor y0.1675m, two-part boarding plank, existing lantern and BoatExit retained. NavMesh unchanged (boat excluded).");
        }

        internal static bool IsPlaced(Transform boat) => boat.Find(VisualName) != null;

        internal static Vector3 MooringPoint(float z)
        {
            float t = (4.2f - z) / 8.8f;
            return new Vector3(-Width(t), VisualHeight + 1.15f + Sheer(t), z);
        }

        // Also called by the terrain and beach builders so they preserve an installed rowboat.
        internal static void Apply(Transform boat)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(RowboatAssets.PrefabPath);
            if (source == null) throw new InvalidOperationException("Build Rowboat Visual Assets first.");
            Transform visual = boat.Find(VisualName);
            if (visual == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, boat.gameObject.scene);
                visual = instance.transform;
                visual.SetParent(boat, false);
                visual.name = VisualName;
                Undo.RegisterCreatedObjectUndo(instance, Label);
            }
            else if (PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject) != source)
                throw new InvalidOperationException("A different object occupies RowboatVisual; inspect it before replacing.");
            Undo.RecordObject(visual, Label);
            visual.SetLocalPositionAndRotation(Vector3.up * VisualHeight, Quaternion.identity);
            visual.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            foreach (Transform part in visual.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(part.gameObject, Label);
                part.gameObject.layer = PhysicsLayers.Level;
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }

            // Exact names from BeachGreyboxBuilder: no artist/user-added objects are hidden.
            foreach (string name in new[] { "Hull", "Bow", "Side_West", "Side_East", "Transom", "Cabin", "CabinRoof",
                "Windshield", "Motor", "MotorShaft", "Mast", "Radar", "Lantern", "Fender_0", "Fender_1", "Fender_2" })
                DisableGeneratedPart(boat.Find("Boat_" + name));
            Transform oldDress = boat.Find("BoatDressing");
            if (oldDress != null)
            {
                DisableGeneratedPart(oldDress.Find("BoatDressing_Deck"));
                for (int side = -1; side <= 1; side += 2)
                {
                    foreach (string name in new[] { "SideWindow", "Gunwale", "RailTop" })
                        DisableGeneratedPart(oldDress.Find("BoatDressing_" + name + side));
                    for (int i = 0; i < 6; i++) DisableGeneratedPart(oldDress.Find($"BoatDressing_RailPost{side}_{i}"));
                }
                for (int i = 0; i < 3; i++) DisableGeneratedPart(oldDress.Find("BoatDressing_RopeCoil" + i));
                for (int i = 0; i < 2; i++)
                {
                    DisableGeneratedPart(oldDress.Find("BoatDressing_PH_JerryCan" + i));
                    DisableGeneratedPart(oldDress.Find("BoatDressing_JerryHandle" + i));
                }
            }
            // Keep the existing lantern cage/emission/light. Extend its post down to the rowboat side.
            Transform post = boat.Find("Boat_LanternPost");
            if (post != null)
            {
                Undo.RecordObject(post, Label);
                post.localPosition = new Vector3(-1.2f, 1.58f, -1.7f);
                post.localScale = new Vector3(0.06f, 0.72f, 0.06f);
            }
            BuildCollisionAndBoarding(boat);
        }

        private static void DisableGeneratedPart(Transform part)
        {
            if (part == null) return;
            if (part.TryGetComponent(out Renderer renderer))
            {
                Undo.RecordObject(renderer, Label);
                renderer.enabled = false;
            }
            if (part.TryGetComponent(out Collider collider))
            {
                Undo.RecordObject(collider, Label);
                collider.enabled = false;
            }
        }

        private static void BuildCollisionAndBoarding(Transform boat)
        {
            Transform collision = GeneratedObjects.FindOrCreateChild(boat, "RowboatCollision", Label);
            Undo.RecordObject(collision, Label);
            collision.SetLocalPositionAndRotation(Vector3.up * VisualHeight, Quaternion.identity);
            collision.localScale = Vector3.one;
            // These dimensions are the Blender floorboards, rather than a floating full-width deck.
            for (int i = -4; i <= 4; i++)
            {
                float x = i * 0.14f;
                float minimumZ = -3.65f + Mathf.Abs(x) * 2.8f;
                Box(collision, "Floor" + (i + 4), new Vector3(x, 0.265f, (minimumZ + 3.75f) * 0.5f),
                    new Vector3(0.14f, 0.065f, 3.75f - minimumZ));
            }
            float[] benches = { 0.12f, 0.49f, 0.77f };
            for (int i = 0; i < benches.Length; i++)
            {
                float t = benches[i];
                Box(collision, "Bench" + i, new Vector3(0f, 0.91f + Sheer(t), 4.2f - 8.8f * t),
                    new Vector3(Width(t) * 1.78f, 0.085f, 0.48f));
            }
            // Short side segments follow the narrowing gunwales; ropes and fenders have no collision.
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f, b = (i + 1) / 6f;
                    Vector3 start = new Vector3(side * Width(a), 0.72f + Sheer(a), 4.2f - 8.8f * a);
                    Vector3 end = new Vector3(side * Width(b), 0.72f + Sheer(b), 4.2f - 8.8f * b);
                    GameObject wall = Box(collision, $"Side{side}_{i}", (start + end) * 0.5f,
                        new Vector3(0.08f, 0.86f, Vector3.Distance(start, end)));
                    wall.transform.localRotation = Quaternion.LookRotation(end - start, Vector3.up);
                }
            Box(collision, "Transom", new Vector3(0f, 0.75f, 4.2f), new Vector3(Width(0f) * 2f, 0.86f, 0.08f));

            Transform boarding = GeneratedObjects.FindOrCreateChild(boat, "RowboatBoarding", Label);
            Material wood = AssetDatabase.LoadAssetAtPath<Material>(GreyboxMaterials.Folder + "/Greybox_DockWood.mat");
            if (wood == null) throw new InvalidOperationException("Missing dock wood material.");
            Vector3 upper = boat.TransformPoint(new Vector3(-1.52f, 1f, 1.9f));
            Vector3 dock = new Vector3(1.2f, IslandLayout.DockDeckY + 0.01f, upper.z);
            Vector3 floor = boat.TransformPoint(new Vector3(0.45f, 0.265f + 0.0325f + VisualHeight + 0.01f, 1.9f));
            Plank(boarding, "DockToGunwale", dock, upper, wood);
            Plank(boarding, "GunwaleToFloor", upper, floor, wood);
        }

        // Same measured hull profile used in the Blender model (metres, bow at local -Z).
        private static float Width(float t) => 1.45f * (0.82f + 0.18f * Mathf.Sin(Mathf.PI * Mathf.Min(t / 0.72f, 1f)))
            * (1f - 0.974f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f, 1f, t)));
        private static float Sheer(float t) => 0.065f * Mathf.Pow(1f - t, 4f)
            + 0.28f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            Transform child = GeneratedObjects.FindOrCreateChild(parent, name, Label);
            Undo.RecordObject(child, Label);
            child.SetLocalPositionAndRotation(centre, Quaternion.identity);
            child.localScale = size;
            child.gameObject.layer = PhysicsLayers.Level;
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            BoxCollider collider = GeneratedObjects.GetOrAddComponent<BoxCollider>(child.gameObject);
            Undo.RecordObject(collider, Label);
            collider.center = Vector3.zero;
            collider.size = Vector3.one;
            collider.enabled = true;
            return child.gameObject;
        }

        private static void Plank(Transform parent, string name, Vector3 start, Vector3 end, Material material)
        {
            GameObject part = Box(parent, name, Vector3.zero, Vector3.one);
            part.transform.position = (start + end) * 0.5f - Vector3.up * 0.035f;
            part.transform.rotation = Quaternion.LookRotation(end - start, Vector3.up);
            part.transform.localScale = new Vector3(0.9f, 0.07f, Vector3.Distance(start, end) + 0.05f);
            GeneratedObjects.GetOrAddComponent<MeshFilter>(part).sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            GeneratedObjects.GetOrAddComponent<MeshRenderer>(part).sharedMaterial = material;
        }

        // Batch entry point with graphics enabled; no gameplay events or Play-mode scripts are executed.
        public static void BuildAndCapture()
        {
            Build();
            CapturePreview();
        }

        [MenuItem("Blackout/World/Capture Rowboat Preview")]
        public static void CapturePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture the rowboat outside Play mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Rowboat capture needs graphics; omit -nographics.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before capturing.");
            string folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BlackoutIsland-Rowboat",
                DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (folder.StartsWith(repo, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("TEMP must be outside the repository.");
            Directory.CreateDirectory(folder);
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            RenderTexture oldTarget = RenderTexture.active;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
                Camera camera = new GameObject("Rowboat_TemporaryCaptureCamera").AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = 60f;
                camera.aspect = 16f / 9f;
                camera.nearClipPlane = 0.08f;
                camera.farClipPlane = 700f;
                camera.allowHDR = true;
                camera.clearFlags = CameraClearFlags.Skybox;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.volumeLayerMask = ~0;
                data.volumeTrigger = camera.transform;
                target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false, false);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Vector3[] eyes = { new Vector3(10f, 6f, -130f), new Vector3(0.3f, 2.93f, -120.8f) };
                for (int i = 0; i < eyes.Length; i++)
                {
                    camera.transform.position = eyes[i];
                    camera.transform.LookAt(new Vector3(3.7f, 0.7f, -122.5f));
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
                    pixels.Apply(false, false);
                    string path = Path.Combine(folder, i == 0 ? "Boat_Overview.png" : "Boat_FromDock.png");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    Debug.Log("[World] Rowboat screenshot: " + path);
                }
            }
            finally
            {
                RenderTexture.active = oldTarget;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (previous.Length > 0 && !string.IsNullOrEmpty(previous[0].path)) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }
    }
}
