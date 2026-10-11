using Blackout.Art;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds Assets/Prefabs/Models/Generator_Visual.prefab (docs/art-pipeline.md section 10): the generator's
    /// look and sockets only, no interaction. The Systems Engineer nests it in their interactable prefab.
    /// Uses SM_Generator.fbx when it exists; until then it builds a primitive placeholder of the same size.
    /// Re-runnable: only the "Model" child is replaced, and sockets, components and references that already
    /// exist are kept, so positions moved by hand and overrides in nested prefabs survive.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.GeneratorPrefabBuilder.Build -logFile -
    /// </summary>
    public static class GeneratorPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Models/Generator_Visual.prefab";
        public const string FbxPath = "Assets/Models/Custom/SM_Generator.fbx";
        public const string ChannelPath = "Assets/ScriptableObjects/Events/GeneratorDisabled.asset";

        public const string ModelName = "Model";
        public const string InteractPointName = "InteractPoint";
        public const string SparkName = "SparkVFX";
        public const string HumName = "HumAudioSource";

        // Local positions in metres. The origin is on the floor under the skid, the control face is at +X.
        // They come from docs/reference/core-developer.md 2.2 and 2.5 (prototype z is flipped, all of these are on z 0).
        private static readonly Vector3 InteractPointPosition = new Vector3(3.5f, 1.3f, 0f); // In front of the gauges and valve wheel
        private static readonly Vector3 SparkPosition = new Vector3(3.3f, 1.9f, 0f); // Top of the control panel
        private static readonly Vector3 HumPosition = new Vector3(1.6f, 1.25f, 0f); // The energy core

        // Collider proxy from the prototype's room code: 6.4 x 3.6 x 3.2 m from the floor up.
        private static readonly Vector3 ColliderSize = new Vector3(6.4f, 3.6f, 3.2f);
        private static readonly Vector3 ColliderCenter = new Vector3(0f, 1.8f, 0f);

        [MenuItem("Blackout/Art/Build Generator Visual Prefab")]
        public static void Build()
        {
            GeneratorMaterialSet materials = GeneratorMaterialBuilder.BuildAll();

            var channel = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(ChannelPath);
            if (channel == null)
            {
                Debug.LogWarning($"[GeneratorPrefab] {ChannelPath} not found: pull develop. The prefab is built without the event channel.");
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[GeneratorPrefab] {FbxPath} not found: building the primitive placeholder model.");
            }

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("Generator_Visual");
            try
            {
                Populate(root, fbx, materials, channel);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[GeneratorPrefab] {(exists ? "Updated" : "Created")} {PrefabPath} ({(fbx != null ? "from SM_Generator.fbx" : "placeholder model")})");
        }

        /// <summary>Fills a root object with the model, sockets, collider and GeneratorVisuals. Safe to call repeatedly.</summary>
        public static void Populate(GameObject root, GameObject fbx, GeneratorMaterialSet materials, VoidEventChannel channel)
        {
            Transform model = ReplaceModel(root.transform, fbx, materials);

            EnsureSocket(root.transform, InteractPointName, InteractPointPosition, Quaternion.identity, out _);

            // Sparks fly out of the control face: the cone's +Z axis points along +X and up.
            Transform sparkSocket = EnsureSocket(root.transform, SparkName, SparkPosition, Quaternion.Euler(-45f, 90f, 0f), out _);
            if (!sparkSocket.TryGetComponent(out ParticleSystem sparks))
            {
                sparks = sparkSocket.gameObject.AddComponent<ParticleSystem>();
                ConfigureSparks(sparks, materials.Spark);
            }

            Transform humSocket = EnsureSocket(root.transform, HumName, HumPosition, Quaternion.identity, out _);
            if (!humSocket.TryGetComponent(out AudioSource hum))
            {
                hum = humSocket.gameObject.AddComponent<AudioSource>();
                ConfigureHum(hum);
            }

            if (!root.TryGetComponent(out BoxCollider _))
            {
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.size = ColliderSize;
                box.center = ColliderCenter;
            }

            if (!root.TryGetComponent(out GeneratorVisuals visuals))
            {
                visuals = root.AddComponent<GeneratorVisuals>();
            }

            Wire(visuals, model, materials, channel, hum, sparks);
        }

        /// <summary>
        /// Replaces every imported material slot with the M_Generator_* asset of the same name. The FBX keeps its
        /// materials inside (docs/art-pipeline.md section 9), so a Blender name that does not match stays as imported.
        /// </summary>
        public static void AssignMaterials(GameObject instance, GeneratorMaterialSet materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slotName = slots[i] != null ? slots[i].name : string.Empty;
                    Material replacement = materials.Find(slotName);
                    if (replacement != null)
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[GeneratorPrefab] {renderer.name} slot {i} uses '{slotName}', which matches no M_Generator_* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }

        /// <summary>The renderer and slot index that show the energy core, or (null, -1) when no slot uses it.</summary>
        public static (Renderer renderer, int slot) FindCore(GameObject model, Material coreMaterial)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == coreMaterial)
                    {
                        return (renderer, i);
                    }
                }
            }

            return (null, -1);
        }

        private static Transform ReplaceModel(Transform root, GameObject fbx, GeneratorMaterialSet materials)
        {
            Transform old = root.Find(ModelName);
            if (old != null && IsCurrentModel(old.gameObject, fbx))
            {
                // Nothing to replace: keep it, so a re-run leaves the prefab file unchanged.
                if (fbx != null)
                {
                    AssignMaterials(old.gameObject, materials);
                }

                return old;
            }

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            if (fbx != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root);
                instance.name = ModelName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                AssignMaterials(instance, materials);
                return instance.transform;
            }

            var placeholder = new GameObject(ModelName).transform;
            placeholder.SetParent(root, false);
            BuildPlaceholder(placeholder, materials);
            return placeholder;
        }

        // True when the existing Model child is already what this run would build: the placeholder while there is
        // no FBX, or an instance of this very FBX once there is one (a placeholder is replaced by the FBX).
        private static bool IsCurrentModel(GameObject model, GameObject fbx)
        {
            if (fbx == null)
            {
                return !PrefabUtility.IsPartOfPrefabInstance(model);
            }

            return PrefabUtility.GetCorrespondingObjectFromSource(model) == fbx;
        }

        // A primitive stand-in at the real sizes (docs/reference/core-developer.md 2.2), so the prefab can be nested
        // and tested before the Blender model exists. Replaced as soon as SM_Generator.fbx is imported.
        private static void BuildPlaceholder(Transform model, GeneratorMaterialSet m)
        {
            AddPart(model, "Skid", PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(6.4f, 0.22f, 2.9f), false, m.Metal);
            AddPart(model, "Engine", PrimitiveType.Cube, new Vector3(-1.7f, 1f, 0f), new Vector3(2.7f, 1.5f, 1.9f), false, m.Body);
            AddPart(model, "Flywheel", PrimitiveType.Cylinder, new Vector3(-3.45f, 1f, 0f), new Vector3(1.24f, 0.07f, 1.24f), true, m.Metal);
            AddPart(model, "HousingFront", PrimitiveType.Cylinder, new Vector3(0.475f, 1.25f, 0f), new Vector3(2.1f, 0.275f, 2.1f), true, m.Body);
            AddPart(model, "HousingBack", PrimitiveType.Cylinder, new Vector3(2.725f, 1.25f, 0f), new Vector3(2.1f, 0.275f, 2.1f), true, m.Body);
            AddPart(model, "Glass", PrimitiveType.Cylinder, new Vector3(1.6f, 1.25f, 0f), new Vector3(2.04f, 0.85f, 2.04f), true, m.Glass);
            AddPart(model, "Core", PrimitiveType.Cylinder, new Vector3(1.6f, 1.25f, 0f), new Vector3(1.24f, 0.8f, 1.24f), true, m.Core);
            AddPart(model, "Stack", PrimitiveType.Cylinder, new Vector3(1.5f, 3.2f, 0f), new Vector3(0.54f, 0.9f, 0.54f), false, m.Metal);
            AddPart(model, "ControlPanel", PrimitiveType.Cube, new Vector3(3.1f, 1.35f, 0f), new Vector3(0.3f, 1.5f, 1.6f), false, m.Metal);
        }

        // A Unity cylinder is 2 m tall and 1 m wide along Y, so "scale" is (diameter, length / 2, diameter).
        // Parts that lie along X are turned 90 degrees about Z.
        private static void AddPart(Transform parent, string partName, PrimitiveType type, Vector3 position, Vector3 scale, bool alongX, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>()); // One collider on the root is enough.
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = alongX ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // Returns the existing child unchanged (its position may have been tuned by hand) or creates it.
        private static Transform EnsureSocket(Transform root, string socketName, Vector3 position, Quaternion rotation, out bool created)
        {
            Transform socket = root.Find(socketName);
            created = socket == null;
            if (created)
            {
                socket = new GameObject(socketName).transform;
                socket.SetParent(root, false);
                socket.localPosition = position;
                socket.localRotation = rotation;
            }

            return socket;
        }

        // Short bursts of orange streaks that fall back under gravity, repeating until the system is stopped.
        private static void ConfigureSparks(ParticleSystem sparks, Material material)
        {
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = sparks.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(1f, 0.75f, 0.25f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)40, 0, 0.9f) }); // 0 cycles = repeat forever

            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.05f;

            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2f;
        }

        // The clip is left empty: it is an audio asset only the Core Developer can supply (see the hand-off list).
        private static void ConfigureHum(AudioSource hum)
        {
            hum.playOnAwake = true;
            hum.loop = true;
            hum.volume = 0.6f;
            hum.spatialBlend = 1f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.minDistance = 3f;
            hum.maxDistance = 30f;
        }

        // The core always follows the current model; the other references are only filled when empty.
        private static void Wire(GeneratorVisuals visuals, Transform model, GeneratorMaterialSet materials,
            VoidEventChannel channel, AudioSource hum, ParticleSystem sparks)
        {
            var serialized = new SerializedObject(visuals);

            (Renderer core, int slot) = FindCore(model.gameObject, materials.Core);
            if (core != null)
            {
                serialized.FindProperty("coreRenderer").objectReferenceValue = core;
                serialized.FindProperty("coreMaterialSlot").intValue = slot;
            }
            else
            {
                Debug.LogWarning($"[GeneratorPrefab] No material slot uses {GeneratorMaterialBuilder.CoreName}: the energy core will not switch off.");
            }

            SetIfEmpty(serialized, "generatorDisabled", channel);
            SetIfEmpty(serialized, "coreOffMaterial", materials.CoreOff);
            SetIfEmpty(serialized, "humSource", hum);
            SetIfEmpty(serialized, "sparks", sparks);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIfEmpty(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
