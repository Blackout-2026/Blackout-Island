using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Mission.Tests
{
    public class MissionStateMachineTests
    {
        // Points on the route, from the zone and marker tables in docs/level-layout.md.
        private static readonly Vector3 Dock = new Vector3(0f, 1.3f, -121f);
        private static readonly Vector3 Trail = new Vector3(0f, 4f, -80f);
        private static readonly Vector3 CompoundYard = new Vector3(0f, 10f, 40f);
        private static readonly Vector3 SecurityArea = new Vector3(0f, 2.5f, 76f);
        private static readonly Vector3 CorridorMiddle = new Vector3(0f, 2.5f, 100f);
        private static readonly Vector3 GeneratorRoom = new Vector3(-11f, 2.5f, 106f);
        private static readonly Vector3 Laboratory = new Vector3(0f, 2.5f, 133f);
        private static readonly Vector3 OutsideGate = new Vector3(0f, 10f, 20f);
        private static readonly Vector3 BoatExit = new Vector3(0f, 1.3f, -124f);

        private static MissionStateMachine NewMission()
        {
            var mission = new MissionStateMachine();
            mission.SetBoatExit(BoatExit);
            return mission;
        }

        [Test]
        public void Start_IsBeach_WithBeachObjectiveAndCheckpoint()
        {
            var mission = NewMission();
            Assert.AreEqual(MissionPhase.Beach, mission.Phase);
            Assert.AreEqual(MissionStateMachine.ObjectiveBeach, mission.Objective);
            Assert.AreEqual(MissionStateMachine.CheckpointBeach, mission.CheckpointId);
        }

        [Test]
        public void Beach_StaysWhileOnTheDock()
        {
            var mission = NewMission();
            Assert.AreEqual(MissionChange.None, mission.Tick(Dock));
            Assert.AreEqual(MissionPhase.Beach, mission.Phase);
        }

        [Test]
        public void Beach_ToForest_ChangesPhaseObjectiveAndCheckpoint()
        {
            var mission = NewMission();
            MissionChange change = mission.Tick(Trail);
            Assert.AreEqual(MissionPhase.Forest, mission.Phase);
            Assert.AreEqual(MissionChange.Phase | MissionChange.Objective | MissionChange.Checkpoint, change);
            Assert.AreEqual(MissionStateMachine.CheckpointForest, mission.CheckpointId);
        }

        [Test]
        public void Forest_ToBunkerEntrance_InTheCompound()
        {
            var mission = NewMission();
            mission.Tick(Trail);
            mission.Tick(CompoundYard);
            Assert.AreEqual(MissionPhase.BunkerEntrance, mission.Phase);
            Assert.AreEqual(MissionStateMachine.ObjectiveBunkerDoor, mission.Objective);
            Assert.AreEqual(MissionStateMachine.CheckpointBunkerEntrance, mission.CheckpointId);
        }

        [Test]
        public void Beach_CanSkipStraightToTheCompound()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            Assert.AreEqual(MissionPhase.BunkerEntrance, mission.Phase);
        }

        [Test]
        public void BunkerDoorOpened_ChangesOnlyTheObjective()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            Assert.AreEqual(MissionChange.Objective, mission.SetBunkerDoorOpen(true));
            Assert.AreEqual(MissionStateMachine.ObjectiveRamp, mission.Objective);
            Assert.AreEqual(MissionChange.None, mission.SetBunkerDoorOpen(true));
        }

        [Test]
        public void BunkerEntrance_ToCorridor_Underground_KeepsTheEntranceCheckpoint()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            MissionChange change = mission.Tick(SecurityArea);
            Assert.AreEqual(MissionPhase.Corridor, mission.Phase);
            Assert.AreEqual(MissionChange.Phase | MissionChange.Objective, change);
            Assert.AreEqual(MissionStateMachine.CheckpointBunkerEntrance, mission.CheckpointId);
        }

        [Test]
        public void Corridor_ToGeneratorRoom()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            mission.Tick(CorridorMiddle);
            mission.Tick(GeneratorRoom);
            Assert.AreEqual(MissionPhase.GeneratorRoom, mission.Phase);
            Assert.AreEqual(MissionStateMachine.ObjectiveGenerator, mission.Objective);
            Assert.AreEqual(MissionStateMachine.CheckpointGeneratorRoom, mission.CheckpointId);
        }

        [Test]
        public void GeneratorDisabled_MovesToLab_AndKeepsTheGeneratorCheckpoint()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            mission.Tick(GeneratorRoom);
            MissionChange change = mission.OnGeneratorDisabled();
            Assert.AreEqual(MissionPhase.Lab, mission.Phase);
            Assert.AreEqual(MissionChange.Phase | MissionChange.Objective, change);
            Assert.AreEqual(MissionStateMachine.ObjectiveCure, mission.Objective);
            Assert.AreEqual(MissionStateMachine.CheckpointGeneratorRoom, mission.CheckpointId);
        }

        [Test]
        public void CureTaken_MovesToEscape()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            mission.Tick(GeneratorRoom);
            mission.OnGeneratorDisabled();
            mission.Tick(Laboratory);
            Assert.AreEqual(MissionPhase.Lab, mission.Phase, "walking into the lab does not change the phase");
            mission.OnCureTaken();
            Assert.AreEqual(MissionPhase.Escape, mission.Phase);
            Assert.AreEqual(MissionStateMachine.ObjectiveEscape, mission.Objective);
        }

        [Test]
        public void Escape_CheckpointsMoveOutwardAlongTheRoute()
        {
            var mission = NewMission();
            mission.JumpTo(MissionPhase.Escape);
            mission.Tick(CorridorMiddle);
            Assert.AreEqual(MissionStateMachine.CheckpointGeneratorRoom, mission.CheckpointId);
            Assert.AreEqual(MissionChange.Checkpoint, mission.Tick(CompoundYard));
            Assert.AreEqual(MissionStateMachine.CheckpointEscapeEntrance, mission.CheckpointId);
            Assert.AreEqual(MissionChange.Checkpoint, mission.Tick(OutsideGate));
            Assert.AreEqual(MissionStateMachine.CheckpointEscapeForest, mission.CheckpointId);
            Assert.AreEqual(MissionChange.None, mission.Tick(CompoundYard), "never moves back inward");
        }

        [Test]
        public void Escape_ToComplete_AtTheBoat()
        {
            var mission = NewMission();
            mission.JumpTo(MissionPhase.Escape);
            mission.Tick(new Vector3(0f, 1.3f, -110f));
            Assert.AreEqual(MissionPhase.Escape, mission.Phase, "14 m from the boat");
            MissionChange change = mission.Tick(BoatExit + new Vector3(3f, 0f, 3f));
            Assert.AreEqual(MissionPhase.Complete, mission.Phase);
            Assert.IsTrue((change & MissionChange.Phase) != 0);
            Assert.AreEqual(string.Empty, mission.Objective);
        }

        [Test]
        public void Boat_InTheWater_DoesNotComplete()
        {
            var mission = NewMission();
            mission.JumpTo(MissionPhase.Escape);
            mission.Tick(new Vector3(0f, 0.2f, -124f));
            Assert.AreEqual(MissionPhase.Escape, mission.Phase);
        }

        [Test]
        public void Boat_WithoutTheCure_DoesNotComplete()
        {
            var mission = NewMission();
            mission.Tick(BoatExit);
            Assert.AreEqual(MissionPhase.Beach, mission.Phase);
        }

        [Test]
        public void Boat_UnknownExit_DoesNotComplete()
        {
            var mission = new MissionStateMachine();
            mission.JumpTo(MissionPhase.Escape);
            mission.Tick(BoatExit);
            Assert.AreEqual(MissionPhase.Escape, mission.Phase);
        }

        [Test]
        public void Phases_NeverGoBack_WhenWalkingBack()
        {
            var mission = NewMission();
            mission.Tick(CompoundYard);
            Assert.AreEqual(MissionChange.None, mission.Tick(Dock));
            Assert.AreEqual(MissionPhase.BunkerEntrance, mission.Phase);
            Assert.AreEqual(MissionStateMachine.CheckpointBunkerEntrance, mission.CheckpointId);
        }

        [Test]
        public void GeneratorDisabled_Early_SkipsToLab()
        {
            var mission = NewMission();
            mission.OnGeneratorDisabled();
            Assert.AreEqual(MissionPhase.Lab, mission.Phase);
        }

        [Test]
        public void Complete_IsFinal()
        {
            var mission = NewMission();
            mission.JumpTo(MissionPhase.Escape);
            mission.Tick(BoatExit);
            Assert.AreEqual(MissionChange.None, mission.Tick(Dock));
            Assert.AreEqual(MissionChange.None, mission.OnCureTaken());
            Assert.AreEqual(MissionPhase.Complete, mission.Phase);
        }

        [Test]
        public void JumpBack_AfterTheBlackout_WalkingForwardReturnsToLab()
        {
            var mission = NewMission();
            mission.JumpTo(MissionPhase.Lab);
            mission.JumpTo(MissionPhase.Beach);
            Assert.AreEqual(MissionPhase.Beach, mission.Phase);
            Assert.AreEqual(MissionStateMachine.CheckpointBeach, mission.CheckpointId);
            mission.Tick(CompoundYard);
            mission.Tick(GeneratorRoom);
            Assert.AreEqual(MissionPhase.Lab, mission.Phase);
        }

        [Test]
        public void FullRun_VisitsEveryPhaseInOrder()
        {
            var mission = NewMission();
            var phases = new List<MissionPhase> { mission.Phase };
            void Record(MissionChange change)
            {
                if ((change & MissionChange.Phase) != 0)
                {
                    phases.Add(mission.Phase);
                }
            }

            Record(mission.Tick(Dock));
            Record(mission.Tick(Trail));
            Record(mission.Tick(CompoundYard));
            Record(mission.SetBunkerDoorOpen(true));
            Record(mission.Tick(SecurityArea));
            Record(mission.Tick(CorridorMiddle));
            Record(mission.Tick(GeneratorRoom));
            Record(mission.OnGeneratorDisabled());
            Record(mission.Tick(Laboratory));
            Record(mission.OnCureTaken());
            Record(mission.Tick(CompoundYard));
            Record(mission.Tick(OutsideGate));
            Record(mission.Tick(BoatExit));

            CollectionAssert.AreEqual(new[]
            {
                MissionPhase.Beach, MissionPhase.Forest, MissionPhase.BunkerEntrance, MissionPhase.Corridor,
                MissionPhase.GeneratorRoom, MissionPhase.Lab, MissionPhase.Escape, MissionPhase.Complete,
            }, phases);
        }

        [TestCase(0f, "0:00")]
        [TestCase(59.6f, "1:00")]
        [TestCase(125.4f, "2:05")]
        [TestCase(3725f, "62:05")]
        public void FormatTime_RoundsToWholeSeconds(float seconds, string expected)
        {
            Assert.AreEqual(expected, MissionStateMachine.FormatTime(seconds));
        }
    }
}
