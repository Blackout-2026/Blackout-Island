using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Art.Tests
{
    public class GeneratorVisualsTests
    {
        private GameObject root;
        private GeneratorVisuals visuals;
        private MeshRenderer core;
        private Material onMaterial;
        private Material offMaterial;
        private Material otherMaterial;
        private AudioSource hum;
        private ParticleSystem sparks;
        private VoidEventChannel channel;

        [SetUp]
        public void SetUp()
        {
            Shader shader = Shader.Find("Sprites/Default");
            onMaterial = new Material(shader) { name = "On" };
            offMaterial = new Material(shader) { name = "Off" };
            otherMaterial = new Material(shader) { name = "Other" };

            root = new GameObject("GeneratorVisualsTest");
            visuals = root.AddComponent<GeneratorVisuals>();

            // A core with two material slots: the core is slot 1, slot 0 must never be touched.
            GameObject coreObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coreObject.transform.SetParent(root.transform);
            core = coreObject.GetComponent<MeshRenderer>();
            core.sharedMaterials = new[] { otherMaterial, onMaterial };

            hum = new GameObject("Hum").AddComponent<AudioSource>();
            hum.transform.SetParent(root.transform);
            hum.clip = AudioClip.Create("TestHum", 4410, 1, 44100, false);
            hum.loop = true;

            sparks = new GameObject("Sparks").AddComponent<ParticleSystem>();
            sparks.transform.SetParent(root.transform);
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var serialized = new SerializedObject(visuals);
            serialized.FindProperty("coreRenderer").objectReferenceValue = core;
            serialized.FindProperty("coreMaterialSlot").intValue = 1;
            serialized.FindProperty("coreOffMaterial").objectReferenceValue = offMaterial;
            serialized.FindProperty("humSource").objectReferenceValue = hum;
            serialized.FindProperty("sparks").objectReferenceValue = sparks;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            channel = ScriptableObject.CreateInstance<VoidEventChannel>();
        }

        [TearDown]
        public void TearDown()
        {
            visuals.Unbind();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(channel);
            Object.DestroyImmediate(onMaterial);
            Object.DestroyImmediate(offMaterial);
            Object.DestroyImmediate(otherMaterial);
        }

        [Test]
        public void PowerDown_SwitchesOnlyTheCoreSlotToTheOffMaterial()
        {
            visuals.PowerDown();

            Material[] materials = core.sharedMaterials;
            Assert.AreSame(otherMaterial, materials[0]);
            Assert.AreSame(offMaterial, materials[1]);
            Assert.IsTrue(visuals.IsPoweredDown);
        }

        [Test]
        public void PowerDown_PlaysTheSparks()
        {
            visuals.PowerDown();

            Assert.IsTrue(sparks.isPlaying);
        }

        [Test]
        public void PowerDown_StopsTheHum()
        {
            hum.Play();
            if (!hum.isPlaying)
            {
                Assert.Ignore("No audio device in this environment, so the hum cannot start.");
            }

            visuals.PowerDown();

            Assert.IsFalse(hum.isPlaying);
        }

        [Test]
        public void RaisingTheBoundChannel_PowersDown()
        {
            visuals.Bind(channel);

            channel.Raise();

            Assert.AreSame(offMaterial, core.sharedMaterials[1]);
        }

        [Test]
        public void Unbind_StopsListening()
        {
            visuals.Bind(channel);
            visuals.Unbind();

            channel.Raise();

            Assert.IsFalse(visuals.IsPoweredDown);
            Assert.AreSame(onMaterial, core.sharedMaterials[1]);
        }

        [Test]
        public void PowerDown_TwiceKeepsTheRunningMaterialForRestore()
        {
            visuals.PowerDown();
            visuals.PowerDown();
            visuals.RestorePower();

            Assert.AreSame(onMaterial, core.sharedMaterials[1]);
            Assert.IsFalse(visuals.IsPoweredDown);
        }

        [Test]
        public void RestorePower_FixesACoreThatWasSavedSwitchedOffOnAFreshComponent()
        {
            var serialized = new SerializedObject(visuals);
            serialized.FindProperty("coreOnMaterial").objectReferenceValue = onMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            core.sharedMaterials = new[] { otherMaterial, offMaterial }; // as if Auto Save kept a Power Down test

            visuals.RestorePower(); // IsPoweredDown is false here, as after a reopen or a recompile

            Assert.AreEqual(onMaterial, core.sharedMaterials[1]);
        }

        [Test]
        public void PowerDown_NeverRemembersTheOffMaterialAsTheRunningOne()
        {
            core.sharedMaterials = new[] { otherMaterial, offMaterial };

            visuals.PowerDown();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no core renderer or core material"));
            visuals.RestorePower(); // Nothing known to restore: warns and leaves the slot alone.
            Assert.AreEqual(offMaterial, core.sharedMaterials[1]);
        }

        [Test]
        public void RestorePower_StopsTheSparks()
        {
            visuals.PowerDown();
            visuals.RestorePower();

            Assert.IsFalse(sparks.isPlaying);
        }

        [Test]
        public void Bind_AcceptsAMissingChannel()
        {
            Assert.DoesNotThrow(() => visuals.Bind(null));
        }

        [Test]
        public void PowerDown_WithMissingReferencesWarnsAndDoesNotThrow()
        {
            var bare = new GameObject("Bare").AddComponent<GeneratorVisuals>();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no core renderer"));

            Assert.DoesNotThrow(() => bare.PowerDown());

            Object.DestroyImmediate(bare.gameObject);
        }
    }
}
