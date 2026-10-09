// Owner: Agent Controller.
using System.IO;
using Blackout.Agents;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates Assets/Prefabs/Agents/AgentBase.prefab: the capsule, Health and AgentRoot every agent shares, with an
    /// empty Visual child where each agent type nests its model (Core Developer's prefabs or a free humanoid).
    /// Re-runnable: if the prefab already exists it is left alone, so hand edits are never overwritten.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.AgentBasePrefabBuilder.Build
    /// </summary>
    public static class AgentBasePrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Agents/AgentBase.prefab";
        public const string VisualChildName = "Visual";
        public const string EnemyLayerName = "Enemy";

        // Player-sized, and matching the NavMesh agent in docs/level-layout.md: radius 0.4 m, height 1.8 m.
        public const float CapsuleHeight = 1.8f;
        public const float CapsuleRadius = 0.38f;

        [MenuItem("Blackout/Agents/Create Agent Base Prefab")]
        public static void Build()
        {
            if (File.Exists(PrefabPath))
            {
                Debug.Log($"AgentBasePrefabBuilder: {PrefabPath} already exists, leaving it unchanged.");
                return;
            }

            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            if (enemyLayer < 0)
            {
                Debug.LogError($"AgentBasePrefabBuilder: layer '{EnemyLayerName}' is missing. Pull develop (Systems Engineer's P0 setup defines it).");
                return;
            }

            var root = new GameObject("AgentBase");
            try
            {
                root.layer = enemyLayer;
                root.tag = EnemyLayerName; // the Enemy tag exists as well (P0)

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 1; // Y axis
                capsule.height = CapsuleHeight;
                capsule.radius = CapsuleRadius;
                capsule.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);

                root.AddComponent<Health>();
                root.AddComponent<AgentRoot>();

                var visual = new GameObject(VisualChildName);
                visual.layer = enemyLayer;
                visual.transform.SetParent(root.transform, false);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"AgentBasePrefabBuilder: created {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
