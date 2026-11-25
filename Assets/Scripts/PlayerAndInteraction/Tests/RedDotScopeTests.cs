// GN1: the red-dot view through the SMG: saved wiring, the overlay's real mesh, and the hands' sight-hidden flag.
using Blackout.Hud;
using Blackout.Player;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Blackout.Player.Tests
{
    public class RedDotScopeTests
    {
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";

        [Test]
        public void SavedPlayer_HasTheRedDotScopeWiredToTheSmgAimPose()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            var scope = player.GetComponent<RedDotScope>();
            if (scope == null) Assert.Ignore("Run Blackout > Systems > Build Red Dot Scope first.");
            var fields = new SerializedObject(scope);
            foreach (string name in new[] { "pose", "hands", "playerCamera", "overlay", "crosshair" })
                Assert.That(fields.FindProperty(name).objectReferenceValue, Is.Not.Null, name);
            var overlay = (GameObject)fields.FindProperty("overlay").objectReferenceValue;
            Assert.That(overlay.GetComponent<RedDotOverlay>(), Is.Not.Null);
            Assert.That(overlay.activeSelf, Is.False, "the view is off until the aim is fully raised");
            Assert.That(fields.FindProperty("pose").objectReferenceValue, Is.SameAs(player.GetComponentInChildren<SmgAimPose>(true)));
        }

        [TestCase(1280, 720)]
        [TestCase(960, 720)]
        [TestCase(1920, 720)]
        public void Overlay_HasAClearWindowAndARedDotAtTheCentre(int width, int height)
        {
            var root = new GameObject("overlay", typeof(RectTransform), typeof(RedDotOverlay));
            Mesh mesh;
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
                RedDotOverlay graphic = root.GetComponent<RedDotOverlay>();
                graphic.canvasRenderer.cull = false;
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
                mesh = graphic.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), "exercise the actual Graphic mesh-generation path");
                float radius = RedDotOverlay.WindowRadius(width, height);
                Assert.That(radius, Is.GreaterThan(0));

                Color32[] colors = mesh.colors32;
                Vector3[] vertices = mesh.vertices;
                bool red = false, blackBeyond = false, blackInside = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float distance = Vector2.Distance(vertices[i], Vector2.zero);
                    if (colors[i].r > 240 && colors[i].g < 60 && colors[i].a > 240 && distance < RedDotOverlay.DotRadius + 0.01f) red = true;
                    if (colors[i].r < 10 && colors[i].g < 10 && colors[i].b < 10 && colors[i].a > 250)
                    {
                        if (distance > radius - 0.01f) blackBeyond = true;
                        if (distance < radius * 0.9f) blackInside = true;
                    }
                }

                Assert.That(red, Is.True, "an opaque red dot sits at the centre");
                Assert.That(blackBeyond, Is.True, "the surround is opaque black");
                Assert.That(blackInside, Is.False, "the window is clear: nothing opaque black inside 90% of its radius");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SightHidden_HidesTheRenderersWithoutCountingAsAHandsHiddenAim()
        {
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            try
            {
                var hands = player.GetComponentInChildren<ViewmodelHands>(true);
                hands.Collect();
                hands.SetSightHidden(true);
                Assert.That(hands.Shown, Is.False, "the renderers are off");
                Assert.That(hands.ShownIgnoringSight, Is.True, "but the SMG aim pose, which reads this, keeps its aim");
                hands.SetScopeHidden(true);
                Assert.That(hands.ShownIgnoringSight, Is.False, "the sniper scope still counts");
                hands.SetScopeHidden(false);
                hands.SetSightHidden(false);
                Assert.That(hands.Shown, Is.True);
            }
            finally { Object.DestroyImmediate(player); }
        }
    }
}
