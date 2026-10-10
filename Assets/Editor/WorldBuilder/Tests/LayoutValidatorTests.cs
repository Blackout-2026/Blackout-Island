using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class LayoutValidatorTests
    {
        private static readonly Vector3 DoorCentre = new Vector3(1000f, -500f, 1000f); // far from the island's colliders
        private const float JambWidth = 2f;

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
            {
                Object.DestroyImmediate(go);
            }

            created.Clear();
        }

        [Test]
        public void Format_ListsEveryCheckWithItsResult()
        {
            var checks = new List<LayoutCheck>
            {
                new LayoutCheck("On NavMesh", "PlayerStart", "0.05 m", "<= 1.0 m", true),
                new LayoutCheck("Door gap", "DoorSocket_D-Lab", "1.10 m", ">= 1.2 m", false),
            };

            string[] lines = LayoutReport.Format(checks).TrimEnd().Split('\n');

            Assert.That(lines[0], Does.StartWith("[World] Validate Layout: FAIL, 1 of 2 checks passed."));
            Assert.That(lines.Length, Is.EqualTo(5), "summary, header, rule and one line per check");
            Assert.That(lines[3], Does.Contain("PlayerStart").And.Contain("PASS"));
            Assert.That(lines[4], Does.Contain("DoorSocket_D-Lab").And.Contain("FAIL"));
        }

        [Test]
        public void Format_LinesUpTheColumns()
        {
            var checks = new List<LayoutCheck>
            {
                new LayoutCheck("NavMesh", "NavMesh", "EnvironmentNavMesh.asset, 1 triangles", "baked", true),
                new LayoutCheck("Path", "PlayerStart -> CureSocket", "300.0 m", "complete", true),
            };

            string[] lines = LayoutReport.Format(checks).TrimEnd().Split('\n');
            int resultColumn = lines[1].LastIndexOf('|');

            Assert.That(lines[3].LastIndexOf('|'), Is.EqualTo(resultColumn));
            Assert.That(lines[4].LastIndexOf('|'), Is.EqualTo(resultColumn));
        }

        [Test]
        public void AllPassed_NeedsAtLeastOneCheck()
        {
            Assert.That(LayoutReport.AllPassed(new List<LayoutCheck>()), Is.False);
            Assert.That(LayoutReport.AllPassed(new List<LayoutCheck> { new LayoutCheck("a", "b", "c", "d", true) }), Is.True);
        }

        [Test]
        public void PathLength_AddsUpTheLegs()
        {
            var corners = new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) };

            Assert.That(LayoutReport.PathLength(corners), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(LayoutReport.PathLength(new Vector3[0]), Is.EqualTo(0f));
        }

        [Test]
        public void MeasureGap_ReturnsTheWidthBetweenTheJambs()
        {
            GameObject west = Jamb(-1);
            Jamb(1);

            float width = LayoutValidator.MeasureGap(DoorCentre, Vector3.right, west.scene);

            Assert.That(width, Is.EqualTo(1.4f).Within(0.001f));
        }

        [Test]
        public void MeasureGap_IsInfiniteWhenAJambIsMissing()
        {
            GameObject west = Jamb(-1);

            float width = LayoutValidator.MeasureGap(DoorCentre, Vector3.right, west.scene);

            Assert.That(width, Is.EqualTo(float.PositiveInfinity));
        }

        /// <summary>A wall piece 2 m long, 3 m tall and 0.4 m thick beside a 1.4 m gap at the door centre (side -1 or 1).</summary>
        private GameObject Jamb(int side)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = DoorCentre + new Vector3(side * (0.7f + JambWidth * 0.5f), 1.5f, 0f);
            go.transform.localScale = new Vector3(JambWidth, 3f, 0.4f);
            created.Add(go);
            return go;
        }
    }
}
