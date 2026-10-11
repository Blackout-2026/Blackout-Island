using Blackout.Player;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds the first-person Player prefab and its movement settings asset in Assets/Prefabs/Player.
    /// Re-runnable: an asset that already exists is left untouched, so hand tuning and its GUID survive.
    /// To regenerate the prefab, delete it in the Project window and run the menu again.
    /// </summary>
    public static class PlayerPrefabBuilder
    {
        public const string Folder = "Assets/Prefabs/Player";
        public const string PrefabPath = Folder + "/Player.prefab";
        public const string SettingsPath = Folder + "/PlayerMovementSettings.asset";

        private const string ActionsPath = "Assets/Scripts/PlayerAndInteraction/Input/PlayerControls.inputactions";
        private const string NoiseChannelPath = EventChannelAssetBuilder.Folder + "/Noise.asset";
        private const string PlayerLayer = "Player";
        private const string PlayerTag = "Player";
        private const string CameraTag = "MainCamera";

        // Prototype camera: 0.08 near, 700 far (docs/reference/systems-engineer.md section 2).
        private const float NearClip = 0.08f;
        private const float FarClip = 700f;

        [MenuItem("Blackout/Systems/Build Player Prefab")]
        public static void Build()
        {
            PlayerMovementSettings settings = CreateSettingsIfMissing();

            if (AssetDatabase.LoadMainAssetAtPath(PrefabPath) != null)
            {
                Debug.Log($"[Blackout] {PrefabPath} already exists; left unchanged. Delete it to rebuild.");
                return;
            }

            Object actions = AssetDatabase.LoadMainAssetAtPath(ActionsPath);
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>(NoiseChannelPath);
            if (actions == null || noise == null)
            {
                Debug.LogError($"[Blackout] Missing {ActionsPath} or {NoiseChannelPath}; prefab not built.");
                return;
            }

            int layer = LayerMask.NameToLayer(PlayerLayer);
            if (layer < 0)
            {
                Debug.LogError($"[Blackout] Layer '{PlayerLayer}' is not defined (P0); prefab not built.");
                return;
            }

            GameObject root = BuildHierarchy(settings, actions, noise, layer);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log(saved
                ? $"[Blackout] Built {PrefabPath}."
                : $"[Blackout] Could not save {PrefabPath}.");
        }

        private static PlayerMovementSettings CreateSettingsIfMissing()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>(SettingsPath);
            if (existing != null)
            {
                return existing;
            }

            var settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            Debug.Log($"[Blackout] Created {SettingsPath} with the prototype defaults.");
            return settings;
        }

        /// <summary>
        /// Player (CharacterController, input, motor, look)
        ///   CameraRig (PlayerCameraEffects: bob, landing, step smoothing, roll)
        ///     CameraPivot (pitch, at eye height)
        ///       PlayerCamera (Camera, AudioListener)
        ///         WeaponHolder (Y3 viewmodels go here)
        /// </summary>
        private static GameObject BuildHierarchy(PlayerMovementSettings settings, Object actions,
            NoiseEventChannel noise, int layer)
        {
            var root = new GameObject("Player");
            root.tag = PlayerTag;

            var controller = root.AddComponent<CharacterController>();
            PlayerMotor.ApplyBodySettings(controller, settings);

            var input = root.AddComponent<PlayerInputReader>();
            Assign(input, "actions", actions);

            var motor = root.AddComponent<PlayerMotor>();
            Assign(motor, "settings", settings);
            Assign(motor, "input", input);
            Assign(motor, "noiseChannel", noise);

            Transform rig = CreateChild("CameraRig", root.transform, Vector3.zero);
            var effects = rig.gameObject.AddComponent<PlayerCameraEffects>();
            Assign(effects, "motor", motor);

            Transform pivot = CreateChild("CameraPivot", rig, new Vector3(0f, settings.EyeHeight, 0f));

            Transform cameraTransform = CreateChild("PlayerCamera", pivot, Vector3.zero);
            cameraTransform.gameObject.tag = CameraTag;
            var camera = cameraTransform.gameObject.AddComponent<Camera>();
            camera.fieldOfView = settings.fieldOfView;
            camera.nearClipPlane = NearClip;
            camera.farClipPlane = FarClip;
            cameraTransform.gameObject.AddComponent<AudioListener>();

            CreateChild("WeaponHolder", cameraTransform, Vector3.zero);

            var look = root.AddComponent<PlayerLook>();
            Assign(look, "settings", settings);
            Assign(look, "input", input);
            Assign(look, "cameraPivot", pivot);
            Assign(look, "playerCamera", camera);

            SetLayerRecursively(root.transform, layer);
            return root;
        }

        private static Transform CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        /// <summary>Sets a private [SerializeField] through the serialization system, as the Inspector does.</summary>
        private static void Assign(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"[Blackout] {component.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target)
            {
                SetLayerRecursively(child, layer);
            }
        }
    }
}
