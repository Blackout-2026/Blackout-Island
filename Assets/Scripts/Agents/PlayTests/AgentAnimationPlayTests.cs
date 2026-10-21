// Owner: Agent Controller. Real callbacks and bone motion on all four saved prefabs (AN1).
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Agents.PlayTests
{
    public class AgentAnimationPlayTests
    {
        private sealed class ConstantVelocitySource : IAgentVelocitySource
        {
            public Vector3 Velocity => Vector3.forward * Speed;
            public float Speed => 4f;
        }

        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject obj in created) if (obj != null) Object.Destroy(obj);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllFourPrefabs_AnimateAimHitDeathAndFreshSpawns_UsingRealCallbacks()
        {
            string[] names = { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" };
            var agents = new GameObject[names.Length];
            var animators = new Animator[names.Length];
            var first = new Quaternion[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                agents[i] = Spawn(names[i], i);
                AnimatorDriver driver = agents[i].GetComponentInChildren<AnimatorDriver>();
                animators[i] = driver.GetComponent<Animator>();
                driver.Configure(animators[i], new ConstantVelocitySource());
                agents[i].GetComponent<AgentRoot>().ApplyIntent(new AgentIntent { aim = true, moveSpeed = MoveSpeed.Stop });
            }
            yield return new WaitForSeconds(0.35f);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(animators[i].GetCurrentAnimatorStateInfo(0).IsName("Aim"), Is.True, names[i]);
                first[i] = animators[i].GetBoneTransform(HumanBodyBones.LeftFoot).rotation;
                var shooter = new SerializedObject(agents[i].GetComponent<AgentShooter>());
                var muzzle = (Transform)shooter.FindProperty("muzzle").objectReferenceValue;
                Assert.That(muzzle, Is.Not.Null, names[i]);
                Assert.That(Vector3.Dot(muzzle.forward, agents[i].transform.forward), Is.GreaterThan(0.5f), names[i] + " muzzle points away from aim");
            }
            yield return new WaitForSeconds(0.17f);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(Quaternion.Angle(first[i], animators[i].GetBoneTransform(HumanBodyBones.LeftFoot).rotation), Is.GreaterThan(2f), names[i]);
                AgentShooter shooter = agents[i].GetComponent<AgentShooter>();
                shooter.Configure(new AgentWeaponConfig { reactionDelay = 0f, range = 45f, burstCount = 1, burstPause = 1f });
                agents[i].GetComponent<AgentRoot>().ApplyIntent(new AgentIntent
                {
                    aim = true, fire = true, hasLookTarget = true, moveSpeed = MoveSpeed.Stop,
                    lookTarget = agents[i].transform.position + new Vector3(0f, 1.4f, 20f)
                });
                shooter.Tick(0.1f);
                Assert.That(shooter.ShotsFired, Is.EqualTo(1), names[i] + " actual shot");
                shooter.CeaseFire();
            }
            yield return new WaitForSeconds(0.06f);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(animators[i].GetCurrentAnimatorStateInfo(animators[i].GetLayerIndex("WeaponPose")).IsName("Shoot"),
                    Is.True, names[i] + " actual shot did not trigger its animation");
                agents[i].GetComponent<Health>().TakeDamage(Damage(1f));
            }
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(animators[i].GetCurrentAnimatorStateInfo(0).IsName("HitReact"), Is.True, names[i]);
                agents[i].GetComponent<Health>().TakeDamage(Damage(1000f));
            }
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < names.Length; i++)
                Assert.That(animators[i].GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True, names[i]);
            yield return new WaitForSeconds(5f);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.That(agents[i].transform.Find("Visual").gameObject.activeSelf, Is.False, names[i] + " death fade did not finish");
                Assert.That(agents[i].activeSelf, Is.True, "fade must not disable the gameplay root");
                GameObject fresh = Spawn(names[i], i + 4);
                Assert.That(fresh.transform.Find("Visual").gameObject.activeSelf, Is.True);
                Assert.That(fresh.GetComponent<Health>().IsAlive, Is.True);
            }
        }

        private GameObject Spawn(string name, int index)
        {
            GameObject obj = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab"));
            created.Add(obj);
            obj.transform.position = new Vector3(index * 3f, 0f, 0f);
            // Keep this test focused on executor callbacks. The real-scene smoke runs the brains and mover.
            obj.GetComponent<AgentRoot>().enabled = false;
            return obj;
        }

        private static DamageInfo Damage(float amount) => new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Bullet);
    }
}
#endif
