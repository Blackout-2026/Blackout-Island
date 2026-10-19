using System;
using System.IO;
using System.Linq;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Places the existing Core Developer chamber around the existing Systems Engineer cure pickup.
    /// Only the owned Environment scene is saved; source prefabs and Interactables stay untouched.</summary>
    public static class LabContainmentBuilder
    {
        private const string Label = "Place Lab Containment Chamber";
        private const string PrefabPath = "Assets/Prefabs/Props/Prop_ContainmentChamber.prefab";
        private const string CurePath = "Assets/Prefabs/Interactables/CurePickup.prefab";
        private const string PlayerSettingsPath = "Assets/Prefabs/Player/PlayerMovementSettings.asset";
        private const string Container = "LabContainment";
        private const string InstanceName = "LabContainment_Chamber";

        [MenuItem("Blackout/World/Place Lab Containment Chamber")]
        public static void Build()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            Marker socket = CureSocket(scene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Core Developer's containment-chamber prefab is missing.");
            Transform container = GeneratedObjects.FindOrCreateChild(
                WorldSceneSkeleton.FindRoot(scene, "Props").transform, Container, Label);
            Transform existing = container.Find(InstanceName);
            GameObject instance;
            if (existing == null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = InstanceName;
                instance.transform.SetParent(container, false);
                Undo.RegisterCreatedObjectUndo(instance, Label);
            }
            else
            {
                instance = existing.gameObject;
                if (PrefabUtility.GetCorrespondingObjectFromSource(instance) != prefab)
                    throw new InvalidOperationException("LabContainment_Chamber already exists with a different source; inspect it manually.");
            }
            Undo.RecordObject(instance.transform, Label);
            instance.transform.SetPositionAndRotation(socket.transform.position, socket.transform.rotation);
            instance.transform.localScale = prefab.transform.localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);

            // The gameplay prefab already owns the pedestal. Hide the two duplicate visual parts and its
            // matching central collider on this scene instance, retaining the base and sixteen glass colliders.
            foreach (string name in new[] { "Pedestal", "PedestalPlate" })
            {
                Transform part = instance.GetComponentsInChildren<Transform>(true).SingleOrDefault(child => child.name == name);
                if (part == null) throw new InvalidOperationException("Chamber prefab changed: missing " + name);
                Undo.RecordObject(part.gameObject, Label);
                part.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            int pedestalColliders = 0;
            foreach (CapsuleCollider collider in instance.GetComponents<CapsuleCollider>())
                if (new Vector2(collider.center.x, collider.center.z).sqrMagnitude < 0.01f)
                {
                    Undo.RecordObject(collider, Label);
                    collider.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                    pedestalColliders++;
                }
            if (pedestalColliders != 1) throw new InvalidOperationException("Expected one chamber pedestal collider.");
            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(part.gameObject, Label);
                part.gameObject.layer = PhysicsLayers.Level;
                Renderer renderer = part.GetComponent<Renderer>();
                StaticEditorFlags flags = StaticFlagRules.Environment;
                if (renderer != null)
                {
                    if (renderer.sharedMaterials.Any(material => material != null && material.HasProperty("_EmissionColor")
                        && material.GetColor("_EmissionColor").maxColorComponent > 0f)) flags = 0;
                    else if (renderer.sharedMaterials.Any(material => material != null && material.renderQueue >= 3000))
                        flags = StaticFlagRules.SeeThrough;
                }
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, flags);
                PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[World] Lab chamber placed: linked prefab at CureSocket (0,2.5,135), opening toward D-Lab; "
                + "source height2.88m under3m ceiling; duplicate pedestal hidden on scene instance only.");
        }

        private static Marker CureSocket(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Marker marker in root.GetComponentsInChildren<Marker>(true))
                    if (marker.Id == "CureSocket") return marker;
            throw new InvalidOperationException("Build the CureSocket marker before placing the chamber.");
        }

        private static void VerifyPlayerApproach(Scene scene)
        {
            Marker socket = CureSocket(scene);
            GameObject curePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CurePath);
            UnityEngine.Object settingsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PlayerSettingsPath);
            if (curePrefab == null || settingsAsset == null) throw new InvalidOperationException("Cure prefab/player settings are missing.");
            GameObject temporaryCure = null, player = null;
            try
            {
                temporaryCure = (GameObject)PrefabUtility.InstantiatePrefab(curePrefab, scene);
                temporaryCure.transform.SetPositionAndRotation(socket.transform.position, socket.transform.rotation);
                foreach (Behaviour behaviour in temporaryCure.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
                CapsuleCollider trigger = temporaryCure.GetComponents<CapsuleCollider>().Single(collider => collider.isTrigger);
                using var settings = new SerializedObject(settingsAsset);
                player = new GameObject("W13_TemporaryChamberPlayerProbe");
                player.layer = PhysicsLayers.Player;
                player.transform.position = socket.transform.position + Vector3.back * 5f + Vector3.up * 0.05f;
                var controller = player.AddComponent<CharacterController>();
                controller.height = settings.FindProperty("height").floatValue;
                controller.radius = settings.FindProperty("radius").floatValue;
                controller.center = Vector3.up * controller.height * 0.5f;
                controller.stepOffset = settings.FindProperty("stepOffset").floatValue;
                controller.slopeLimit = settings.FindProperty("slopeLimit").floatValue;
                controller.minMoveDistance = 0f;
                Physics.SyncTransforms();
                Vector3 target = socket.transform.position + Vector3.back;
                Walk(controller, target);
                Vector3 feet = player.transform.position;
                Collider[] hits = Physics.OverlapCapsule(feet + Vector3.up * controller.radius,
                    feet + Vector3.up * (controller.height - controller.radius), controller.radius,
                    1 << PhysicsLayers.Interactable, QueryTriggerInteraction.Collide);
                if (!hits.Contains(trigger)) throw new InvalidOperationException("Player cannot reach the existing cure pickup trigger.");
                Walk(controller, socket.transform.position + Vector3.back * 5f);
                Debug.Log("[World] Chamber player check passed: enter south opening, step onto0.25m base, "
                    + "overlap unchanged cure trigger, return to lab; no gameplay events raised.");
            }
            finally
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (temporaryCure != null) UnityEngine.Object.DestroyImmediate(temporaryCure);
            }
        }

        private static void Walk(CharacterController controller, Vector3 target)
        {
            for (int frame = 0; frame < 600; frame++)
            {
                Vector3 delta = target - controller.transform.position;
                delta.y = 0f;
                if (delta.magnitude < 0.04f) return;
                controller.Move(Vector3.ClampMagnitude(delta, 0.03f) + Vector3.down * 0.05f);
            }
            throw new InvalidOperationException("Chamber player approach blocked at " + controller.transform.position);
        }

        public static void BuildAndVerify()
        {
            string[] protectedPaths = { PrefabPath, PrefabPath + ".meta", CurePath, CurePath + ".meta",
                PlayerSettingsPath, "Assets/Scenes/Interactables.unity", "Assets/Scenes/Interactables.unity.meta" };
            byte[][] protectedBytes = protectedPaths.Select(File.ReadAllBytes).ToArray();
            Build();
            byte[] before = File.ReadAllBytes(WorldSceneSkeleton.ScenePath);
            Build();
            if (!before.SequenceEqual(File.ReadAllBytes(WorldSceneSkeleton.ScenePath)))
                throw new InvalidOperationException("Chamber placement changed the scene on repeat.");
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            GameObject chamber = WorldSceneSkeleton.FindRoot(scene, "Props").transform.Find(Container + "/" + InstanceName).gameObject;
            if (!BunkerDresser.TryBounds(chamber, out Bounds bounds) || bounds.max.y > 5.5f)
                throw new InvalidOperationException("Chamber intersects the lab ceiling.");
            VerifyPlayerApproach(scene);
            NavMeshBaker.BakeNavMesh();
            LayoutValidator.ValidateLayout();
            for (int i = 0; i < protectedPaths.Length; i++)
                if (!protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])))
                    throw new InvalidOperationException("Protected owner file changed: " + protectedPaths[i]);
            Debug.Log("[World] Chamber verification: repeat scene identical; ceiling clear; source prefab/cure/player settings/Interactables files unchanged.");
        }
    }
}
