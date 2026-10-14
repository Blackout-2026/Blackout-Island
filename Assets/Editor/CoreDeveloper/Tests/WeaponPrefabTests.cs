using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class WeaponPrefabTests
    {
        private PartMaterials materials;

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
            materials = WeaponMaterialBuilder.BuildAll(TestAssetFolders.Materials, TestAssetFolders.Textures);
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFolders.Delete();
        }

        private static IEnumerable<PropEntry> Weapons()
        {
            foreach (PropEntry entry in PropCatalog.All)
            {
                if (entry.Group == PropGroup.Weapon)
                {
                    yield return entry;
                }
            }
        }

        // The same entry as the catalog's, but saved in the scratch folder.
        private GameObject Build(PropEntry real)
        {
            var entry = new PropEntry(real.Name, TestAssetFolders.Prefabs, real.Source, real.Group, real.Pivot, real.Size, real.Count, real.Note);
            ModelPrefabBuilder.Build(entry, WeaponDefinitions.For(real.Name), materials, false, root => WeaponPrefabBuilder.EnsureMuzzleFlash(root, materials));
            return AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
        }

        [Test]
        public void TheThreeWeapons_HaveADefinition()
        {
            int count = 0;
            foreach (PropEntry entry in Weapons())
            {
                Assert.IsNotNull(WeaponDefinitions.For(entry.Name), entry.Name);
                count++;
            }

            Assert.AreEqual(3, count);
            Assert.IsNull(WeaponDefinitions.For("Weapon_Typo_Visual"));
        }

        [Test]
        public void EveryPaintKey_ExistsInTheWeaponMaterials()
        {
            foreach (PropEntry entry in Weapons())
            {
                foreach (PartSpec part in WeaponDefinitions.For(entry.Name).Parts)
                {
                    Assert.IsNotNull(materials.Get(part.Paint), $"{entry.Name}/{part.Name} uses '{part.Paint}'");
                }
            }
        }

        [Test]
        public void TheGuns_ShootAlongPlusZFromThePrototypesMuzzle()
        {
            // weapons.js: muzzle at z -0.33 (SMG) and z -0.81 (sniper) in the gun frame; z flips to +Z in Unity.
            GameObject smg = Build(PropCatalog.Find("Weapon_Smg_Visual"));
            GameObject sniper = Build(PropCatalog.Find("Weapon_Sniper_Visual"));

            Transform smgMuzzle = RobotRig.FindDeep(smg.transform, WeaponDefinitions.MuzzlePoint);
            Transform sniperMuzzle = RobotRig.FindDeep(sniper.transform, WeaponDefinitions.MuzzlePoint);
            Assert.AreEqual(new Vector3(0f, 0.004f, 0.33f), smgMuzzle.localPosition);
            Assert.AreEqual(new Vector3(0f, 0.008f, 0.81f), sniperMuzzle.localPosition);
            Assert.AreEqual(Vector3.forward, smgMuzzle.forward);
        }

        [Test]
        public void TheMuzzle_IsTheFrontmostPointOfEachGun()
        {
            foreach (string name in new[] { "Weapon_Smg_Visual", "Weapon_Sniper_Visual" })
            {
                GameObject prefab = Build(PropCatalog.Find(name));
                Transform muzzle = RobotRig.FindDeep(prefab.transform, WeaponDefinitions.MuzzlePoint);
                Bounds bounds = PropStats.LocalBounds(prefab);

                Assert.AreEqual(bounds.max.z, muzzle.localPosition.z, 0.03f, name + ": the barrel tip is at the muzzle");
            }
        }

        [Test]
        public void TheMuzzleFlash_IsHiddenAndMadeOfThreeCrossedQuads()
        {
            GameObject prefab = Build(PropCatalog.Find("Weapon_Smg_Visual"));

            Transform flash = RobotRig.FindDeep(prefab.transform, WeaponPrefabBuilder.MuzzleFlash);

            Assert.IsNotNull(flash);
            Assert.IsFalse(flash.gameObject.activeSelf);
            Assert.AreEqual(3, flash.childCount);
            Assert.AreEqual(materials.Get("Flash"), flash.GetChild(0).GetComponent<Renderer>().sharedMaterial);
            Assert.AreEqual(0.22f, flash.GetChild(0).localScale.x);
            Assert.AreEqual(0.154f, flash.GetChild(2).localScale.x, 0.0001f); // the flat one is 0.7 times as big
            Assert.IsFalse(flash.IsChildOf(prefab.transform.Find("Model")), "the flash belongs to the muzzle, not to the model that can be swapped");
        }

        [Test]
        public void TheKnife_HasNoMuzzleAndNoFlash()
        {
            GameObject knife = Build(PropCatalog.Find("Weapon_Knife_Visual"));

            Assert.IsNull(RobotRig.FindDeep(knife.transform, WeaponDefinitions.MuzzlePoint));
            Assert.IsNull(RobotRig.FindDeep(knife.transform, WeaponPrefabBuilder.MuzzleFlash));
        }

        [Test]
        public void TheMovingParts_AreOwnObjectsWithTheirPivots()
        {
            GameObject sniper = Build(PropCatalog.Find("Weapon_Sniper_Visual"));
            GameObject knife = Build(PropCatalog.Find("Weapon_Knife_Visual"));

            Transform bolt = RobotRig.FindDeep(sniper.transform, WeaponDefinitions.BoltHandle);
            Assert.IsNotNull(bolt);
            Assert.AreEqual(2, bolt.childCount); // shaft and knob
            Assert.AreEqual(new Vector3(0.03f, 0.01f, -0.08f), bolt.localPosition); // weapons.js: (0.03, 0.01, 0.08), z flipped

            Transform hand = RobotRig.FindDeep(knife.transform, WeaponDefinitions.Hand);
            Assert.IsNotNull(hand);
            Assert.Greater(hand.GetComponentsInChildren<MeshFilter>().Length, 8, "the whole knife and its arm hang off the swinging hand");
        }

        [Test]
        public void EveryViewmodel_HasHandsAndSleevesAndNoColliders()
        {
            foreach (PropEntry entry in Weapons())
            {
                GameObject prefab = Build(entry);
                Assert.AreEqual(0, PropStats.Colliders(prefab), entry.Name);
                Assert.IsNotNull(RobotRig.FindDeep(prefab.transform, "GloveR") ?? RobotRig.FindDeep(prefab.transform, "GloveL"), entry.Name + " has a glove");
                Assert.IsNotNull(RobotRig.FindDeep(prefab.transform, "SleeveR") ?? RobotRig.FindDeep(prefab.transform, "SleeveL"), entry.Name + " has a sleeve");
            }
        }

        [Test]
        public void EveryViewmodel_IsUnderTheWeaponTriangleTarget()
        {
            foreach (PropEntry entry in Weapons())
            {
                int triangles = PropStats.Triangles(Build(entry));
                Assert.LessOrEqual(triangles, entry.Budget.TargetTriangles, $"{entry.Name}: {triangles} triangles"); // art-pipeline.md section 6: 2,000
            }
        }

        [Test]
        public void TheHeldPositions_AreThePrototypesWithZFlipped()
        {
            Assert.AreEqual(new Vector3(0.13f, -0.125f, 0.45f), WeaponDefinitions.SmgHeldPosition);
            Assert.AreEqual(new Vector3(0.12f, -0.12f, 0.46f), WeaponDefinitions.SniperHeldPosition);
            Assert.AreEqual(new Vector3(0.17f, -0.18f, 0.33f), WeaponDefinitions.KnifeHeldPosition);
        }

        [Test]
        public void RunningAgain_DoesNotAddASecondFlash()
        {
            PropEntry real = PropCatalog.Find("Weapon_Smg_Visual");
            Build(real);

            GameObject prefab = Build(real);

            int flashes = 0;
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == WeaponPrefabBuilder.MuzzleFlash)
                {
                    flashes++;
                }
            }

            Assert.AreEqual(1, flashes);
        }
    }
}
