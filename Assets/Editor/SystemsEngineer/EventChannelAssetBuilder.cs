using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the shared event channel assets in Assets/ScriptableObjects/Events (playbook section 3).
    /// Re-runnable: an asset that already exists is left untouched, so its GUID never changes and every
    /// reference to it keeps working. Nothing is ever deleted.
    /// </summary>
    public static class EventChannelAssetBuilder
    {
        public const string Folder = "Assets/ScriptableObjects/Events";

        private static readonly string[] VoidChannels =
        {
            "GeneratorDisabled",
            "CureTaken",
            "PlayerDied",
            "PlayerRespawned",
            "MissionComplete"
        };

        private const string NoiseChannel = "Noise";
        private const string ObjectiveChannel = "ObjectiveChanged";

        [MenuItem("Blackout/Systems/Create Event Channels")]
        public static void CreateAll()
        {
            int created = 0;
            foreach (string channelName in VoidChannels)
            {
                created += CreateIfMissing<VoidEventChannel>(channelName);
            }

            created += CreateIfMissing<NoiseEventChannel>(NoiseChannel);
            created += CreateIfMissing<StringEventChannel>(ObjectiveChannel);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Blackout] Event channels: created {created}, total {VoidChannels.Length + 2} in {Folder}.");
        }

        private static int CreateIfMissing<T>(string assetName) where T : ScriptableObject
        {
            string path = $"{Folder}/{assetName}.asset";
            Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                // CreateAsset would replace whatever is there, so never touch a file of another type.
                if (!(existing is T))
                {
                    Debug.LogWarning($"[Blackout] {path} exists but is not a {typeof(T).Name}; left unchanged.");
                }

                return 0;
            }

            T channel = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(channel, path);
            return 1;
        }
    }
}
