using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// The footbridge dock (step C12): the plan made from the World Builder's greybox dock (a synthetic copy of what BeachGreyboxBuilder builds:
    /// deck x -1.4 .. 1.4, z -128 .. -97, top at y 1.3, posts every 3 m from z -98, rails in 3 m panels, the east rail open 4.5 m either side
    /// of the boat at z -122.5), and the three SM_Dock_* models.
    /// </summary>
    public class DockFootbridgeTests
    {
        private const float DeckY = 1.3f;
        private const float BoatZ = -122.5f;

        private static List<GreyPart> GreyDock(string prefix = "")
        {
            var parts = new List<GreyPart>
            {
                new GreyPart(prefix + "Deck", new Vector3(0f, DeckY - 0.175f, -112.5f), new Vector3(2.8f, 0.35f, 31f)),
            };
            for (int index = 0; index <= 10; index++)
            {
                float z = -98f - 3f * index;
                bool seaEnd = index == 10;
                bool bollard = !seaEnd && !HasEastRail(z + 1.5f) && !HasEastRail(z - 1.5f);
                parts.Add(new GreyPart(prefix + $"Post_W{index:00}", new Vector3(-1.5f, (-4f + DeckY + 0.9f) * 0.5f, z), new Vector3(0.3f, DeckY + 0.9f + 4f, 0.3f)));
                float eastTop = DeckY + (bollard ? 0.25f : 0.9f);
                parts.Add(new GreyPart(prefix + $"Post_E{index:00}", new Vector3(1.5f, (-4f + eastTop) * 0.5f, z), new Vector3(0.3f, eastTop + 4f, 0.3f)));
                if (seaEnd)
                {
                    continue;
                }

                float railZ = z - 1.5f;
                parts.Add(new GreyPart(prefix + $"Rail_W{index:00}", new Vector3(-1.5f, DeckY + 0.85f, railZ), new Vector3(0.1f, 0.1f, 3f)));
                if (HasEastRail(railZ))
                {
                    parts.Add(new GreyPart(prefix + $"Rail_E{index:00}", new Vector3(1.5f, DeckY + 0.85f, railZ), new Vector3(0.1f, 0.1f, 3f)));
                }
            }

            parts.Add(new GreyPart(prefix + "Rail_End", new Vector3(0f, DeckY + 0.85f, -128f), new Vector3(3f, 0.1f, 0.1f)));
            return parts;
        }

        private static bool HasEastRail(float railZ)
        {
            return Mathf.Abs(railZ - BoatZ) >= 4.5f;
        }

        [Test]
        public void Plan_PutsTheDeckOnTheGreyboxDeckTop()
        {
            DockPlan plan = DockFootbridgePlan.Create(GreyDock());

            Assert.IsTrue(plan.HasDeck);
            Assert.That(plan.DeckPosition.y, Is.EqualTo(DeckY).Within(0.001f));
            Assert.That(plan.DeckPosition.z, Is.EqualTo(-112.5f).Within(0.001f));
            Assert.That(plan.DeckLength, Is.EqualTo(31f).Within(0.001f));
        }

        [Test]
        public void Plan_HasAPanelWhereverThereIsARail_AndNoneAtTheBoatGap()
        {
            DockPlan plan = DockFootbridgePlan.Create(GreyDock());

            int west = 0, east = 0, ends = 0;
            foreach (DockPlacement panel in plan.Panels)
            {
                Assert.That(panel.Position.y, Is.EqualTo(DeckY).Within(0.001f), panel.Name);
                if (panel.Name == "Rail_End")
                {
                    ends++;
                    Assert.That(panel.Yaw, Is.EqualTo(90f));
                }
                else if (panel.Name.StartsWith("Rail_W"))
                {
                    west++;
                }
                else
                {
                    east++;
                    Assert.That(Mathf.Abs(panel.Position.z - BoatZ), Is.GreaterThanOrEqualTo(4.5f), "no east panel at the boat");
                }
            }

            Assert.AreEqual(10, west);
            Assert.AreEqual(7, east);
            Assert.AreEqual(1, ends);
        }

        [Test]
        public void Plan_LeavesTheMooringBollardsAlone()
        {
            DockPlan plan = DockFootbridgePlan.Create(GreyDock());

            // 11 west posts, and east posts except the two bollards beside the boat (z -122 and -125)
            Assert.AreEqual(11 + 9, plan.Posts.Count);
            foreach (DockPlacement post in plan.Posts)
            {
                Assert.That(post.Name, Is.Not.EqualTo("Post_E08").And.Not.EqualTo("Post_E09"), "bollards keep their greybox look");
            }

            Assert.IsFalse(plan.Hide.Contains("Post_E08"));
            Assert.IsFalse(plan.Hide.Contains("Post_E09"));
            Assert.IsTrue(plan.Hide.Contains("Deck"));
            Assert.IsTrue(plan.Hide.Contains("Rail_End"));
        }

        [Test]
        public void Plan_HidesExactlyWhatItReplaces()
        {
            DockPlan plan = DockFootbridgePlan.Create(GreyDock());

            Assert.AreEqual(1 + plan.Panels.Count + plan.Posts.Count, plan.Hide.Count);
            Assert.AreEqual(new HashSet<string>(plan.Hide).Count, plan.Hide.Count, "no name twice");
        }

        [Test]
        public void Plan_WorksWithTheScenesDockPrefix()
        {
            // the Environment scene names the dock's objects Dock_Deck, Dock_Post_W00, Dock_Rail_E03 ...
            DockPlan plan = DockFootbridgePlan.Create(GreyDock("Dock_"));
            DockPlan plain = DockFootbridgePlan.Create(GreyDock());

            Assert.IsTrue(plan.HasDeck);
            Assert.AreEqual(plain.Panels.Count, plan.Panels.Count);
            Assert.AreEqual(plain.Posts.Count, plan.Posts.Count);
            Assert.AreEqual(plain.Hide.Count, plan.Hide.Count);
            Assert.IsTrue(plan.Hide.Contains("Dock_Deck"), "hides the object by its real name");
            Assert.IsFalse(plan.Hide.Contains("Dock_Post_E08"), "the bollards are still left alone");
        }

        [Test]
        public void Plan_WithoutADeck_ChangesNothing()
        {
            DockPlan plan = DockFootbridgePlan.Create(new List<GreyPart> { new GreyPart("Post_W00", Vector3.zero, Vector3.one) });

            Assert.IsFalse(plan.HasDeck);
            Assert.AreEqual(0, plan.Hide.Count);
            Assert.AreEqual(0, plan.Panels.Count);
        }

        private static Bounds ModelBounds(string piece)
        {
            DockFootbridgeBuilder.Piece entry = DockFootbridgeBuilder.Find(piece);
            Assert.IsNotNull(entry, piece);
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
            if (fbx == null)
            {
                Assert.Ignore($"{entry.FbxPath} does not exist yet.");
            }

            var instance = Object.Instantiate(fbx);
            try
            {
                var bounds = new Bounds();
                bool first = true;
                foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
                {
                    Bounds b = filter.sharedMesh.bounds;
                    if (first)
                    {
                        bounds = b;
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(b);
                    }
                }

                Assert.IsFalse(first, "the model has a mesh");
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void DeckModel_Is2p8By31WithItsTopAtZero()
        {
            Bounds b = ModelBounds("Deck");

            Assert.That(b.size.x, Is.EqualTo(2.8f).Within(0.02f));
            Assert.That(b.size.z, Is.EqualTo(31f).Within(0.05f));
            Assert.That(b.max.y, Is.InRange(0f, 0.1f), "the top face is at the origin height");
            Assert.That(b.center.z, Is.EqualTo(0f).Within(0.05f));
        }

        [Test]
        public void RailPanelModel_Is3mLongAndAbout0p9High()
        {
            Bounds b = ModelBounds("RailPanel");

            Assert.That(b.size.z, Is.InRange(2.98f, 3.1f));
            Assert.That(b.max.y, Is.InRange(0.85f, 1.0f), "the handrail top");
            Assert.That(b.min.y, Is.GreaterThanOrEqualTo(0f).And.LessThan(0.3f), "stands on the deck");
        }

        [Test]
        public void RailPostModel_StandsOnAPileThatGoesDown()
        {
            Bounds b = ModelBounds("RailPost");

            Assert.That(b.max.y, Is.InRange(0.95f, 1.1f));
            Assert.That(b.min.y, Is.LessThan(-1.0f), "the pile reaches below the water");
        }
    }
}
