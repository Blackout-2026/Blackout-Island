using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One of the three humanoid agents (Security Patrol, Hunter, Commander): its FBX, its visual prefab and its materials.</summary>
    public sealed class CharacterEntry
    {
        public readonly string Name;
        public readonly string AgentPrefab; // the Agent Controller's prefab that gets this model in place of the capsule
        public readonly float MuzzleAhead;  // metres from the Gun socket to the muzzle (docs/reference/core-developer.md 4.4 and 4.5)

        public CharacterEntry(string name, string agentPrefab, float muzzleAhead)
        {
            Name = name;
            AgentPrefab = agentPrefab;
            MuzzleAhead = muzzleAhead;
        }

        public string FbxPath => $"Assets/Models/Custom/SM_Char_{Name}.fbx";
        public string VisualPrefabName => $"Char_{Name}_Visual";
        public string VisualPrefabPath => $"Assets/Prefabs/Models/{VisualPrefabName}.prefab";
    }

    /// <summary>
    /// The skeleton shared by the three humanoid agents (docs/reference/core-developer.md 4.1, docs/art-pipeline.md section 18):
    /// 17 joint objects and the Gun socket, every one at rotation (0, 0, 0). Heights are metres above the floor in the rest pose.
    /// </summary>
    public static class CharacterRig
    {
        public const string Gun = "Gun";
        public const string MuzzlePoint = "MuzzlePoint";
        public const float Height = 1.87f;

        public static readonly CharacterEntry[] Characters =
        {
            new CharacterEntry("Patrol", "Assets/Prefabs/Agents/SecurityPatrol.prefab", 0.74f),
            new CharacterEntry("Hunter", "Assets/Prefabs/Agents/Hunter.prefab", 0.74f),
            new CharacterEntry("Commander", "Assets/Prefabs/Agents/Commander.prefab", 0.22f),
        };

        /// <summary>Joint name and its world height in the rest pose.</summary>
        public static readonly KeyValuePair<string, float>[] JointHeights =
        {
            new KeyValuePair<string, float>("Hips", 0.98f), new KeyValuePair<string, float>("Spine", 1.08f),
            new KeyValuePair<string, float>("Chest", 1.28f), new KeyValuePair<string, float>("Neck", 1.55f),
            new KeyValuePair<string, float>("Head", 1.65f),
            new KeyValuePair<string, float>("UpperArm_L", 1.48f), new KeyValuePair<string, float>("UpperArm_R", 1.48f),
            new KeyValuePair<string, float>("ForeArm_L", 1.19f), new KeyValuePair<string, float>("ForeArm_R", 1.19f),
            new KeyValuePair<string, float>("Hand_L", 0.93f), new KeyValuePair<string, float>("Hand_R", 0.93f),
            new KeyValuePair<string, float>("Thigh_L", 0.92f), new KeyValuePair<string, float>("Thigh_R", 0.92f),
            new KeyValuePair<string, float>("Shin_L", 0.48f), new KeyValuePair<string, float>("Shin_R", 0.48f),
            new KeyValuePair<string, float>("Foot_L", 0.06f), new KeyValuePair<string, float>("Foot_R", 0.06f),
            new KeyValuePair<string, float>(Gun, 1.40f),
        };

        public static CharacterEntry Find(string name)
        {
            foreach (CharacterEntry entry in Characters)
            {
                if (entry.Name == name)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
