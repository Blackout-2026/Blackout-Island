// Owner: Agent Controller. Real scene lifecycle checks for E1.
#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Blackout.Agents.PlayTests
{
    public class AgentRespawnPlayTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CureLocksLab_OnlyNonLabDeathsRespawn_AndFinalKillOpensExit()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Environment.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Interactables.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Agents.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var spawner = Object.FindFirstObjectByType<AgentSpawner>();
            Assert.That(spawner.Spawned.Count, Is.EqualTo(24));
            Assert.That(AgentRegistry.Current.Agents.Count, Is.EqualTo(24));
            Assert.That(spawner.LivingLabDefenders, Is.EqualTo(6));
            var door = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .First(b => b.name == "Door_D-Lab" && b is IDoor) as IDoor;
            // Let the real generator event and its delayed unlock open the entry first.
            AssetDatabase.LoadAssetAtPath<VoidEventChannel>("Assets/ScriptableObjects/Events/GeneratorDisabled.asset").Raise();
            yield return new WaitForSeconds(4f);
            Assert.That(door.IsLocked, Is.False);
            Assert.That(door.IsOpen, Is.True);

            AssetDatabase.LoadAssetAtPath<VoidEventChannel>("Assets/ScriptableObjects/Events/CureTaken.asset").Raise();
            Assert.That(AgentRegistry.Current.Blackboard.cureTaken, Is.True);
            Assert.That(door.IsLocked, Is.True);
            Assert.That(door.IsOpen, Is.False);
            GameObject oldHunter = spawner.Spawned.First(a => a.name == "Hunter-1");
            Kill(oldHunter);
            var defenders = new[] { "Patrol-10", "Patrol-11", "Patrol-12", "TG-02", "TG-04", "Commander" };
            foreach (string name in defenders.Take(5)) Kill(spawner.Spawned.First(a => a.name == name));
            spawner.TickRespawns(spawner.RespawnDelay);
            yield return null; // old objects really destroy; replacements really Start/register
            yield return null;
            Assert.That(oldHunter == null, Is.True);
            Assert.That(AgentRegistry.Current.Agents.Count, Is.EqualTo(19), "old OnDestroy must not erase the replacement's registration");
            Assert.That(spawner.Spawned.First(a => a.name == "Hunter-1").GetComponent<AgentRoot>().VisionRangeMultiplier, Is.EqualTo(1.4f));
            Assert.That(door.IsLocked, Is.True, "the Commander still lives");
            Kill(spawner.Spawned.First(a => a.name == "Commander"));
            spawner.TickRespawns(spawner.RespawnDelay);
            Assert.That(spawner.LivingLabDefenders, Is.Zero);
            Assert.That(door.IsLocked, Is.False);
            yield return new WaitForSeconds(1.2f);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(spawner.Spawned.Count, Is.EqualTo(24));
            foreach (string name in defenders)
                Assert.That(spawner.Spawned.First(a => a.name == name).GetComponent<Health>().IsAlive, Is.False, name);

            spawner.enabled = false;
            spawner.enabled = true;
            Assert.That(door.IsLocked, Is.False, "reenabling must not restart a cleared encounter");
        }

        private static void Kill(GameObject agent) => agent.GetComponent<Health>().TakeDamage(
            new DamageInfo(1000f, agent.transform.position, Vector3.forward, null, DamageType.Bullet));
    }
}
#endif
