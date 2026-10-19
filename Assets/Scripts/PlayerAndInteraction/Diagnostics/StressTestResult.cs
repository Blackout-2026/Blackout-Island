// Owner: Systems Engineer.
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Blackout.Diagnostics
{
    /// <summary>What a <see cref="StressTest"/> run counted and measured, and the text report made from it.</summary>
    public sealed class StressTestResult
    {
        // At most this many log messages and missed-hit details are kept for the report.
        private const int MaxDetails = 10;

        public readonly int[] CollidersPerLayer = new int[32];
        public readonly int[] PelletsStoppedPerLayer = new int[32];
        public readonly List<string> Details = new List<string>();

        // Scene inventory, taken before the dummy is made.
        public int Colliders;
        public int Triggers;
        public int MeshColliders;
        public int DynamicBodies;
        public int KinematicBodies;
        public int NamedLayers;
        public int NamedPairs;
        public int NamedPairsOff;
        public float FixedDeltaTime;
        public int SolverIterations;

        // The run.
        public int PhasesVisited;
        public int PhasesWithoutRoom;
        public bool ReachedComplete;
        public int Pellets;
        public int DummyHits;
        public int BlockedBeforeDummy;
        public int MissedHits;
        public int SelfBlocked;
        public int PelletsWide;
        public int KnifeSwings;
        public int KnifeLanded;
        public int Kills;

        // Problems logged while the run was going.
        public int NullReferences;
        public int Exceptions;
        public int Errors;

        // Allocation by the firing calls, after the warm-up shots.
        public int MeasuredShots;
        public long FireAllocatedBytes;

        // Raycast benchmark.
        public int BenchmarkRaysPerMask;
        public long MaskedTicks;
        public long AllLayersTicks;
        public int MaskedRayHits;
        public int AllLayersRayHits;

        public double MaskedMicrosecondsPerRay => Microseconds(MaskedTicks, BenchmarkRaysPerMask);
        public double AllLayersMicrosecondsPerRay => Microseconds(AllLayersTicks, BenchmarkRaysPerMask);

        /// <summary>True when the run found no null reference, exception or missed hit.</summary>
        public bool Passed => NullReferences == 0 && Exceptions == 0 && MissedHits == 0 && KnifeLanded == KnifeSwings;

        public void AddDetail(string text)
        {
            if (Details.Count < MaxDetails)
            {
                Details.Add(text);
            }
        }

        public string ToReport()
        {
            var text = new StringBuilder();
            text.AppendLine($"[Blackout] Stress Test: {(Passed ? "PASSED" : "FAILED")}");
            text.AppendLine($"Problems: null references {NullReferences}, exceptions {Exceptions}, errors {Errors}, " +
                            $"missed hits {MissedHits}, knife {KnifeLanded}/{KnifeSwings} landed");
            text.AppendLine($"Run: {PhasesVisited} phases ({PhasesWithoutRoom} without room for the gun dummy), " +
                            $"Complete reached: {ReachedComplete}, kills counted {Kills}");
            text.AppendLine($"Pellets {Pellets}: dummy {DummyHits}, blocked in front of it {BlockedBeforeDummy}, " +
                            $"wide of it {PelletsWide}, own collider {SelfBlocked}, missed {MissedHits}");
            text.AppendLine("Pellets stopped per layer: " + Layers(PelletsStoppedPerLayer));
            double bytesPerShot = MeasuredShots > 0 ? (double)FireAllocatedBytes / MeasuredShots : 0d;
            text.AppendLine($"Firing allocated {FireAllocatedBytes} B over {MeasuredShots} shots " +
                            $"({bytesPerShot:0.#} B per shot, after the warm-up)");
            text.AppendLine($"Raycast benchmark, {BenchmarkRaysPerMask} rays per mask: PlayerShots " +
                            $"{MaskedMicrosecondsPerRay:0.###} us/ray ({MaskedRayHits} hit), all layers " +
                            $"{AllLayersMicrosecondsPerRay:0.###} us/ray ({AllLayersRayHits} hit)");
            text.AppendLine($"Scene: {Colliders} colliders ({Triggers} triggers, {MeshColliders} mesh), rigidbodies " +
                            $"{DynamicBodies} dynamic + {KinematicBodies} kinematic");
            text.AppendLine("Colliders per layer: " + Layers(CollidersPerLayer));
            text.AppendLine($"Matrix: {NamedPairsOff} of {NamedPairs} pairs between the {NamedLayers} named layers " +
                            $"are off; fixed step {FixedDeltaTime} s, solver iterations {SolverIterations}");
            foreach (string detail in Details)
            {
                text.AppendLine("  " + detail);
            }

            return text.ToString();
        }

        private static string Layers(int[] counts)
        {
            var text = new StringBuilder();
            for (int layer = 0; layer < counts.Length; layer++)
            {
                if (counts[layer] == 0)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(", ");
                }

                string name = LayerMask.LayerToName(layer);
                text.Append(string.IsNullOrEmpty(name) ? $"layer {layer}" : name).Append(' ').Append(counts[layer]);
            }

            return text.Length > 0 ? text.ToString() : "none";
        }

        private static double Microseconds(long ticks, int rays)
        {
            return rays > 0 ? ticks * 1e6 / Stopwatch.Frequency / rays : 0d;
        }
    }
}
