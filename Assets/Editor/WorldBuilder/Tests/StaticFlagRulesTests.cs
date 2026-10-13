using NUnit.Framework;
using UnityEditor;

namespace Blackout.Editor.Tests
{
    public class StaticFlagRulesTests
    {
        [TestCase("Bunker", "Corridor")]
        [TestCase("Bunker", "GeneratorRoom")]
        [TestCase("Beach", "Dock")]
        [TestCase("Terrain", "IslandTerrain")]
        public void Environment_IsLightmappedOccludingBatchedAndInReflections(string root, string child)
        {
            StaticEditorFlags? flags = StaticFlagRules.For(root, child);
            Assert.That(flags, Is.EqualTo(StaticFlagRules.Environment));
            Assert.That(flags.Value.HasFlag(StaticEditorFlags.ContributeGI), Is.True);
            Assert.That(flags.Value.HasFlag(StaticEditorFlags.BatchingStatic), Is.True);
            Assert.That(flags.Value.HasFlag(StaticEditorFlags.OccluderStatic), Is.True);
            Assert.That(flags.Value.HasFlag(StaticEditorFlags.OccludeeStatic), Is.True);
        }

        [Test]
        public void Water_HidesNothingAndGetsNoLightmap()
        {
            StaticEditorFlags flags = StaticFlagRules.For("Beach", "Water").Value;
            Assert.That(flags.HasFlag(StaticEditorFlags.OccluderStatic), Is.False);
            Assert.That(flags.HasFlag(StaticEditorFlags.ContributeGI), Is.False);
            Assert.That(flags.HasFlag(StaticEditorFlags.BatchingStatic), Is.True);
        }

        [Test]
        public void Boat_StaysDynamic()
        {
            Assert.That(StaticFlagRules.For("Beach", "Boat"), Is.EqualTo((StaticEditorFlags)0));
        }

        [TestCase("Markers", "PlayerStart")]
        [TestCase("Lighting", "PoweredLights")]
        [TestCase("Props", "Crate")]
        [TestCase("Terrain", "IslandBoundary")]
        public void OtherObjects_AreLeftAlone(string root, string child)
        {
            Assert.That(StaticFlagRules.For(root, child), Is.Null);
        }
    }
}
