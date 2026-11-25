using System;
using System.Collections.Generic;
using System.IO;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Step OC1: a proper sea and shore for the beach. <b>Blackout/World/Build Ocean and Shore</b> does two things and can be
    /// re-run safely:
    /// 1. Beach/Water gets the animated Blackout/Ocean material on a 128 x 128 mesh (replacing the flat 520 m plane).
    /// 2. Beach/Shore gets a wet sand band along the waterline, seven shore rocks and five pieces of driftwood, and the
    ///    terrain's Sand layer gets a ripple normal map.
    /// Nothing here has a collider, so the NavMesh, the player and the AI are not affected. Build Terrain keeps the ocean once
    /// it is installed (see <see cref="BeachGreyboxBuilder"/>), but Paint Terrain resets the Sand layer, so run this tool
    /// again after it.
    /// </summary>
    public static class OceanBuilder
    {
        private const string Label = "Build Ocean and Shore";
        public const string MeshPath = "Assets/Environment/OceanSurface.asset";
        public const string OceanShaderPath = "Assets/Environment/Shaders/BlackoutOcean.shader";
        public const string WetSandShaderPath = "Assets/Environment/Shaders/BlackoutWetSand.shader";
        public const string OceanMaterialPath = "Assets/Materials/Environment/Ocean.mat";
        public const string WetSandMaterialPath = "Assets/Materials/Environment/Shore_WetSand.mat";
        public const string DriftwoodMaterialPath = "Assets/Materials/Environment/Shore_Driftwood.mat";
        public const string SandNormalPath = "Assets/Textures/Environment/Sand/Sand_Ripple_Normal.png";
        public const string SandLayerPath = "Assets/Environment/Terrain/Layers/TL_Sand.terrainlayer";
        public const string WetSandMeshPath = "Assets/Environment/ShoreWetSand.asset";
        public const string DriftwoodMeshFormat = "Assets/Environment/ShoreDriftwood_{0}.asset";
        public const string ShoreRootName = "Shore";
        private const float SandNormalScale = 0.7f;
        private static readonly uint[] DriftwoodSeeds = { 9311, 9312, 9313 };
        private static readonly float[] DriftwoodRadius = { 0.12f, 0.09f, 0.15f };

        [MenuItem("Blackout/World/Build Ocean and Shore")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build the ocean outside Play mode.");
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }

            Transform beach = WorldSceneSkeleton.FindRoot(scene, "Beach").transform;
            UnityEngine.Terrain terrain = WorldSceneSkeleton.FindRoot(scene, "Terrain").transform.Find("IslandTerrain")?.GetComponent<UnityEngine.Terrain>();
            if (terrain == null) throw new InvalidOperationException("Run Build Terrain first: Terrain/IslandTerrain is missing.");

            string ocean = ApplyOcean(beach);
            string shore = BuildShore(beach, terrain);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[World] Ocean and shore built: {ocean}; {shore}.");
        }

        // ---- the sea -------------------------------------------------------------------------------------------

        public static string ApplyOcean(Transform beach)
        {
            Transform water = beach.Find("Water");
            if (water == null) throw new InvalidOperationException("Run Build Terrain first: Beach/Water is missing.");

            Mesh mesh = SaveMesh(OceanMesh.Create(), MeshPath);
            Material material = LoadOrCreateMaterial(OceanMaterialPath, OceanShaderPath, "Ocean");

            Undo.RecordObject(water, Label);
            water.SetPositionAndRotation(new Vector3(0f, IslandLayout.WaterY, BeachGreyboxBuilder.WaterCentreZ), Quaternion.identity);
            water.localScale = Vector3.one; // the mesh is already 520 m wide (the old plane was 10 m scaled by 52)
            var filter = water.GetComponent<MeshFilter>();
            Undo.RecordObject(filter, Label);
            filter.sharedMesh = mesh;
            var meshRenderer = water.GetComponent<MeshRenderer>();
            Undo.RecordObject(meshRenderer, Label);
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return $"ocean {OceanMesh.Cells} x {OceanMesh.Cells} cells, {mesh.triangles.Length / 3} triangles";
        }

        // ---- the beach -----------------------------------------------------------------------------------------

        private static string BuildShore(Transform beach, UnityEngine.Terrain terrain)
        {
            string sand = ApplySandRipples();

            float groundY = terrain.transform.position.y;
            Func<float, float, float> height = (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + groundY;

            Transform root = GeneratedObjects.FindOrCreateChild(beach, ShoreRootName, Label);
            Undo.RecordObject(root, Label);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;
            var built = new HashSet<string>();

            // Wet sand band.
            Mesh strip = SaveMesh(WetSandStrip.Create(height), WetSandMeshPath);
            Material wetSand = LoadOrCreateMaterial(WetSandMaterialPath, WetSandShaderPath, "Shore_WetSand");
            GameObject stripObject = GeneratedObjects.GetOrCreate(root, "WetSand", built, Label);
            Configure(stripObject, strip, wetSand, castShadows: false);

            // Rocks reuse the forest's rock meshes and material (LOD0 only, no collider). Driftwood is generated.
            Material driftwood = LoadOrCreateDriftwoodMaterial();
            var driftwoodMeshes = new Mesh[DriftwoodSeeds.Length];
            for (int i = 0; i < driftwoodMeshes.Length; i++)
            {
                driftwoodMeshes[i] = SaveMesh(
                    DriftwoodMesh.Create("ShoreDriftwood_" + i, DriftwoodSeeds[i], ShorePlacement.DriftwoodLength[i], DriftwoodRadius[i]),
                    string.Format(DriftwoodMeshFormat, i));
            }

            var rockMeshes = new Dictionary<ShoreKind, (Mesh mesh, Material material)>
            {
                { ShoreKind.Rock, LoadRock("Rock") },
                { ShoreKind.RockSmall, LoadRock("RockSmall") },
                { ShoreKind.RockRound, LoadRock("RockRound") },
            };

            List<ShoreProp> props = ShorePlacement.Plan(height, TreePositions(terrain));
            int rocks = 0;
            int logs = 0;
            for (int i = 0; i < props.Count; i++)
            {
                ShoreProp prop = props[i];
                bool isRock = prop.kind <= ShoreKind.RockRound;
                string part = (isRock ? "Rock_" : "Driftwood_") + (isRock ? rocks++ : logs++).ToString("00");
                GameObject go = GeneratedObjects.GetOrCreate(root, part, built, Label);
                Undo.RecordObject(go.transform, Label);
                go.transform.SetPositionAndRotation(prop.position, Quaternion.Euler(prop.euler));
                go.transform.localScale = Vector3.one * prop.scale;
                if (isRock)
                {
                    (Mesh mesh, Material material) rock = rockMeshes[prop.kind];
                    Configure(go, rock.mesh, rock.material, castShadows: true);
                }
                else
                {
                    Configure(go, driftwoodMeshes[prop.kind - ShoreKind.Driftwood0], driftwood, castShadows: true);
                }
            }

            GeneratedObjects.RemoveStale(root, built);
            return $"{sand}; wet sand strip {strip.triangles.Length / 3} triangles; {rocks} rocks, {logs} driftwood";
        }

        private static void Configure(GameObject go, Mesh mesh, Material material, bool castShadows)
        {
            GeneratedObjects.GetOrAddComponent<MeshFilter>(go).sharedMesh = mesh;
            var meshRenderer = GeneratedObjects.GetOrAddComponent<MeshRenderer>(go);
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = castShadows;
            go.layer = PhysicsLayers.Level;
            // See-through static (batching, occludee) like the water: no lightmap bake is needed, the pieces take probe light.
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlagRules.SeeThrough);
        }

        private static (Mesh, Material) LoadRock(string prefabName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Environment/Vegetation/{prefabName}.prefab");
            if (prefab == null) throw new InvalidOperationException($"Missing forest rock prefab {prefabName}; run Apply Forest Models.");
            LODGroup group = prefab.GetComponent<LODGroup>();
            Renderer lod0 = group != null ? group.GetLODs()[0].renderers[0] : prefab.GetComponentInChildren<Renderer>();
            return (lod0.GetComponent<MeshFilter>().sharedMesh, lod0.sharedMaterial);
        }

        private static List<Vector2> TreePositions(UnityEngine.Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            var positions = new List<Vector2>(data.treeInstanceCount);
            foreach (TreeInstance tree in data.treeInstances)
            {
                positions.Add(new Vector2(terrain.transform.position.x + tree.position.x * data.size.x,
                    terrain.transform.position.z + tree.position.z * data.size.z));
            }

            return positions;
        }

        // ---- sand ripple normal map ----------------------------------------------------------------------------

        private static string ApplySandRipples()
        {
            Texture2D generated = SandRippleTexture.Create();
            byte[] png = generated.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(generated);
            string full = Path.GetFullPath(SandNormalPath);
            bool changed = !File.Exists(full) || !AreEqual(File.ReadAllBytes(full), png);
            if (changed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, png);
                AssetDatabase.ImportAsset(SandNormalPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SandNormalPath);
            if (importer.textureType != TextureImporterType.NormalMap || importer.wrapMode != TextureWrapMode.Repeat
                || !importer.mipmapEnabled || importer.maxTextureSize != SandRippleTexture.Size)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = SandRippleTexture.Size;
                importer.SaveAndReimport();
            }

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(SandNormalPath);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SandLayerPath);
            if (layer == null) throw new InvalidOperationException("Missing Sand terrain layer; run Paint Terrain first.");
            if (layer.normalMapTexture != normal || !Mathf.Approximately(layer.normalScale, SandNormalScale))
            {
                Undo.RecordObject(layer, Label);
                layer.normalMapTexture = normal;
                layer.normalScale = SandNormalScale;
                EditorUtility.SetDirty(layer);
            }

            return $"Sand layer ripple normal {SandRippleTexture.Size} px at scale {SandNormalScale}";
        }

        private static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }

        // ---- assets --------------------------------------------------------------------------------------------

        private static Mesh SaveMesh(Mesh built, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }

            existing.Clear();
            existing.SetVertices(built.vertices);
            existing.SetNormals(built.normals);
            existing.SetUVs(0, built.uv);
            if (built.colors.Length > 0) existing.SetColors(built.colors);
            existing.SetTriangles(built.triangles, 0);
            existing.bounds = built.bounds;
            UnityEngine.Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>The material is created once; later runs keep any hand tuning and only make sure the shader is right.</summary>
        private static Material LoadOrCreateMaterial(string materialPath, string shaderPath, string materialName)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath)
                ?? throw new InvalidOperationException("Missing shader " + shaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = materialName, renderQueue = (int)RenderQueue.Transparent };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Material LoadOrCreateDriftwoodMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(DriftwoodMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Shore_Driftwood" };
            ColorUtility.TryParseHtmlString("#6B6358", out Color colour); // weathered grey-brown
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.12f);
            BaseShaderGUI.SetMaterialKeywords(material);
            AssetDatabase.CreateAsset(material, DriftwoodMaterialPath);
            return material;
        }
    }
}
