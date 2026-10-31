#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Blackout.Agents;
using Blackout.AI.Hunter;
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Takedowns;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    /// <summary>Real imported pair coverage plus TEST ONLY diagnostic clips for isolated lifecycle cases.</summary>
    public class TakedownPlayTests
    {
        private readonly List<Object> created = new List<Object>();
        private GameObject player, enemy;
        private TakedownController takedown;
        private WeaponController weapons;
        private AgentTakedownTarget target;
        private TakedownProfile profile;
        private float fixedDelta, captureDelta;
        private NoiseEventChannel noise;
        private System.Action<NoiseEvent> noiseListener;
        private bool loadedBootstrap;
        private Scene fixtureScene;
        private Mouse testMouse;
        private InputSettings originalInputSettings, testInputSettings;
        private HideFlags inputSettingsFlags;
        private readonly Vector3 origin = new Vector3(500f, 50f, 500f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            fixtureScene = SceneManager.GetActiveScene();
            fixedDelta = Time.fixedDeltaTime;
            captureDelta = Time.captureDeltaTime;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(floor);
            floor.layer = PhysicsLayers.Level;
            floor.transform.position = origin - Vector3.up * 0.5f;
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            var registry = new GameObject("Takedown test registry");
            created.Add(registry);
            registry.AddComponent<AgentRegistry>();
            player = Spawn("Assets/Prefabs/Player/Player.prefab", origin + Vector3.up * 0.02f);
            takedown = player.GetComponent<TakedownController>();
            weapons = player.GetComponentInChildren<WeaponController>(true);
            enemy = SpawnEnemy("SecurityPatrol", origin + Vector3.forward * 1.2f);
            target = enemy.GetComponent<AgentTakedownTarget>();
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(player.GetComponent<PlayerMotor>().Grounded, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (takedown != null) takedown.Cancel();
            if (noise != null && noiseListener != null) noise.OnRaised -= noiseListener;
            noise = null; noiseListener = null;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = fixedDelta;
            Time.captureDeltaTime = captureDelta;
            ClearTestDrops();
            foreach (Object value in created.AsEnumerable().Reverse()) if (value != null) Object.Destroy(value);
            created.Clear();
            yield return null;
            if (loadedBootstrap)
            {
                SceneManager.SetActiveScene(fixtureScene);
                foreach (string name in new[] { "Bootstrap", "Agents", "Interactables", "Environment" })
                {
                    Scene scene = SceneManager.GetSceneByName(name);
                    if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
                }
                loadedBootstrap = false;
            }
            if (testMouse != null) InputSystem.RemoveDevice(testMouse);
            if (originalInputSettings != null)
            {
                InputSystem.settings = originalInputSettings;
                originalInputSettings.hideFlags = inputSettingsFlags;
            }
            if (testInputSettings != null) Object.Destroy(testInputSettings);
            testMouse = null;
            originalInputSettings = testInputSettings = null;
        }

        private GameObject Spawn(string path, Vector3 position)
        {
            var value = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), position, Quaternion.identity);
            created.Add(value);
            return value;
        }

        private void ClearTestDrops()
        {
            foreach (MonoBehaviour item in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (item is ICheckpointSpawnedObject && Vector3.Distance(item.transform.position, origin) < 20f)
                    Object.Destroy(item.gameObject);
        }

        private GameObject SpawnEnemy(string name, Vector3 position)
        {
            GameObject value = Spawn("Assets/Prefabs/Agents/" + name + ".prefab", position);
            value.GetComponent<AgentRoot>().SetBrain(null); // Executor/control test; architecture tests exercise the brains separately.
            return value;
        }

        private void AssignDiagnosticClips()
        {
            profile = ScriptableObject.CreateInstance<TakedownProfile>();
            created.Add(profile);
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/HumanM@Death01.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            profile.playerAttack = Object.Instantiate(source);
            profile.victimReaction = Object.Instantiate(source);
            created.Add(profile.playerAttack); created.Add(profile.victimReaction);
            profile.playerAttack.name = "TEST ONLY event carrier - not a knife attack";
            AnimationUtility.SetAnimationEvents(profile.playerAttack, new[] { new AnimationEvent { functionName = "TakedownSound", time = 0.08f },
                new AnimationEvent { functionName = "TakedownImpact", time = 0.25f } });
            AnimationUtility.SetAnimationEvents(profile.victimReaction, new AnimationEvent[0]);
            profile.alignmentTime = profile.cameraReturnTime = 0.1f;
            takedown.Profile = profile;
            Assert.That(profile.MissingRequirement, Is.Null);
        }

        private IEnumerator WaitForPlaying()
        {
            for (int i = 0; i < 120 && takedown.State == TakedownController.Phase.Aligning; i++) yield return null;
            Assert.That(takedown.State, Is.EqualTo(TakedownController.Phase.Playing), takedown.LastResult);
        }

        private void FaceAndAttackPlayer(bool protectPlayer = true)
        {
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            enemy.GetComponent<PathFollower>().StopImmediately();
            if (protectPlayer) player.GetComponent<Health>().SetInvulnerable(120f);
            enemy.GetComponent<AgentShooter>().Apply(new AgentIntent { aim = true, fire = true,
                hasLookTarget = true, lookTarget = player.transform.position + Vector3.up });
            Physics.SyncTransforms();
        }

        private void ShootPlayer(float damage)
        {
            // Actual AgentShooter hitscan/Health path, with deterministic aim and no invulnerability.
            var attacker = new GameObject("Incoming gunfire regression");
            created.Add(attacker);
            attacker.transform.position = player.transform.position + Vector3.right * 3f + Vector3.up;
            Vector3 aim = player.transform.position + Vector3.up;
            attacker.transform.LookAt(aim);
            var shooter = attacker.AddComponent<AgentShooter>();
            shooter.enabled = false; // Step its normal Update path once, rather than firing every fixture frame.
            shooter.Configure(newConfig: new AgentWeaponConfig { damage = damage, spread = 0f, reactionDelay = 0f },
                newMuzzle: attacker.transform, newHitMask: 1 << PhysicsLayers.Player);
            shooter.Apply(new AgentIntent { fire = true, aim = true, hasLookTarget = true, lookTarget = aim });
            Physics.SyncTransforms();
            float before = player.GetComponent<Health>().Current;
            shooter.Tick(0.1f);
            Assert.That(shooter.ShotsFired, Is.EqualTo(1));
            Assert.That(player.GetComponent<Health>().Current, Is.EqualTo(Mathf.Max(0f, before - damage)).Within(0.001f),
                "the test bullet must actually damage the vulnerable player");
        }

        [UnityTest]
        public IEnumerator FrontalNonlethalGunfire_CompletesKickAndTwoStabsWithoutLosingControl()
        {
            FaceAndAttackPlayer(false);
            Assert.That(player.GetComponent<Health>().IsInvulnerable, Is.False);
            Assert.That(takedown.TryBegin(), Is.True, takedown.LastResult);
            ShootPlayer(10f);
            Assert.That(takedown.State, Is.EqualTo(TakedownController.Phase.Aligning), "a nonlethal alignment hit must not cancel the frontal counter");
            yield return WaitForPlaying();
            ShootPlayer(10f);
            Assert.That(takedown.State, Is.EqualTo(TakedownController.Phase.Playing), "a nonlethal hit must not cancel the kick");
            Assert.That(weapons.enabled || player.GetComponent<PlayerMotor>().enabled, Is.False);
            Assert.That(takedown.CinematicBody.activeInHierarchy, Is.True);
            for (int i = 0; i < 250 && takedown.ImpactCount == 0; i++) yield return null;
            Assert.That(takedown.KickCount, Is.EqualTo(1));
            Assert.That(takedown.ImpactCount, Is.EqualTo(1));
            Assert.That(target.Health.IsAlive, Is.True);
            ShootPlayer(10f);
            Assert.That(takedown.State, Is.EqualTo(TakedownController.Phase.Playing), "the finishing stab must still play after another hit");
            for (int i = 0; i < 900 && takedown.State == TakedownController.Phase.Playing; i++) yield return null;
            Assert.That(takedown.State, Is.EqualTo(TakedownController.Phase.Returning));
            Assert.That(takedown.ImpactCount, Is.EqualTo(2));
            Assert.That(target.Health.IsAlive, Is.False);
            ShootPlayer(10f);
            Assert.That(takedown.State, Is.EqualTo(TakedownController.Phase.Returning), "return blend must continue");
            for (int i = 0; i < 100 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.LastResult, Is.EqualTo("Completed"));
            Assert.That(player.GetComponent<Health>().Current, Is.EqualTo(player.GetComponent<Health>().Max - 40f));
            Assert.That(weapons.enabled && player.GetComponent<PlayerMotor>().enabled, Is.True);
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
            Assert.That(player.GetComponentInChildren<Camera>().fieldOfView, Is.EqualTo(72f));
        }

        [UnityTest]
        public IEnumerator FrontalLethalGunfire_ReleasesCameraAndGunWithoutAFinishingHit()
        {
            FaceAndAttackPlayer(false);
            Assert.That(takedown.TryBegin(), Is.True);
            yield return WaitForPlaying();
            for (int i = 0; i < 160 && takedown.KickCount == 0; i++) yield return null;
            Assert.That(takedown.KickCount, Is.EqualTo(1));
            // The kicked Gun is temporarily detached, so retrieve its serialized reference.
            Transform gun = (Transform)new SerializedObject(target).FindProperty("disarmWeapon").objectReferenceValue;
            Assert.That(gun.parent, Is.Null);
            ShootPlayer(player.GetComponent<Health>().Current);
            yield return null;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
            Assert.That(target.Available && target.Health.IsAlive, Is.True);
            Assert.That(gun.IsChildOf(enemy.transform), Is.True);
            Assert.That(gun.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(takedown.ImpactCount, Is.Zero);
            Assert.That(player.GetComponent<Health>().IsAlive, Is.False);
            Assert.That(player.GetComponent<PlayerHealth>().IsDead, Is.True);
            Assert.That(player.GetComponent<PlayerInputReader>().enabled, Is.False);
            Assert.That(player.GetComponentInChildren<Camera>().fieldOfView, Is.EqualTo(72f));
            takedown.ApplyStab(1); takedown.ApplyStab(2);
            Assert.That(target.Health.IsAlive, Is.True, "late callbacks after player death cannot finish the victim");
        }

        [UnityTest]
        public IEnumerator FacingRules_SelectRearBehindAndFrontalForSideOrPassiveFront()
        {
            foreach (float angle in new[] { 0f, 90f, 180f })
            {
                player.GetComponent<PlayerMotor>().Teleport(origin, 0f);
                enemy.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                enemy.GetComponent<PathFollower>().StopImmediately();
                enemy.GetComponent<AgentShooter>().CeaseFire();
                for (int i = 0; i < 40; i++) yield return null;
                Assert.That(takedown.TryBegin(), Is.True, takedown.LastResult);
                Assert.That(takedown.Style, Is.EqualTo(angle == 0f ? TakedownStyle.Rear : TakedownStyle.Frontal));
                yield return WaitForPlaying();
                takedown.Cancel();
                Assert.That(target.Available && weapons.enabled, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator ConsecutiveKnifePresses_ReuseOneBodyAcrossFourDifferentEnemies()
        {
            PrepareKnifeInput();
            player.GetComponent<Health>().SetInvulnerable(120f);
            int deaths = 0, swings = 0;
            weapons.GetComponentInChildren<MeleeWeapon>().Swung += () => swings++;
            GameObject reusedBody = null;
            string[] names = { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" };
            float[] facing = { 180f, 180f, 90f, 0f };
            for (int run = 0; run < names.Length; run++)
            {
                Object.Destroy(enemy);
                ClearTestDrops();
                yield return null;
                player.GetComponent<PlayerMotor>().Teleport(origin, 0f);
                enemy = SpawnEnemy(names[run], origin + Vector3.forward * 1.2f);
                target = enemy.GetComponent<AgentTakedownTarget>();
                target.Health.OnDied += () => deaths++;
                enemy.transform.rotation = Quaternion.Euler(0f, facing[run], 0f);
                enemy.GetComponent<PathFollower>().StopImmediately();
                for (int i = 0; i < 40; i++) yield return null;
                if (run == 0) FaceAndAttackPlayer(); // Subsequent victims are deliberately NOT firing.
                InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left));
                InputSystem.Update();
                Assert.That(player.GetComponent<PlayerInputReader>().FirePressed, Is.True);
                weapons.Tick(Time.deltaTime, true); // Real input/routing, with unavailable native batch cursor capture supplied.
                Assert.That(takedown.IsBusy, Is.True, names[run] + ": " + takedown.LastResult);
                Assert.That(takedown.Style, Is.EqualTo(run == 3 ? TakedownStyle.Rear : TakedownStyle.Frontal));
                if (reusedBody == null) reusedBody = takedown.CinematicBody;
                else Assert.That(takedown.CinematicBody, Is.SameAs(reusedBody), "reuse the same Animator/event receiver");
                InputSystem.QueueStateEvent(testMouse, new MouseState());
                InputSystem.Update(); // Release during the cinematic, not after it.
                for (int frame = 0; frame < 1000 && takedown.IsBusy; frame++) yield return null;
                Assert.That(takedown.LastResult, Is.EqualTo("Completed"), names[run]);
                Assert.That(takedown.WaitingForRelease, Is.False, "an earlier release must not block the next click");
                Assert.That(target.Health.IsAlive, Is.False);
                Assert.That(takedown.ImpactCount, Is.EqualTo(run == 3 ? 1 : 2));
                Assert.That(player.GetComponent<PlayerMotor>().enabled && weapons.enabled, Is.True);
                Assert.That(reusedBody.activeSelf, Is.False);
                Assert.That(deaths, Is.EqualTo(run + 1));
            }
            Assert.That(swings, Is.Zero, "every eligible knife press must route to a cinematic");
        }

        private void PrepareKnifeInput()
        {
            originalInputSettings = InputSystem.settings;
            inputSettingsFlags = originalInputSettings.hideFlags;
            originalInputSettings.hideFlags = HideFlags.DontSave;
            testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            testMouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTest]
        public IEnumerator HeldKnifeAttack_AfterCooldown_StartsCinematicAndRequiresReleaseBeforeTheNext()
        {
            PrepareKnifeInput();
            enemy.transform.position = origin + Vector3.forward * 8f;
            Physics.SyncTransforms();
            weapons.DebugFire(); // Put an ordinary missed swing on cooldown before approaching the next enemy.
            weapons.Tick(0.12f, false);
            enemy.transform.position = origin + Vector3.forward * 1.2f;
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            enemy.GetComponent<PathFollower>().StopImmediately();
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            weapons.Tick(0.01f, true);
            Assert.That(takedown.IsBusy, Is.False, "the press occurred during cooldown");
            InputSystem.Update(); // The button stays held, but this is no longer its press update.
            Assert.That(player.GetComponent<PlayerInputReader>().FirePressed, Is.False);
            Assert.That(player.GetComponent<PlayerInputReader>().FireHeld, Is.True);
            weapons.Tick(0.5f, true);
            Assert.That(takedown.IsBusy, Is.True, "cooldown recovery must select the cinematic instead of a normal swing");
            for (int frame = 0; frame < 1000 && takedown.IsBusy; frame++) yield return null;
            Assert.That(takedown.LastResult, Is.EqualTo("Completed"));
            Assert.That(takedown.WaitingForRelease, Is.True, "holding the old press must not chain another takedown");
            Object.Destroy(enemy);
            ClearTestDrops();
            yield return null;
            player.GetComponent<PlayerMotor>().Teleport(origin, 0f);
            enemy = SpawnEnemy("Hunter", origin + Vector3.forward * 1.2f);
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            enemy.GetComponent<PathFollower>().StopImmediately();
            for (int i = 0; i < 40; i++) yield return null;
            weapons.Tick(0.6f, true);
            Assert.That(takedown.IsBusy, Is.False, "release is required after completion");
            InputSystem.QueueStateEvent(testMouse, new MouseState());
            InputSystem.Update();
            yield return null;
            InputSystem.QueueStateEvent(testMouse, new MouseState().WithButton(MouseButton.Left));
            InputSystem.Update();
            weapons.Tick(Time.deltaTime, true);
            Assert.That(takedown.IsBusy, Is.True, takedown.LastResult);
        }

        [UnityTest]
        public IEnumerator FrontalClips_DisarmThenTwoNativeHits_KillOnceAndRestoreFps()
        {
            FaceAndAttackPlayer();
            float startingHealth = target.Health.Current;
            Transform gun = enemy.GetComponentsInChildren<Transform>(true).First(t => t.name == "Gun");
            Transform parent = gun.parent;
            int deaths = 0, kills = 0, sounds = 0;
            float firstStabTime = -1f, secondStabTime = -1f;
            target.Health.OnDied += () => deaths++;
            weapons.HitConfirmed += killed =>
            {
                if (killed) { kills++; secondStabTime = Time.time; }
                else firstStabTime = Time.time;
            };
            // Use the same serialized event asset as the live WeaponController.
            noise = (NoiseEventChannel)new SerializedObject(weapons).FindProperty("noiseChannel").objectReferenceValue;
            noiseListener = value => { if (value.source == player && value.type == NoiseType.Melee) sounds++; };
            noise.OnRaised += noiseListener;
            Assert.That(takedown.TryBegin(), Is.True, takedown.LastResult);
            Assert.That(takedown.TryBegin(), Is.False);
            yield return WaitForPlaying();
            takedown.ApplyStab(2); // Out-of-order callbacks cannot skip the kick and first stab.
            float spacing = Vector3.ProjectOnPlane(player.transform.position - enemy.transform.position, Vector3.up).magnitude;
            Assert.That(spacing, Is.EqualTo(1.1f).Within(0.08f), "slightly wider cinematic root spacing");
            Assert.That(takedown.ImpactCount, Is.Zero);
            for (int i = 0; i < 160 && takedown.KickCount == 0; i++) yield return null;
            Assert.That(takedown.KickCount, Is.EqualTo(1), "native kick callback");
            Assert.That(target.Health.Current, Is.EqualTo(startingHealth), "the kick only disarms");
            Assert.That(gun.parent, Is.Null);
            Assert.That(gun.GetComponent<Rigidbody>(), Is.Not.Null);
            CaptureDiagnostic(player.GetComponentInChildren<Camera>(), "FRONTAL-kick.png");
            for (int i = 0; i < 200 && takedown.ImpactCount == 0; i++) yield return null;
            Assert.That(takedown.ImpactCount, Is.EqualTo(1));
            Assert.That(target.Health.IsAlive, Is.True);
            Assert.That(target.Health.Current, Is.EqualTo(startingHealth * 0.5f).Within(0.01f));
            takedown.ApplyStab(1);
            Assert.That(target.Health.Current, Is.EqualTo(startingHealth * 0.5f).Within(0.01f), "duplicate first stab");
            CaptureDiagnostic(player.GetComponentInChildren<Camera>(), "FRONTAL-first-stab.png");
            for (int i = 0; i < 200 && takedown.ImpactCount == 1; i++) yield return null;
            Assert.That(takedown.ImpactCount, Is.EqualTo(2));
            Assert.That(target.Health.IsAlive, Is.False);
            Assert.That(gun.GetComponentsInChildren<Renderer>().All(r => !r.enabled), Is.True, "the normal pickup replaces the kicked visual on death");
            takedown.ApplyStab(2); takedown.KickWeapon();
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(sounds, Is.EqualTo(3), "one kick sound and two native stab sounds");
            Assert.That(firstStabTime, Is.GreaterThan(0f));
            Assert.That(secondStabTime - firstStabTime, Is.EqualTo(0.4f).Within(0.05f), "two fast native stab callbacks, with no added pause");
            Debug.Log($"[TD1] Frontal root spacing={spacing:F3}m; actual stab interval={secondStabTime - firstStabTime:F3}s.");
            CaptureDiagnostic(player.GetComponentInChildren<Camera>(), "FRONTAL-second-stab.png");
            for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.LastResult, Is.EqualTo("Completed"));
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(gun.parent, Is.EqualTo(parent), "no orphaned detached gun after release");
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
            Assert.That(weapons.enabled && player.GetComponent<PlayerMotor>().enabled, Is.True);
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
            Assert.That(player.GetComponentInChildren<Camera>().fieldOfView, Is.EqualTo(72f));
            Assert.That(target.Animator.enabled, Is.False);
            Assert.That(target.Animator.GetBoneTransform(HumanBodyBones.Head).position.y - origin.y, Is.LessThan(0.7f));
        }

        [UnityTest]
        public IEnumerator FrontalCancellationAfterKick_RestoresLivingGunAndExecutors()
        {
            FaceAndAttackPlayer();
            Transform gun = enemy.GetComponentsInChildren<Transform>(true).First(t => t.name == "Gun");
            Transform parent = gun.parent;
            Vector3 position = gun.localPosition;
            Quaternion rotation = gun.localRotation;
            int layer = gun.gameObject.layer;
            var visibility = gun.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => r.enabled);
            Assert.That(takedown.TryBegin(), Is.True);
            yield return WaitForPlaying();
            for (int i = 0; i < 160 && takedown.KickCount == 0; i++) yield return null;
            Assert.That(gun.parent, Is.Null);
            takedown.Cancel();
            yield return null;
            Assert.That(gun.parent, Is.EqualTo(parent));
            Assert.That(gun.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(gun.localRotation, rotation), Is.LessThan(0.001f));
            Assert.That(gun.gameObject.layer, Is.EqualTo(layer));
            Assert.That(gun.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(gun.GetComponent<BoxCollider>(), Is.Null);
            foreach (var part in visibility) Assert.That(part.Key.enabled, Is.EqualTo(part.Value), part.Key.name);
            Assert.That(target.Available && target.Health.IsAlive && weapons.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator FastFrontalStabs_At20Fps_DoNotSkipTheFinishingEvent()
        {
            Time.captureDeltaTime = 1f / 20f;
            FaceAndAttackPlayer();
            Assert.That(takedown.TryBegin(), Is.True);
            yield return WaitForPlaying();
            for (int i = 0; i < 220 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.LastResult, Is.EqualTo("Completed"));
            Assert.That(takedown.KickCount, Is.EqualTo(1));
            Assert.That(takedown.ImpactCount, Is.EqualTo(2));
            Assert.That(target.Health.IsAlive, Is.False);
            Assert.That(weapons.enabled && player.GetComponent<PlayerMotor>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator MissingSecondStabCallback_DoesNotGrantATimedKill()
        {
            FaceAndAttackPlayer();
            Assert.That(takedown.TryBegin(), Is.True);
            yield return WaitForPlaying();
            for (int i = 0; i < 250 && takedown.ImpactCount == 0; i++) yield return null;
            Assert.That(takedown.ImpactCount, Is.EqualTo(1));
            takedown.CinematicBody.GetComponentInChildren<TakedownActor>().Animator.fireEvents = false;
            for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
            Assert.That(target.Health.IsAlive && target.Available, Is.True);
            Assert.That(takedown.ImpactCount, Is.EqualTo(1));
            Assert.That(takedown.LastResult, Does.Contain("without a confirmed finishing hit"));
        }

        [UnityTest]
        public IEnumerator FrontalClips_AlsoCompleteOnHunterGuardAndCommander()
        {
            foreach (string name in new[] { "Hunter", "TacticalGuard", "Commander" })
            {
                Object.Destroy(enemy);
                ClearTestDrops();
                yield return null;
                player.GetComponent<PlayerMotor>().Teleport(origin, 0f);
                enemy = SpawnEnemy(name, origin + Vector3.forward * 1.2f);
                target = enemy.GetComponent<AgentTakedownTarget>();
                for (int i = 0; i < 40; i++) yield return null;
                FaceAndAttackPlayer();
                Assert.That(takedown.TryBegin(), Is.True, name + ": " + takedown.LastResult);
                yield return WaitForPlaying();
                for (int i = 0; i < 300 && takedown.ImpactCount < 2; i++) yield return null;
                Assert.That(takedown.KickCount, Is.EqualTo(1), name);
                Assert.That(takedown.ImpactCount, Is.EqualTo(2), name);
                CaptureDiagnostic(player.GetComponentInChildren<Camera>(), "FRONTAL-" + name + ".png");
                for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
                Assert.That(takedown.LastResult, Is.EqualTo("Completed"), name);
                Assert.That(target.Health.IsAlive, Is.False, name);
                Assert.That(weapons.enabled && player.GetComponent<PlayerMotor>().enabled, Is.True, name);
                Assert.That(target.Animator.GetBoneTransform(HumanBodyBones.Head).position.y - origin.y, Is.LessThan(0.7f), name);
            }
        }

        [UnityTest]
        public IEnumerator MissingClips_LeavesOrdinaryKnifeAttacksAndFpsUntouched()
        {
            profile = ScriptableObject.CreateInstance<TakedownProfile>();
            created.Add(profile);
            takedown.Profile = profile;
            Assert.That(takedown.TryBegin(), Is.False);
            Assert.That(takedown.CinematicBody, Is.Null);
            Assert.That(player.GetComponent<PlayerMotor>().enabled, Is.True);
            int swings = 0;
            weapons.GetComponentInChildren<MeleeWeapon>().Swung += () => swings++;
            weapons.DebugFire();
            yield return null;
            Assert.That(swings, Is.EqualTo(1));
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
        }

        [UnityTest]
        public IEnumerator SavedRanges_AllowRearAt3Point5Metres_AndKeepFrontAndSideAt1Point5()
        {
            foreach (var sample in new[]
            {
                (distance: 3.6f, yaw: 0f, allowed: false),
                (distance: 3.5f, yaw: 0f, allowed: true),
                (distance: 3.5f, yaw: 180f, allowed: false),
                (distance: 1.6f, yaw: 180f, allowed: false),
                (distance: 1.5f, yaw: 180f, allowed: true),
                (distance: 1.6f, yaw: 90f, allowed: false),
                (distance: 1.5f, yaw: 90f, allowed: true)
            })
            {
                player.GetComponent<PlayerMotor>().Teleport(origin, 0f);
                enemy.transform.rotation = Quaternion.Euler(0f, sample.yaw, 0f);
                enemy.GetComponent<PathFollower>().StopImmediately();
                for (int i = 0; i < 40; i++) yield return null; // Ground the player and finish any previous knife cooldown.
                enemy.transform.position = player.transform.position + Vector3.forward * sample.distance;
                Physics.SyncTransforms();
                Assert.That(takedown.TryBegin(), Is.EqualTo(sample.allowed),
                    $"distance={sample.distance}, yaw={sample.yaw}: {takedown.LastResult}");
                if (sample.allowed)
                {
                    Assert.That(takedown.Style, Is.EqualTo(sample.yaw == 0f ? TakedownStyle.Rear : TakedownStyle.Frontal));
                    takedown.Cancel();
                }
                Assert.That(target.Health.IsAlive && target.Available, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator RearAt3Point5Metres_ReservesLiveHunterBeforeCloseDetection_AndCompletes()
        {
            Object.Destroy(enemy);
            yield return null;
            // Keep the real Hunter brain active: the ordinary SpawnEnemy helper removes brains for executor tests.
            enemy = Spawn("Assets/Prefabs/Agents/Hunter.prefab", player.transform.position + Vector3.forward * 3.5f);
            target = enemy.GetComponent<AgentTakedownTarget>();
            var hunter = enemy.GetComponent<HunterBrain>();
            for (int i = 0; i < 20; i++) yield return null;
            enemy.transform.position = player.transform.position + Vector3.forward * 3.5f;
            enemy.GetComponent<PathFollower>().StopImmediately();
            Physics.SyncTransforms();
            Assert.That(enemy.GetComponent<AgentRoot>().enabled, Is.True);
            Assert.That(hunter.Vision.Detected, Is.False);
            Assert.That(hunter.Vision.Awareness, Is.Zero);
            Assert.That(Vector3.Distance(player.transform.position, enemy.transform.position), Is.GreaterThan(hunter.Config.closeSenseRange));
            Assert.That(takedown.TryBegin(), Is.True, takedown.LastResult);
            Assert.That(takedown.Style, Is.EqualTo(TakedownStyle.Rear));
            Assert.That(enemy.GetComponent<AgentRoot>().enabled, Is.False, "Reserve before approaching inside close-sense range.");
            Assert.That(enemy.GetComponent<PathFollower>().enabled, Is.False);
            Assert.That(enemy.GetComponent<AgentShooter>().enabled, Is.False);
            yield return WaitForPlaying();
            Assert.That(Vector3.Distance(player.transform.position, enemy.transform.position), Is.LessThan(hunter.Config.closeSenseRange));
            Assert.That(hunter.Vision.Awareness, Is.Zero, "Alignment must not give the reserved Hunter another perception tick.");
            for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.LastResult, Is.EqualTo("Completed"));
            Assert.That(takedown.ImpactCount, Is.EqualTo(1));
            Assert.That(target.Health.IsAlive, Is.False);
            Assert.That(weapons.enabled && player.GetComponent<PlayerMotor>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RequiresKnife_CloseLivingTarget_AndUnobstructedStaging()
        {
            AssignDiagnosticClips();
            enemy.transform.position = origin + Vector3.forward * 1.6f;
            Physics.SyncTransforms();
            Assert.That(takedown.TryBegin(), Is.False, "outside 1.5 metres");
            enemy.transform.position = origin + Vector3.forward * 1.2f;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.layer = PhysicsLayers.Level;
            wall.transform.position = origin + new Vector3(0f, 1f, 0.6f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(takedown.TryBegin(), Is.False, "wall blocks the pair");
            wall.SetActive(false);
            weapons.TakeNextLootWeapon();
            weapons.DebugEquip(1);
            Assert.That(takedown.TryBegin(), Is.False, "a gun is equipped");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NativeEvents_ApplyOneDeathAndKillReport_ThenRestoreCameraAndControls()
        {
            AssignDiagnosticClips();
            Camera camera = player.GetComponentInChildren<Camera>();
            Vector3 localPosition = camera.transform.localPosition;
            Quaternion localRotation = camera.transform.localRotation;
            Transform cameraParent = camera.transform.parent;
            int deaths = 0, kills = 0, attackSounds = 0;
            bool soundBeforeImpact = false;
            noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset");
            noiseListener = value =>
            {
                if (value.source == player && value.type == NoiseType.Melee)
                {
                    attackSounds++;
                    soundBeforeImpact = target.Health.IsAlive;
                }
            };
            noise.OnRaised += noiseListener;
            target.Health.OnDied += () => deaths++;
            weapons.HitConfirmed += killed => { if (killed) kills++; };
            Assert.That(takedown.TryBegin(), Is.True);
            Assert.That(takedown.TryBegin(), Is.False, "cannot start the same sequence again");
            Assert.That(player.GetComponent<PlayerMotor>().enabled, Is.False);
            Assert.That(enemy.GetComponent<AgentRoot>().enabled, Is.False);
            Assert.That(enemy.GetComponent<PathFollower>().enabled, Is.False);
            Assert.That(enemy.GetComponent<AgentShooter>().enabled, Is.False);
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.False);
            Assert.That(takedown.CinematicBody.activeSelf, Is.True);
            yield return WaitForPlaying();
            Assert.That(target.Health.IsAlive, Is.True, "alignment must not deal damage");
            CaptureDiagnostic(camera);
            for (int i = 0; i < 100 && target.Health.IsAlive; i++) yield return null;
            Assert.That(target.Health.IsAlive, Is.False, "the real Animation Event must deliver damage");
            Assert.That(takedown.ImpactCount, Is.EqualTo(1));
            takedown.ApplyImpact(); // A duplicate/late animation callback must not award another kill.
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(kills, Is.EqualTo(1));
            Assert.That(attackSounds, Is.EqualTo(1), "one Animation Event uses the existing knife audio/noise path");
            Assert.That(soundBeforeImpact, Is.True);
            for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
            Assert.That(weapons.GetComponent<ViewmodelHands>().Shown, Is.True);
            Assert.That(player.GetComponent<PlayerMotor>().enabled, Is.True);
            Assert.That(camera.transform.parent, Is.SameAs(cameraParent));
            Assert.That(camera.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, localRotation), Is.LessThan(0.001f));
            Assert.That(camera.fieldOfView, Is.EqualTo(72f));
            Assert.That(deaths, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PauseAndCheckpointCancellation_ReleaseLocksWithoutUnpausingTheGame()
        {
            AssignDiagnosticClips();
            profile.timeScale = 0.5f;
            Assert.That(takedown.TryBegin(), Is.True);
            yield return WaitForPlaying();
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));
            Time.timeScale = 0f;
            yield return null; yield return null;
            Vector3 cameraPosition = player.GetComponentInChildren<Camera>().transform.position;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.That(target.Health.IsAlive, Is.True);
            Assert.That(player.GetComponentInChildren<Camera>().transform.position, Is.EqualTo(cameraPosition));
            takedown.RestoreCheckpoint(null);
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(target.Available, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(fixedDelta));
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator PlayerDamageOrDisabledController_AbortBeforeImpactWithoutAFreeKill()
        {
            AssignDiagnosticClips();
            Assert.That(takedown.TryBegin(), Is.True);
            player.GetComponent<Health>().TakeDamage(new DamageInfo(1f, player.transform.position, Vector3.forward, null, DamageType.Bullet));
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(target.Available, Is.True);
            Assert.That(target.Health.IsAlive, Is.True);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.That(takedown.TryBegin(), Is.True);
            takedown.enabled = false;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(target.Available, Is.True);
            Assert.That(weapons.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator AllFourEnemyAdapters_ReserveExclusively_AndResumeNormalDeathFade()
        {
            foreach (string name in new[] { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" })
            {
                GameObject value = SpawnEnemy(name, origin + new Vector3(8f, 0f, 0f));
                yield return null;
                var adapter = value.GetComponent<AgentTakedownTarget>();
                Assert.That(adapter.TryReserve(player), Is.True, name);
                Assert.That(adapter.TryReserve(enemy), Is.False, name + " is already claimed");
                adapter.Health.TakeDamage(new DamageInfo(1000f, value.transform.position, Vector3.forward, player, DamageType.Melee));
                adapter.Release(player);
                Assert.That(adapter.Animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True, name);
                Assert.That(adapter.Available, Is.False, "dead targets cannot be reserved again");
                Object.Destroy(value);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator LostTargetOrBody_ReleasesCameraAndControlOwnership()
        {
            AssignDiagnosticClips();
            Assert.That(takedown.TryBegin(), Is.True);
            Object.Destroy(enemy);
            yield return null; yield return null;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(weapons.enabled, Is.True);
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
            for (int i = 0; i < 40; i++) yield return null;
            enemy = SpawnEnemy("Hunter", origin + Vector3.forward * 1.2f);
            yield return null;
            Assert.That(takedown.TryBegin(), Is.True);
            Object.Destroy(takedown.CinematicBody);
            yield return null; yield return null;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(enemy.GetComponent<AgentTakedownTarget>().Available, Is.True);
        }

        [UnityTest]
        public IEnumerator MissingLiveImpactCallback_TimesOutWithoutApplyingFallbackDamage()
        {
            AssignDiagnosticClips();
            Assert.That(takedown.TryBegin(), Is.True);
            takedown.CinematicBody.GetComponentInChildren<Animator>().fireEvents = false;
            for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(target.Health.IsAlive, Is.True);
            Assert.That(takedown.ImpactCount, Is.Zero);
            Assert.That(target.Available, Is.True);
            Assert.That(takedown.LastResult, Does.Contain("no damage"));
        }

        [UnityTest]
        public IEnumerator Bootstrap_LoadsWiredPlayerAndStagesOnActualIslandFloor()
        {
            foreach (Object value in created.AsEnumerable().Reverse()) if (value != null) Object.Destroy(value);
            created.Clear();
            yield return null;
            loadedBootstrap = true;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Additive);
            SceneBootstrap bootstrap = Object.FindFirstObjectByType<SceneBootstrap>();
            double deadline = Time.realtimeSinceStartupAsDouble + 60d;
            while (!bootstrap.IsLoaded && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(bootstrap.IsLoaded, Is.True, "Bootstrap additive scenes did not finish loading within 60 real seconds.");
            takedown = Object.FindFirstObjectByType<TakedownController>();
            Assert.That(takedown, Is.Not.Null, "Bootstrap must inherit the updated Player prefab");
            Assert.That(takedown.SetupStatus, Is.EqualTo("Ready"));
            player = takedown.gameObject;
            weapons = player.GetComponentInChildren<WeaponController>();
            player.GetComponent<Health>().SetInvulnerable(600f);
            AgentRoot[] agents = Object.FindObjectsByType<AgentRoot>(FindObjectsSortMode.None);
            Assert.That(agents.Length, Is.GreaterThan(0));
            // Keep the roster still to isolate saved-scene wiring and actual level collision.
            foreach (AgentRoot agent in agents)
            {
                agent.enabled = false;
                agent.GetComponent<PathFollower>().StopImmediately();
                agent.GetComponent<AgentShooter>().enabled = false;
                Assert.That(agent.GetComponent<AgentTakedownTarget>(), Is.Not.Null, agent.name);
            }
            int accepted = 0, hunterAccepted = 0;
            var stagedHunters = new HashSet<string>();
            var results = new List<string>();
            int world = 1 << PhysicsLayers.Default | 1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable;
            foreach (AgentRoot agent in agents)
            {
                Vector3 stand = agent.transform.position - agent.transform.forward * 1.15f;
                if (!Physics.Raycast(stand + Vector3.up, Vector3.down, out RaycastHit ground, 2f, world, QueryTriggerInteraction.Ignore)) continue;
                stand.y = ground.point.y + 0.04f;
                player.GetComponent<PlayerMotor>().Teleport(stand, agent.transform.eulerAngles.y);
                for (int i = 0; i < 40; i++) yield return null;
                bool began = takedown.TryBegin();
                results.Add($"{agent.name} kind={agent.Kind} position={agent.transform.position} began={began} result={takedown.LastResult}");
                if (began)
                {
                    accepted++;
                    if (agent.Kind == AgentKind.Hunter) { hunterAccepted++; stagedHunters.Add(agent.name); }
                    if (agent.name == "Hunter-1" || agent.name == "Hunter-2")
                    {
                        yield return WaitForPlaying();
                        Assert.That(takedown.CinematicBody.activeInHierarchy, Is.True);
                        for (int i = 0; i < 140; i++) yield return null;
                        CaptureDiagnostic(player.GetComponentInChildren<Camera>(), "BOOTSTRAP-" + agent.name + ".png");
                        for (int i = 0; i < 900 && takedown.IsBusy; i++) yield return null;
                        Assert.That(takedown.LastResult, Is.EqualTo("Completed"), agent.name);
                        Assert.That(takedown.ImpactCount, Is.EqualTo(1), agent.name);
                        Assert.That(player.GetComponent<PlayerMotor>().enabled, Is.True);
                        results.Add(agent.name + " completed actual-island playback, one impact, controls restored.");
                    }
                    else takedown.Cancel();
                }
            }
            string report = string.Join("\n", results);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "blackout-TD1", "bootstrap-eligibility.txt"), report);
            Debug.Log("[TD1 Bootstrap] " + report);
            Assert.That(accepted, Is.GreaterThan(0), "No island position could stage: " + report);
            Assert.That(hunterAccepted, Is.GreaterThan(0), "No forest Hunter position could stage: " + report);
            Assert.That(stagedHunters, Does.Contain("Hunter-1"), "The first forest Hunter must support a takedown on its normal terrain: " + report);
            Assert.That(stagedHunters, Does.Contain("Hunter-2"), "The second forest Hunter must support a takedown on its normal terrain: " + report);
        }

        [UnityTest]
        public IEnumerator ImportedPair_PlaysOnAllFourRigs_AndKeepsTheFinalCorpsePose()
        {
            Assert.That(takedown.SetupStatus, Is.EqualTo("Ready"));
            Camera camera = player.GetComponentInChildren<Camera>();
            foreach (string name in new[] { "SecurityPatrol", "Hunter", "TacticalGuard", "Commander" })
            {
                if (enemy != null) Object.Destroy(enemy);
                ClearTestDrops();
                yield return null;
                player.GetComponent<PlayerMotor>().Teleport(origin, 0f);
                enemy = SpawnEnemy(name, origin + Vector3.forward * 1.2f);
                target = enemy.GetComponent<AgentTakedownTarget>();
                for (int i = 0; i < 40; i++) yield return null;
                Assert.That(takedown.TryBegin(), Is.True, name + ": " + takedown.SetupStatus);
                yield return WaitForPlaying();
                Transform head = target.Animator.GetBoneTransform(HumanBodyBones.Head);
                Vector3 lastHead = head.position;
                float clock = 0f;
                int shot = 0;
                float[] samples = { 1.8f, 2.2f, 4.2f, 6.8f };
                for (int i = 0; i < 900 && takedown.IsBusy; i++)
                {
                    if (takedown.State == TakedownController.Phase.Playing)
                    {
                        clock += Time.deltaTime;
                        lastHead = head.position;
                        if (shot < samples.Length && clock >= samples[shot])
                        {
                            var actor = takedown.CinematicBody.GetComponentInChildren<TakedownActor>();
                            var data = new SerializedObject(actor);
                            var knife = (Transform)data.FindProperty("knife").objectReferenceValue;
                            Transform hand = actor.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                            Vector3 expected = hand.TransformPoint(data.FindProperty("knifePosition").vector3Value);
                            Assert.That(Vector3.Distance(knife.position, expected), Is.LessThan(0.1f), name + " knife must follow the animated hand");
                            Assert.That(knife.GetComponentsInChildren<Renderer>().Any(r => r.enabled && !ViewmodelHands.IsArmPart(r.name)), Is.True);
                            CaptureDiagnostic(camera, $"REAL-{name}-{samples[shot]:F1}.png");
                            shot++;
                        }
                    }
                    yield return null;
                }
                Assert.That(takedown.IsBusy, Is.False, name);
                Assert.That(target.Health.IsAlive, Is.False, name + " native impact event");
                Assert.That(takedown.ImpactCount, Is.EqualTo(1), name);
                Assert.That(target.Animator.enabled, Is.False, "preserve the paired collapse while the existing fade runs");
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(Vector3.Distance(head.position, lastHead), Is.LessThan(0.03f), name + " corpse must not snap to another death clip");
                Assert.That(head.position.y - origin.y, Is.LessThan(0.7f), name + " must visibly collapse");
                Assert.That(player.GetComponent<PlayerMotor>().enabled && weapons.enabled, Is.True);
                Assert.That(camera.fieldOfView, Is.EqualTo(72f));
                Debug.Log($"[TD1] Real Mixamo pair completed on {name}; impact count={takedown.ImpactCount}.");
            }
            for (int i = 0; i < 310; i++) yield return null;
            Assert.That(enemy.transform.Find("Visual").gameObject.activeSelf, Is.False, "normal death fade still completes");
        }

        [UnityTest]
        public IEnumerator ObstacleIntroducedDuringAlignment_CancelsWithoutDamage()
        {
            AssignDiagnosticClips();
            Assert.That(takedown.TryBegin(), Is.True);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(wall);
            wall.layer = PhysicsLayers.Level;
            wall.transform.position = origin + new Vector3(0f, 1f, 0.55f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.1f);
            Physics.SyncTransforms();
            for (int i = 0; i < 120 && takedown.IsBusy; i++) yield return null;
            Assert.That(takedown.IsBusy, Is.False);
            Assert.That(takedown.LastResult, Does.Contain("Alignment did not reach"));
            Assert.That(target.Health.IsAlive && target.Available, Is.True);
            Assert.That(player.GetComponent<PlayerMotor>().enabled && weapons.enabled, Is.True);
            Assert.That(takedown.CinematicBody.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ImportedPair_RejectsBlockedCollapseSpace()
        {
            foreach (Vector3 position in new[] { new Vector3(0f, 1f, 2.8f), new Vector3(-1.5f, 1f, 0.7f), new Vector3(1.5f, 1f, 0.7f) })
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(wall);
                wall.layer = PhysicsLayers.Level;
                wall.transform.position = origin + position;
                wall.transform.localScale = position.x == 0f ? new Vector3(4f, 3f, 0.1f) : new Vector3(0.1f, 3f, 4f);
            }
            Physics.SyncTransforms();
            Assert.That(takedown.TryBegin(), Is.False, "the visible collapse cannot pass through a wall beyond the target");
            yield return null;
        }

        private static void CaptureDiagnostic(Camera camera, string filename = "TEST-ONLY-reused-death-clips-not-takedown-animation.png")
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            string folder = Path.Combine(Path.GetTempPath(), "blackout-TD1", "diagnostic-preview");
            Directory.CreateDirectory(folder);
            var render = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                render.Release();
                Object.DestroyImmediate(render);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
#endif
