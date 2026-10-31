// Owner: Agent Controller. Saved prefab, animation and actual tracer integration (EG1).
using System.Linq;
using Blackout.Editor;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class EnemyGunTests
    {
        [TestCase("SecurityPatrol")]
        [TestCase("Hunter")]
        [TestCase("TacticalGuard")]
        [TestCase("Commander")]
        public void SavedEnemy_UsesNewGunAndMuzzle_AndKeepsItsTakedownReferences(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab");
            Transform supplied = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == EnemyGunBuilder.InstanceName);
            Assert.That(supplied.IsChildOf(prefab.transform.Find("Visual")), Is.True, "weapon must follow the death fade");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(supplied.gameObject),
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyGunBuilder.VisualPath)));
            Transform muzzle = (Transform)new SerializedObject(prefab.GetComponent<AgentShooter>()).FindProperty("muzzle").objectReferenceValue;
            Assert.That(muzzle, Is.EqualTo(supplied.Find("MuzzlePoint")));
            Renderer[] parts = supplied.GetComponentsInChildren<Renderer>(true);
            Assert.That(parts.Length, Is.EqualTo(3));
            Assert.That(parts.All(r => r.enabled), Is.True);
            var target = new SerializedObject(prefab.GetComponent<AgentTakedownTarget>());
            SerializedProperty array = target.FindProperty("carriedWeapon");
            var assigned = Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            CollectionAssert.AreEquivalent(parts, assigned);
            Assert.That(target.FindProperty("disarmWeapon").objectReferenceValue, Is.EqualTo(name == "TacticalGuard" ? null : supplied.parent));
            Assert.That(supplied.parent.GetComponentsInChildren<Renderer>(true).Where(r => !r.transform.IsChildOf(supplied)).All(r => !r.enabled), Is.True);
        }

        [TestCase("SecurityPatrol")]
        [TestCase("Hunter")]
        [TestCase("TacticalGuard")]
        [TestCase("Commander")]
        public void AnimatedAim_FitsTheGrip_AndFiresATracerFromTheNewBarrel(string name)
        {
            GameObject agent = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab"));
            var channel = ScriptableObject.CreateInstance<NoiseEventChannel>();
            try
            {
                var driver = agent.GetComponentInChildren<AnimatorDriver>();
                Animator animator = driver.GetComponent<Animator>();
                animator.Rebind();
                animator.SetBool("Aiming", true);
                animator.SetLayerWeight(animator.GetLayerIndex("WeaponPose"), 1f);
                animator.Play("Aim", 0, 0f); animator.Update(0f); animator.Update(.2f);
                driver.SyncWeaponGrip();
                var shooter = agent.GetComponent<AgentShooter>();
                Transform muzzle = (Transform)new SerializedObject(shooter).FindProperty("muzzle").objectReferenceValue;
                Transform gun = muzzle.parent.parent;
                Assert.That(Vector3.Dot(muzzle.forward, agent.transform.forward), Is.GreaterThan(.99f));
                Assert.That(Vector3.Distance(gun.position, animator.GetBoneTransform(HumanBodyBones.RightHand).position), Is.InRange(.025f, .05f));
                MeshRenderer lod0 = muzzle.parent.GetComponentsInChildren<MeshRenderer>().Single(r => r.name.EndsWith("LOD0"));
                Vector3 barrelTip = gun.InverseTransformPoint(muzzle.position);
                Bounds bounds = lod0.GetComponent<MeshFilter>().sharedMesh.bounds;
                Assert.That(barrelTip.z, Is.EqualTo(bounds.max.z).Within(.012f), "muzzle must be at the mesh's forward tip");
                int noise = 0;
                channel.OnRaised += e => noise++;
                var config = new AgentWeaponConfig { reactionDelay = 0f, spread = 0f, range = 45f, burstCount = 1 };
                // Tracers are optional/unassigned in the base prefab. Inject a test template to
                // observe the real shot origin without adding effects to the game as part of EG1.
                var template = new GameObject("Test tracer").AddComponent<LineRenderer>();
                template.transform.SetParent(agent.transform, false);
                template.gameObject.SetActive(false);
                AgentTracerPool pool = agent.GetComponent<AgentTracerPool>();
                var poolSettings = new SerializedObject(pool);
                poolSettings.FindProperty("tracerPrefab").objectReferenceValue = template;
                poolSettings.ApplyModifiedPropertiesWithoutUndo();
                shooter.Configure(config, channel, pool);
                Vector3 aim = muzzle.position + agent.transform.forward * 20f;
                shooter.Apply(new AgentIntent { aim = true, fire = true, hasLookTarget = true, lookTarget = aim });
                shooter.Tick(.1f);
                Assert.That(shooter.ShotsFired, Is.EqualTo(1));
                Assert.That(noise, Is.EqualTo(1), "gunshot noise must survive a visual replacement");
                LineRenderer line = agent.GetComponentsInChildren<LineRenderer>().Single(l => l.gameObject.activeInHierarchy);
                Assert.That(Vector3.Distance(line.GetPosition(0), muzzle.position + agent.transform.forward * config.muzzleForwardOffset), Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(agent); Object.DestroyImmediate(channel); }
        }

        [Test]
        public void Visual_HasThreeReducedLevels_WithTexturedUrpMaterials()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyGunBuilder.VisualPath);
            LOD[] levels = prefab.GetComponentInChildren<LODGroup>().GetLODs();
            Assert.That(levels.Length, Is.EqualTo(3));
            int previous = 15000;
            foreach (LOD level in levels)
            {
                Assert.That(level.renderers.Length, Is.EqualTo(1));
                var renderer = (MeshRenderer)level.renderers[0];
                int triangles = renderer.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                Assert.That(triangles, Is.LessThan(previous)); previous = triangles;
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                    foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                        Assert.That(material.GetTexture(slot), Is.Not.Null, material.name + " " + slot);
                }
            }
        }
    }
}
