using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Agents.PlayTests
{
    /// <summary>
    /// Plays the real game scenes (Environment plus Agents, so the spawner uses the real markers and the real NavMesh)
    /// with every agent running, and measures what the A5 spec asks for: frame time, errors (null references counted
    /// on their own) and stuck counts. The numbers go to a text file outside the repo and to the Console.
    /// Frame time is CPU work per frame: nothing is drawn when Unity runs in batch mode, and the frame rate is left
    /// uncapped, so the figure is the cost of the agents, physics and scripts. Run it from the Editor for a figure
    /// that includes rendering.
    /// </summary>
    public sealed class AgentStressRun
    {
        public const string EnvironmentScene = "Assets/Scenes/Environment.unity";
        public const string AgentsScene = "Assets/Scenes/Agents.unity";
        public const int ExpectedAgents = 8; // Hunter x2, Security Patrol x3, Tactical Guard x2, Commander x1

        private const float BinMilliseconds = 0.25f;
        private const int BinCount = 400; // up to 100 ms

        public sealed class AgentResult
        {
            public string name;
            public float distanceMoved;
            public int stuckCount;
            public bool pathFailed;
            public bool alive;
            public string state;
        }

        public int agentsSpawned;
        public int agentsAliveAtEnd;
        public double seconds;
        public long frames;
        public float averageFrameMs;
        public float p95FrameMs;
        public float maxFrameMs;
        public int exceptions;
        public int nullReferences;
        public int errorLogs;
        public int warnings;
        public int totalStuck;
        public int agentsThatMoved;
        public readonly List<AgentResult> agents = new List<AgentResult>();
        public string reportPath;

        private readonly int[] histogram = new int[BinCount];

        /// <summary>Loads the scenes, runs for the given real seconds, then fills in this object and writes the report.</summary>
        public IEnumerator Run(float runSeconds)
        {
            // The scenes are not in Build Settings, so they are loaded by path; that call exists only in the Editor,
            // which is the only place these tests run (the assembly is excluded from builds by UNITY_INCLUDE_TESTS).
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(EnvironmentScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(AgentsScene, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            Debug.LogError("AgentStressRun can only load its scenes in the Editor.");
            yield break;
#endif

            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Application.logMessageReceived += OnLog;
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true; // count problems instead of stopping at the first one
            try
            {
                // Spawning happens in the spawner's Start; give every agent a few frames to start and register.
                AgentRegistry registry = null;
                float waited = 0f;
                while (waited < 5f)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                    registry = AgentRegistry.Current;
                    if (registry != null && registry.Agents.Count >= ExpectedAgents)
                    {
                        break;
                    }
                }

                var transforms = new List<Transform>();
                var followers = new List<PathFollower>();
                var names = new List<string>();
                if (registry != null)
                {
                    foreach (IAgentInfo agent in registry.Agents)
                    {
                        transforms.Add(agent.Transform);
                        followers.Add(agent.Transform.GetComponent<PathFollower>());
                        names.Add(agent.AgentName);
                    }
                }
                agentsSpawned = transforms.Count;

                var previous = new Vector3[agentsSpawned];
                var moved = new float[agentsSpawned];
                for (int i = 0; i < agentsSpawned; i++)
                {
                    previous[i] = transforms[i].position;
                }

                double elapsed = 0;
                double sum = 0;
                while (elapsed < runSeconds)
                {
                    yield return null;
                    float dt = Time.unscaledDeltaTime;
                    elapsed += dt;
                    sum += dt;
                    frames++;
                    maxFrameMs = Mathf.Max(maxFrameMs, dt * 1000f);
                    histogram[Mathf.Min(BinCount - 1, (int)(dt * 1000f / BinMilliseconds))]++;

                    for (int i = 0; i < agentsSpawned; i++)
                    {
                        if (transforms[i] == null)
                        {
                            continue; // destroyed
                        }
                        Vector3 position = transforms[i].position;
                        moved[i] += Vector3.Distance(previous[i], position);
                        previous[i] = position;
                    }
                }

                seconds = elapsed;
                averageFrameMs = frames > 0 ? (float)(sum / frames * 1000.0) : 0f;
                p95FrameMs = Percentile(0.95f);
                CollectAgentResults(registry, transforms, followers, names, moved);
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            WriteReport(runSeconds);
        }

        private void CollectAgentResults(AgentRegistry registry, List<Transform> transforms, List<PathFollower> followers, List<string> names, float[] moved)
        {
            for (int i = 0; i < transforms.Count; i++)
            {
                bool exists = transforms[i] != null;
                PathFollower follower = exists ? followers[i] : null;
                IReadOnlyDictionary<string, string> states = registry != null ? registry.Blackboard.AgentStates : null;
                string state = null;
                if (states != null)
                {
                    states.TryGetValue(names[i], out state);
                }

                var result = new AgentResult
                {
                    name = names[i],
                    distanceMoved = moved[i],
                    stuckCount = follower != null ? follower.StuckCount : 0,
                    pathFailed = follower != null && follower.HasFailed,
                    alive = exists && transforms[i].GetComponent<Blackout.Shared.Health>().IsAlive,
                    state = state ?? "-",
                };
                agents.Add(result);
                totalStuck += result.stuckCount;
                if (result.alive)
                {
                    agentsAliveAtEnd++;
                }
                if (result.distanceMoved > 1f)
                {
                    agentsThatMoved++;
                }
            }
        }

        private float Percentile(float fraction)
        {
            long target = (long)Math.Ceiling(frames * fraction);
            long seen = 0;
            for (int bin = 0; bin < BinCount; bin++)
            {
                seen += histogram[bin];
                if (seen >= target)
                {
                    return (bin + 1) * BinMilliseconds; // the top edge of the bin: never understates
                }
            }
            return BinCount * BinMilliseconds;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Exception:
                    exceptions++;
                    if (condition.Contains("NullReferenceException"))
                    {
                        nullReferences++;
                    }
                    break;
                case LogType.Error:
                case LogType.Assert:
                    errorLogs++;
                    break;
                case LogType.Warning:
                    warnings++;
                    break;
            }
        }

        public string Summary(float requestedSeconds)
        {
            var text = new StringBuilder();
            text.AppendLine("Blackout Island: agent stress run");
            text.AppendLine($"Scenes: {EnvironmentScene} + {AgentsScene}");
            text.AppendLine($"Duration: {seconds:F1} s real time (asked for {requestedSeconds:F0} s), {frames} frames");
            text.AppendLine($"Frame time (CPU work, no rendering in batch mode): average {averageFrameMs:F2} ms, 95th percentile {p95FrameMs:F2} ms, worst {maxFrameMs:F2} ms");
            text.AppendLine($"Agents: {agentsSpawned} spawned (expected {ExpectedAgents}), {agentsAliveAtEnd} alive at the end, {agentsThatMoved} moved more than 1 m");
            text.AppendLine($"Errors: {exceptions} exceptions ({nullReferences} null references), {errorLogs} error logs, {warnings} warnings");
            text.AppendLine($"Stuck events: {totalStuck} in total");
            text.AppendLine();
            text.AppendLine("agent            moved(m)  stuck  pathFailed  alive  state");
            foreach (AgentResult a in agents)
            {
                text.AppendLine($"{a.name,-16} {a.distanceMoved,8:F1}  {a.stuckCount,5}  {a.pathFailed,-10}  {a.alive,-5}  {a.state}");
            }
            return text.ToString();
        }

        private void WriteReport(float requestedSeconds)
        {
            string summary = Summary(requestedSeconds);
            reportPath = Path.Combine(Path.GetTempPath(), $"blackout-agent-stress-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(reportPath, summary);
            Debug.Log($"Agent stress run report written to {reportPath}\n{summary}");
        }
    }
}
