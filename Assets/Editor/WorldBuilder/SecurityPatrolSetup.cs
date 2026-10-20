// Owner: World Builder. Agent Controller approved edits to the SecurityPatrol gameplay prefab for S3.
using System;
using System.IO;
using System.Security.Cryptography;
using Blackout.Agents;
using Blackout.AI.SecurityPatrol;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>Wire S3 onto the existing variant. No randomness, asset deletion or scene saves.</summary>
    public static class SecurityPatrolSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Agents/SecurityPatrol.prefab";
        public const string ConfigFolder = "Assets/Scripts/AI/SecurityPatrol/Config";
        public const string ConfigPath = ConfigFolder + "/SecurityPatrolConfig.asset";
        private const string NoisePath = "Assets/ScriptableObjects/Events/Noise.asset";
        private const string RespawnPath = "Assets/ScriptableObjects/Events/PlayerRespawned.asset";

        [MenuItem("Blackout/World/Setup Security Patrol")]
        public static void Build()
        {
            Require<GameObject>(PrefabPath);
            NoiseEventChannel noise = Require<NoiseEventChannel>(NoisePath);
            VoidEventChannel respawn = Require<VoidEventChannel>(RespawnPath);
            SecurityPatrolConfig config = AssetDatabase.LoadAssetAtPath<SecurityPatrolConfig>(ConfigPath);
            if (config == null)
            {
                if (File.Exists(ConfigPath)) throw new InvalidOperationException("A different asset already occupies " + ConfigPath);
                if (!AssetDatabase.IsValidFolder(ConfigFolder))
                    AssetDatabase.CreateFolder("Assets/Scripts/AI/SecurityPatrol", "Config");
                config = ScriptableObject.CreateInstance<SecurityPatrolConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AgentRoot root = instance.GetComponent<AgentRoot>();
                PathFollower mover = instance.GetComponent<PathFollower>();
                AgentShooter shooter = instance.GetComponent<AgentShooter>();
                Health health = instance.GetComponent<Health>();
                if (root == null || mover == null || shooter == null || health == null)
                    throw new InvalidOperationException("SecurityPatrol needs the AgentBase executor components.");
                foreach (MonoBehaviour component in instance.GetComponents<MonoBehaviour>())
                    if (component is IAgentBrain && !(component is SecurityPatrolBrain))
                        throw new InvalidOperationException("Another brain is already on SecurityPatrol; leave it for its owner to review.");
                if (instance.GetComponents<SecurityPatrolBrain>().Length > 1)
                    throw new InvalidOperationException("SecurityPatrol has duplicate brains; this tool never deletes components.");

                var brain = instance.GetComponent<SecurityPatrolBrain>() ?? instance.AddComponent<SecurityPatrolBrain>();
                var brainFields = new SerializedObject(brain);
                brainFields.FindProperty("config").objectReferenceValue = config;
                brainFields.FindProperty("noiseChannel").objectReferenceValue = noise;
                brainFields.FindProperty("playerRespawned").objectReferenceValue = respawn;
                brainFields.FindProperty("geometryMask").intValue =
                    (1 << PhysicsLayers.Default) | (1 << PhysicsLayers.Level) | (1 << PhysicsLayers.Interactable);
                brainFields.ApplyModifiedPropertiesWithoutUndo();

                root.SetKind(AgentKind.SecurityPatrol);
                var rootFields = new SerializedObject(root);
                rootFields.FindProperty("brainBehaviour").objectReferenceValue = brain;
                rootFields.ApplyModifiedPropertiesWithoutUndo();

                // Baseline prefab values work when placed manually. The spawner still applies DifficultyConfig.
                AgentStats stats = DifficultyConfig.DefaultStats()[(int)AgentKind.SecurityPatrol];
                stats.walkSpeed = config.walkSpeed;
                stats.runSpeed = config.runSpeed;
                stats.reactionDelay = config.reactionDelay;
                stats.spread = config.aimError;
                AgentStatsApplier.Apply(instance, stats);
                var shooterFields = new SerializedObject(shooter);
                shooterFields.FindProperty("gunfireChannel").objectReferenceValue = noise;
                shooterFields.FindProperty("eyeHeight").floatValue = config.eyeHeight;
                shooterFields.ApplyModifiedPropertiesWithoutUndo();
                Record(root);
                Record(mover);
                Record(shooter);
                Record(health);
                if (PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath) == null)
                    throw new InvalidOperationException("Could not save " + PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }

            AssetDatabase.SaveAssetIfDirty(config);
        }

        /// <summary>Batch entry: build twice, verify the saved asset, and prove protected files stayed identical.</summary>
        public static void BuildAndVerify()
        {
            string[] protectedPaths =
            {
                PrefabPath + ".meta", "Assets/Prefabs/Agents/AgentBase.prefab",
                "Assets/Prefabs/Agents/Hunter.prefab", "Assets/Prefabs/Agents/TacticalGuard.prefab",
                "Assets/Prefabs/Agents/Commander.prefab", "Assets/Prefabs/Agents/Materials/PH_SecurityPatrol.mat",
                "Assets/Scenes/Environment.unity", "Assets/Scenes/Agents.unity"
            };
            var before = new string[protectedPaths.Length];
            for (int i = 0; i < before.Length; i++) before[i] = Hash(protectedPaths[i]);
            Build();
            Verify();
            string prefabHash = Hash(PrefabPath);
            string configHash = Hash(ConfigPath);
            string configMetaHash = Hash(ConfigPath + ".meta");
            Build();
            Verify();
            if (prefabHash != Hash(PrefabPath) || configHash != Hash(ConfigPath) || configMetaHash != Hash(ConfigPath + ".meta"))
                throw new InvalidOperationException("Security Patrol setup changed on its second run.");
            for (int i = 0; i < before.Length; i++)
                if (before[i] != Hash(protectedPaths[i]))
                    throw new InvalidOperationException("Protected file changed: " + protectedPaths[i]);

            Debug.Log("[SecurityPatrol] Setup verified: one FSM brain, config/channels bound, Kind SecurityPatrol, matching executor tuning.");
            Debug.Log("[SecurityPatrol] Second run identical; eight protected files unchanged, including prefab GUID and both scenes.");
        }

        private static void Verify()
        {
            GameObject prefab = Require<GameObject>(PrefabPath);
            var config = Require<SecurityPatrolConfig>(ConfigPath);
            var brain = prefab.GetComponent<SecurityPatrolBrain>();
            var root = prefab.GetComponent<AgentRoot>();
            if (brain == null || prefab.GetComponents<SecurityPatrolBrain>().Length != 1 || brain.Config != config ||
                root.Kind != AgentKind.SecurityPatrol || new SerializedObject(root).FindProperty("brainBehaviour").objectReferenceValue != brain)
                throw new InvalidOperationException("The saved Security Patrol brain/root/config binding is incomplete.");
            var brainFields = new SerializedObject(brain);
            var shooter = prefab.GetComponent<AgentShooter>();
            var mover = prefab.GetComponent<PathFollower>();
            if (brainFields.FindProperty("noiseChannel").objectReferenceValue != Require<NoiseEventChannel>(NoisePath) ||
                brainFields.FindProperty("playerRespawned").objectReferenceValue != Require<VoidEventChannel>(RespawnPath) ||
                new SerializedObject(shooter).FindProperty("gunfireChannel").objectReferenceValue != Require<NoiseEventChannel>(NoisePath) ||
                !Mathf.Approximately(mover.Settings.walkSpeed, config.walkSpeed) ||
                !Mathf.Approximately(mover.Settings.runSpeed, config.runSpeed) ||
                !Mathf.Approximately(shooter.Config.reactionDelay, config.reactionDelay) ||
                !Mathf.Approximately(shooter.Config.spread, config.aimError))
                throw new InvalidOperationException("The saved Security Patrol channels or executor tuning do not match.");
        }

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing required asset: " + path);

        private static void Record(Component component)
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        private static string Hash(string path)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
