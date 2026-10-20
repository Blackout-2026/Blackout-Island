using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Checks the real SM_Weapon_SniperHeavy.fbx (step C9, the heavy anti-materiel rifle): the muzzle distance, the length, the
    /// weapon budget and the material. Skipped (not failed) while the FBX does not exist.
    /// </summary>
    public class SniperHeavyFbxTests
    {
        private const string FbxPath = "Assets/Models/Custom/SM_Weapon_SniperHeavy.fbx";
        private GameObject instance;

        [SetUp]
        public void SetUp()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Assert.Ignore($"{FbxPath} does not exist yet.");
            }

            instance = Object.Instantiate(fbx);
        }

        [TearDown]
        public void TearDown()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
            }
        }

        private Bounds ModelBounds()
        {
            Bounds bounds = default;
            bool first = true;
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        [Test]
        public void Muzzle_IsAt0Point81MetresAndTheRifleIsAboutAMetreAndAHalfLong()
        {
            Bounds bounds = ModelBounds();

            Assert.AreEqual(0.81f, bounds.max.z, 0.01f, "The muzzle brake is the frontmost point, 0.81 m from the receiver centre.");
            Assert.That(bounds.size.z, Is.InRange(1.40f, 1.52f), "A .50 calibre rifle is about 1.45 m long.");
            Assert.That(bounds.size.x, Is.InRange(0.2f, 0.4f), "Slim: the bipod feet are the widest part.");
            Assert.AreEqual(0f, bounds.center.x, 0.02f, "Symmetric left to right.");
        }

        [Test]
        public void Budget_StaysInsideTheWeaponBudgetWithOneMaterial()
        {
            int triangles = 0;
            var materials = new HashSet<string>();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                triangles += MeshStats.CountTriangles(filter.sharedMesh);
                Assert.IsTrue(filter.sharedMesh.uv.Length > 0, filter.name + " has UV0.");
            }

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    materials.Add(material.name);
                }
            }

            Assert.That(triangles, Is.LessThanOrEqualTo(6000), "Target of docs/art-pipeline.md section 6 for SM_Weapon_.");
            Assert.AreEqual(1, materials.Count, "The three palette slots are collapsed into one baked material.");
            Assert.That(materials, Has.Member("M_Weapon_SniperHeavy"));
        }

        [Test]
        public void ModelIsOneUpright_RotationZeroObject()
        {
            Assert.That(Quaternion.Angle(Quaternion.identity, instance.transform.rotation), Is.LessThan(0.1f));
            MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(1, renderers.Length, "One static mesh, no hierarchy (Apply Transform on).");
        }
    }
}
