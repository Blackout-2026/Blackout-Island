using System;
using System.Collections.Generic;
using Blackout.Environment;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ParticleGUI = UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI;

namespace Blackout.Editor
{
    /// <summary>One boat-mounted wood/iron torch, using built-in particles and the existing flickering light.
    /// Generated assets live in World Builder folders. No runtime mesh generation or custom Update is needed.</summary>
    public static class BoatTorchBuilder
    {
        private const string Label = "Build Boat Torch";
        private const string MaterialFolder = "Assets/Materials/Environment/";
        private const string TexturePath = "Assets/Environment/BoatTorchFlame.asset";
        private const string TorchName = "BoatTorch";
        private static readonly Vector3 Mount = new Vector3(-1.25f, 0.6f, -1.7f);

        internal static Renderer FindEmber(Scene scene) => FindTorch(scene)?.Find("BoatTorch_Embers")?.GetComponent<Renderer>();
        internal static Transform FindTorch(Scene scene) => WorldSceneSkeleton.FindRoot(scene, "Beach")?.transform.Find("Boat/" + TorchName);

        internal static void Apply(Transform boat)
        {
            Transform root = GeneratedObjects.FindOrCreateChild(boat, TorchName, Label);
            Undo.RecordObject(root, Label);
            root.SetLocalPositionAndRotation(Mount, Quaternion.identity);
            root.localScale = Vector3.one;
            Material wood = Material("Wood", "#382218");
            Material iron = Material("Iron", "#292725", 0.65f);
            Material ember = Material("Ember", "#351207");
            ember.SetColor("_EmissionColor", LightingLayout.FromLinearHex("#FF6B18", 1.25f));
            BaseShaderGUI.SetMaterialKeywords(ember);
            EditorUtility.SetDirty(ember);
            AssetDatabase.SaveAssetIfDirty(ember);
            var built = new HashSet<string>();
            Part(root, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.53f, 0f), new Vector3(0.075f, 0.48f, 0.075f), wood, built);
            foreach (float y in new[] { 0.2f, 0.65f, 0.9f })
                Part(root, "Band" + Mathf.RoundToInt(y * 100f), PrimitiveType.Cylinder, new Vector3(0f, y, 0f), new Vector3(0.095f, 0.025f, 0.095f), iron, built);
            Part(root, "Cup", PrimitiveType.Sphere, new Vector3(0f, 1f, 0f), new Vector3(0.27f, 0.16f, 0.27f), iron, built);
            Part(root, "Rim", PrimitiveType.Cylinder, new Vector3(0f, 1.085f, 0f), new Vector3(0.3f, 0.025f, 0.3f), iron, built);
            Part(root, "Embers", PrimitiveType.Sphere, new Vector3(0f, 1.115f, 0f), new Vector3(0.25f, 0.07f, 0.25f), ember, built);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 start = new Vector3(Mathf.Cos(a) * 0.055f, 0.87f, Mathf.Sin(a) * 0.055f);
                Vector3 end = new Vector3(Mathf.Cos(a) * 0.16f, 1.13f, Mathf.Sin(a) * 0.16f);
                GameObject rib = Part(root, "BasketRib" + i, PrimitiveType.Cylinder, (start + end) * 0.5f,
                    new Vector3(0.018f, Vector3.Distance(start, end) * 0.5f, 0.018f), iron, built);
                rib.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            }
            Material fire = FireMaterial();
            Particles(root, "Flames", fire, false);
            Particles(root, "Sparks", fire, true);
            ConfigureLight(root);
            AssetDatabase.SaveAssets();
        }

        // Setup Lighting also calls this, after its reference light settings have been applied.
        internal static void ConfigureLight(Transform torch)
        {
            Transform fixture = WorldSceneSkeleton.FindRoot(torch.gameObject.scene, LightingSetup.LightingRootName)?.transform
                .Find("OutdoorLights/OutdoorLights_BoatLantern");
            if (fixture == null) throw new InvalidOperationException("Run Setup Lighting before installing the boat torch.");
            Undo.RecordObject(fixture, Label);
            fixture.position = torch.TransformPoint(new Vector3(0f, 1.3f, 0f));
            Light light = fixture.GetComponent<Light>();
            Undo.RecordObject(light, Label);
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = LightingLayout.FromLinearHex("#FFAA46");
            light.intensity = 3.5f;
            light.range = 7f;
            light.shadows = LightShadows.None;
            FlickerLight flicker = fixture.GetComponent<FlickerLight>();
            Undo.RecordObject(flicker, Label);
            flicker.Configure(FlickerLight.Pattern.Flame, 3, FindEmber(torch.gameObject.scene));
            EditorUtility.SetDirty(flicker);
            EditorUtility.SetDirty(light);
        }

        private static GameObject Part(Transform root, string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material, ISet<string> built)
        {
            GameObject part = GeneratedObjects.GetOrCreate(root, name, built, Label, primitive);
            Undo.RecordObject(part.transform, Label);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.identity);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Undo.DestroyObjectImmediate(collider);
            part.layer = PhysicsLayers.Level;
            GameObjectUtility.SetStaticEditorFlags(part, 0);
            return part;
        }

        private static Material Material(string suffix, string hex, float metallic = 0f)
        {
            string path = MaterialFolder + "BoatTorch_" + suffix + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BoatTorch_" + suffix };
                AssetDatabase.CreateAsset(mat, path);
            }
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            mat.SetColor("_BaseColor", colour);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        private static Material FireMaterial()
        {
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            bool create = mask == null;
            if (create) mask = new Texture2D(64, 128, TextureFormat.RGBA32, false) { name = "BoatTorchFlame" };
            var pixels = new Color32[64 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 64; x++)
                {
                    float v = (y + 0.5f) / 128f, u = (x + 0.5f) / 64f;
                    float centre = 0.5f + 0.1f * Mathf.Sin(v * 8f) + 0.035f * Mathf.Sin(v * 19f);
                    float radius = 0.28f * Mathf.Pow(1f - v, 0.8f) + 0.015f;
                    float edge = Mathf.Clamp01(1f - Mathf.Pow((u - centre) / radius, 2f));
                    float tipFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, v));
                    float alpha = edge * edge * Mathf.SmoothStep(0f, 1f, v * 12f) * tipFade;
                    pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            mask.SetPixels32(pixels);
            mask.wrapMode = TextureWrapMode.Clamp;
            mask.filterMode = FilterMode.Bilinear;
            mask.Apply(false, false);
            if (create) AssetDatabase.CreateAsset(mask, TexturePath);
            else EditorUtility.SetDirty(mask);
            AssetDatabase.SaveAssetIfDirty(mask);
            string path = MaterialFolder + "BoatTorch_Fire.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "BoatTorch_Fire" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", mask);
            // Particle vertex colours are clamped; put fire brightness in the HDR material colour.
            mat.SetColor("_BaseColor", new Color(4f, 2f, 0.6f, 1f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f); // Additive: flame colour brightens the background.
            mat.SetFloat("_ColorMode", 0f);
            mat.SetFloat("_SoftParticlesEnabled", 0f);
            mat.SetFloat("_CameraFadingEnabled", 0f);
            BaseShaderGUI.SetMaterialKeywords(mat, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            return mat;
        }

        private static void Particles(Transform root, string name, Material material, bool sparks)
        {
            Transform child = GeneratedObjects.FindOrCreateChild(root, "BoatTorch_" + name, Label);
            child.SetLocalPositionAndRotation(new Vector3(0f, 1.14f, 0f), Quaternion.LookRotation(Vector3.up, Vector3.forward));
            child.localScale = Vector3.one;
            child.gameObject.layer = PhysicsLayers.Level;
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            ParticleSystem system = GeneratedObjects.GetOrAddComponent<ParticleSystem>(child.gameObject);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed = false;
            system.randomSeed = sparks ? 32u : 31u;
            var main = system.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.maxParticles = sparks ? 12 : 48;
            main.startLifetime = sparks ? new ParticleSystem.MinMaxCurve(0.5f, 0.9f) : new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = sparks ? new ParticleSystem.MinMaxCurve(0.8f, 1.3f) : new ParticleSystem.MinMaxCurve(0.3f, 0.65f);
            main.startSize3D = true;
            main.startSizeX = sparks ? new ParticleSystem.MinMaxCurve(0.012f, 0.022f) : new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSizeY = sparks ? new ParticleSystem.MinMaxCurve(0.035f, 0.065f) : new ParticleSystem.MinMaxCurve(0.32f, 0.55f);
            main.startSizeZ = 1f;
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = sparks ? 5f : 24f;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = sparks ? 15f : 8f;
            shape.radius = sparks ? 0.06f : 0.08f;
            var colour = system.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.4f, 0.025f), 0.45f), new GradientColorKey(new Color(0.7f, 0.04f, 0f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            colour.color = gradient;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
