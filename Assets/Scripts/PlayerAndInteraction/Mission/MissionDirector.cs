// Owner: Systems Engineer.
using System.Collections.Generic;
using Blackout.Interaction;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEngine;

namespace Blackout.Mission
{
    /// <summary>
    /// Runs the mission (docs/reference/systems-engineer.md section 11) on top of <see cref="MissionStateMachine"/>:
    /// feeds it the player's position and the GeneratorDisabled and CureTaken events, raises ObjectiveChanged, tells
    /// the CheckpointTracker which checkpoint to use, counts kills and deaths, and on reaching the boat with the cure
    /// raises MissionComplete and shows the Mission Complete screen. Started by SceneBootstrap once the level
    /// scenes have loaded; markers and the bunker door are in other scenes, so they are found at runtime.
    /// </summary>
    public class MissionDirector : MonoBehaviour
    {
        private const string BunkerDoorName = "Door_D-Bunker";

        [Header("Event channels (Assets/ScriptableObjects/Events)")]
        [SerializeField] private VoidEventChannel generatorDisabled;
        [SerializeField] private VoidEventChannel cureTaken;
        [SerializeField] private VoidEventChannel playerDied;
        [SerializeField] private VoidEventChannel missionComplete;
        [SerializeField] private StringEventChannel objectiveChanged;

        [SerializeField] private MissionCompleteScreen completeScreen;

        private readonly MissionStateMachine mission = new MissionStateMachine();
        private readonly Dictionary<string, Marker> markers = new Dictionary<string, Marker>();
        private GameObject player;
        private PlayerMotor motor;
        private PlayerHealth playerHealth;
        private PlayerInputReader input;
        private CheckpointTracker checkpoints;
        private WeaponController weapons;
        private IDoor bunkerDoor;
        private float startTime;
        private bool running;
        private bool paused;

        public MissionPhase Phase => mission.Phase;
        public bool IsRunning => running;
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public float ElapsedTime => running ? Time.time - startTime : 0f;

        private void OnEnable()
        {
            Subscribe(generatorDisabled, OnGeneratorDisabled, true);
            Subscribe(cureTaken, OnCureTaken, true);
            Subscribe(playerDied, OnPlayerDied, true);
        }

        private void OnDisable()
        {
            Subscribe(generatorDisabled, OnGeneratorDisabled, false);
            Subscribe(cureTaken, OnCureTaken, false);
            Subscribe(playerDied, OnPlayerDied, false);
        }

        private void OnDestroy()
        {
            if (weapons != null)
            {
                weapons.HitConfirmed -= OnHitConfirmed;
            }

            if (paused)
            {
                Time.timeScale = 1f; // never leave the game frozen for the next scene or Edit mode
            }
        }

        /// <summary>Starts the mission with this player. Called by SceneBootstrap after the scenes have loaded.</summary>
        public void Begin(GameObject playerObject)
        {
            player = playerObject;
            motor = player.GetComponent<PlayerMotor>();
            playerHealth = player.GetComponent<PlayerHealth>();
            input = player.GetComponent<PlayerInputReader>();
            checkpoints = player.GetComponent<CheckpointTracker>();
            weapons = player.GetComponentInChildren<WeaponController>(true);
            if (motor == null || checkpoints == null)
            {
                Debug.LogError("[Blackout] MissionDirector: the player needs a PlayerMotor and a CheckpointTracker.", this);
                return;
            }

            checkpoints.ReachByDistance = false;
            if (weapons != null)
            {
                weapons.HitConfirmed += OnHitConfirmed;
            }

            FindMarkers();
            bunkerDoor = FindBunkerDoor();
            startTime = Time.time;
            running = true;
            Apply(MissionChange.Objective | MissionChange.Checkpoint);
            Debug.Log($"[Blackout] Mission started: {mission.Phase}.", this);
        }

        private void Update()
        {
            if (!running || mission.IsComplete)
            {
                return;
            }

            // The objective switches once the bunker door has been opened, and stays switched (as in the prototype).
            if (mission.Phase == MissionPhase.BunkerEntrance && !mission.BunkerDoorOpen && bunkerDoor != null
                && bunkerDoor.IsOpen)
            {
                Apply(mission.SetBunkerDoorOpen(true));
            }

            if (playerHealth == null || !playerHealth.IsDead)
            {
                Apply(mission.Tick(motor.transform.position));
            }
        }

        private void OnGeneratorDisabled()
        {
            Apply(mission.OnGeneratorDisabled());
        }

        private void OnCureTaken()
        {
            Apply(mission.OnCureTaken());
        }

        private void OnPlayerDied()
        {
            if (running && !mission.IsComplete)
            {
                Deaths++;
            }
        }

        private void OnHitConfirmed(bool killed)
        {
            if (killed && running && !mission.IsComplete)
            {
                Kills++;
            }
        }

        /// <summary>Turns what the state machine changed into events, the checkpoint and the end screen.</summary>
        private void Apply(MissionChange change)
        {
            if (!running || change == MissionChange.None)
            {
                return;
            }

            if ((change & MissionChange.Phase) != 0)
            {
                Debug.Log($"[Blackout] Mission phase: {mission.Phase}.", this);
            }

            if ((change & MissionChange.Objective) != 0 && objectiveChanged != null)
            {
                objectiveChanged.Raise(mission.Objective);
            }

            if ((change & MissionChange.Checkpoint) != 0)
            {
                if (markers.TryGetValue(mission.CheckpointId, out Marker marker) && marker != null)
                {
                    checkpoints.SetCheckpoint(marker);
                }
                else
                {
                    Debug.LogWarning($"[Blackout] No marker '{mission.CheckpointId}'; the checkpoint is unchanged.", this);
                }
            }

            if ((change & MissionChange.Phase) != 0 && mission.IsComplete)
            {
                Complete();
            }
        }

        /// <summary>Raises MissionComplete once, stops the player and the game, and shows the end screen.</summary>
        private void Complete()
        {
            float time = ElapsedTime;
            if (missionComplete != null)
            {
                missionComplete.Raise();
            }

            if (input != null)
            {
                input.enabled = false; // stops movement, look and weapons
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
            paused = true;
            if (completeScreen != null)
            {
                completeScreen.Show(time, Kills, Deaths);
            }

            Debug.Log($"[Blackout] Mission complete: {MissionStateMachine.FormatTime(time)}, {Kills} kills, " +
                      $"{Deaths} deaths.", this);
        }

        private void FindMarkers()
        {
            markers.Clear();
            foreach (Marker marker in FindObjectsByType<Marker>(FindObjectsSortMode.None))
            {
                if (string.IsNullOrEmpty(marker.Id))
                {
                    continue;
                }

                markers[marker.Id] = marker;
                if (marker.Type == MarkerType.BoatExit)
                {
                    mission.SetBoatExit(marker.transform.position);
                }
            }

            if (markers.Count == 0)
            {
                Debug.LogWarning("[Blackout] MissionDirector found no markers; is Environment loaded?", this);
            }
        }

        private static IDoor FindBunkerDoor()
        {
            foreach (SlidingDoor door in FindObjectsByType<SlidingDoor>(FindObjectsSortMode.None))
            {
                if (door.name == BunkerDoorName)
                {
                    return door;
                }
            }

            return null;
        }

        private static void Subscribe(VoidEventChannel channel, System.Action handler, bool on)
        {
            if (channel == null)
            {
                return;
            }

            if (on)
            {
                channel.OnRaised += handler;
            }
            else
            {
                channel.OnRaised -= handler;
            }
        }
    }
}
