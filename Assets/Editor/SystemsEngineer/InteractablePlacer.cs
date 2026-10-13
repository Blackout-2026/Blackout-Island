using System.Collections.Generic;
using Blackout.Interaction;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Places the interactable prefabs at the World Builder's socket markers, into the Systems Engineer's own scene
    /// Assets/Scenes/Interactables.unity. Environment.unity is opened additively only to read the markers, and is
    /// closed again without saving. Re-runnable: an instance that already exists (same name) is never moved or
    /// replaced, so hand adjustments survive; delete one to place it again. Empty event references on existing
    /// doors and the listener are filled in (never overwritten).
    /// Also adds a GeneratorDisabled listener that logs to the Console, which proves the event chain.
    /// </summary>
    public static class InteractablePlacer
    {
        public const string ScenePath = "Assets/Scenes/Interactables.unity";

        private const string EnvironmentPath = "Assets/Scenes/Environment.unity";
        private const string RootName = "Interactables";
        private const string ListenerName = "Debug_GeneratorDisabledListener";

        // The barricade crate has no marker: docs/level-layout.md reserves a 1.5 m cube in the corridor here.
        private static readonly Vector3 CratePosition = new Vector3(0.7f, 2.5f, 95f);

        /// <summary>Per-door settings from the prototype (docs/reference/systems-engineer.md section 10).</summary>
        private struct DoorRules
        {
            public string Label;
            public float Speed;
            public float Noise;
            public bool Maglock;
        }

        private static readonly Dictionary<string, DoorRules> Doors = new Dictionary<string, DoorRules>
        {
            { "D-Bunker", new DoorRules { Label = "bunker door", Speed = 0.45f, Noise = 22f } },
            { "D-Security", new DoorRules { Label = "security door", Speed = 1.1f, Noise = 14f } },
            { "D-Generator", new DoorRules { Label = "generator room door", Speed = 1.1f, Noise = 14f } },
            { "D-Lab", new DoorRules { Label = "laboratory door", Speed = 1.1f, Noise = 14f, Maglock = true } },
        };

        [MenuItem("Blackout/Systems/Place Interactables")]
        public static void Place()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(InteractablePrefabBuilder.DoorPath) == null)
            {
                Debug.LogError("[Blackout] Missing interactable prefabs; run Build Interactable Prefabs first.");
                return;
            }

            Scene scene = OpenOrCreateInteractablesScene();
            Scene environment = EditorSceneManager.OpenScene(EnvironmentPath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                // Loaded only now: opening a scene in Single mode unloads unused assets, which would turn an asset
                // loaded earlier into a dead reference that saves as empty.
                var generatorDisabled = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(
                    EventChannelAssetBuilder.Folder + "/GeneratorDisabled.asset");
                if (generatorDisabled == null)
                {
                    Debug.LogError("[Blackout] Missing GeneratorDisabled.asset; run Create Event Channels first.");
                    return;
                }

                Transform root = FindOrCreateRoot(scene);
                int placed = 0;
                foreach (Marker marker in FindMarkers(environment))
                {
                    placed += PlaceAtMarker(marker, root, generatorDisabled);
                }

                placed += PlaceIfMissing(InteractablePrefabBuilder.CratePath, "Crate", root, CratePosition,
                    Quaternion.identity) != null ? 1 : 0;
                placed += AddGeneratorListener(root, generatorDisabled);

                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Blackout] Place Interactables: placed {placed}, existing ones left where they are. " +
                          $"Saved {ScenePath}.");
            }
            finally
            {
                // Read-only: the World Builder's scene is closed without saving.
                EditorSceneManager.CloseScene(environment, true);
            }
        }

        private static Scene OpenOrCreateInteractablesScene()
        {
            if (AssetDatabase.LoadMainAssetAtPath(ScenePath) != null)
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Blackout] Created {ScenePath}.");
            return scene;
        }

        private static Transform FindOrCreateRoot(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                {
                    return go.transform;
                }
            }

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.transform;
        }

        private static List<Marker> FindMarkers(Scene environment)
        {
            var markers = new List<Marker>();
            foreach (GameObject go in environment.GetRootGameObjects())
            {
                markers.AddRange(go.GetComponentsInChildren<Marker>(true));
            }

            // Sorted so the scene's object order is the same on every machine.
            markers.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return markers;
        }

        private static int PlaceAtMarker(Marker marker, Transform root, VoidEventChannel generatorDisabled)
        {
            Transform socket = marker.transform;
            switch (marker.Type)
            {
                case MarkerType.DoorSocket:
                {
                    string door = Suffix(marker.Id);
                    GameObject instance = PlaceIfMissing(InteractablePrefabBuilder.DoorPath, "Door_" + door, root,
                        socket.position, socket.rotation);
                    if (instance != null)
                    {
                        ApplyDoorRules(instance.GetComponent<SlidingDoor>(), door, generatorDisabled);
                        return 1;
                    }

                    FillMaglockIfEmpty(root.Find("Door_" + door), door, generatorDisabled);
                    return 0;
                }
                case MarkerType.GeneratorSocket:
                    return Count(PlaceIfMissing(InteractablePrefabBuilder.GeneratorPath, "Generator", root,
                        socket.position, socket.rotation));
                case MarkerType.CureSocket:
                    return Count(PlaceIfMissing(InteractablePrefabBuilder.CurePath, "CurePickup", root,
                        socket.position, socket.rotation));
                case MarkerType.HealthPickup:
                    return Count(PlaceIfMissing(InteractablePrefabBuilder.HealthPath, marker.Id, root,
                        socket.position, socket.rotation));
                default:
                    return 0;
            }
        }

        /// <summary>Instantiates a prefab under the root unless a child with this name exists. Returns the new one.</summary>
        private static GameObject PlaceIfMissing(string prefabPath, string name, Transform root, Vector3 position,
            Quaternion rotation)
        {
            if (root.Find(name) != null)
            {
                return null; // never move or replace an existing one
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.gameObject.scene);
            instance.name = name;
            instance.transform.SetParent(root, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>Per-socket overrides on the door instance: label, speed, noise and the lab's maglock.</summary>
        private static void ApplyDoorRules(SlidingDoor door, string doorId, VoidEventChannel generatorDisabled)
        {
            if (door == null || !Doors.TryGetValue(doorId, out DoorRules rules))
            {
                return;
            }

            var serialized = new SerializedObject(door);
            serialized.FindProperty("label").stringValue = rules.Label;
            serialized.FindProperty("speed").floatValue = rules.Speed;
            serialized.FindProperty("noiseRadius").floatValue = rules.Noise;
            serialized.FindProperty("startLocked").boolValue = rules.Maglock;
            serialized.FindProperty("unlockOn").objectReferenceValue = rules.Maglock ? generatorDisabled : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A GameEventListener on GeneratorDisabled that logs to the Console: the step's proof.</summary>
        private static int AddGeneratorListener(Transform root, VoidEventChannel generatorDisabled)
        {
            Transform existing = root.Find(ListenerName);
            if (existing != null)
            {
                FillIfEmpty(existing.GetComponent<GameEventListener>(), "channel", generatorDisabled);
                return 0;
            }

            var go = new GameObject(ListenerName);
            go.transform.SetParent(root, false);
            var logger = go.AddComponent<DebugEventLogger>();
            var listener = go.AddComponent<GameEventListener>();
            var serialized = new SerializedObject(listener);
            serialized.FindProperty("channel").objectReferenceValue = generatorDisabled;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddStringPersistentListener(listener.Response, logger.Log,
                "GeneratorDisabled received: the generator is off.");
            return 1;
        }

        /// <summary>Gives an existing maglocked door its unlock event if the field is empty; nothing else changes.</summary>
        private static void FillMaglockIfEmpty(Transform door, string doorId, VoidEventChannel generatorDisabled)
        {
            if (door != null && Doors.TryGetValue(doorId, out DoorRules rules) && rules.Maglock)
            {
                FillIfEmpty(door.GetComponent<SlidingDoor>(), "unlockOn", generatorDisabled);
            }
        }

        /// <summary>Sets an object reference only if it is empty, so hand-made changes are never overwritten.</summary>
        private static void FillIfEmpty(Object target, string field, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[Blackout] Filled the empty '{field}' on {target.name}.");
            }
        }

        private static string Suffix(string id)
        {
            int underscore = id.IndexOf('_');
            return underscore >= 0 ? id.Substring(underscore + 1) : id;
        }

        private static int Count(GameObject placed)
        {
            return placed != null ? 1 : 0;
        }
    }
}
