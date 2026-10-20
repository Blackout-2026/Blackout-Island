// Owner: Systems Engineer (Hunter AI).
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The Hunter's cover on the real island (docs/reference/systems-engineer.md section 15): Environment.unity is
    /// opened additively and closed again without saving. Trees, palms and rocks are terrain trees; bushes are terrain
    /// details, read from the detail layer. The World Builder's numbers are in docs/level-layout.md (W6).
    /// </summary>
    public class HunterCoverLevelTests
    {
        private const string EnvironmentPath = "Assets/Scenes/Environment.unity";
        // 431 pines + 63 palms + 163 rocks, and 376 bushes (docs/level-layout.md, "Terrain painting and vegetation").
        private const int PlantedTrees = 431 + 63 + 163;
        private const int PlantedBushes = 376;

        private Scene environment;
        private Terrain terrain;

        [OneTimeSetUp]
        public void OpenLevel()
        {
            environment = EditorSceneManager.OpenScene(EnvironmentPath, OpenSceneMode.Additive);
            foreach (GameObject root in environment.GetRootGameObjects())
            {
                terrain = terrain != null ? terrain : root.GetComponentInChildren<Terrain>();
            }

            Assert.IsNotNull(terrain, "Environment has a terrain");
        }

        [OneTimeTearDown]
        public void CloseLevel()
        {
            if (environment.IsValid() && environment.isLoaded)
            {
                EditorSceneManager.CloseScene(environment, true);
            }
        }

        [Test]
        public void Trees_AreAllTheWorldBuildersTrees()
        {
            Assert.AreEqual(PlantedTrees, terrain.terrainData.treeInstanceCount);
        }

        [Test]
        public void Bushes_AreFound_OnePerOccupiedCell()
        {
            int bushes = HunterWorld.Bushes(terrain).Count;

            Debug.Log($"[Blackout] Hunter cover on the real island: {PlantedTrees} trees and rocks, {bushes} bush cells");
            Assert.That(bushes, Is.InRange(PlantedBushes * 9 / 10, PlantedBushes),
                "one cover per 0.78 m cell holding a bush (two bushes may share a cell)");
        }

        /// <summary>
        /// The World Builder keeps every bush 6 m from the Hunters' route points, so a bush inside that ring would
        /// mean the cell-to-world conversion is wrong (axes swapped or the origin off).
        /// </summary>
        [Test]
        public void Bushes_RespectTheHunterClearings()
        {
            var routePoints = new List<Vector3>();
            foreach (Marker marker in Object.FindObjectsByType<Marker>(FindObjectsSortMode.None))
            {
                if (marker.Type == MarkerType.Waypoint && marker.Owner != null && marker.Owner.StartsWith("Hunter-"))
                {
                    routePoints.Add(marker.transform.position);
                }
            }

            Assert.AreEqual(12, routePoints.Count, "6 waypoints for each Hunter");
            float nearest = float.MaxValue;
            foreach (Vector3 bush in HunterWorld.Bushes(terrain))
            {
                foreach (Vector3 point in routePoints)
                {
                    nearest = Mathf.Min(nearest, CoverMath.FlatDistance(bush, point));
                }
            }

            // 6 m clearing, less half a 0.78 m cell's diagonal for the cell centre.
            Assert.GreaterOrEqual(nearest, 6f - 0.56f, $"nearest bush to a Hunter route point: {nearest:0.00} m");
        }

        [Test]
        public void Bushes_SitOnTheGround()
        {
            foreach (Vector3 bush in HunterWorld.Bushes(terrain))
            {
                float ground = terrain.transform.position.y + terrain.SampleHeight(bush);
                Assert.AreEqual(ground, bush.y, 1e-3f);
                Assert.GreaterOrEqual(bush.y, 1.6f - 0.1f, "bushes grow on ground at least 1.6 m high");
            }
        }

        [Test]
        public void DetailCellCentre_IsTheMiddleOfTheCell()
        {
            Vector3 corner = HunterWorld.DetailCellCentre(new Vector3(-200f, -8f, -180f), new Vector3(400f, 32f, 400f),
                512, 512, 0, 0);
            Vector3 last = HunterWorld.DetailCellCentre(new Vector3(-200f, -8f, -180f), new Vector3(400f, 32f, 400f),
                512, 512, 511, 511);

            Assert.AreEqual(-200f + 0.390625f, corner.x, 1e-4f);
            Assert.AreEqual(-180f + 0.390625f, corner.z, 1e-4f);
            Assert.AreEqual(200f - 0.390625f, last.x, 1e-4f);
            Assert.AreEqual(220f - 0.390625f, last.z, 1e-4f);
        }
    }
}
