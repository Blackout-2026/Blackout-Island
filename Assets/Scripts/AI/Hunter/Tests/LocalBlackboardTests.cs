// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    public class LocalBlackboardTests
    {
        private static readonly BlackboardKey<float> Loudness = new BlackboardKey<float>("Loudness");
        private static readonly BlackboardKey<Vector3> NoisePosition = new BlackboardKey<Vector3>("NoisePosition");
        private static readonly BlackboardKey<string> Branch = new BlackboardKey<string>("Branch");

        [Test]
        public void Set_ThenTryGet_ReturnsTheValue()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(Loudness, 0.4f);

            Assert.IsTrue(blackboard.TryGet(Loudness, out float value));
            Assert.AreEqual(0.4f, value);
        }

        [Test]
        public void TryGet_KeyNeverSet_IsFalseAndDefault()
        {
            var blackboard = new LocalBlackboard();

            Assert.IsFalse(blackboard.TryGet(NoisePosition, out Vector3 value));
            Assert.AreEqual(Vector3.zero, value);
            Assert.IsFalse(blackboard.Has(NoisePosition));
        }

        [Test]
        public void ValueAtTheOrigin_IsStillThere()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(NoisePosition, Vector3.zero);

            Assert.IsTrue(blackboard.Has(NoisePosition), "set to zero is not the same as not set");
        }

        [Test]
        public void Get_ReturnsTheFallbackOnlyWhenUnset()
        {
            var blackboard = new LocalBlackboard();
            Assert.AreEqual(-1f, blackboard.Get(Loudness, -1f));

            blackboard.Set(Loudness, 0f);
            Assert.AreEqual(0f, blackboard.Get(Loudness, -1f));
        }

        [Test]
        public void Set_Twice_KeepsTheLastValue()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(Branch, "Prowl");
            blackboard.Set(Branch, "Engage");

            Assert.AreEqual("Engage", blackboard.Get(Branch));
            Assert.AreEqual(1, blackboard.Count);
        }

        [Test]
        public void Remove_ForgetsTheValue()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(Loudness, 0.4f);

            Assert.IsTrue(blackboard.Remove(Loudness));
            Assert.IsFalse(blackboard.Has(Loudness));
            Assert.IsFalse(blackboard.Remove(Loudness), "second remove has nothing to remove");
        }

        [Test]
        public void KeysWithTheSameName_AreDifferentKeys()
        {
            var blackboard = new LocalBlackboard();
            var first = new BlackboardKey<float>("Timer");
            var second = new BlackboardKey<float>("Timer");
            blackboard.Set(first, 1f);

            Assert.IsFalse(blackboard.Has(second));
        }

        [Test]
        public void ValuesOfDifferentTypes_AreKeptApart()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(Loudness, 0.4f);
            blackboard.Set(NoisePosition, new Vector3(1f, 2f, 3f));
            blackboard.Set(Branch, "Prowl");

            Assert.AreEqual(3, blackboard.Count);
            Assert.AreEqual(0.4f, blackboard.Get(Loudness));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), blackboard.Get(NoisePosition));
            Assert.AreEqual("Prowl", blackboard.Get(Branch));
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(Loudness, 0.4f);
            blackboard.Set(Branch, "Prowl");
            blackboard.Clear();

            Assert.AreEqual(0, blackboard.Count);
            Assert.IsFalse(blackboard.Has(Loudness));
            Assert.IsFalse(blackboard.Has(Branch));
        }

        [Test]
        public void NullKey_Throws()
        {
            var blackboard = new LocalBlackboard();

            Assert.Throws<ArgumentNullException>(() => blackboard.Set(null, 1f));
            Assert.Throws<ArgumentNullException>(() => blackboard.TryGet<float>(null, out _));
            Assert.Throws<ArgumentNullException>(() => blackboard.Remove<float>(null));
        }

        [Test]
        public void Overwriting_ValueTypes_AllocatesNothing()
        {
            var blackboard = new LocalBlackboard();
            blackboard.Set(Loudness, 0f);
            blackboard.Set(NoisePosition, Vector3.zero);
            blackboard.TryGet(Loudness, out _);
            blackboard.TryGet(NoisePosition, out _);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                blackboard.Set(Loudness, i);
                blackboard.Set(NoisePosition, new Vector3(i, 0f, i));
                blackboard.TryGet(Loudness, out _);
                blackboard.TryGet(NoisePosition, out _);
            }

            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
