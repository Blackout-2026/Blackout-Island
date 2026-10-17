// Owner: Core Developer (Tactical Guard AI).
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.TacticalGuard.Tests
{
    public sealed class TacticalGuardConfigTests
    {
        [Test]
        public void GuardEyeHeight_DefaultsToThePrototypeGuardEyeHeight()
        {
            var config = ScriptableObject.CreateInstance<TacticalGuardConfig>();
            try
            {
                // docs/reference/agent-controller.md: eye height 1.95 for the Tactical Guard (the Agent Controller's prefab uses it too).
                Assert.AreEqual(1.95f, config.guardEyeHeight);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
