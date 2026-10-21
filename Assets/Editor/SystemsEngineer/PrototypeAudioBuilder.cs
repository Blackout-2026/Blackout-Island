// Owner: Systems Engineer. AU1: patches only managed audio components, references and listeners.
using System;
using Blackout.Audio;
using Blackout.Interaction;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    public static class PrototypeAudioBuilder
    {
        public const string BankPath = "Assets/ScriptableObjects/Audio/PrototypeAudio.asset";
        private const string Clips = "Assets/Audio/Prototype/";
        private const string GeneratorVisual = "Assets/Prefabs/Models/Generator_Visual.prefab";

        [MenuItem("Blackout/Systems/Build Prototype Audio")]
        public static void Build()
        {
            PrototypeAudioBank bank = BuildBank();
            ReadVolumes(out Bounds bunker, out Bounds entrance);
            Patch(PlayerPrefabBuilder.PrefabPath, root =>
            {
                var audio = GetOrAdd<GameplayAudio>(root);
                var fields = new SerializedObject(audio);
                fields.FindProperty("bank").objectReferenceValue = bank;
                fields.FindProperty("noise").objectReferenceValue = Channel<NoiseEventChannel>("Noise");
                fields.FindProperty("generatorDisabled").objectReferenceValue = Channel<VoidEventChannel>("GeneratorDisabled");
                fields.FindProperty("cureTaken").objectReferenceValue = Channel<VoidEventChannel>("CureTaken");
                fields.FindProperty("missionComplete").objectReferenceValue = Channel<VoidEventChannel>("MissionComplete");
                fields.FindProperty("motor").objectReferenceValue = root.GetComponent<PlayerMotor>();
                fields.FindProperty("weapons").objectReferenceValue = root.GetComponentInChildren<WeaponController>(true);
                fields.FindProperty("health").objectReferenceValue = root.GetComponent<Health>();
                fields.FindProperty("bunker").boundsValue = bunker;
                fields.FindProperty("entrance").boundsValue = entrance;
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            string[] actors = { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" };
            AudioCue[] guns = { AudioCue.PatrolGun, AudioCue.HunterGun, AudioCue.GuardGun, AudioCue.CommanderGun };
            for (int i = 0; i < actors.Length; i++)
            {
                int index = i;
                Patch("Assets/Prefabs/Agents/" + actors[i] + ".prefab", root =>
                {
                    var actor = GetOrAdd<ActorAudio>(root);
                    var fields = new SerializedObject(actor);
                    fields.FindProperty("gunCue").enumValueIndex = (int)guns[index];
                    fields.FindProperty("robot").boolValue = actors[index] == "TacticalGuard";
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    // Editor-only reflection on the serialized component avoids a new assembly dependency.
                    foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
                        if (component != null && component.GetType().Name == "AgentShooter")
                        {
                            var shooter = new SerializedObject(component);
                            var channel = shooter.FindProperty("gunfireChannel");
                            if (channel.objectReferenceValue == null)
                            {
                                channel.objectReferenceValue = Channel<NoiseEventChannel>("Noise");
                                shooter.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                });
            }
            Patch("Assets/Prefabs/Interactables/Door.prefab", root =>
            {
                var hook = GetOrAdd<InteractionAudio>(root);
                var door = root.GetComponent<SlidingDoor>();
                ListenOnce(door.onOpened, hook.DoorMove);
                ListenOnce(door.onClosed, hook.DoorMove);
                ListenOnce(door.onLockedUsed, hook.DoorLocked);
            });
            Patch("Assets/Prefabs/Interactables/Generator.prefab", root =>
                ListenOnce(root.GetComponentInChildren<GeneratorInteractable>(true).onHoldSpark, GetOrAdd<InteractionAudio>(root).HoldTick));
            foreach (string name in new[] { "HealthPickup", "MedkitDrop", "AmmoDrop", "WeaponDrop" })
                Patch("Assets/Prefabs/Interactables/" + name + ".prefab", root =>
                {
                    var hook = GetOrAdd<InteractionAudio>(root);
                    if (root.TryGetComponent(out HealthPickup health)) ListenOnce(health.onPickedUp, hook.Pickup);
                    if (root.TryGetComponent(out MedkitPickup medkit)) ListenOnce(medkit.onPickedUp, hook.Pickup);
                    if (root.TryGetComponent(out AmmoPickup ammo)) ListenOnce(ammo.onPickedUp, hook.Pickup);
                    if (root.TryGetComponent(out WeaponDropPickup weapon)) ListenOnce(weapon.onTaken, hook.Pickup);
                });
            Patch(GeneratorVisual, root =>
            {
                AudioSource hum = root.transform.Find("HumAudioSource").GetComponent<AudioSource>();
                hum.clip = bank.Get(AudioCue.GeneratorHum);
                hum.volume = 0.55f;
                hum.loop = true;
                hum.playOnAwake = true;
                hum.spatialBlend = 1f;
                hum.rolloffMode = AudioRolloffMode.Logarithmic;
                hum.minDistance = 4f;
                hum.maxDistance = 120f;
                hum.dopplerLevel = 0f;
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[Blackout] AU1: prototype bank, player, four agents, six interaction prefabs and generator hum wired.");
        }

        private static PrototypeAudioBank BuildBank()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects/Audio"))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Audio");
            var cues = (AudioCue[])Enum.GetValues(typeof(AudioCue));
            var clips = new AudioClip[cues.Length];
            for (int i = 0; i < cues.Length; i++)
            {
                string path = Clips + cues[i] + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing " + path + "; run tools/generate-prototype-audio.py first.");
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (!importer.forceToMono || !settings.preloadAudioData || settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                    settings.compressionFormat != AudioCompressionFormat.PCM || settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate)
                {
                    importer.forceToMono = true;
                    settings.preloadAudioData = true;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.defaultSampleSettings = settings;
                    importer.SaveAndReimport();
                }
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            var bank = AssetDatabase.LoadAssetAtPath<PrototypeAudioBank>(BankPath);
            if (bank == null) { bank = ScriptableObject.CreateInstance<PrototypeAudioBank>(); AssetDatabase.CreateAsset(bank, BankPath); }
            var fields = new SerializedObject(bank);
            var list = fields.FindProperty("clips");
            list.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
            return bank;
        }

        private static void ReadVolumes(out Bounds bunker, out Bounds entrance)
        {
            const string path = "Assets/Scenes/Environment.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (component != null && component.GetType().Name == "PowerStateLighting")
                        {
                            var fields = new SerializedObject(component);
                            bunker = fields.FindProperty("bunker").boundsValue;
                            entrance = fields.FindProperty("entrance").boundsValue;
                            return;
                        }
                throw new InvalidOperationException("Environment needs its existing PowerStateLighting volumes.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); } // never save Environment
        }

        private static T Channel<T>(string name) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>("Assets/ScriptableObjects/Events/" + name + ".asset");
        private static T GetOrAdd<T>(GameObject root) where T : Component => root.TryGetComponent(out T existing) ? existing : root.AddComponent<T>();
        private static void Patch(string path, Action<GameObject> change)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { change(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void ListenOnce(UnityEvent channel, UnityAction action)
        {
            for (int i = 0; i < channel.GetPersistentEventCount(); i++)
                if (channel.GetPersistentTarget(i) == action.Target as Object && channel.GetPersistentMethodName(i) == action.Method.Name) return;
            UnityEventTools.AddPersistentListener(channel, action);
        }
    }
}
