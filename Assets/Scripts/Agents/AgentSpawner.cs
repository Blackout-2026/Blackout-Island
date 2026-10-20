// Owner: Agent Controller.
using System;
using System.Collections.Generic;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Creates the agents in the Agents scene: one for every AgentSpawn marker in the Environment scene, from the
    /// prefab of its type (Hunter x2, Security Patrol x3, Tactical Guard x2, Commander x1 with the current markers).
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

        private readonly List<GameObject> spawned = new List<GameObject>();
        private AgentRegistry registry;
        private VoidEventChannel boundRestart;

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
        }

        /// <summary>Replaces the Inspector setup. Awake uses the serialized fields; tests plug in their own.</summary>
        public void Configure(AgentRegistry agentRegistry, DifficultyConfig newDifficulty, IEnumerable<SpawnEntry> newEntries,
            VoidEventChannel newRestartChannel = null, bool addPlaceholderBrain = false)
        {
            placeholderBrainWhenMissing = addPlaceholderBrain;
            registry = agentRegistry;
            difficulty = newDifficulty;
            entries = new List<SpawnEntry>(newEntries);
            Bind(newRestartChannel);
        }

        /// <summary>Creates an agent for every matching spawn marker and returns how many it created.</summary>
        public int SpawnAll()
        {
            AgentRegistry source = Registry();
            if (source == null)
            {
                Debug.LogWarning("AgentSpawner: no AgentRegistry in the loaded scenes, so there are no markers to spawn from.", this);
                return 0;
            }

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
                        created++;
                    }
                }
            }
            return created;
        }

        /// <summary>Removes every agent this spawner created and spawns a fresh set. Dead agents are replaced too.</summary>
        public void RespawnAll()
        {
            DestroyAll();
            SpawnAll();
        }

        private void Spawn(SpawnEntry entry, Marker marker)
        {
            Vector3 position = marker.transform.position;
            Quaternion rotation = Quaternion.Euler(0f, marker.transform.eulerAngles.y, 0f); // upright, facing as the marker does
            GameObject agent = Instantiate(entry.prefab, position, rotation);
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
            AgentRegistry source = Registry();
            foreach (GameObject agent in spawned)
            {
                if (agent == null)
                {
                    continue;
                }

                // Leave the registry now: Destroy only takes effect at the end of the frame, and the replacement has
                // the same name, so the old agent's late clean-up would otherwise erase the new one's Blackboard entry.
                AgentRoot root = agent.GetComponent<AgentRoot>();
                if (source != null && root != null)
                {
                    source.Unregister(root);
                }

                if (Application.isPlaying)
                {
                    Destroy(agent);
                }
                else
                {
                    DestroyImmediate(agent);
                }
            }
            spawned.Clear();
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
