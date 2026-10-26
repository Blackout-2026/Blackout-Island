using System.Collections.Generic;
using System.Linq;
using Blackout.Player;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Interaction.Tests
{
    /// <summary>
    /// Step VM1: the downloaded MP9, M9 and first-person arms. The player's two hands are on every weapon and hidden only while sprinting, the knife
    /// swings when the player attacks, and the models are built to the frames the weapon prefabs and the muzzle expect.
    /// </summary>
    public class ViewmodelTests
    {
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        private const string HolderPath = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";
        private static readonly string[] ArmParts = { "GloveR", "SleeveR", "GloveL", "SleeveL" };

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }

            created.Clear();
        }

        private GameObject Make(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            created.Add(go);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        private GameObject Instance(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            created.Add(instance);
            return instance;
        }

        private static Bounds BoundsOf(Transform root, string part)
        {
            Transform target = root.Find(part);
            Assert.That(target, Is.Not.Null, part);
            return target.GetComponent<Renderer>().bounds;
        }

        // ---------------------------------------------------------------------------------------------------------- hands

        [Test]
        public void ArmParts_AreTheGloveAndSleeveMeshes()
        {
            foreach (string part in ArmParts)
            {
                Assert.That(ViewmodelHands.IsArmPart(part), Is.True, part);
            }

            foreach (string other in new[] { "Smg", "Knife", "Sniper", "Muzzle", "Hand", "", null })
            {
                Assert.That(ViewmodelHands.IsArmPart(other), Is.False, other);
            }
        }

        [Test]
        public void HandsAreShown_ExceptWhileSprinting()
        {
            Assert.That(ViewmodelHands.ShouldShow(MovementState.Idle), Is.True);
            Assert.That(ViewmodelHands.ShouldShow(MovementState.Walk), Is.True);
            Assert.That(ViewmodelHands.ShouldShow(MovementState.Airborne), Is.True);
            Assert.That(ViewmodelHands.ShouldShow(MovementState.Sprint), Is.False);
        }

        [Test]
        public void ApplyingHidden_SwitchesTheArmsAndTheWeaponTogether()
        {
            GameObject holder = Make("Holder");
            var hands = holder.AddComponent<ViewmodelHands>();
            GameObject weapon = Make("Weapon", holder.transform);
            Renderer[] arms = ArmParts.Select(name => Make(name, weapon.transform).AddComponent<MeshRenderer>()).Cast<Renderer>().ToArray();
            Renderer body = Make("Smg", weapon.transform).AddComponent<MeshRenderer>();
            Renderer keptOff = Make("Spare", weapon.transform).AddComponent<MeshRenderer>();
            keptOff.enabled = false;

            hands.Collect();
            Assert.That(hands.ArmCount, Is.EqualTo(6), "the four arm parts, the weapon and the spare mesh");

            hands.Apply(false);
            Assert.That(arms.All(arm => !arm.enabled), Is.True, "the hands are hidden");
            Assert.That(body.enabled, Is.False, "and the weapon with them: a gun without hands looked wrong");
            Assert.That(hands.Shown, Is.False);

            hands.Apply(true);
            Assert.That(arms.All(arm => arm.enabled), Is.True, "and come back");
            Assert.That(body.enabled, Is.True);
            Assert.That(keptOff.enabled, Is.False, "a renderer the prefab keeps off stays off");
        }

        // ---------------------------------------------------------------------------------------------------------- swing

        [Test]
        public void Swing_StartsAndEndsAtRest()
        {
            const float hit = 0.11f;
            Assert.That(KnifeSwing.Evaluate(0f, hit).Shift, Is.EqualTo(Vector3.zero));
            Assert.That(KnifeSwing.Evaluate(KnifeSwing.Duration(hit), hit).Shift, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(KnifeSwing.Evaluate(KnifeSwing.Duration(hit) + 1f, hit).Rotation, Quaternion.identity), Is.Zero);
            Assert.That(KnifeSwing.Duration(hit), Is.LessThan(60f / 125f), "it ends before the knife's cooldown (125 a minute) does");
        }

        [Test]
        public void Swing_WindsUpThenSlashesAcrossToTheLeftAndLandsAtTheHitDelay()
        {
            const float hit = 0.11f;
            KnifeSwing.Pose windup = KnifeSwing.Evaluate(0.35f * hit, hit);
            KnifeSwing.Pose landed = KnifeSwing.Evaluate(hit, hit);
            KnifeSwing.Pose through = KnifeSwing.Evaluate(1.5f * hit, hit);

            Assert.That(windup.Shift.x, Is.GreaterThan(0f), "the wind-up goes to the right");
            Assert.That(windup.Shift.z, Is.LessThan(0f), "and back");
            Assert.That(landed.Shift.x, Is.LessThan(0f), "the slash is across to the left");
            Assert.That(landed.Shift.z, Is.GreaterThan(0.05f), "and out towards the enemy");
            Assert.That(through.Shift.z, Is.GreaterThan(landed.Shift.z), "the follow-through goes on");
            Assert.That(Quaternion.Angle(Quaternion.identity, landed.Rotation), Is.GreaterThan(15f), "the blade has turned across the view when it lands");
        }

        [Test]
        public void Swing_IsSmooth()
        {
            const float hit = 0.11f;
            float duration = KnifeSwing.Duration(hit);
            KnifeSwing.Pose last = KnifeSwing.Evaluate(0f, hit);
            for (float t = 0.001f; t < duration; t += 0.001f)
            {
                KnifeSwing.Pose pose = KnifeSwing.Evaluate(t, hit);
                Assert.That(Quaternion.Angle(last.Rotation, pose.Rotation), Is.LessThan(4f), "no jump in the turn at " + t);
                Assert.That((pose.Shift - last.Shift).magnitude, Is.LessThan(0.01f), "no jump in the position at " + t);
                last = pose;
            }
        }

        [Test]
        public void Swing_TurnsTheRightHandAndTheKnifeAndLeavesTheLeftHand()
        {
            GameObject weapon = Make("Knife");
            Transform hand = Make("Hand", weapon.transform).transform;
            hand.localPosition = new Vector3(0.17f, -0.13f, 0.40f);
            hand.localRotation = Quaternion.Euler(-19f, -33f, 12f);
            Make("Knife", hand);
            Make("GloveR", hand);
            Make("SleeveR", hand);
            Transform leftGlove = Make("GloveL", hand).transform;
            Transform leftSleeve = Make("SleeveL", hand).transform;
            var swing = weapon.AddComponent<KnifeSwing>();
            new SerializedObject(swing).FindProperty("hand").objectReferenceValue = hand;
            new SerializedObject(swing).ApplyModifiedPropertiesWithoutUndo();
            var serialized = new SerializedObject(swing);
            serialized.FindProperty("hand").objectReferenceValue = hand;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(swing.Prepare(), Is.True);
            Transform group = swing.Group;
            Assert.That(group, Is.Not.Null);
            Assert.That(group.Find("Knife"), Is.Not.Null);
            Assert.That(group.Find("GloveR"), Is.Not.Null);
            Assert.That(group.Find("SleeveR"), Is.Not.Null);
            Assert.That(leftGlove.parent, Is.SameAs(hand), "the left hand does not swing");
            Assert.That(leftSleeve.parent, Is.SameAs(hand));

            Vector3 restPosition = group.localPosition;
            Quaternion restRotation = group.localRotation;
            Vector3 bladeBefore = group.TransformPoint(Vector3.forward * 0.23f);
            Vector3 leftBefore = leftGlove.position;
            swing.SetHitDelay(0.11f);
            swing.ApplyAt(0.11f);
            Assert.That(Vector3.Distance(group.TransformPoint(Vector3.forward * 0.23f), bladeBefore),
                Is.GreaterThan(0.1f), "measure the blade's sweep around the elbow, rather than the group's origin");
            Assert.That(leftGlove.position, Is.EqualTo(leftBefore), "the left hand stays still");

            swing.ApplyAt(KnifeSwing.Duration(0.11f));
            Assert.That((group.localPosition - restPosition).magnitude, Is.LessThan(1e-5f), "it comes back exactly");
            Assert.That(Quaternion.Angle(group.localRotation, restRotation), Is.LessThan(0.01f));
            Assert.That(swing.Prepare(), Is.True, "preparing again changes nothing");
            Assert.That(weapon.transform.Cast<Transform>().Count(child => child.name == "Swing"), Is.EqualTo(1));
        }

        // ---------------------------------------------------------------------------------------------------------- the Player prefab

        [Test]
        public void KnifeSleeve_CroppedEndStaysBelowCameraThroughoutSwing()
        {
            GameObject player = Instance(PlayerPath);
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform holder = player.transform.Find(HolderPath);
            Camera camera = holder.parent.GetComponent<Camera>();
            camera.aspect = 16f / 9f;
            camera.ResetProjectionMatrix();
            var swing = holder.Find("Knife").GetComponent<KnifeSwing>();
            Assert.That(swing.Prepare(), Is.True);
            Assert.That(swing.Pivot, Is.EqualTo(KnifeSwing.DefaultPivot), "the saved prefab uses the fitted pivot");
            Transform sleeve = swing.Group.Find("SleeveR");
            // Read only in EditMode. The supplied sleeve's proximal rim runs along this model-space axis.
            Vector3[] vertices = sleeve.GetComponent<MeshFilter>().sharedMesh.vertices;
            Vector3 axis = new Vector3(0.5f, 0f, 0.85f).normalized;
            float end = vertices.Max(v => Vector3.Dot(v, axis));
            Vector3[] rim = vertices.Where(v => Vector3.Dot(v, axis) >= end - 0.015f).ToArray();
            Assert.That(rim.Length, Is.GreaterThan(10), "sample the cropped rim, not a single point");
            float highest = float.NegativeInfinity;
            string worst = string.Empty;
            for (int frame = 0; frame <= 120; frame++)
            {
                swing.ApplyAt(KnifeSwing.Duration(0.11f) * frame / 120f);
                foreach (Vector3 point in rim)
                {
                    Vector3 projected = camera.WorldToViewportPoint(sleeve.TransformPoint(point));
                    if (projected.y > highest)
                    {
                        highest = projected.y;
                        worst = $"sample {frame}, vertex {point}, camera point {camera.transform.InverseTransformPoint(sleeve.TransformPoint(point))}, FOV {camera.fieldOfView}";
                    }
                }
            }
            Assert.That(highest, Is.LessThan(0f), "cut sleeve end entered the view: " + worst);
            Assert.That(holder.Find("Knife").GetComponent<MeleeWeapon>().Data.kickRotate, Is.Zero,
                "the obsolete holder kick must not lift both cropped arms during a swing");
        }

        [Test]
        public void PlayerPrefab_HasTheHandsAndTheSwingWired()
        {
            GameObject player = Instance(PlayerPath);
            // The swing regroups the hand's parts at run time; in Edit mode a prefab instance's children cannot be moved, so unpack this copy first.
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform holder = player.transform.Find(HolderPath);
            var hands = holder.GetComponent<ViewmodelHands>();
            Assert.That(hands, Is.Not.Null, "WeaponHolder needs ViewmodelHands (Blackout > Systems > Build Viewmodel Hands And Knife Swing)");
            Assert.That(new SerializedObject(hands).FindProperty("motor").objectReferenceValue, Is.SameAs(player.GetComponent<PlayerMotor>()));

            hands.Collect();
            // two hands and two sleeves on each of the three weapons
            Assert.That(hands.ArmCount, Is.GreaterThanOrEqualTo(15), "twelve arm parts and at least one mesh for each of the three weapons");
            foreach (string weapon in new[] { "SMG", "Rifle", "Knife" })
            {
                var arms = holder.Find(weapon).GetComponentsInChildren<Renderer>(true).Where(r => ViewmodelHands.IsArmPart(r.gameObject.name)).Select(r => r.gameObject.name).OrderBy(n => n);
                Assert.That(arms, Is.EqualTo(new[] { "GloveL", "GloveR", "SleeveL", "SleeveR" }), weapon + " shows both hands");
            }

            Transform knife = holder.Find("Knife");
            var swing = knife.GetComponent<KnifeSwing>();
            Assert.That(swing, Is.Not.Null);
            var serialized = new SerializedObject(swing);
            Assert.That(serialized.FindProperty("weapon").objectReferenceValue, Is.SameAs(knife.GetComponent<MeleeWeapon>()));
            Assert.That(serialized.FindProperty("hand").objectReferenceValue, Is.SameAs(knife.Find("Hand")));
            Assert.That(swing.Prepare(), Is.True);
            Assert.That(swing.Group.Cast<Transform>().Select(t => t.name).OrderBy(n => n), Is.EqualTo(new[] { "GloveR", "Knife", "SleeveR" }), "the swing group");
            foreach (string part in KnifeSwing.SwingingParts)
            {
                Assert.That(swing.Group.Find(part), Is.Not.Null, part + " in the swing group; the hand holds: " + string.Join(", ", knife.Find("Hand").GetComponentsInChildren<Transform>(true).Select(t => t.name)));
            }
        }

        // ---------------------------------------------------------------------------------------------------------- the models

        [TestCase("Assets/Models/Custom/SM_Weapon_Smg.fbx", "Smg")]
        [TestCase("Assets/Models/Custom/SM_Weapon_Knife.fbx", "Knife")]
        [TestCase("Assets/Models/Custom/SM_Weapon_Sniper.fbx", "Sniper")]
        public void WeaponModels_HaveABodyAndBothArms_AndStayUnderTheBudget(string path, string body)
        {
            GameObject model = Instance(path);
            foreach (string part in ArmParts.Concat(new[] { body }))
            {
                Assert.That(model.transform.Find(part), Is.Not.Null, path + " has no " + part);
            }

            int triangles = model.GetComponentsInChildren<MeshFilter>().Sum(filter => filter.sharedMesh.triangles.Length / 3);
            Assert.That(triangles, Is.LessThanOrEqualTo(10000), path + " triangles (docs/art-pipeline.md section 6)");
            var materials = new HashSet<string>(model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name));
            Assert.That(materials.Count, Is.LessThanOrEqualTo(3));
            Assert.That(materials.All(name => name.StartsWith("M_Weapon_")), Is.True, string.Join(", ", materials));
        }

        [Test]
        public void TheSmgAndTheRifle_AreHeldInBothHands_ThePistolGripAndTheForend()
        {
            foreach (var (path, body) in new[] { ("Assets/Models/Custom/SM_Weapon_Smg.fbx", "Smg"), ("Assets/Models/Custom/SM_Weapon_Sniper.fbx", "Sniper") })
            {
                GameObject model = Instance(path);
                Bounds gun = BoundsOf(model.transform, body);
                Bounds right = BoundsOf(model.transform, "GloveR");
                Bounds left = BoundsOf(model.transform, "GloveL");
                Assert.That(right.Intersects(gun), Is.True, path + ": the right hand is on the gun");
                Assert.That(left.Intersects(gun), Is.True, path + ": the left hand is on the gun");
                Assert.That(left.center.z, Is.GreaterThan(right.center.z + 0.05f), path + ": the left hand is forward of the right");
                Assert.That(right.center.x, Is.GreaterThanOrEqualTo(left.center.x - 0.02f), path + ": the right hand is on the right");
            }
        }

        [Test]
        public void TheKnife_IsHeldInTheRightHandAndTheLeftHandIsFree()
        {
            GameObject model = Instance("Assets/Models/Custom/SM_Weapon_Knife.fbx");
            Bounds knife = BoundsOf(model.transform, "Knife");
            Assert.That(BoundsOf(model.transform, "GloveR").Intersects(knife), Is.True, "the right hand holds the handle");
            Bounds left = BoundsOf(model.transform, "GloveL");
            Assert.That(left.Intersects(knife), Is.False, "the left hand is empty");
            Assert.That(knife.max.z, Is.EqualTo(0.232f).Within(0.02f), "the tip of the M9 is the frontmost point");
            Assert.That(model.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.z), Is.LessThanOrEqualTo(0.25f), "no hand goes beyond the tip");
        }

        [Test]
        public void TheHandsMaterial_UsesTheDownloadedMaps()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/M_Weapon_Hands.mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Does.EndWith("T_WeaponHands_BaseColor.png"));
            foreach (string gun in new[] { "Smg", "Knife" })
            {
                var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Models/M_Weapon_" + gun + ".mat");
                Assert.That(body.GetTexture("_BaseMap"), Is.Not.Null, gun);
                Assert.That(body.GetTexture("_MetallicGlossMap"), Is.Not.Null, gun + " metal map");
            }
        }
    }
}
