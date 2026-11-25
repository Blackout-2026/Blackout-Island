// Owner: Systems Engineer. SC1 saved wiring and scope aperture geometry.
using Blackout.Hud;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blackout.Player.Tests
{
    public class SniperScopeTests
    {
        [Test]
        public void SavedPlayer_WiresTheRifleScopeWithoutReplacingTheWeaponPose()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Assert.That(player.GetComponents<SniperScope>().Length, Is.EqualTo(1));
            var fields = new SerializedObject(player.GetComponent<SniperScope>());
            foreach (string field in new[] { "input", "look", "motor", "weapons", "rifle", "playerCamera", "hands", "overlay", "crosshair" })
                Assert.That(fields.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(fields.FindProperty("rifle").objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/ScriptableObjects/Weapons/Rifle.asset")));
            Transform mask = player.transform.Find("HUD/SniperScope");
            Assert.That(mask.gameObject.activeSelf, Is.False);
            Assert.That(mask.GetComponent<CanvasRenderer>(), Is.Not.Null);
            Assert.That(mask.GetComponent<ScopeOverlay>().raycastTarget, Is.False);
            Assert.That(player.transform.Find("HUD/Crosshair").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Transform holder = player.transform.Find("CameraRig/CameraPivot/PlayerCamera/WeaponHolder");
            Assert.That(holder.Find("Rifle").localPosition, Is.EqualTo(new Vector3(0.12f, -0.14f, 0.58f)), "preserve approved VM2 pose");
            // SG1 owns SMG framing independently; the rifle retains its approved VM2 pose above.
            Assert.That(holder.Find("SMG").GetComponent<SmgAimPose>(), Is.Not.Null);
        }

        [Test]
        public void Aim_IsAHeldRightMouseButtonAlongsideTheExistingFireAction()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/PlayerAndInteraction/Input/PlayerControls.inputactions");
            InputAction aim = actions.FindAction("Player/Aim", true);
            Assert.That(aim.type, Is.EqualTo(InputActionType.Button));
            Assert.That(aim.bindings.Count, Is.EqualTo(1));
            Assert.That(aim.bindings[0].path, Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(aim.interactions, Is.Null.Or.Empty, "no toggle/timed hold interaction");
            Assert.That(actions.FindAction("Player/Fire", true).bindings[0].path, Is.EqualTo("<Mouse>/leftButton"));
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1440f, 1080f)]
        [TestCase(720f, 1080f)]
        public void Mask_CoversCornersButLeavesTheScopeWindowClear(float width, float height)
        {
            var root = new GameObject("Scope geometry", typeof(RectTransform), typeof(ScopeOverlay));
            Mesh mesh = null;
            try
            {
                ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
                ScopeOverlay graphic = root.GetComponent<ScopeOverlay>();
                graphic.canvasRenderer.cull = false;
                graphic.SetAllDirty();
                graphic.Rebuild(CanvasUpdate.PreRender);
                mesh = graphic.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), "exercise the actual Graphic mesh-generation path");
                Vector3[] positions = mesh.vertices;
                foreach (Vector3 point in positions)
                    Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y), Is.False);
                float radius = Mathf.Min(height * 0.34f, width * 0.48f);
                Assert.That(Covered(mesh, new Vector2(radius * 0.4f, radius * 0.3f)), Is.False, "clear lens away from reticle");
                foreach (float x in new[] { -1f, 1f })
                    foreach (float y in new[] { -1f, 1f })
                        Assert.That(Covered(mesh, new Vector2(x * (width * 0.5f - 1f), y * (height * 0.5f - 1f))), Is.True, "black corners");
                Assert.That(Covered(mesh, Vector2.zero), Is.True, "centre reticle");
            }
            finally { if (mesh != null) Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); }
        }

        private static bool Covered(Mesh mesh, Vector2 point)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
                if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
            }
            return false;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
