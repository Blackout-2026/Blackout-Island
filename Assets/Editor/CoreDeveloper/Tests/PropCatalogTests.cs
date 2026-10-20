using System.Collections.Generic;
using NUnit.Framework;

namespace Blackout.Editor.Tests
{
    public class PropCatalogTests
    {
        [Test]
        public void Names_AreUnique()
        {
            var seen = new HashSet<string>();
            foreach (PropEntry entry in PropCatalog.All)
            {
                Assert.IsTrue(seen.Add(entry.Name), "Duplicate: " + entry.Name);
            }
        }

        [Test]
        public void PropPrefabs_FollowTheNamingRules()
        {
            foreach (PropEntry entry in PropCatalog.All)
            {
                if (entry.Folder == PropCatalog.PropsFolder)
                {
                    StringAssert.StartsWith("Prop_", entry.Name);
                }
                else
                {
                    Assert.AreEqual(PropCatalog.ModelsFolder, entry.Folder, entry.Name);
                    StringAssert.EndsWith("_Visual", entry.Name);
                }

                Assert.AreEqual($"{entry.Folder}/{entry.Name}.prefab", entry.PrefabPath);
            }
        }

        [Test]
        public void EveryEntry_HasASizeAndACount()
        {
            foreach (PropEntry entry in PropCatalog.All)
            {
                Assert.Greater(entry.Size.x, 0f, entry.Name);
                Assert.Greater(entry.Size.y, 0f, entry.Name);
                Assert.Greater(entry.Size.z, 0f, entry.Name);
                Assert.Greater(entry.Count, 0, entry.Name);
            }
        }

        [Test]
        public void TheStepBrief_IsCovered()
        {
            // Playbook C4: modelled in Blender are the cure chamber, lab table, crate, monitor, desk and barrel;
            // taken from CC0 packs are pipes, cables, signs and vents (signs are the decal tool's, step 3).
            foreach (string name in new[] { "ContainmentChamber", "LabBench", "Crate", "Monitor", "Desk", "Barrel" })
            {
                PropEntry entry = PropCatalog.Find("Prop_" + name);
                Assert.IsNotNull(entry, name);
                Assert.AreEqual(PropSource.Blender, entry.Source, name);
            }

            foreach (string name in new[] { "PipeStraight", "CableTray", "Vent" })
            {
                Assert.AreEqual(PropSource.Cc0Kit, PropCatalog.Find("Prop_" + name).Source, name);
            }
        }

        [Test]
        public void PivotRules_FollowArtPipelineSection5()
        {
            Assert.AreEqual(PropPivot.Centre, PropCatalog.Find("Prop_Crate").Pivot); // pushed by physics
            Assert.AreEqual(PropPivot.WallFace, PropCatalog.Find("Prop_WallMonitor").Pivot);
            Assert.AreEqual(PropPivot.BaseCentre, PropCatalog.Find("Prop_LabBench").Pivot);
            Assert.AreEqual(PropPivot.BaseCentre, PropCatalog.Find("CureCase_Visual").Pivot);
            Assert.AreEqual(PropPivot.Grip, PropCatalog.Find("Weapon_Smg_Visual").Pivot);
        }

        [Test]
        public void TheChamber_FitsTheThreeMetreLabCeiling()
        {
            // docs/level-layout.md: the lab ceiling is 3.0 m and the chamber must be under 2.9 m including its base.
            Assert.Less(PropCatalog.Find("Prop_ContainmentChamber").Size.y, 2.9f);
        }

        [Test]
        public void SizeClass_PicksTheBudget()
        {
            Assert.AreEqual(PropSize.Large, PropCatalog.Find("Prop_Locker").SizeClass); // 2.0 m tall
            Assert.AreEqual(PropSize.Medium, PropCatalog.Find("Prop_Crate").SizeClass);
            Assert.AreEqual(PropSize.Small, PropCatalog.Find("Medkit_Visual").SizeClass);
            Assert.AreEqual(3000, PropCatalog.Find("Prop_LabBench").Budget.TargetTriangles); // 3.5 m long: a Large prop
            Assert.AreEqual(6000, PropCatalog.Find("Weapon_Smg_Visual").Budget.TargetTriangles);
            Assert.AreEqual(10000, PropCatalog.Find("Generator_Visual").Budget.TargetTriangles);
            Assert.AreEqual(5000, PropCatalog.Find("SecurityRobot_Visual").Budget.TargetTriangles);
        }

        [Test]
        public void Find_ReturnsNothingForAnUnknownName()
        {
            Assert.IsNull(PropCatalog.Find("Prop_Typo"));
        }
    }
}
