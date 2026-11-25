// Owner: Systems Engineer. R1 exercises saved additive scenes and the real death/respawn callbacks.
#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Blackout.Agents;
using Blackout.Environment;
using Blackout.Hud;
using Blackout.Interaction;
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Blackout.Diagnostics.PlayTests
{
    public class CheckpointWorldPlayTests
    {
        private GameObject player;
        private GameObject directorObject;
        private MissionDirector director;
        private AgentSpawner spawner;
        private PlayerHealth life;
        private Health health;
        private CheckpointTracker tracker;
        private WeaponController weapons;
        private MedkitInventory medkits;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Environment.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Interactables.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Agents.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            spawner = Object.FindFirstObjectByType<AgentSpawner>();
            FreezeEnemies();
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponent<PlayerLook>().enabled = false;
            life = player.GetComponent<PlayerHealth>();
            health = player.GetComponent<Health>();
            tracker = player.GetComponent<CheckpointTracker>();
            weapons = player.GetComponentInChildren<WeaponController>(true);
            medkits = player.GetComponent<MedkitInventory>();
            // Begin the real mission without the unrelated cinematic introduction.
            directorObject = new GameObject("Checkpoint test mission");
            directorObject.SetActive(false);
            director = directorObject.AddComponent<MissionDirector>();
            var serialized = new SerializedObject(director);
            foreach (string channel in new[] { "GeneratorDisabled", "CureTaken", "PlayerDied", "MissionComplete" })
            {
                string field = char.ToLowerInvariant(channel[0]) + channel.Substring(1);
                serialized.FindProperty(field).objectReferenceValue = Channel(channel);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            directorObject.SetActive(true);
            director.Begin(player);
            life.Respawned += FreezeEnemies;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (directorObject != null) Object.Destroy(directorObject);
            if (player != null) Object.Destroy(player);
            yield return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator KnifeStart_LootsBothGuns_AndRestoresTheSavedLoadout()
        {
            Assert.That(weapons.ActiveWeapon.kind, Is.EqualTo(WeaponKind.Melee));
            Assert.That(weapons.DebugEquip(1), Is.False);
            Assert.That(weapons.DebugEquip(2), Is.False);
            AssertVisibleSlots(3);

            Kill(Agent("Hunter-1"));
            var first = Agent("Hunter-1").GetComponent<EnemyWeaponDrop>().LastDrop.GetComponent<WeaponDropPickup>();
            Assert.That(first.Offer.slot, Is.EqualTo(1), "runtime lookup finds the player's missing SMG");
            Assert.That(first.CanInteract, Is.True);
            first.Interact(player);
            first.Interact(player); // a second use of the same body must not grant the rifle
            yield return new WaitForSeconds(0.6f);
            Assert.That(weapons.ActiveWeapon.slot, Is.EqualTo(1));
            Assert.That(weapons.DebugEquip(2), Is.False);
            Assert.That(weapons.AmmoOf(weapons.ActiveWeapon).CanFire, Is.True);
            AssertVisibleSlots(1, 3);
            tracker.SetCheckpoint(tracker.Current);
            yield return null; // save the SMG, its collected drop and the dead first enemy

            Kill(Agent("Hunter-2"));
            var second = Agent("Hunter-2").GetComponent<EnemyWeaponDrop>().LastDrop.GetComponent<WeaponDropPickup>();
            Assert.That(second.Offer.slot, Is.EqualTo(2));
            second.Interact(player);
            yield return new WaitForSeconds(0.6f);
            Assert.That(weapons.ActiveWeapon.slot, Is.EqualTo(2));
            Assert.That(weapons.NextLootWeapon(), Is.Null);
            Assert.That(weapons.AmmoOf(weapons.ActiveWeapon).CanFire, Is.True);
            AssertVisibleSlots(1, 2, 3);

            Kill(Agent("Patrol-1"));
            var third = Agent("Patrol-1").GetComponent<EnemyWeaponDrop>().LastDrop.GetComponent<WeaponDropPickup>();
            Assert.That(third.CanInteract, Is.False, "both guns are already owned");
            weapons.gameObject.SetActive(false);
            weapons.gameObject.SetActive(true);
            Assert.That(weapons.ActiveWeapon.slot, Is.EqualTo(2), "re-enabling must preserve earned weapons");

            yield return DieAndRespawn();
            Assert.That(weapons.ActiveWeapon.slot, Is.EqualTo(1), "restore the saved SMG, not a fresh knife-only state");
            Assert.That(weapons.DebugEquip(2), Is.False, "the later rifle acquisition rewinds");
            Assert.That(weapons.NextLootWeapon().slot, Is.EqualTo(2));
            Assert.That(first.IsTaken, Is.True);
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(Object.FindObjectsByType<WeaponDropPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                Is.EqualTo(1), "later drops disappear and restoring a dead enemy must not duplicate loot");
            AssertVisibleSlots(1, 3);
        }

        private void AssertVisibleSlots(params int[] expected)
        {
            var slots = player.transform.Find("HUD/Weapons").GetComponentsInChildren<WeaponSlot>(true);
            Assert.That(slots.Where(slot => slot.gameObject.activeSelf).Select(slot => slot.Weapon.slot),
                Is.EquivalentTo(expected));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Medkits_RewindStoredCountAndCollectedDrops_WithoutDuplicateLoot()
        {
            director.DebugJump(MissionPhase.GeneratorRoom);
            Kill(Agent("Hunter-1"));
            var savedDrop = Object.FindFirstObjectByType<MedkitPickup>();
            Assert.That(savedDrop, Is.Not.Null);
            medkits.AddMedkit();
            tracker.SetCheckpoint(tracker.Current);
            yield return null;
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            Assert.That(player.transform.Find("HUD/Compass").gameObject.activeSelf, Is.True);
            Assert.That(player.transform.Find("HUD/Compass/Heading").GetComponent<Text>().text, Is.EqualTo("090°"));
            StringAssert.StartsWith("GENERATOR", player.transform.Find("HUD/Compass/Destination").GetComponent<Text>().text);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.That(savedDrop.TryCollect(medkits), Is.True);
                Assert.That(medkits.Count, Is.EqualTo(2));
                health.Tick(10000f);
                Damage(health, 50f);
                Assert.That(medkits.TryUse(), Is.EqualTo(25f));
                Assert.That(medkits.TryUse(), Is.EqualTo(25f));
                Kill(Agent("Hunter-2"));
                Assert.That(Object.FindObjectsByType<MedkitPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                    Is.EqualTo(2));
                yield return DieAndRespawn();
                Assert.That(medkits.Count, Is.EqualTo(1));
                Assert.That(savedDrop.gameObject.activeSelf, Is.True);
                Assert.That(savedDrop.IsTaken, Is.False);
                Assert.That(Object.FindObjectsByType<MedkitPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                    Is.EqualTo(1), "later drops discarded; restoring dead enemies must not create another drop");
                Assert.That(director.TryGetNavigationTarget(out _, out string label), Is.True);
                Assert.That(label, Is.EqualTo("GENERATOR"));
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator Death_RewindsGeneratorLootHealthDoorsCrateAndEnemies_Repeatedly()
        {
            director.DebugJump(MissionPhase.GeneratorRoom);
            Kill(Agent("Hunter-1"));
            GameObject wounded = Agent("Hunter-2");
            Damage(wounded.GetComponent<Health>(), 10f);
            float savedEnemyHealth = wounded.GetComponent<Health>().Current;
            Vector3 savedEnemyPosition = wounded.transform.position;
            Damage(health, 25f);
            var kit = Object.FindFirstObjectByType<HealthPickup>();
            var crate = Object.FindFirstObjectByType<PushableCrate>().GetComponent<Rigidbody>();
            Vector3 savedCrate = crate.position;
            var door = Object.FindObjectsByType<SlidingDoor>(FindObjectsSortMode.None).First(d => d.name == "Door_D-Security");
            bool savedDoorOpen = door.IsOpen;
            var drop = Object.FindFirstObjectByType<WeaponDropPickup>();
            Assert.That(drop, Is.Not.Null);
            drop.SetLoot(weapons);
            tracker.SetCheckpoint(tracker.Current);
            yield return null; // checkpoint LateUpdate, including all world participants

            for (int attempt = 0; attempt < 2; attempt++)
            {
                drop.Interact(player);
                Assert.That(drop.IsTaken, Is.True);
                Assert.That(weapons.DebugEquip(1), Is.True);
                kit.TryHeal(health);
                Kill(Agent("Hunter-2"));
                crate.position += Vector3.right * 3f;
                Object.FindFirstObjectByType<GeneratorInteractable>().Interact(player);
                AgentRegistry.Current.Blackboard.BroadcastAlert(new AgentAlert(1.25f, 1.4f, 0.9f));
                door.RequestOpen(player);
                yield return new WaitForSeconds(3f);
                Assert.That(director.Phase, Is.EqualTo(MissionPhase.Lab));
                Assert.That(Object.FindFirstObjectByType<PowerStateLighting>().GeneratorIsDisabled, Is.True);
                yield return DieAndRespawn();

                Assert.That(director.Phase, Is.EqualTo(MissionPhase.GeneratorRoom));
                Assert.That(director.Deaths, Is.Zero, "counters rewind with the saved run");
                Assert.That(health.Current, Is.EqualTo(health.Max), "respawn heals fully even from a wounded checkpoint");
                Assert.That(health.IsInvulnerable, Is.True);
                Assert.That(weapons.NextLootWeapon().slot, Is.EqualTo(1));
                Assert.That(weapons.ActiveWeapon.kind, Is.EqualTo(WeaponKind.Melee));
                Assert.That(drop.IsTaken, Is.False);
                Assert.That(drop.gameObject.activeSelf, Is.True);
                Assert.That(Object.FindObjectsByType<WeaponDropPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(kit.IsTaken, Is.False);
                Assert.That(Object.FindFirstObjectByType<GeneratorInteractable>().IsRunning, Is.True);
                Assert.That(Object.FindFirstObjectByType<PowerStateLighting>().GeneratorIsDisabled, Is.False);
                Assert.That(AgentRegistry.Current.Blackboard.cureTaken, Is.False);
                Assert.That(AgentRegistry.Current.Blackboard.HasAlertBuff, Is.False);
                Assert.That(Agent("Hunter-1").GetComponent<Health>().IsAlive, Is.False);
                Assert.That(Agent("Hunter-2").GetComponent<Health>().Current, Is.EqualTo(savedEnemyHealth));
                Assert.That(Vector3.Distance(Agent("Hunter-2").transform.position, savedEnemyPosition), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(crate.position, savedCrate), Is.LessThan(0.05f));
                Assert.That(door.IsOpen, Is.EqualTo(savedDoorOpen));
                Assert.That(tracker.Current.Id, Is.EqualTo(MissionStateMachine.CheckpointGeneratorRoom));
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CureCheckpoint_SavesCompletedWave_AndRestoresSurvivingLabDefenders()
        {
            Kill(Agent("Hunter-1"));
            Kill(Agent("Patrol-10"));
            Object.FindFirstObjectByType<GeneratorInteractable>().Interact(player);
            Object.FindFirstObjectByType<CurePickup>().Take(player);
            yield return null;
            yield return null;
            FreezeEnemies();
            Assert.That(tracker.Current.Id, Is.EqualTo(MissionStateMachine.CheckpointEscapeLab));
            Assert.That(Agent("Hunter-1").GetComponent<Health>().IsAlive, Is.True);
            Assert.That(spawner.LivingLabDefenders, Is.EqualTo(5));
            var door = Object.FindObjectsByType<SlidingDoor>(FindObjectsSortMode.None).First(d => d.name == "Door_D-Lab");

            for (int attempt = 0; attempt < 2; attempt++)
            {
                foreach (string name in new[] { "Patrol-11", "Patrol-12", "TG-02", "TG-04", "Commander" }) Kill(Agent(name));
                Kill(Agent("Hunter-1"));
                spawner.TickRespawns(600f);
                yield return new WaitForSeconds(3f);
                Assert.That(door.IsLocked, Is.False);
                yield return DieAndRespawn();
                Assert.That(director.Phase, Is.EqualTo(MissionPhase.Escape));
                Assert.That(tracker.IsCureTaken, Is.True);
                Assert.That(Object.FindFirstObjectByType<CurePickup>().IsTaken, Is.True);
                Assert.That(Object.FindFirstObjectByType<GeneratorInteractable>().IsRunning, Is.False);
                Assert.That(spawner.LivingLabDefenders, Is.EqualTo(5));
                Assert.That(Agent("Patrol-10").GetComponent<Health>().IsAlive, Is.False);
                Assert.That(AgentRegistry.Current.Agents.Count, Is.EqualTo(23));
                Assert.That(door.IsLocked, Is.True);
                Assert.That(door.IsOpen, Is.False);
                Assert.That(Vector3.Distance(player.transform.position, tracker.Current.transform.position), Is.LessThan(0.01f));
                Kill(Agent("Hunter-1"));
                Channel("CureTaken").Raise();
                spawner.enabled = false;
                spawner.enabled = true;
                spawner.TickRespawns(600f);
                Assert.That(Agent("Hunter-1").GetComponent<Health>().IsAlive, Is.False, "restoration must not grant another cure wave");
            }

            // A newer outdoor checkpoint must replace the combat save, including a fully cleared lab.
            foreach (string name in new[] { "Patrol-11", "Patrol-12", "TG-02", "TG-04", "Commander" }) Kill(Agent(name));
            spawner.TickRespawns(0f);
            yield return new WaitForSeconds(4f); // restored 2.6 s power delay, then about 0.9 s to slide open
            Assert.That(door.IsOpen, Is.True);
            player.GetComponent<PlayerMotor>().Teleport(new Vector3(0f, 10f, 40f), 180f);
            yield return null;
            yield return null;
            Assert.That(tracker.Current.Id, Is.EqualTo(MissionStateMachine.CheckpointEscapeEntrance));
            yield return DieAndRespawn();
            Assert.That(spawner.LivingLabDefenders, Is.Zero);
            Assert.That(door.IsLocked, Is.False);
            Assert.That(door.IsOpen, Is.True, "restoring a cleared roster must preserve the saved open leaves");
            Assert.That(tracker.Current.Id, Is.EqualTo(MissionStateMachine.CheckpointEscapeEntrance));
        }

        private IEnumerator DieAndRespawn()
        {
            health.Tick(10000f);
            Damage(health, 10000f);
            Assert.That(life.IsDead, Is.True);
            yield return new WaitForSeconds(2.4f);
            Assert.That(life.IsDead, Is.False);
        }

        private void FreezeEnemies()
        {
            if (player != null) player.GetComponent<PlayerInputReader>().enabled = false;
            foreach (GameObject agent in spawner.Spawned)
            {
                agent.GetComponent<AgentRoot>().enabled = false;
                agent.GetComponent<PathFollower>().enabled = false;
                agent.GetComponent<AgentShooter>().enabled = false;
            }
        }

        private GameObject Agent(string name) => spawner.Spawned.First(a => a.name == name);
        private static void Kill(GameObject agent) => Damage(agent.GetComponent<Health>(), 10000f);
        private static void Damage(Health target, float amount) => target.TakeDamage(new DamageInfo(amount, target.transform.position, Vector3.forward, null, DamageType.Bullet));
        private static VoidEventChannel Channel(string name) => AssetDatabase.LoadAssetAtPath<VoidEventChannel>("Assets/ScriptableObjects/Events/" + name + ".asset");
    }
}
#endif
