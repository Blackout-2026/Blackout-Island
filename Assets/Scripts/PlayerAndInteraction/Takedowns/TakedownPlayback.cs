using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Blackout.Takedowns
{
    /// <summary>One graph starts both Animator clips together. It never replaces their controllers/assets.</summary>
    internal sealed class TakedownPlayback
    {
        private PlayableGraph graph;
        private Binding player, victim;
        private TakedownProfile profile;
        private bool stabbing, recovering;
        private int pendingReaction;

        private sealed class Binding
        {
            public Animator Animator;
            public bool RootMotion;
            public AnimatorCullingMode Culling;
            public Vector3 Position;
            public Quaternion Rotation;
            public int[] States;
            public float[] Times;
            public AnimationMixerPlayable Mixer;
            public AnimationClipPlayable[] Clips;
            public int Current = -1, Previous = -1;
            public float Blend;
        }

        public void Begin(Animator actor, Animator target, TakedownProfile profile)
        {
            this.profile = profile;
            stabbing = recovering = false;
            pendingReaction = 0;
            graph = PlayableGraph.Create("Blackout paired knife takedown");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            bool frontal = profile.style == TakedownStyle.Frontal;
            player = Bind(actor, frontal ? new[] { profile.kick, profile.playerAttack } : new[] { profile.playerAttack }, "Player");
            victim = Bind(target, frontal ? new[] { profile.victimIdle, profile.victimReaction, profile.victimDeath }
                : new[] { profile.victimReaction }, "Victim");
            graph.Play();
        }

        private Binding Bind(Animator animator, AnimationClip[] clips, string name)
        {
            var binding = new Binding { Animator = animator, RootMotion = animator.applyRootMotion,
                Culling = animator.cullingMode, Position = animator.transform.localPosition,
                Rotation = animator.transform.localRotation, States = new int[animator.layerCount], Times = new float[animator.layerCount] };
            for (int i = 0; i < binding.States.Length; i++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(i);
                binding.States[i] = state.fullPathHash;
                binding.Times[i] = state.normalizedTime;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            binding.Mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            binding.Clips = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                var playable = AnimationClipPlayable.Create(graph, clips[i]);
                playable.SetApplyFootIK(false);
                playable.SetApplyPlayableIK(false);
                playable.SetSpeed(0d);
                graph.Connect(playable, 0, binding.Mixer, i);
                binding.Clips[i] = playable;
            }
            var output = AnimationPlayableOutput.Create(graph, name, animator);
            output.SetSourcePlayable(binding.Mixer);
            Switch(binding, 0);
            return binding;
        }

        public void React(bool dead) => pendingReaction = dead ? 2 : 1;

        public void Tick(float elapsed, float dt)
        {
            if (profile.style == TakedownStyle.Frontal)
            {
                if (!stabbing && elapsed >= profile.kick.length)
                {
                    stabbing = true;
                    Switch(player, 1);
                    player.Clips[1].SetSpeed(profile.stabSpeed);
                }
                // Read the native clip clock so a late frame cannot cut off the second hit event.
                if (!recovering && stabbing && player.Clips[1].GetTime() >= profile.playerAttack.length)
                {
                    recovering = true;
                    Switch(player, 0, profile.kick.length, false);
                }
                if (pendingReaction != 0)
                {
                    Switch(victim, pendingReaction);
                    pendingReaction = 0;
                }
            }
            Blend(player, dt);
            Blend(victim, dt);
        }

        private static void Switch(Binding binding, int slot, double time = 0d, bool run = true)
        {
            foreach (AnimationClipPlayable clip in binding.Clips) clip.SetSpeed(0d);
            binding.Clips[slot].SetTime(time);
            binding.Clips[slot].SetSpeed(run ? 1d : 0d);
            if (binding.Current == slot) return;
            binding.Previous = binding.Current;
            binding.Current = slot;
            binding.Blend = binding.Previous < 0 ? 1f : 0f;
            for (int i = 0; i < binding.Clips.Length; i++)
                binding.Mixer.SetInputWeight(i, i == slot ? binding.Blend : i == binding.Previous ? 1f - binding.Blend : 0f);
        }

        private static void Blend(Binding binding, float dt)
        {
            if (binding.Previous < 0) return;
            binding.Blend = Mathf.Min(1f, binding.Blend + dt / 0.08f);
            binding.Mixer.SetInputWeight(binding.Current, binding.Blend);
            binding.Mixer.SetInputWeight(binding.Previous, 1f - binding.Blend);
            if (binding.Blend >= 1f) binding.Previous = -1;
        }

        public void End(bool victimDead)
        {
            // Keep the paired collapse's final bone pose. Returning to the unrelated controller's
            // Death clip would move the corpse back to its original root and replay another fall.
            // The existing AnimatorDriver still runs the normal death fade; respawns use fresh prefabs.
            if (victimDead && victim != null && victim.Animator != null) victim.Animator.enabled = false;
            if (graph.IsValid()) graph.Destroy();
            Restore(player, true);
            Restore(victim, !victimDead);
            player = victim = null;
        }

        private static void Restore(Binding binding, bool restoreStates)
        {
            if (binding == null || binding.Animator == null) return;
            Animator animator = binding.Animator;
            animator.applyRootMotion = binding.RootMotion;
            animator.cullingMode = binding.Culling;
            animator.transform.SetLocalPositionAndRotation(binding.Position, binding.Rotation);
            if (restoreStates && animator.runtimeAnimatorController != null)
            {
                for (int i = 0; i < binding.States.Length; i++)
                    if (animator.HasState(i, binding.States[i])) animator.Play(binding.States[i], i, binding.Times[i]);
                animator.Update(0f);
            }
        }
    }
}
