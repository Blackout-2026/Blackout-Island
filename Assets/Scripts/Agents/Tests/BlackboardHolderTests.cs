using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class BlackboardHolderTests
    {
        private GameObject gameObject;
        private BlackboardHolder holder;
        private VoidEventChannel generatorDisabled;
        private VoidEventChannel cureTaken;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("BlackboardHolderTest");
            holder = gameObject.AddComponent<BlackboardHolder>();
            generatorDisabled = ScriptableObject.CreateInstance<VoidEventChannel>();
            cureTaken = ScriptableObject.CreateInstance<VoidEventChannel>();
        }

        [TearDown]
        public void TearDown()
        {
            holder.Unbind();
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(generatorDisabled);
            Object.DestroyImmediate(cureTaken);
        }

        [Test]
        public void Flags_StartCleared()
        {
            Assert.IsFalse(holder.Instance.generatorDisabled);
            Assert.IsFalse(holder.Instance.cureTaken);
        }

        [Test]
        public void GeneratorChannel_SetsOnlyTheGeneratorFlag()
        {
            holder.Bind(generatorDisabled, cureTaken);

            generatorDisabled.Raise();

            Assert.IsTrue(holder.Instance.generatorDisabled);
            Assert.IsFalse(holder.Instance.cureTaken);
        }

        [Test]
        public void CureChannel_SetsOnlyTheCureFlag()
        {
            holder.Bind(generatorDisabled, cureTaken);

            cureTaken.Raise();

            Assert.IsTrue(holder.Instance.cureTaken);
            Assert.IsFalse(holder.Instance.generatorDisabled);
        }

        [Test]
        public void Unbind_StopsListening()
        {
            holder.Bind(generatorDisabled, cureTaken);
            holder.Unbind();

            generatorDisabled.Raise();

            Assert.IsFalse(holder.Instance.generatorDisabled);
        }

        [Test]
        public void Bind_AcceptsMissingChannels()
        {
            Assert.DoesNotThrow(() => holder.Bind(null, null));
        }
    }
}
