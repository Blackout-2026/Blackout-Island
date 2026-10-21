// Owner: Systems Engineer.
namespace Blackout.Weapons
{
    /// <summary>
    /// The rounds of one gun (step F2): a magazine, spare magazines and the reload. A gun fires from its magazine; the shot that empties it starts a
    /// reload that takes <c>reloadTime</c> seconds (1 s for both guns) and, if there is a spare magazine, fills the magazine from it. With no spare
    /// magazine the gun stays empty until the player picks one up from a dead body. A gun with a magazine size of 0 (the knife) has no ammunition and
    /// always fires. Pure C#, so the rules are tested without a scene; WeaponController owns one per weapon and the HUD reads it.
    /// </summary>
    public sealed class WeaponAmmo
    {
        private readonly int magazineSize;
        private readonly float reloadTime;
        private int loaded;
        private int spareMagazines;
        private float reloadRemaining;

        /// <summary>What a checkpoint stores for this gun.</summary>
        public struct State
        {
            public int Loaded;
            public int SpareMagazines;
            public float ReloadRemaining;
        }

        public WeaponAmmo(int magazineSize, int spareMagazines, float reloadTime)
        {
            this.magazineSize = magazineSize < 0 ? 0 : magazineSize;
            this.spareMagazines = spareMagazines < 0 ? 0 : spareMagazines;
            this.reloadTime = reloadTime < 0f ? 0f : reloadTime;
            loaded = this.magazineSize;
        }

        /// <summary>False for the knife.</summary>
        public bool IsLimited => magazineSize > 0;

        public int MagazineSize => magazineSize;

        /// <summary>Rounds in the gun now.</summary>
        public int Loaded => loaded;

        public int SpareMagazines => spareMagazines;

        /// <summary>Rounds in the spare magazines (what the HUD shows next to the loaded rounds).</summary>
        public int ReserveRounds => spareMagazines * magazineSize;

        public bool Reloading => reloadRemaining > 0f;

        /// <summary>Seconds of the reload left (0 when not reloading).</summary>
        public float ReloadRemaining => reloadRemaining;

        /// <summary>True when the gun can fire a round now: it is not reloading and has a round (the knife always can).</summary>
        public bool CanFire => !IsLimited || (loaded > 0 && !Reloading);

        /// <summary>Spends one round. Emptying the magazine starts the reload when a spare magazine is left. Returns false if there was no round.</summary>
        public bool Consume()
        {
            if (!IsLimited)
            {
                return true;
            }

            if (!CanFire)
            {
                return false;
            }

            loaded--;
            StartReloadIfEmpty();
            return true;
        }

        /// <summary>Adds spare magazines (from a dead body). An empty gun with nothing to reload from starts reloading at once.</summary>
        public void AddMagazines(int count)
        {
            if (!IsLimited || count <= 0)
            {
                return;
            }

            spareMagazines += count;
            StartReloadIfEmpty();
        }

        /// <summary>Runs the reload. Returns true on the frame it finishes and the magazine is full again.</summary>
        public bool Tick(float dt)
        {
            if (reloadRemaining <= 0f)
            {
                return false;
            }

            reloadRemaining -= dt;
            if (reloadRemaining > 0f)
            {
                return false;
            }

            reloadRemaining = 0f;
            spareMagazines--;
            loaded = magazineSize;
            return true;
        }

        public State Capture()
        {
            return new State { Loaded = loaded, SpareMagazines = spareMagazines, ReloadRemaining = reloadRemaining };
        }

        public void Restore(State state)
        {
            loaded = state.Loaded;
            spareMagazines = state.SpareMagazines;
            reloadRemaining = state.ReloadRemaining;
        }

        private void StartReloadIfEmpty()
        {
            if (loaded <= 0 && !Reloading && spareMagazines > 0)
            {
                reloadRemaining = reloadTime > 0f ? reloadTime : 0.0001f;
            }
        }
    }
}
