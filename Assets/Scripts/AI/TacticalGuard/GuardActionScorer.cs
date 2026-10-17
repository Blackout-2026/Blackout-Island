// Owner: Core Developer (Tactical Guard AI).
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// The guard's utility AI: four UtilityActions built from the T1 framework, with every curve and weight read
    /// from the config. <see cref="Choose"/> loads a situation into the considerations, scores the actions and
    /// returns the winner with the hysteresis bonus applied to the current one. Allocation-free after construction.
    /// </summary>
    public sealed class GuardActionScorer
    {
        public const int ActionCount = 4;

        private readonly TacticalGuardConfig config;
        private readonly UtilityAction[] actions = new UtilityAction[ActionCount];
        private readonly float[] scores = new float[ActionCount];

        private readonly Consideration attackLineOfSight = new Consideration();
        private readonly Consideration attackProximity = new Consideration();
        private readonly Consideration attackHealth = new Consideration();
        private readonly Consideration coverAvailability = new Consideration();
        private readonly Consideration coverThreat = new Consideration();
        private readonly Consideration repositionKnowledge = new Consideration();
        private readonly Consideration repositionDisengaged = new Consideration();
        private readonly Consideration holdPostPull = new Consideration();
        private readonly Consideration holdCalm = new Consideration();

        private UtilityAction current;

        public GuardActionScorer(TacticalGuardConfig config)
        {
            this.config = config;
            actions[(int)GuardActionType.Attack] = new UtilityAction("Attack", 1f, attackLineOfSight, attackProximity, attackHealth);
            actions[(int)GuardActionType.MoveToCover] = new UtilityAction("MoveToCover", 1f, coverAvailability, coverThreat);
            actions[(int)GuardActionType.Reposition] = new UtilityAction("Reposition", 1f, repositionKnowledge, repositionDisengaged);
            actions[(int)GuardActionType.HoldPost] = new UtilityAction("HoldPost", 1f, holdPostPull, holdCalm);
        }

        /// <summary>The action chosen by the last call to <see cref="Choose"/> (HoldPost before the first).</summary>
        public GuardActionType CurrentAction { get; private set; } = GuardActionType.HoldPost;

        /// <summary>The raw score (without the hysteresis bonus) of an action as of the last <see cref="Choose"/>.</summary>
        public float GetScore(GuardActionType action) => scores[(int)action];

        /// <summary>Forgets the current action, so the next choice has no hysteresis bonus (after respawn or a new post).</summary>
        public void Reset()
        {
            current = null;
            CurrentAction = GuardActionType.HoldPost;
        }

        public GuardActionType Choose(in GuardSituation situation)
        {
            SyncSettings();
            LoadInputs(situation);

            for (int i = 0; i < ActionCount; i++)
            {
                scores[i] = actions[i].Evaluate();
            }

            UtilityAction winner = UtilityActionSelector.Select(actions, current, config.hysteresis);
            current = winner;
            for (int i = 0; i < ActionCount; i++)
            {
                if (ReferenceEquals(actions[i], winner))
                {
                    CurrentAction = (GuardActionType)i;
                    break;
                }
            }
            return CurrentAction;
        }

        private void LoadInputs(in GuardSituation s)
        {
            float visible = s.canSeePlayer ? 1f : 0f;
            float closeness = 1f - GuardMath.Normalise(s.distanceToPlayer, config.attackRange);

            attackLineOfSight.inputValue = visible;
            attackProximity.inputValue = closeness;
            attackHealth.inputValue = s.healthFraction;

            coverAvailability.inputValue = s.coverQuality;
            coverThreat.inputValue = s.threat;

            // How ready the guard already is to shoot: only counts while it can see the player.
            repositionKnowledge.inputValue = s.knowledge;
            repositionDisengaged.inputValue = visible * attackProximity.Evaluate(closeness);

            holdPostPull.inputValue = GuardMath.Normalise(s.distanceFromPost, config.postRadius);
            holdCalm.inputValue = s.knowledge;
        }

        // Copied every call so a config edited in the Inspector during play mode takes effect straight away.
        private void SyncSettings()
        {
            config.attackLineOfSight.CopyTo(attackLineOfSight);
            config.attackProximity.CopyTo(attackProximity);
            config.attackHealth.CopyTo(attackHealth);
            config.coverAvailability.CopyTo(coverAvailability);
            config.coverThreat.CopyTo(coverThreat);
            config.repositionKnowledge.CopyTo(repositionKnowledge);
            config.repositionDisengaged.CopyTo(repositionDisengaged);
            config.holdPostPull.CopyTo(holdPostPull);
            config.holdCalm.CopyTo(holdCalm);

            actions[(int)GuardActionType.Attack].weight = config.attackWeight;
            actions[(int)GuardActionType.MoveToCover].weight = config.moveToCoverWeight;
            actions[(int)GuardActionType.Reposition].weight = config.repositionWeight;
            actions[(int)GuardActionType.HoldPost].weight = config.holdPostWeight;
        }
    }
}
