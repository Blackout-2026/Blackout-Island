namespace Blackout.Editor
{
    /// <summary>
    /// The Security Patrol soldier's pose (step PS4): the Mixamo skeleton holding the rifle at the low ready. The maths is <see cref="HumanoidRiflePose"/>, shared with the Hunter;
    /// the hold is the one found by a reach search over this soldier's measured arms.
    /// </summary>
    public static class PatrolSoldierPose
    {
        public static readonly HumanoidRiflePose Instance = new HumanoidRiflePose(new MixamoBones(), RifleHold.LowReady);
    }
}
