using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Environment.PlayTests
{
    /// <summary>The rain with a camera tagged MainCamera, outdoors and then under a roof.</summary>
    public class RainEmitterPlayTests
    {
        private GameObject cameraObject;
        private GameObject rainObject;
        private GameObject roof;

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(cameraObject);
            Object.Destroy(rainObject);
            Object.Destroy(roof);
        }

        [UnityTest]
        public IEnumerator Rain_FollowsTheCameraAndStopsUnderARoof()
        {
            cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(10f, 2f, -40f);
            Assume.That(Camera.main, Is.EqualTo(camera), "no other camera is tagged MainCamera");

            rainObject = new GameObject("Atmosphere_Rain");
            var rain = rainObject.AddComponent<ParticleSystem>();
            RainEmitter emitter = rainObject.AddComponent<RainEmitter>();
            emitter.Configure(12f, 30f);
            yield return null;

            Assert.That(rainObject.transform.position, Is.EqualTo(new Vector3(10f, 14f, -40f)), "12 m above the camera");
            Assert.That(emitter.IsCovered, Is.False);
            Assert.That(rain.emission.enabled, Is.True, "raining in the open");

            // A ceiling 3 m overhead, like the bunker's.
            roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = new Vector3(10f, 5f, -40f);
            roof.transform.localScale = new Vector3(10f, 0.4f, 10f);
            Physics.SyncTransforms();
            cameraObject.transform.position = new Vector3(11f, 2f, -41f);
            yield return null;

            Assert.That(rainObject.transform.position, Is.EqualTo(new Vector3(11f, 14f, -41f)), "it keeps following");
            Assert.That(emitter.IsCovered, Is.True);
            Assert.That(rain.emission.enabled, Is.False, "no rain under the roof");

            Object.Destroy(roof);
            yield return null;
            yield return null;
            Assert.That(rain.emission.enabled, Is.True, "back out in the rain");
        }
    }
}
