using System;
using System.Collections.Generic;
using System.IO;
using Blackout.Environment;
using Blackout.Shared;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds linked visual prefabs from the W10 table. Checks candidates in a temporary preview scene before
    /// touching Environment. Reuses matching instances and never deletes existing scene objects or assets.
    /// Removed table rows stay in the scene for manual review; changed prefab types require manual replacement.
    /// </summary>
    public static class BunkerDresser
    {
        public const string ContainerName = "BunkerDressing";
        private const string UndoLabel = "Dress Bunker";
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [MenuItem("Blackout/World/Dress Bunker")]
        public static void DressBunker()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Dress Bunker works outside Play mode only.");

            // Parse and check room names before opening or changing any scene.
            List<DressingSpec> specs = DressingLayout.Parse(File.ReadAllText(DressingLayout.DocumentPath));
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            ValidateRooms(specs, layout);
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }

            GameObject props = WorldSceneSkeleton.FindRoot(scene, "Props");
            if (props == null) throw new InvalidOperationException("Environment needs its Props root; run Create Scene Skeleton first.");
            Transform container = GeneratedObjects.FindOrCreateChild(props.transform, ContainerName, UndoLabel);
            var markers = new List<MarkerSpec>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Marker marker in root.GetComponentsInChildren<Marker>(true))
                    markers.Add(new MarkerSpec(marker.Type, marker.Id, marker.Owner, marker.transform.position, marker.transform.eulerAngles.y));

            int placed = Dress(container, specs, layout, markers, message => Debug.LogWarning("[World] " + message));
            GameObject lighting = WorldSceneSkeleton.FindRoot(scene, LightingSetup.LightingRootName);
            PowerStateLighting power = lighting != null ? lighting.GetComponent<PowerStateLighting>() : null;
            if (power != null) WireScreens(power, container);
            else Debug.LogWarning("[World] Dress Bunker: no PowerStateLighting; run Setup Blackout Lighting to enable screen fading.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[World] Dress Bunker: {placed}/{specs.Count} placements accepted; {specs.Count - placed} skipped. "
                + "Prefab assets and hand-added objects preserved. Run Bake NavMesh, then Validate Layout.");
        }

        /// <summary>Core build operation, also used by tests. Never saves the supplied scene.</summary>
        public static int Dress(Transform container, IReadOnlyList<DressingSpec> specs, BunkerLayout layout,
            IReadOnlyList<MarkerSpec> markers, Action<string> report)
        {
            ValidateRooms(specs, layout);
            RequireUnitParents(container);
            foreach (Transform room in container) RequireUnitParents(room);
            int placed = 0;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (DressingSpec spec in specs)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                    if (prefab == null) { report($"{spec.Id}: missing {spec.PrefabPath}; skipped."); continue; }
                    Transform room = container.Find(spec.Room);
                    string name = spec.Room + "_" + spec.Id;
                    Transform existing = room != null ? room.Find(name) : null;
                    if (existing != null && PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) != prefab)
                    { report($"{spec.Id}: existing object has a different source; left unchanged. Replace it manually in the Editor."); continue; }
                    GameObject source = existing != null ? existing.gameObject : prefab;
                    if (HasDynamicContent(source))
                    { report($"{spec.Id}: dynamic components or scripts; left unchanged."); continue; }

                    // Cloning an existing instance includes its overrides and hand-added children in the safety check.
                    GameObject candidate;
                    if (existing != null)
                    {
                        candidate = Object.Instantiate(existing.gameObject);
                        SceneManager.MoveGameObjectToScene(candidate, preview);
                    }
                    else candidate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                    Place(candidate.transform, spec, prefab.transform.localScale);
                    string issue = TryBounds(candidate, out Bounds bounds) ?
                        DressingLayout.ClearanceIssue(bounds, markers) ?? RoomIssue(bounds, layout.FindRoom(spec.Room)) :
                        "no supported visible/collider bounds";
                    if (issue != null)
                    { report($"{spec.Id}: {issue}; left unchanged."); Object.DestroyImmediate(candidate); continue; }

                    if (room == null) room = GeneratedObjects.FindOrCreateChild(container, spec.Room, UndoLabel);
                    GameObject instance;
                    if (existing == null)
                    {
                        instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.gameObject.scene);
                        instance.name = name;
                        instance.transform.SetParent(room, false);
                        Undo.RegisterCreatedObjectUndo(instance, UndoLabel);
                    }
                    else
                    {
                        instance = existing.gameObject;
                        Undo.RecordObject(instance.transform, UndoLabel);
                    }
                    Place(instance.transform, spec, prefab.transform.localScale);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    ApplyStaticFlags(instance);
                    Object.DestroyImmediate(candidate);
                    placed++;
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            var expected = new HashSet<string>();
            foreach (DressingSpec spec in specs) expected.Add(spec.Room + "_" + spec.Id);
            foreach (Transform room in container)
                foreach (Transform child in room)
                    if (child.name.StartsWith(room.name + "_", StringComparison.Ordinal) && !expected.Contains(child.name))
                        report(child.name + ": not in current table; preserved for manual review.");
            return placed;
        }

        /// <summary>Include rotated/scaled mesh and collider envelopes, even when inactive or in a preview scene.</summary>
        public static bool TryBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                AddBounds(ref bounds, ref found, renderer.localBounds, renderer.transform.localToWorldMatrix);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger || !collider.enabled) continue;
                Bounds local;
                if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (collider is SphereCollider sphere)
                {
                    Vector3 scale = Abs(collider.transform.lossyScale);
                    float diameter = sphere.radius * 2 * Mathf.Max(scale.x, scale.y, scale.z);
                    local = new Bounds(sphere.center, new Vector3(diameter / scale.x, diameter / scale.y, diameter / scale.z));
                }
                else if (collider is CapsuleCollider capsule)
                {
                    Vector3 scale = Abs(collider.transform.lossyScale);
                    int axis = capsule.direction;
                    float diameter = capsule.radius * 2 * Mathf.Max(scale[(axis + 1) % 3], scale[(axis + 2) % 3]);
                    Vector3 size = new Vector3(diameter / scale.x, diameter / scale.y, diameter / scale.z);
                    size[axis] = Mathf.Max(capsule.height, diameter / scale[axis]);
                    local = new Bounds(capsule.center, size);
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
                else return false; // An unfamiliar collider needs manual review, not an optimistic placement.
                AddBounds(ref bounds, ref found, local, collider.transform.localToWorldMatrix);
            }
            return found && Finite(bounds.center) && Finite(bounds.size);
        }

        public static bool HasDynamicContent(GameObject root)
            => root.GetComponentInChildren<Rigidbody>(true) != null || root.GetComponentInChildren<Animator>(true) != null
            || root.GetComponentInChildren<Animation>(true) != null || root.GetComponentInChildren<MonoBehaviour>(true) != null
            || root.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
            || root.GetComponentInChildren<ParticleSystem>(true) != null || root.GetComponentInChildren<Light>(true) != null;

        private static void Place(Transform transform, DressingSpec spec, Vector3 authoredScale)
        {
            transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0, spec.Yaw, 0));
            transform.localScale = Vector3.Scale(authoredScale, spec.Scale);
        }

        // Avoid parent scale/shear changing an accepted preview's envelope in the saved scene.
        private static void RequireUnitParents(Transform parent)
        {
            if (Vector3.Distance(parent.lossyScale, Vector3.one) > 0.001f || Quaternion.Angle(parent.rotation, Quaternion.identity) > 0.001f)
                throw new InvalidOperationException("Dressing containers need unit world scale and no rotation: " + parent.name);
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

        private static bool Finite(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);

        private static void AddBounds(ref Bounds result, ref bool found, Bounds local, Matrix4x4 matrix)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 world = matrix.MultiplyPoint3x4(corner);
                if (!found) { result = new Bounds(world, Vector3.zero); found = true; }
                else result.Encapsulate(world);
            }
        }

        private static string RoomIssue(Bounds bounds, Room room)
        {
            const float tolerance = 0.02f;
            if (bounds.min.x < room.position.x - room.size.x / 2 - tolerance || bounds.max.x > room.position.x + room.size.x / 2 + tolerance
                || bounds.min.z < room.position.z - room.size.z / 2 - tolerance || bounds.max.z > room.position.z + room.size.z / 2 + tolerance)
                return "outside room footprint";
            float south = BunkerShell.FloorY(room, bounds.min.z);
            float north = BunkerShell.FloorY(room, bounds.max.z);
            if (bounds.min.y < Mathf.Max(south, north) - tolerance || bounds.max.y > Mathf.Min(south, north) + room.size.y + tolerance)
                return "intersects room floor or ceiling";
            return null;
        }

        private static void ValidateRooms(IReadOnlyList<DressingSpec> specs, BunkerLayout layout)
        {
            if (layout == null) throw new InvalidOperationException("BunkerLayout.asset is missing.");
            foreach (DressingSpec spec in specs)
                if (layout.FindRoom(spec.Room) == null) throw new FormatException("Unknown dressing room: " + spec.Room);
        }

        private static bool Emissive(Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
                if (material != null && material.HasProperty(EmissionId) && material.GetColor(EmissionId).maxColorComponent > 0)
                    return true;
            return false;
        }

        private static void ApplyStaticFlags(GameObject instance)
        {
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                Renderer renderer = child.GetComponent<Renderer>();
                StaticEditorFlags flags = StaticFlagRules.Environment;
                if (renderer != null)
                {
                    bool transparent = false;
                    foreach (Material material in renderer.sharedMaterials)
                        transparent |= material != null && material.renderQueue >= 3000;
                    if (Emissive(renderer)) flags = 0; // Runtime screen/LED emission must never enter the lightmap.
                    else if (transparent) flags = StaticFlagRules.SeeThrough;
                }
                Undo.RecordObject(child.gameObject, UndoLabel);
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
                PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
            }
        }

        /// <summary>Append the generated monitor screens, retaining manually assigned renderers and their order.</summary>
        public static void WireScreens(PowerStateLighting power, Transform container)
        {
            Undo.RecordObject(power, UndoLabel);
            var serialized = new SerializedObject(power);
            SerializedProperty screens = serialized.FindProperty("screens");
            var assigned = new HashSet<Object>();
            for (int i = 0; i < screens.arraySize; i++) assigned.Add(screens.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (Transform room in container)
                foreach (Transform instance in room)
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance.gameObject);
                    if (source == null || (source.name != "Prop_Monitor" && source.name != "Prop_WallMonitor")) continue;
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                        if (Emissive(renderer) && assigned.Add(renderer))
                        {
                            int index = screens.arraySize;
                            screens.arraySize++;
                            screens.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
                        }
                }
            serialized.ApplyModifiedProperties();
        }
    }
}
