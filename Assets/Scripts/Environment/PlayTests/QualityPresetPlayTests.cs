using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Blackout.Environment.PlayTests
{
    /// <summary>
    /// Plays the real component with a camera tagged MainCamera, as the player's camera is, and the project's URP asset.
    /// </summary>
    public class QualityPresetPlayTests
    {
        private GameObject cameraObject;
        private GameObject root;
        private UniversalRenderPipelineAsset pipeline;
        private float pipelineShadowDistance;

        [SetUp]
        public void SetUp()
        {
            pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assume.That(pipeline, Is.Not.Null, "the project renders with URP");
            pipelineShadowDistance = pipeline.shadowDistance;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(cameraObject);
            if (pipeline != null)
            {
                pipeline.shadowDistance = pipelineShadowDistance; // in case a test failed before its own check
            }
        }

        [UnityTest]
        public IEnumerator Levels_SwitchTheMainCamerasPostProcessingAndTheShadowDistance()
        {
            cameraObject = new GameObject("PlayerCamera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            Assume.That(Camera.main, Is.EqualTo(camera), "no other camera is tagged MainCamera");

            root = new GameObject("Volumes");
            QualityPreset preset = root.AddComponent<QualityPreset>(); // Medium unless told otherwise
            yield return null; // Start applies the level
            yield return null; // Update configures the camera

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            Assert.That(data.renderPostProcessing, Is.True, "Medium: post-processing on");
            Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.FastApproximateAntialiasing));
            Assert.That(pipeline.shadowDistance, Is.EqualTo(50f));

            preset.Apply(QualityLevel.Low);
            yield return null;
            Assert.That(data.renderPostProcessing, Is.False, "Low: no post chain, like the prototype");
            Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.None));
            Assert.That(pipeline.shadowDistance, Is.EqualTo(40f));

            preset.Apply(QualityLevel.High);
            yield return null;
            Assert.That(data.renderPostProcessing, Is.True);
            Assert.That(pipeline.shadowDistance, Is.EqualTo(80f));

            preset.Apply(QualityLevel.Medium);
            yield return null;
            Assert.That(pipeline.shadowDistance, Is.EqualTo(50f), "Medium returns to W11's authored distance");
        }

        [UnityTest]
        public IEnumerator Destroying_PutsTheUrpAssetsShadowDistanceBack()
        {
            root = new GameObject("Volumes");
            root.AddComponent<QualityPreset>().Apply(QualityLevel.Low);
            Assert.That(pipeline.shadowDistance, Is.EqualTo(40f));

            Object.Destroy(root);
            yield return null;
            Assert.That(pipeline.shadowDistance, Is.EqualTo(pipelineShadowDistance), "the asset is left as it was");
        }
    }
}
