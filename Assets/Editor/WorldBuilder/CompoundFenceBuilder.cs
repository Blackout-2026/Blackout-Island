using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Builds only BunkerExterior/CompoundFence. Prototype fence dimensions, converted to +Z north.
    /// Fixed open gates; movement blockers permit shots/sight through the chain link. Owner: World Builder.</summary>
    public static class CompoundFenceBuilder
    {
        private const string Label = "Build Compound Fence";
        private const string MeshPath = "Assets/Environment/CompoundFence.asset";
        private const string TexturePath = "Assets/Textures/Environment/CompoundFence_ChainLink.png";
        private const string MaterialPath = "Assets/Materials/Environment/CompoundFence.mat";
        private const float GateHalfWidth = 3.2f;
        private const float Height = 3f;
        private static MeshBuilder mesh;

        [MenuItem("Blackout/World/Build Compound Fence")]
        public static void Build()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            Transform exterior = WorldSceneSkeleton.FindRoot(scene, "BunkerExterior").transform;
            Transform root = GeneratedObjects.FindOrCreateChild(exterior, "CompoundFence", Label);
            if (root.localPosition != Vector3.zero || root.localRotation != Quaternion.identity || root.localScale != Vector3.one)
                throw new InvalidOperationException("CompoundFence must keep an identity transform; change layout numbers in the builder.");
            var built = new HashSet<string>();
            mesh = new MeshBuilder(2); // chain link and steel, combined once in the Editor
            BuildRun(root, built, "West", new Vector3(-22f, 0f, 28f), new Vector3(-22f, 0f, 61f));
            BuildRun(root, built, "East", new Vector3(22f, 0f, 28f), new Vector3(22f, 0f, 61f));
            BuildRun(root, built, "SouthWest", new Vector3(-22f, 0f, 28f), new Vector3(-GateHalfWidth, 0f, 28f));
            BuildRun(root, built, "SouthEast", new Vector3(GateHalfWidth, 0f, 28f), new Vector3(22f, 0f, 28f));
            for (int side = -1; side <= 1; side += 2) BuildOpenGate(root, built, side);

            // Raised barrier, outside the 6.4 m passage, matching the reference's 1.25 rad tilt.
            Vector3 armCentre = new Vector3(3.5f, 13.3f, 26.8f);
            Vector3 armHalf = Quaternion.Euler(1.25f * Mathf.Rad2Deg, 0f, 0f) * Vector3.forward * 2.8f;
            Tube(armCentre - armHalf, armCentre + armHalf, 0.05f);
            Box(new Vector3(3.5f, 10.55f, 26.85f), new Vector3(0.4f, 1.1f, 0.5f));
            Blocker(root, built, "BarrierBase", new Vector3(3.5f, 10.55f, 26.85f), new Vector3(0.4f, 1.1f, 0.5f), Quaternion.identity);

            Mesh output = mesh.ToMesh("CompoundFence");
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null) { AssetDatabase.CreateAsset(output, MeshPath); saved = output; }
            else { EditorUtility.CopySerialized(output, saved); UnityEngine.Object.DestroyImmediate(output); }
            var geometry = GeneratedObjects.GetOrCreate(root, "Geometry", built, Label);
            GeneratedObjects.GetOrAddComponent<MeshFilter>(geometry).sharedMesh = saved;
            GeneratedObjects.GetOrAddComponent<MeshRenderer>(geometry).sharedMaterials = new[]
            {
                ChainMaterial(), AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath("MetalPlate")),
            };
            if (geometry.GetComponent<MeshRenderer>().sharedMaterials.Any(material => material == null))
                throw new InvalidOperationException("Build Material Library before the compound fence.");
            GeneratedObjects.RemoveStale(root, built);
            foreach (string generatedName in built)
            {
                Transform item = root.Find(generatedName);
                item.gameObject.layer = PhysicsLayers.IgnoreRaycast;
                // An open mesh must not become an occlusion wall or a solid GI surface.
                GameObjectUtility.SetStaticEditorFlags(item.gameObject, StaticFlagRules.SeeThrough);
            }
            EditorUtility.SetDirty(saved);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            mesh = null;
            Debug.Log("[World] Compound fence built: 4 runs, 36 panels, 2 open leaves, raised barrier; 6.4 m gate; "
                + saved.triangles.Length / 3 + " triangles; Ignore Raycast blockers.");
        }

        private static void BuildRun(Transform root, HashSet<string> built, string name, Vector3 start, Vector3 end)
        {
            Vector3 direction = (end - start).normalized;
            float length = Vector3.Distance(start, end);
            int panels = Mathf.CeilToInt(length / 3f);
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i <= panels; i++)
            {
                Vector3 foot = Vector3.Lerp(start, end, (float)i / panels);
                foot.y = Ground(foot);
                lowest = Mathf.Min(lowest, foot.y);
                highest = Mathf.Max(highest, foot.y);
                // Gate end posts sit outside the clear opening, rather than reducing its width.
                if (Mathf.Abs(Mathf.Abs(foot.x) - GateHalfWidth) < 0.001f) foot.x += Mathf.Sign(foot.x) * 0.06f;
                Tube(foot - Vector3.up * 0.1f, foot + Vector3.up * 3.5f, 0.06f);
                if (i == panels) continue;
                Vector3 next = Vector3.Lerp(start, end, (float)(i + 1) / panels);
                next.y = Ground(next);
                float width = length / panels;
                Vector3 centre = (foot + next) * 0.5f + Vector3.up * 1.55f;
                Panel(centre, direction * width, Vector3.up * Height, 0);
                Tube(foot + Vector3.up * 3.05f, next + Vector3.up * 3.05f, 0.025f);
                Tube(foot + Vector3.up * 0.05f, next + Vector3.up * 0.05f, 0.025f);
            }
            // A light, deterministic helix replaces the reference's overlapping razor-wire rings.
            int steps = Mathf.CeilToInt(length * 12f);
            Vector3 across = Vector3.Cross(direction, Vector3.up);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(start, end, (float)i / steps);
                float angle = length * i / steps * Mathf.PI * 4f;
                point.y = Ground(point) + 3.75f;
                point += (Vector3.up * Mathf.Cos(angle) + across * Mathf.Sin(angle)) * 0.28f;
                if (i > 0) Tube(previous, point, 0.012f);
                previous = point;
            }
            Vector3 middle = (start + end) * 0.5f;
            middle.y = (lowest + highest + 3.8f) * 0.5f;
            Blocker(root, built, name, middle, new Vector3(0.12f, highest - lowest + 3.8f, length), Quaternion.LookRotation(direction));
        }

        private static void BuildOpenGate(Transform root, HashSet<string> built, int side)
        {
            Vector3 hinge = new Vector3(side * (GateHalfWidth + 0.06f), 10f, 28f);
            Quaternion turn = Quaternion.Euler(0f, -side * 1.9f * Mathf.Rad2Deg, 0f);
            Vector3 span = turn * new Vector3(-side * 3f, 0f, 0f);
            Vector3 centre = hinge + span * 0.5f + Vector3.up * 1.35f;
            Panel(centre, span, Vector3.up * 2.6f, 0);
            Tube(hinge + Vector3.up * 0.08f, hinge + span + Vector3.up * 0.08f, 0.03f);
            Tube(hinge + Vector3.up * 2.65f, hinge + span + Vector3.up * 2.65f, 0.03f);
            Tube(hinge + span + Vector3.up * 0.08f, hinge + span + Vector3.up * 2.65f, 0.03f);
            Blocker(root, built, side < 0 ? "GateWest" : "GateEast", centre, new Vector3(0.08f, 2.6f, 3f), Quaternion.LookRotation(span));
        }

        private static float Ground(Vector3 point) => IslandHeightField.Sample(point.x, point.z);

        private static void Blocker(Transform root, HashSet<string> built, string name, Vector3 centre, Vector3 size, Quaternion rotation)
        {
            GameObject go = GeneratedObjects.GetOrCreate(root, "Blocker_" + name, built, Label);
            go.transform.SetPositionAndRotation(centre, rotation);
            go.transform.localScale = Vector3.one;
            GeneratedObjects.GetOrAddComponent<BoxCollider>(go).size = size;
        }

        private static void Panel(Vector3 centre, Vector3 horizontal, Vector3 vertical, int submesh)
        {
            Vector3 normal = Vector3.Cross(horizontal, vertical).normalized;
            Vector2 uv = new Vector2(horizontal.magnitude, vertical.magnitude) / 0.4f;
            int a = mesh.Add(centre - horizontal * 0.5f - vertical * 0.5f, normal, Vector2.zero);
            int b = mesh.Add(centre + horizontal * 0.5f - vertical * 0.5f, normal, new Vector2(uv.x, 0f));
            int c = mesh.Add(centre + horizontal * 0.5f + vertical * 0.5f, normal, uv);
            int d = mesh.Add(centre - horizontal * 0.5f + vertical * 0.5f, normal, new Vector2(0f, uv.y));
            mesh.Triangle(submesh, a, b, c);
            mesh.Triangle(submesh, a, c, d);
        }

        private static void Tube(Vector3 start, Vector3 end, float radius)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 other = Vector3.Cross(axis, across);
            for (int side = 0; side < 4; side++)
            {
                float a = side * Mathf.PI * 0.5f, b = (side + 1) * Mathf.PI * 0.5f;
                Vector3 offsetA = (across * Mathf.Cos(a) + other * Mathf.Sin(a)) * radius;
                Vector3 offsetB = (across * Mathf.Cos(b) + other * Mathf.Sin(b)) * radius;
                Vector3 centre = (start + end + offsetA + offsetB) * 0.5f;
                Panel(centre, start - end, offsetB - offsetA, 1);
            }
        }

        private static void Box(Vector3 centre, Vector3 size)
        {
            Vector3 x = Vector3.right * size.x, y = Vector3.up * size.y, z = Vector3.forward * size.z;
            Panel(centre + z * 0.5f, x, y, 1); Panel(centre - z * 0.5f, -x, y, 1);
            Panel(centre + x * 0.5f, -z, y, 1); Panel(centre - x * 0.5f, z, y, 1);
            Panel(centre + y * 0.5f, x, -z, 1); Panel(centre - y * 0.5f, x, z, 1);
        }

        private static Material ChainMaterial()
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int a = (x + y) % 64, b = (x - y + 64) % 64;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a < 3 || a > 61 || b < 3 || b > 61 ? 1f : 0f));
                }
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            if (!File.Exists(TexturePath) || !File.ReadAllBytes(TexturePath).SequenceEqual(png))
            {
                File.WriteAllBytes(TexturePath, png);
                AssetDatabase.ImportAsset(TexturePath);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            if (importer.wrapMode != TextureWrapMode.Repeat || importer.alphaIsTransparency == false || importer.mipmapEnabled)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; // preserve narrow cutout wires in this initial 64 px tile
                importer.SaveAndReimport();
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            material.SetColor("_BaseColor", new Color32(176, 180, 180, 255));
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Batch entry: builds twice to check repeatability, rebakes collision, then checks the mission route.
        public static void BuildAndVerify()
        {
            Build();
            string[] paths = { WorldSceneSkeleton.ScenePath, MeshPath, MaterialPath, TexturePath };
            byte[][] before = paths.Select(File.ReadAllBytes).ToArray();
            Build();
            for (int i = 0; i < paths.Length; i++)
                if (!before[i].SequenceEqual(File.ReadAllBytes(paths[i])))
                    throw new InvalidOperationException("Compound fence rebuild changed on repeat: " + paths[i]);
            Physics.SyncTransforms();
            if (Physics.CheckBox(new Vector3(0f, 11.3f, 28f), new Vector3(3.19f, 1.1f, 0.15f), Quaternion.identity,
                1 << PhysicsLayers.IgnoreRaycast, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("The open gate is obstructed.");
            if (Physics.GetIgnoreLayerCollision(PhysicsLayers.Player, PhysicsLayers.IgnoreRaycast))
                throw new InvalidOperationException("Player/fence collisions are disabled in the current physics settings.");
            Debug.Log("[World] Fence verification: repeat scene/mesh/material/texture identical; 6.4 m gate clear; player/fence collisions enabled.");
            NavMeshBaker.BakeNavMesh();
            LayoutValidator.ValidateLayout();
        }
    }
}
