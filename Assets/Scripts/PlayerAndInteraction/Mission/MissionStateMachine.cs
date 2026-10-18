// Owner: Systems Engineer.
using System;
using UnityEngine;

namespace Blackout.Mission
{
    /// <summary>What a call changed, so the MissionDirector only raises events when something did.</summary>
    [Flags]
    public enum MissionChange
    {
        None = 0,
        Phase = 1,
        Objective = 2,
        Checkpoint = 4,
    }

    /// <summary>
    /// The mission's rules without Unity objects, so the phase transitions can be tested in EditMode
    /// (docs/reference/systems-engineer.md section 11). The MissionDirector feeds it the player's position and
    /// the GeneratorDisabled and CureTaken events, and turns the returned changes into events and checkpoints.
    /// Zone transitions only move forward. GeneratorDisabled moves any earlier phase to Lab and CureTaken any
    /// earlier phase to Escape; Escape becomes Complete at the boat.
    /// </summary>
    public sealed class MissionStateMachine
    {
        public const string CheckpointBeach = "Checkpoint_Beach";
        public const string CheckpointForest = "Checkpoint_Forest";
        public const string CheckpointBunkerEntrance = "Checkpoint_BunkerEntrance";
        public const string CheckpointGeneratorRoom = "Checkpoint_GeneratorRoom";
        public const string CheckpointEscapeEntrance = "Checkpoint_EscapeEntrance";
        public const string CheckpointEscapeForest = "Checkpoint_EscapeForest";

        // The prototype's objective strings (game/mission.js).
        public const string ObjectiveBeach = "Reach the bunker in the north of the island";
        public const string ObjectiveForest = "Follow the trail and get into the bunker";
        public const string ObjectiveBunkerDoor = "Open the bunker door [E]";
        public const string ObjectiveRamp = "Go down the ramp to level B1";
        public const string ObjectiveCorridor = "Find the generator room";
        public const string ObjectiveGenerator = "Disable the generator — hold [E] at the control console";
        public const string ObjectiveCure = "Take the cure from the laboratory";
        public const string ObjectiveEscape = "Escape — get back to the boat!";

        private Vector3 boatExit;
        private bool hasBoatExit;

        public MissionStateMachine()
        {
            Phase = MissionPhase.Beach;
            CheckpointId = CheckpointBeach;
            Objective = ObjectiveFor(Phase);
        }

        public MissionPhase Phase { get; private set; }
        public bool GeneratorDisabled { get; private set; }
        public bool CureTaken { get; private set; }
        public bool BunkerDoorOpen { get; private set; }
        public bool IsComplete => Phase == MissionPhase.Complete;
        /// <summary>The Checkpoint marker id the player respawns at.</summary>
        public string CheckpointId { get; private set; }
        /// <summary>The HUD objective; empty once the mission is complete.</summary>
        public string Objective { get; private set; }

        /// <summary>The BoatExit marker's position. Until it is known the mission cannot be completed.</summary>
        public void SetBoatExit(Vector3 position)
        {
            boatExit = position;
            hasBoatExit = true;
        }

        /// <summary>Checks the player's position against the zones. Call once per frame while the player is alive.</summary>
        public MissionChange Tick(Vector3 player)
        {
            if (IsComplete)
            {
                return MissionChange.None;
            }

            MissionPhase next = Phase;
            if (next == MissionPhase.Beach && MissionZones.IsInForest(player))
            {
                next = MissionPhase.Forest;
            }

            if (next <= MissionPhase.Forest && MissionZones.IsInCompound(player))
            {
                next = MissionPhase.BunkerEntrance;
            }

            if (next == MissionPhase.BunkerEntrance && MissionZones.IsUnderground(player))
            {
                next = MissionPhase.Corridor;
            }

            if (next >= MissionPhase.BunkerEntrance && next <= MissionPhase.Corridor
                && MissionZones.IsInGeneratorRoom(player))
            {
                next = MissionPhase.GeneratorRoom;
            }

            // After a debug jump back, the generator and cure state pull the mission forward again at their phase.
            if (next == MissionPhase.GeneratorRoom && GeneratorDisabled)
            {
                next = MissionPhase.Lab;
            }

            if (next == MissionPhase.Lab && CureTaken)
            {
                next = MissionPhase.Escape;
            }

            if (next == MissionPhase.Escape && CureTaken && hasBoatExit && MissionZones.IsAtBoat(player, boatExit))
            {
                next = MissionPhase.Complete;
            }

            MissionChange change = SetPhase(next);
            if (Phase == MissionPhase.Escape)
            {
                change |= UpdateEscapeCheckpoint(player);
            }

            return change;
        }

        public MissionChange OnGeneratorDisabled()
        {
            GeneratorDisabled = true;
            return SetPhase(Phase < MissionPhase.Lab ? MissionPhase.Lab : Phase);
        }

        public MissionChange OnCureTaken()
        {
            if (IsComplete)
            {
                return MissionChange.None;
            }

            CureTaken = true;
            return SetPhase(Phase < MissionPhase.Escape ? MissionPhase.Escape : Phase);
        }

        /// <summary>The BunkerEntrance objective changes once the bunker door has been opened.</summary>
        public MissionChange SetBunkerDoorOpen(bool open)
        {
            if (open == BunkerDoorOpen)
            {
                return MissionChange.None;
            }

            BunkerDoorOpen = open;
            return RefreshObjective();
        }

        /// <summary>
        /// Debug: go straight to a phase, forwards or back. Lab and later count as the generator being off and
        /// Escape and later as the cure being taken; jumping back keeps both.
        /// </summary>
        public MissionChange JumpTo(MissionPhase phase)
        {
            GeneratorDisabled |= phase >= MissionPhase.Lab;
            CureTaken |= phase >= MissionPhase.Escape;
            MissionChange change = SetPhase(phase);
            if (phase >= MissionPhase.Escape && !IsEscapeCheckpoint(CheckpointId)
                && CheckpointId != CheckpointGeneratorRoom)
            {
                // The escape starts from the generator room checkpoint, as if the player had come that way.
                CheckpointId = CheckpointGeneratorRoom;
                change |= MissionChange.Checkpoint;
            }

            return change;
        }

        public static string ObjectiveFor(MissionPhase phase, bool bunkerDoorOpen = false, bool generatorDisabled = false)
        {
            switch (phase)
            {
                case MissionPhase.Beach: return ObjectiveBeach;
                case MissionPhase.Forest: return ObjectiveForest;
                case MissionPhase.BunkerEntrance: return bunkerDoorOpen ? ObjectiveRamp : ObjectiveBunkerDoor;
                case MissionPhase.Corridor: return ObjectiveCorridor;
                case MissionPhase.GeneratorRoom: return generatorDisabled ? ObjectiveCure : ObjectiveGenerator;
                case MissionPhase.Lab: return ObjectiveCure;
                case MissionPhase.Escape: return ObjectiveEscape;
                default: return string.Empty;
            }
        }

        /// <summary>The checkpoint a phase starts with, or null to keep the current one (Escape, Complete).</summary>
        public static string CheckpointFor(MissionPhase phase)
        {
            switch (phase)
            {
                case MissionPhase.Beach: return CheckpointBeach;
                case MissionPhase.Forest: return CheckpointForest;
                case MissionPhase.BunkerEntrance:
                case MissionPhase.Corridor: return CheckpointBunkerEntrance;
                case MissionPhase.GeneratorRoom:
                case MissionPhase.Lab: return CheckpointGeneratorRoom;
                default: return null;
            }
        }

        /// <summary>Mission time as the prototype shows it: rounded to whole seconds, "m:ss".</summary>
        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }

        private MissionChange SetPhase(MissionPhase phase)
        {
            if (phase == Phase)
            {
                return MissionChange.None;
            }

            Phase = phase;
            MissionChange change = MissionChange.Phase | RefreshObjective();
            string checkpoint = CheckpointFor(phase);
            if (checkpoint != null && checkpoint != CheckpointId)
            {
                CheckpointId = checkpoint;
                change |= MissionChange.Checkpoint;
            }

            return change;
        }

        private MissionChange RefreshObjective()
        {
            string objective = ObjectiveFor(Phase, BunkerDoorOpen, GeneratorDisabled);
            if (objective == Objective)
            {
                return MissionChange.None;
            }

            Objective = objective;
            return MissionChange.Objective;
        }

        /// <summary>During the escape the checkpoint follows the route back out, so a death near the boat does not
        /// send the player back underground.</summary>
        private MissionChange UpdateEscapeCheckpoint(Vector3 player)
        {
            bool inCompound = MissionZones.IsInCompound(player);
            if (!IsEscapeCheckpoint(CheckpointId) && inCompound)
            {
                CheckpointId = CheckpointEscapeEntrance;
                return MissionChange.Checkpoint;
            }

            if (CheckpointId == CheckpointEscapeEntrance && !inCompound && player.z < MissionZones.EscapeForestZ)
            {
                CheckpointId = CheckpointEscapeForest;
                return MissionChange.Checkpoint;
            }

            return MissionChange.None;
        }

        private static bool IsEscapeCheckpoint(string id)
        {
            return id == CheckpointEscapeEntrance || id == CheckpointEscapeForest;
        }
    }
}
