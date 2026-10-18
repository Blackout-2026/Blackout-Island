using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    public class VegetationLodTunerTests
    {
        private GameObject root;
        private LODGroup group;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PalmLodTest");
            group = root.AddComponent<LODGroup>();
            float[] heights = { 0.25f, 0.10f, 0.02f };
            var levels = new LOD[3];
            for (int i = 0; i < levels.Length; i++)
            {
                GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                child.transform.SetParent(root.transform, false);
                levels[i] = new LOD(heights[i], new[] { child.GetComponent<Renderer>() }) { fadeTransitionWidth = 0.1f };
            }
            group.SetLODs(levels);
            group.size = 8f;
            group.localReferencePoint = new Vector3(1f, 4f, 0f);
            root.AddComponent<CapsuleCollider>().radius = 0.25f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void ApplyChangesOnlyMiddleHeightAndRepeatedApplyDoesNothing()
        {
            LOD[] before = group.GetLODs();
            string colliderBefore = EditorJsonUtility.ToJson(root.GetComponent<CapsuleCollider>());

            Assert.That(VegetationLodTuner.ApplyThreshold(group), Is.True);

            LOD[] after = group.GetLODs();
            Assert.That(after[0].screenRelativeTransitionHeight, Is.EqualTo(before[0].screenRelativeTransitionHeight));
            Assert.That(after[1].screenRelativeTransitionHeight, Is.EqualTo(VegetationLibrary.PalmLod1Height));
            Assert.That(after[2].screenRelativeTransitionHeight, Is.EqualTo(before[2].screenRelativeTransitionHeight));
            for (int i = 0; i < after.Length; i++)
            {
                Assert.That(after[i].renderers, Is.EqualTo(before[i].renderers));
                Assert.That(after[i].fadeTransitionWidth, Is.EqualTo(before[i].fadeTransitionWidth));
            }
            Assert.That(group.size, Is.EqualTo(8f));
            Assert.That(group.localReferencePoint, Is.EqualTo(new Vector3(1f, 4f, 0f)));
            Assert.That(EditorJsonUtility.ToJson(root.GetComponent<CapsuleCollider>()), Is.EqualTo(colliderBefore));
            string groupAfter = EditorJsonUtility.ToJson(group);
            Assert.That(VegetationLodTuner.ApplyThreshold(group), Is.False);
            Assert.That(EditorJsonUtility.ToJson(group), Is.EqualTo(groupAfter));
        }

        [Test]
        public void ApplyRefusesAnUnexpectedAuthoredThresholdWithoutChangingIt()
        {
            LOD[] levels = group.GetLODs();
            levels[1].screenRelativeTransitionHeight = 0.12f;
            group.SetLODs(levels);
            string before = EditorJsonUtility.ToJson(group);

            Assert.Throws<InvalidOperationException>(() => VegetationLodTuner.ApplyThreshold(group));

            Assert.That(EditorJsonUtility.ToJson(group), Is.EqualTo(before));
        }
    }
}
