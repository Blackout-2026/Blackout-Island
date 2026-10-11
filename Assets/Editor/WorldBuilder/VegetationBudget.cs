using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>A player's view: eye position and facing (degrees, 0 = north).</summary>
    public readonly struct Viewpoint
    {
        public readonly string Name;
        public readonly Vector3 Eye;
        public readonly float Yaw;

        public Viewpoint(string name, Vector3 eye, float yaw)
        {
            Name = name;
            Eye = eye;
            Yaw = yaw;
        }
    }

    /// <summary>What one view draws: tree instances per LOD, their triangles, detail instances and theirs.</summary>
    public sealed class ViewCount
    {
        public readonly int[] TreesPerLod = new int[VegetationMeshes.LodCount];
        public int TreeTriangles;
        public int Details;
        public int DetailTriangles;

        public int Total => TreeTriangles + DetailTriangles;
    }

    /// <summary>
    /// The visible-triangle budget for vegetation, and a measurement of the painted terrain against it.
    /// Budget: <see cref="VisibleTriangleBudget"/> triangles of trees, rocks, bushes and grass in the main camera's view
    /// from any of the <see cref="Viewpoints"/>, shadow passes not counted. That leaves room in a frame of about
    /// 1.5 million triangles for the shadow passes (the moon shadow draws the nearby trees again), the terrain, the
    /// compound, agents and weapons.
    /// The count follows Unity's own rules on the real data: a tree is drawn if its bounds are in the view and it is
    /// within the terrain's tree distance, at the LOD its screen height selects (its LOD group's size x scale, the
    /// camera's 72 degree FOV and the quality level's LOD bias). Details are counted from Unity's own instance positions
    /// (TerrainData.ComputeDetailInstanceTransforms), a whole 25 m patch at a time when the patch is in view and within
    /// the detail distance, as the terrain draws them; this can overcount a little, never undercount.
    /// </summary>
    public static class VegetationBudget
    {
        public const int VisibleTriangleBudget = 250000;

        public const float EyeHeight = 1.65f; // docs/level-layout.md real-scale rules
        public const float Aspect = 16f / 9f;
        public const float NearClip = 0.08f; // reference section 4: camera near 0.08, far 700
        public const float FarClip = 700f;

        /// <summary>
        /// Five places the player stands: the dock looking up the beach, the forest checkpoint, the middle of the trail
        /// looking along it, the gate looking back over the whole forest, and Hunter-1's spawn in the forest.
        /// </summary>
        public static List<Viewpoint> Viewpoints(List<MarkerSpec> markers)
        {
            // The trail's middle: prototype (4, 44), facing its next control point (-9, -32 in Unity).
            var trailMiddle = new Vector2(4f, -44f);
            float trailYaw = Yaw(new Vector2(-9f, -32f) - trailMiddle);
            return new List<Viewpoint>
            {
                FromMarker(markers, "PlayerStart", "Dock (start)"),
                FromMarker(markers, "Checkpoint_Forest", "Forest checkpoint"),
                new Viewpoint("Trail middle", new Vector3(trailMiddle.x, IslandHeightField.Sample(trailMiddle.x, trailMiddle.y) + EyeHeight, trailMiddle.y), trailYaw),
                FromMarker(markers, "Checkpoint_EscapeForest", "Gate, looking back"),
                FromMarker(markers, "AgentSpawn_Hunter-1", "Hunter-1 spawn"),
            };
        }

        /// <summary>The report for the painted terrain: one row per viewpoint, the worst against the budget.</summary>
        public static string Report(Terrain terrain, List<MarkerSpec> markers)
        {
            var text = new StringBuilder();
            text.Append($"\n  visible vegetation triangles (main camera: FOV {VegetationLibrary.CameraFov:0}, 16:9, eye {EyeHeight} m, "
                + $"LOD bias {QualitySettings.lodBias:0.##} ({QualitySettings.names[QualitySettings.GetQualityLevel()]}); "
                + $"shadows not counted), budget {VisibleTriangleBudget:N0}:");
            int worst = 0;
            string worstName = string.Empty;
            foreach (Viewpoint view in Viewpoints(markers))
            {
                ViewCount count = Measure(terrain, view);
                text.Append($"\n    {view.Name,-20} trees LOD0/1/2 {count.TreesPerLod[0]} / {count.TreesPerLod[1]} / {count.TreesPerLod[2]}"
                    + $" = {count.TreeTriangles:N0} tris; bushes + grass {count.Details} = {count.DetailTriangles:N0} tris;"
                    + $" total {count.Total:N0} ({100f * count.Total / VisibleTriangleBudget:0.0} % of budget)");
                if (count.Total > worst)
                {
                    worst = count.Total;
                    worstName = view.Name;
                }
            }

            text.Append($"\n    worst: {worstName}, {worst:N0} of {VisibleTriangleBudget:N0} "
                + (worst <= VisibleTriangleBudget ? "(within budget)" : "(OVER BUDGET)"));
            return text.ToString();
        }

        public static ViewCount Measure(Terrain terrain, Viewpoint view)
        {
            TerrainData data = terrain.terrainData;
            Vector3 origin = terrain.GetPosition();
            Plane[] frustum = Frustum(view);
            var count = new ViewCount();

            TreePrototype[] prototypes = data.treePrototypes;
            var lodInfo = new LodInfo[prototypes.Length];
            for (int i = 0; i < prototypes.Length; i++)
            {
                lodInfo[i] = new LodInfo(prototypes[i].prefab);
            }

            foreach (TreeInstance tree in data.treeInstances)
            {
                LodInfo info = lodInfo[tree.prototypeIndex];
                Vector3 position = origin + Vector3.Scale(tree.position, data.size);
                if (Vector3.Distance(view.Eye, position) > terrain.treeDistance)
                {
                    continue;
                }

                // Rotation-proof box: the wider horizontal extent on both horizontal axes.
                Vector3 extents = info.Bounds.extents;
                float horizontal = Mathf.Max(extents.x, extents.z) * tree.widthScale;
                var bounds = new Bounds(position + Vector3.Scale(info.Bounds.center, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale)),
                    2f * new Vector3(horizontal, extents.y * tree.heightScale, horizontal));
                if (!GeometryUtility.TestPlanesAABB(frustum, bounds))
                {
                    continue;
                }

                Vector3 reference = position + Vector3.Scale(info.ReferencePoint, new Vector3(tree.widthScale, tree.heightScale, tree.widthScale));
                float size = info.Size * Mathf.Max(tree.widthScale, tree.heightScale);
                int lod = SelectLod(RelativeHeight(size, Vector3.Distance(view.Eye, reference), QualitySettings.lodBias), info.Transitions);
                if (lod < 0)
                {
                    continue;
                }

                count.TreesPerLod[Mathf.Min(lod, count.TreesPerLod.Length - 1)]++;
                count.TreeTriangles += info.Triangles[lod];
            }

            DetailPrototype[] details = data.detailPrototypes;
            int patches = data.detailResolution / data.detailResolutionPerPatch;
            float patchSize = data.size.x / patches;
            for (int layer = 0; layer < details.Length; layer++)
            {
                int triangles = MeshTriangles(details[layer].prototype);
                for (int pz = 0; pz < patches; pz++)
                {
                    for (int px = 0; px < patches; px++)
                    {
                        DetailInstanceTransform[] instances = data.ComputeDetailInstanceTransforms(px, pz, layer, terrain.detailObjectDensity, out Bounds _);
                        if (instances.Length == 0)
                        {
                            continue;
                        }

                        Bounds patch = PatchBounds(instances, origin, px, pz, patchSize);
                        bool near = Mathf.Sqrt(patch.SqrDistance(view.Eye)) <= terrain.detailObjectDistance;
                        if (near && GeometryUtility.TestPlanesAABB(frustum, patch))
                        {
                            count.Details += instances.Length;
                            count.DetailTriangles += instances.Length * triangles;
                        }
                    }
                }
            }

            return count;
        }

        /// <summary>How much of the screen's height an object fills (Unity's LOD rule; LOD bias makes it look bigger).</summary>
        public static float RelativeHeight(float size, float distance, float lodBias)
        {
            return size * lodBias / (2f * Mathf.Max(distance, 1e-3f) * Mathf.Tan(VegetationLibrary.CameraFov * 0.5f * Mathf.Deg2Rad));
        }

        /// <summary>The first LOD whose transition height the object still fills, or -1 when it is culled.</summary>
        public static int SelectLod(float relativeHeight, float[] transitions)
        {
            for (int i = 0; i < transitions.Length; i++)
            {
                if (relativeHeight >= transitions[i])
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The camera's six frustum planes for a viewpoint.</summary>
        public static Plane[] Frustum(Viewpoint view)
        {
            Matrix4x4 projection = Matrix4x4.Perspective(VegetationLibrary.CameraFov, Aspect, NearClip, FarClip);
            // A camera looks down its -Z in view space, while a Unity transform's forward is +Z: flip Z.
            Matrix4x4 worldToCamera = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                * Matrix4x4.TRS(view.Eye, Quaternion.Euler(0f, view.Yaw, 0f), Vector3.one).inverse;
            return GeometryUtility.CalculateFrustumPlanes(projection * worldToCamera);
        }

        // Detail positions are relative to the terrain; the box covers every instance in the patch, plus a metre.
        private static Bounds PatchBounds(DetailInstanceTransform[] instances, Vector3 origin, int px, int pz, float patchSize)
        {
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (DetailInstanceTransform instance in instances)
            {
                minY = Mathf.Min(minY, instance.posY);
                maxY = Mathf.Max(maxY, instance.posY + instance.scaleY);
            }

            var min = new Vector3(origin.x + px * patchSize, origin.y + minY, origin.z + pz * patchSize);
            var max = new Vector3(min.x + patchSize, origin.y + maxY + 1f, min.z + patchSize);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static int MeshTriangles(GameObject prefab)
        {
            int triangles = 0;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    triangles += VegetationPlaceholderBuilder.TriangleCount(filter.sharedMesh);
                }
            }

            return triangles;
        }

        private static Viewpoint FromMarker(List<MarkerSpec> markers, string id, string name)
        {
            MarkerSpec marker = markers.Find(m => m.Name == id);
            return new Viewpoint(name, marker.Position + Vector3.up * EyeHeight, marker.Yaw);
        }

        private static float Yaw(Vector2 direction)
        {
            float yaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            return yaw < 0f ? yaw + 360f : yaw;
        }

        /// <summary>A tree prefab's LOD transitions, triangles per LOD, LOD group size and bounds (local, unscaled).</summary>
        private sealed class LodInfo
        {
            public readonly float[] Transitions;
            public readonly int[] Triangles;
            public readonly float Size;
            public readonly Vector3 ReferencePoint;
            public readonly Bounds Bounds;

            public LodInfo(GameObject prefab)
            {
                Bounds = LocalBounds(prefab);
                var group = prefab.GetComponent<LODGroup>();
                if (group == null)
                {
                    // No LOD group: drawn whole until the terrain's tree distance.
                    Transitions = new[] { 0f };
                    Triangles = new[] { MeshTriangles(prefab) };
                    Size = Mathf.Max(Bounds.size.x, Mathf.Max(Bounds.size.y, Bounds.size.z));
                    ReferencePoint = Bounds.center;
                    return;
                }

                LOD[] lods = group.GetLODs();
                Transitions = new float[lods.Length];
                Triangles = new int[lods.Length];
                for (int i = 0; i < lods.Length; i++)
                {
                    Transitions[i] = lods[i].screenRelativeTransitionHeight;
                    foreach (Renderer renderer in lods[i].renderers)
                    {
                        var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                        if (filter != null && filter.sharedMesh != null)
                        {
                            Triangles[i] += VegetationPlaceholderBuilder.TriangleCount(filter.sharedMesh);
                        }
                    }
                }

                Size = group.size;
                ReferencePoint = group.localReferencePoint;
            }

            private static Bounds LocalBounds(GameObject prefab)
            {
                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
                bool any = false;
                var bounds = new Bounds();
                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                    {
                        continue;
                    }

                    Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                    Bounds mesh = filter.sharedMesh.bounds;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        var local = new Vector3(corner % 2 == 0 ? mesh.min.x : mesh.max.x, corner / 2 % 2 == 0 ? mesh.min.y : mesh.max.y,
                            corner / 4 == 0 ? mesh.min.z : mesh.max.z);
                        Vector3 point = matrix.MultiplyPoint3x4(local);
                        if (!any)
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            any = true;
                        }
                        else
                        {
                            bounds.Encapsulate(point);
                        }
                    }
                }

                return bounds;
            }
        }
    }
}
