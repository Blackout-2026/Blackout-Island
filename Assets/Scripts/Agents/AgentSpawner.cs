// Owner: Agent Controller.
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Creates the agents in the Agents scene: one for every AgentSpawn marker in the Environment scene, from the
    /// prefab of its type. Population and routes come from markers, not hard-coded type counts.
    /// The marker's Owner becomes the agent's name, which is also the Owner of its Waypoint markers, so every
    /// brain finds its own route through <c>context.GetMarkers(MarkerType.Waypoint, AgentName)</c>. Each agent gets
    /// its type's numbers from the <see cref="DifficultyConfig"/>. <see cref="RespawnAll"/> replaces every agent
    /// with a fresh one, and is raised by the optional restart channel.
    /// </summary>
    public class AgentSpawner : MonoBehaviour
    {
        [Serializable]
        public class SpawnEntry
        {
            public AgentKind kind;
            [Tooltip("The agent prefab for this type: AgentBase with its model and its brain.")]
            public GameObject prefab;
            [Tooltip("Spawns one agent for each AgentSpawn marker whose Owner starts with this text, for example Hunter- or Patrol-.")]
            public string ownerPrefix;
        }

        [SerializeField] private DifficultyConfig difficulty;
        [SerializeField] private List<SpawnEntry> entries = new List<SpawnEntry>();
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Optional: raise this to replace every agent with a fresh one (the mission's Restart button).")]
        [SerializeField] private VoidEventChannel restartChannel;
        [Tooltip("For a prefab with no brain: add the placeholder that walks the agent's waypoints, instead of leaving it standing still. Turn off once the real brains are in the prefabs.")]
        [SerializeField] private bool placeholderBrainWhenMissing;
        [Tooltip("Seconds after death before a replacement returns to its spawn marker. Lab defenders stop returning once the cure is taken.")]
        [SerializeField, Min(5f)] private float respawnDelay = 20f;

        private sealed class SpawnSlot
        {
            public SpawnEntry Entry;
            public Marker Marker;
            public Health Health;
            public bool LabDefender;
            public float DeadSeconds;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<SpawnSlot> slots = new List<SpawnSlot>();
        private AgentRegistry registry;
        private VoidEventChannel boundRestart;
        private Blackboard boundBoard;
        private IDoorCombatLock labDoor;
        private Component labDoorComponent;

        public float RespawnDelay => respawnDelay;
        public int LivingLabDefenders { get; private set; }

        /// <summary>The agents this spawner created and has not replaced.</summary>
        public IReadOnlyList<GameObject> Spawned => spawned;

        private void Awake()
        {
            Bind(restartChannel);
        }

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnAll();
            }
        }

        private void OnDestroy()
        {
            Bind(null);
            BindBoard(null);
            DestroyAll();
        }

        private void OnEnable()
        {
            if (registry != null) BindBoard(registry.Blackboard);
        }

        private void OnDisable() => BindBoard(null);

        private void Update() => TickRespawns(Time.deltaTime);

        /// <summary>Replaces the Inspector setup. Awake uses the serialized fields; tests plug in their own.</summary>
        public void Configure(AgentRegistry agentRegistry, DifficultyConfig newDifficulty, IEnumerable<SpawnEntry> newEntries,
            VoidEventChannel newRestartChannel = null, bool addPlaceholderBrain = false)
        {
            placeholderBrainWhenMissing = addPlaceholderBrain;
            registry = agentRegistry;
            difficulty = newDifficulty;
            entries = new List<SpawnEntry>(newEntries);
            Bind(newRestartChannel);
            if (isActiveAndEnabled && registry != null) BindBoard(registry.Blackboard);
        }

        /// <summary>Creates an agent for every matching spawn marker and returns how many it created.</summary>
        public int SpawnAll()
        {
            if (slots.Count != 0) return 0; // repeated calls must not double the roster
            AgentRegistry source = Registry();
            if (source == null)
            {
                Debug.LogWarning("AgentSpawner: no AgentRegistry in the loaded scenes, so there are no markers to spawn from.", this);
                return 0;
            }

            BindBoard(source.Blackboard);
            FindLabDoor();
            IReadOnlyList<Marker> spawnMarkers = source.GetMarkers(MarkerType.AgentSpawn);
            int created = 0;
            foreach (SpawnEntry entry in entries)
            {
                if (entry.prefab == null)
                {
                    Debug.LogWarning($"AgentSpawner: the {entry.kind} entry has no prefab, so no {entry.kind} was spawned.", this);
                    continue;
                }

                foreach (Marker marker in spawnMarkers)
                {
                    if (!string.IsNullOrEmpty(entry.ownerPrefix) && marker.Owner != null && marker.Owner.StartsWith(entry.ownerPrefix, StringComparison.Ordinal))
                    {
                        Spawn(entry, marker);
                        slots.Add(new SpawnSlot
                        {
                            Entry = entry, Marker = marker,
                            Health = spawned[spawned.Count - 1].GetComponent<Health>(),
                            LabDefender = IsInLab(marker.transform.position)
                        });
                        created++;
                    }
                }
            }
            RefreshLabLock();
            return created;
        }

        /// <summary>Removes every agent this spawner created and spawns a fresh set. Dead agents are replaced too.</summary>
        public void RespawnAll()
        {
            DestroyAll();
            SpawnAll();
        }

        /// <summary>Update's clock, exposed for focused tests. One bounded slot per original spawn.</summary>
        public void TickRespawns(float deltaTime)
        {
            bool cureTaken = registry != null && registry.Blackboard.cureTaken;
            for (int i = 0; i < slots.Count; i++)
            {
                SpawnSlot slot = slots[i];
                if (slot.Health != null && slot.Health.IsAlive) continue;
                // The membership belongs to the spawn, even if the defender chased out of the lab.
                if (cureTaken && slot.LabDefender) continue;
                slot.DeadSeconds += Mathf.Max(0f, deltaTime);
                if (slot.DeadSeconds >= respawnDelay && slot.Marker != null) Replace(i);
            }
            RefreshLabLock();
        }

        private void Replace(int index)
        {
            SpawnSlot slot = slots[index];
            RemoveAgent(spawned[index]);
            Spawn(slot.Entry, slot.Marker);
            spawned[index] = spawned[spawned.Count - 1];
            spawned.RemoveAt(spawned.Count - 1);
            slot.Health = spawned[index].GetComponent<Health>();
            slot.DeadSeconds = 0f;
        }

        private static bool IsInLab(Vector3 p) => Mathf.Abs(p.x) <= 10f && p.z >= 124f && p.z <= 142f && p.y < 9f;

        private void BindBoard(Blackboard board)
        {
            if (boundBoard != null) boundBoard.CureTakenReported -= OnCureTaken;
            boundBoard = board;
            if (boundBoard != null)
            {
                boundBoard.CureTakenReported += OnCureTaken;
                if (boundBoard.cureTaken) OnCureTaken();
            }
        }

        private void OnCureTaken()
        {
            // A living lab defender may have followed the player outside. Return it to its post so
            // locking the exit cannot strand a required target on the other side of the door.
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].LabDefender && slots[i].Health != null && slots[i].Health.IsAlive &&
                    !IsInLab(spawned[i].transform.position)) Replace(i);
            if (labDoorComponent == null) FindLabDoor();
            RefreshLabLock();
        }

        private void FindLabDoor()
        {
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (component.name == "Door_D-Lab" && component is IDoorCombatLock door)
                {
                    labDoor = door;
                    labDoorComponent = component;
                    return;
                }
        }

        private void RefreshLabLock()
        {
            LivingLabDefenders = 0;
            foreach (SpawnSlot slot in slots)
                if (slot.LabDefender && slot.Health != null && slot.Health.IsAlive) LivingLabDefenders++;
            if (labDoorComponent != null)
                labDoor.SetCombatLocked(registry != null && registry.Blackboard.cureTaken && LivingLabDefenders > 0);
        }

        private void Spawn(SpawnEntry entry, Marker marker)
        {
            Vector3 position = marker.transform.position;
            Quaternion rotation = Quaternion.Euler(0f, marker.transform.eulerAngles.y, 0f); // upright, facing as the marker does
            GameObject agent = Instantiate(entry.prefab, position, rotation, transform);
            agent.name = marker.Owner;
            agent.GetComponent<AgentRoot>()?.SetKind(entry.kind);

            if (difficulty != null)
            {
                AgentStatsApplier.Apply(agent, difficulty.Get(entry.kind));
            }
            else
            {
                Debug.LogWarning("AgentSpawner: no DifficultyConfig, so agents keep their prefab numbers.", this);
            }

            if (placeholderBrainWhenMissing && agent.GetComponent<IAgentBrain>() == null)
            {
                AgentRoot root = agent.GetComponent<AgentRoot>();
                if (root != null)
                {
                    root.SetBrain(agent.AddComponent<WaypointWalkerBrain>());
                }
            }

            spawned.Add(agent);
        }

        private void DestroyAll()
        {
            foreach (GameObject agent in spawned)
            {
                RemoveAgent(agent);
            }
            spawned.Clear();
            slots.Clear();
            LivingLabDefenders = 0;
        }

        private void RemoveAgent(GameObject agent)
        {
            if (agent == null) return;
            // Unregister before deferred destruction: the replacement reuses the same Blackboard name.
            AgentRoot root = agent.GetComponent<AgentRoot>();
            if (registry != null && root != null) registry.Unregister(root);
            agent.SetActive(false);
            if (Application.isPlaying) Destroy(agent);
            else DestroyImmediate(agent);
        }

        private AgentRegistry Registry()
        {
            if (registry == null)
            {
                registry = AgentRegistry.Current != null ? AgentRegistry.Current : FindFirstObjectByType<AgentRegistry>();
            }
            return registry;
        }

        private void Bind(VoidEventChannel channel)
        {
            if (boundRestart != null)
            {
                boundRestart.OnRaised -= RespawnAll;
            }
            boundRestart = channel;
            if (boundRestart != null)
            {
                boundRestart.OnRaised += RespawnAll;
            }
        }
    }
}
