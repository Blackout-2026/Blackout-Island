using System.Collections.Generic;
using System.IO;
using Blackout.Environment;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using ParticleGUI = UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI;
using SimpleLitGUI = UnityEditor.Rendering.Universal.ShaderGUI.SimpleLitGUI;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Build Atmosphere Particles: the W9 particle systems of Environment.unity, from <see cref="AtmosphereLayout"/>.
    /// - Volumes/Atmosphere/Atmosphere_&lt;Name&gt;: steam from the two compound vents, dust in the corridor, fog drifting
    ///   over the beach, and light rain, built switched off (turn the GameObject on to use it; a re-run switches it off
    ///   again). The rain has a <see cref="RainEmitter"/>, which keeps it over the camera and out of the bunker.
    /// - Assets/Environment/Atmosphere/SoftParticle.png: the prototype's soft round sprite, rewritten only when its pixels
    ///   change. Atmosphere_Steam, _Dust, _Fog and _Rain materials in Assets/Materials/Environment/Atmosphere (URP particle
    ///   shaders, regenerated in full on every run).
    /// - The <see cref="QualityPreset"/> on the Volumes root lists the systems, so it can scale their counts.
    /// Each system simulates in world space with a fixed random seed. Re-runnable and deterministic: systems are reused by
    /// name, and it removes only objects it generated under Volumes/Atmosphere. The vents themselves are props (W10).
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.AtmosphereParticlesBuilder.BuildAtmosphereParticles
    /// </summary>
    public static class AtmosphereParticlesBuilder
    {
        private const string UndoLabel = "Build Atmosphere Particles";
        private const string UnlitShader = "Universal Render Pipeline/Particles/Unlit";
        private const string SimpleLitShader = "Universal Render Pipeline/Particles/Simple Lit";

        /// <summary>The first system's random seed; the others count up from it.</summary>
        public const uint FirstSeed = 9001;

        [MenuItem("Blackout/World/Build Atmosphere Particles")]
        public static void BuildAtmosphereParticles()
        {
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Build Atmosphere Particles cancelled (the Environment scene is not open).");
                return;
            }

            BunkerLayout layout = BunkerLayoutAssetBuilder.LoadOrCreate();
            List<AtmosphereSpec> specs = AtmosphereLayout.Build(layout);
            Texture2D texture = SaveTexture();
            var materials = new Dictionary<AtmosphereKind, Material>();
            foreach (AtmosphereKind kind in System.Enum.GetValues(typeof(AtmosphereKind)))
            {
                materials.Add(kind, SaveMaterial(kind, texture));
            }

            Transform volumes = WorldSceneSkeleton.FindRoot(scene, PostProcessLayout.VolumesRootName).transform;
            Transform container = GeneratedObjects.FindOrCreateChild(volumes, AtmosphereLayout.ContainerName, UndoLabel);
            container.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            container.localScale = Vector3.one;

            var built = new HashSet<string>();
            var systems = new ParticleSystem[specs.Count];
            for (int i = 0; i < specs.Count; i++)
            {
                GameObject go = GeneratedObjects.GetOrCreate(container, specs[i].Name, built, UndoLabel);
                systems[i] = Configure(go, specs[i], materials[specs[i].Kind], FirstSeed + (uint)i);
                if (specs[i].Kind == AtmosphereKind.Rain)
                {
                    GeneratedObjects.GetOrAddComponent<RainEmitter>(go).Configure(AtmosphereLayout.RainHeight, AtmosphereLayout.RainRoofCheck);
                }
            }

            int removed = GeneratedObjects.RemoveStale(container, built);
            QualityPreset preset = GeneratedObjects.GetOrAddComponent<QualityPreset>(volumes.gameObject);
            preset.SetParticles(systems);
            EditorUtility.SetDirty(preset);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[World] Atmosphere particles built under {volumes.name}/{container.name} ({removed} removed):{Report(specs)}"
                + $"\n  QualityPreset on {volumes.name} scales the {systems.Length} systems (Low 0.5, Medium 1, High 1.5).");
        }

        /// <summary>
        /// Sets up a particle system from its spec, on a GameObject that may already have one. Every module is set on every
        /// run, unused ones switched off, so a re-run gives the same system whatever was there before.
        /// </summary>
        public static ParticleSystem Configure(GameObject go, AtmosphereSpec spec, Material material, uint seed)
        {
            go.transform.SetPositionAndRotation(spec.Position, EmitterRotation(spec.Direction));
            go.transform.localScale = Vector3.one;
            go.SetActive(spec.StartsActive);

            ParticleSystem particles = GeneratedObjects.GetOrAddComponent<ParticleSystem>(go);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // the seed and duration need a stopped system
            particles.useAutoRandomSeed = false;
            particles.randomSeed = seed;

            ParticleSystem.MainModule main = particles.main;
            main.duration = spec.Lifetime.y; // a prewarm simulates one cycle, so one full life fills the system
            main.loop = true;
            main.prewarm = spec.Prewarm;
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(spec.Lifetime.x, spec.Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spec.Speed.x, spec.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(spec.Size.x, spec.Size.y);
            main.startRotation = spec.RandomRotation ? new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI) : new ParticleSystem.MinMaxCurve(0f);
            main.startColor = spec.Colour;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            main.maxParticles = spec.MaxParticles;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = spec.Rate;
            emission.rateOverDistance = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = spec.Shape;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = spec.Shape == ParticleSystemShapeType.Box ? spec.BoxSize : Vector3.one;
            shape.angle = spec.ConeAngle;
            shape.radius = Mathf.Max(spec.ConeRadius, 0.0001f);
            shape.radiusThickness = 1f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 0f;

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = spec.DriftX != Vector2.zero;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(spec.DriftX.x, spec.DriftX.y);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f); // all three axes must use the same curve mode
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
            limit.enabled = spec.Drag > 0f;
            limit.limit = 100f; // no speed limit: the module is used for its drag only
            limit.dampen = 0f;
            limit.drag = spec.Drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = spec.NoiseStrength > 0f;
            noise.strength = spec.NoiseStrength;
            noise.frequency = Mathf.Max(spec.NoiseFrequency, 0.0001f);
            noise.scrollSpeed = 0.05f;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = !Mathf.Approximately(spec.EndSizeScale, 1f);
            float largest = Mathf.Max(1f, spec.EndSizeScale);
            size.size = new ParticleSystem.MinMaxCurve(largest, AnimationCurve.Linear(0f, 1f / largest, 1f, spec.EndSizeScale / largest));

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = spec.SpinDegrees > 0f;
            spin.z = new ParticleSystem.MinMaxCurve(-spec.SpinDegrees * Mathf.Deg2Rad, spec.SpinDegrees * Mathf.Deg2Rad);

            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            colour.color = OpacityGradient(spec);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            bool stretch = spec.StretchVelocityScale > 0f;
            renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = spec.StretchVelocityScale;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = spec.Kind == AtmosphereKind.Dust;
            renderer.sortMode = spec.SortByDistance ? ParticleSystemSortMode.Distance : ParticleSystemSortMode.None;
            renderer.maxParticleSize = spec.MaxScreenSize;
            renderer.minParticleSize = 0f;
            renderer.alignment = ParticleSystemRenderSpace.View;

            EditorUtility.SetDirty(particles);
            EditorUtility.SetDirty(renderer);
            return particles;
        }

        /// <summary>The emitter faces its direction. Straight up or down, its own up is north, so a box keeps its X along east.</summary>
        public static Quaternion EmitterRotation(Vector3 direction)
        {
            Vector3 up = Mathf.Abs(direction.normalized.y) > 0.99f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(direction, up);
        }

        /// <summary>White, with the spec's opacity curve (<see cref="AtmosphereLayout.AlphaAt"/>) as alpha keys.</summary>
        public static Gradient OpacityGradient(AtmosphereSpec spec)
        {
            float[] times = AtmosphereLayout.AlphaKeyTimes(spec);
            var alphas = new GradientAlphaKey[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                alphas[i] = new GradientAlphaKey(AtmosphereLayout.AlphaAt(spec, times[i]), times[i]);
            }

            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alphas);
            return gradient;
        }

        /// <summary>
        /// The prototype's glow sprite: white, with an alpha that falls from 1 at the centre to 0.55 at a quarter of the
        /// radius and 0 at the edge (canvas radial gradient, linear between the stops).
        /// </summary>
        public static Color32[] SoftParticlePixels(int size)
        {
            var pixels = new Color32[size * size];
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2(x + 0.5f - radius, y + 0.5f - radius).magnitude / radius;
                    float alpha = r < 0.25f ? Mathf.Lerp(1f, 0.55f, r / 0.25f) : Mathf.Lerp(0.55f, 0f, (r - 0.25f) / 0.75f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            return pixels;
        }

        // ---- Assets -----------------------------------------------------------------------------------------------

        private static Texture2D SaveTexture()
        {
            if (!AssetDatabase.IsValidFolder(AtmosphereLayout.TextureFolder))
            {
                AssetDatabase.CreateFolder("Assets/Environment", "Atmosphere");
            }

            int size = AtmosphereLayout.TextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(SoftParticlePixels(size));
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string file = Path.Combine(Path.GetDirectoryName(Application.dataPath), AtmosphereLayout.TexturePath);
            if (!File.Exists(file) || !SameBytes(File.ReadAllBytes(file), png))
            {
                File.WriteAllBytes(file, png);
                AssetDatabase.ImportAsset(AtmosphereLayout.TexturePath, ImportAssetOptions.ForceUpdate);
            }

            // A small soft gradient: uncompressed, so its alpha does not band; clamped, so the edge stays clear.
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtmosphereLayout.TexturePath);
            bool changed = importer.textureType != TextureImporterType.Default
                || !importer.alphaIsTransparency
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.maxTextureSize != size
                || !importer.mipmapEnabled;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtmosphereLayout.TexturePath);
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A transparent, alpha-blended URP particle material, regenerated in full on every run. Steam and fog fade where
        /// they meet the ground (soft particles, which use the depth texture the PC URP asset already has), and the fog also
        /// fades out near the camera, so a 10 m puff never fills the view.
        /// </summary>
        public static Material SaveMaterial(AtmosphereKind kind, Texture2D texture)
        {
            if (!AssetDatabase.IsValidFolder(AtmosphereLayout.MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Materials/Environment", "Atmosphere");
            }

            string path = $"{AtmosphereLayout.MaterialFolder}/Atmosphere_{kind}.mat";
            Shader shader = Shader.Find(kind == AtmosphereKind.Dust ? SimpleLitShader : UnlitShader);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = $"Atmosphere_{kind}" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", 0f); // alpha
            material.SetFloat("_ColorMode", 0f); // multiply by the particle colour
            SetIfPresent(material, "_ReceiveShadows", kind == AtmosphereKind.Dust ? 1f : 0f);

            bool soft = kind == AtmosphereKind.Steam || kind == AtmosphereKind.Fog;
            material.SetFloat("_SoftParticlesEnabled", soft ? 1f : 0f);
            material.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            material.SetFloat("_SoftParticlesFarFadeDistance", kind == AtmosphereKind.Fog ? 2f : 1f);

            bool cameraFade = kind == AtmosphereKind.Fog;
            material.SetFloat("_CameraFadingEnabled", cameraFade ? 1f : 0f);
            material.SetFloat("_CameraNearFadeDistance", 1f);
            material.SetFloat("_CameraFarFadeDistance", 4f);

            // What the material Inspector does on a change: render queue, blend state and keywords.
            if (kind == AtmosphereKind.Dust)
            {
                BaseShaderGUI.SetMaterialKeywords(material, SimpleLitGUI.SetMaterialKeywords, ParticleGUI.SetMaterialKeywords);
            }
            else
            {
                BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static string Report(List<AtmosphereSpec> specs)
        {
            var report = new System.Text.StringBuilder();
            foreach (AtmosphereSpec spec in specs)
            {
                Vector3 p = spec.Position;
                float alive = spec.Rate * (spec.Lifetime.x + spec.Lifetime.y) * 0.5f;
                report.Append($"\n  Atmosphere_{spec.Name}: {spec.Kind}, at ({p.x:0.##}, {p.y:0.##}, {p.z:0.##}), {spec.Rate} per second, "
                    + $"life {spec.Lifetime.x}-{spec.Lifetime.y} s, about {alive:0} alive (cap {spec.MaxParticles})"
                    + (spec.StartsActive ? "" : ", switched off"));
            }

            return report.ToString();
        }
    }
}
