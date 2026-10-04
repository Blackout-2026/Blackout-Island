using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class GeneratedObjectsTests
    {
        private const string UndoLabel = "Test";
        private GameObject container;

        [SetUp]
        public void SetUp()
        {
            container = new GameObject("Dock");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(container);
        }

        [Test]
        public void GetOrCreate_ReusesTheChildWithTheSameName()
        {
            var built = new HashSet<string>();
            GameObject first = GeneratedObjects.GetOrCreate(container.transform, "Post_00", built, UndoLabel);
            GameObject second = GeneratedObjects.GetOrCreate(container.transform, "Post_00", built, UndoLabel);

            Assert.That(second, Is.SameAs(first));
            Assert.That(first.name, Is.EqualTo("Dock_Post_00"));
            Assert.That(container.transform.childCount, Is.EqualTo(1));
        }

        [Test]
        public void RemoveStale_RemovesOnlyGeneratedChildrenThatWereNotBuilt()
        {
            GeneratedObjects.GetOrCreate(container.transform, "Old", new HashSet<string>(), UndoLabel);
            var handMade = new GameObject("Crate");
            handMade.transform.SetParent(container.transform, false);

            var built = new HashSet<string>();
            GeneratedObjects.GetOrCreate(container.transform, "Post_00", built, UndoLabel, PrimitiveType.Cylinder);
            int removed = GeneratedObjects.RemoveStale(container.transform, built);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(container.transform.Find("Dock_Old"), Is.Null);
            Assert.That(container.transform.Find("Dock_Post_00"), Is.Not.Null);
            Assert.That(container.transform.Find("Crate"), Is.Not.Null, "hand-made child must survive");
        }
    }
}
