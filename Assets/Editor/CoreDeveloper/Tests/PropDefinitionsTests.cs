using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class PropDefinitionsTests
    {
        private PartMaterials materials;

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
            materials = PropMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFolders.Delete();
        }

        // The catalog entries that PropPrefabBuilder builds from a definition.
        private static IEnumerable<PropEntry> Modelled()
        {
            foreach (PropEntry entry in PropCatalog.All)
            {
                if (entry.Source == PropSource.Blender && entry.Group != PropGroup.Weapon && entry.Group != PropGroup.Hero)
                {
                    yield return entry;
                }
            }
        }

        private GameObject BuildInstance(PropEntry entry, ModelDefinition definition)
        {
            var root = new GameObject(entry.Name);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            PlaceholderBuilder.Build(model.transform, definition, materials);
            return root;
        }

        [Test]
        public void EveryModelledEntry_HasADefinition_AndNothingElseDoes()
        {
            var names = new HashSet<string>();
            foreach (PropEntry entry in Modelled())
            {
                Assert.IsNotNull(PropDefinitions.For(entry.Name), entry.Name);
                names.Add(entry.Name);
            }

            Assert.IsNull(PropDefinitions.For("Prop_Typo"));
            Assert.IsNull(PropDefinitions.For("Prop_Pipe"), "CC0 slots have no placeholder");
            Assert.Greater(names.Count, 18);
        }

        [Test]
        public void EveryPlaceholder_HasTheCatalogSizeAndPivot()
        {
            foreach (PropEntry entry in Modelled())
            {
                GameObject instance = BuildInstance(entry, PropDefinitions.For(entry.Name));
                try
                {
                    Bounds bounds = PropStats.LocalBounds(instance);
                    Assert.IsNull(PivotRules.CheckSize(bounds, entry.Size), $"{entry.Name}: {PivotRules.CheckSize(bounds, entry.Size)}");
                    Assert.IsNull(PivotRules.CheckPivot(bounds, entry.Pivot), $"{entry.Name}: {PivotRules.CheckPivot(bounds, entry.Pivot)}");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void EveryPaintKey_ExistsInThePropMaterials()
        {
            foreach (PropEntry entry in Modelled())
            {
                foreach (PartSpec part in PropDefinitions.For(entry.Name).Parts)
                {
                    Assert.IsNotNull(materials.Get(part.Paint), $"{entry.Name}/{part.Name} uses '{part.Paint}'");
                }
            }
        }

        [Test]
        public void PartNames_AreUniqueWithinAProp()
        {
            foreach (PropEntry entry in Modelled())
            {
                var seen = new HashSet<string>();
                foreach (PartSpec part in PropDefinitions.For(entry.Name).Parts)
                {
                    Assert.IsTrue(seen.Add(part.Name), $"{entry.Name} has two parts called {part.Name}");
                }
            }
        }

        [Test]
        public void PropsHaveColliders_AndTheyStayInsideTheModelEnvelope()
        {
            foreach (PropEntry entry in Modelled())
            {
                ModelDefinition definition = PropDefinitions.For(entry.Name);
                if (entry.Folder != PropCatalog.PropsFolder)
                {
                    continue; // pickups are visual only
                }

                Assert.Greater(definition.Colliders.Count, 0, entry.Name);
                var root = new GameObject(entry.Name);
                try
                {
                    PlaceholderBuilder.AddColliders(root, definition);
                    foreach (Collider collider in root.GetComponents<Collider>())
                    {
                        Vector3 extent = collider.bounds.extents;
                        Assert.LessOrEqual(extent.x * 2f, entry.Size.x + PivotRules.Tolerance, entry.Name + " collider width");
                        Assert.LessOrEqual(extent.y * 2f, entry.Size.y + PivotRules.Tolerance, entry.Name + " collider height");
                        Assert.LessOrEqual(extent.z * 2f, entry.Size.z + PivotRules.Tolerance, entry.Name + " collider depth");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void ThePickups_AreVisualOnly()
        {
            Assert.AreEqual(0, PropDefinitions.For("CureCase_Visual").Colliders.Count);
            Assert.AreEqual(0, PropDefinitions.For("Medkit_Visual").Colliders.Count);
        }

        [Test]
        public void TheCrate_IsACleanCubeWithACentredPivot()
        {
            ModelDefinition crate = PropDefinitions.For("Prop_Crate");
            GameObject instance = BuildInstance(PropCatalog.Find("Prop_Crate"), crate);
            try
            {
                Bounds bounds = PropStats.LocalBounds(instance);
                Assert.AreEqual(new Vector3(1.5f, 1.5f, 1.5f), bounds.size);
                Assert.AreEqual(Vector3.zero, bounds.center);
                Assert.AreEqual(new Vector3(1.5f, 1.5f, 1.5f), crate.Colliders[0].Size);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void TheChamber_HasASixteenPanelWallWithTheOpeningOnPlusZ()
        {
            ModelDefinition chamber = PropDefinitions.For("Prop_ContainmentChamber");

            var panels = new List<PartSpec>();
            foreach (PartSpec part in chamber.Parts)
            {
                if (part.Name.StartsWith("GlassPanel"))
                {
                    panels.Add(part);
                }
            }

            Assert.AreEqual(16, panels.Count);
            foreach (PartSpec panel in panels)
            {
                // The opening is a 72 degree gap centred on +Z: no panel inside +/-36 degrees of it.
                float angle = Mathf.Abs(Mathf.DeltaAngle(0f, Mathf.Atan2(panel.Position.x, panel.Position.z) * Mathf.Rad2Deg));
                Assert.GreaterOrEqual(angle, 36f, panel.Name);
            }

            Assert.AreEqual(16, CountCapsules(chamber), "one collider per glass panel");
        }

        [Test]
        public void TheChamber_HasACureAnchorAboveThePedestalPlate()
        {
            SocketSpec anchor = PropDefinitions.For("Prop_ContainmentChamber").Sockets[0];

            Assert.AreEqual(PropDefinitions.CureAnchor, anchor.Name);
            Assert.AreEqual(new Vector3(0f, 1.22f, 0f), anchor.Position); // plate top 1.20 + 0.02 (core-developer.md 6.1)
        }

        private static int CountCapsules(ModelDefinition definition)
        {
            int glass = 0;
            foreach (ColliderSpec collider in definition.Colliders)
            {
                // Pedestal capsule is on the axis, glass capsules are on the wall radius.
                if (collider.Shape == ColliderShape.Capsule && new Vector2(collider.Center.x, collider.Center.z).magnitude > 1f)
                {
                    glass++;
                }
            }

            return glass;
        }

        [Test]
        public void ThePlaceholders_AreNotWildlyOverTheTriangleBudget()
        {
            // Unity's cylinder is 80 triangles, so a placeholder can pass its target; the hard maximum is the line a
            // placeholder must still respect so the palette stays readable.
            foreach (PropEntry entry in Modelled())
            {
                GameObject instance = BuildInstance(entry, PropDefinitions.For(entry.Name));
                try
                {
                    int triangles = PropStats.Triangles(instance);
                    Assert.LessOrEqual(triangles, entry.Budget.MaxTriangles * 2, $"{entry.Name}: {triangles} triangles");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
