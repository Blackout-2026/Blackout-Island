using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Create Scene Skeleton: makes sure Assets/Scenes/Environment.unity exists and holds the
    /// empty root objects that later World Builder steps fill in, plus one empty child of "Bunker" per room
    /// in the BunkerLayout asset.
    /// Re-runnable: it only creates what is missing and never moves, renames or deletes existing objects.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.WorldSceneSkeleton.CreateSceneSkeleton
    /// </summary>
    public static class WorldSceneSkeleton
    {
        public const string ScenePath = "Assets/Scenes/Environment.unity";
        private const string BunkerRootName = "Bunker";

        // Hierarchy order on a fresh scene.
        private static readonly string[] RootNames =
        {
            "Terrain", "Beach", "Forest", "BunkerExterior", BunkerRootName, "Markers", "Lighting", "Props", "Volumes",
        };

        [MenuItem("Blackout/World/Create Scene Skeleton")]
        public static void CreateSceneSkeleton()
        {
            BunkerLayout layout = BunkerLayoutAssetBuilder.LoadOrCreate();

            Scene scene = OpenOrCreateEnvironmentScene();
            if (!scene.IsValid())
            {
                Debug.Log("[World] Scene skeleton cancelled (open scenes were not saved).");
                return;
            }

            int created = 0;
            foreach (string rootName in RootNames)
            {
                if (FindRoot(scene, rootName) == null)
                {
                    CreateRoot(scene, rootName);
                    created++;
                }
            }

            // Each room child sits at its floor centre so later tools and teammates can find the room by name.
            Transform bunker = FindRoot(scene, BunkerRootName).transform;
            foreach (Room room in layout.rooms)
            {
                if (bunker.Find(room.name) == null)
                {
                    CreateChild(bunker, room.name, room.position);
                    created++;
                }
            }

            if (created > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[World] Scene skeleton ready in {ScenePath}: {created} object(s) created, existing objects left untouched.");
        }

        private static Scene OpenOrCreateEnvironmentScene()
        {
            Scene loaded = SceneManager.GetSceneByPath(ScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                return loaded;
            }

            // Opening a scene replaces the open ones, so give the user the chance to save first.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        internal static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }

            return null;
        }

        private static void CreateRoot(Scene scene, string rootName)
        {
            var go = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(go, scene); // in case another scene is active
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Skeleton");
        }

        private static void CreateChild(Transform parent, string childName, Vector3 worldPosition)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            Undo.RegisterCreatedObjectUndo(go, "Create Scene Skeleton");
        }
    }
}
