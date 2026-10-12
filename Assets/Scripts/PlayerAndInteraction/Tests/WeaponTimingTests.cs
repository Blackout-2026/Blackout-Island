using NUnit.Framework;

namespace Blackout.Weapons.Tests
{
    public class WeaponTimingTests
    {
        private const float Tolerance = 0.001f;
        private const float Frame = 1f / 60f;

        // ---- cooldown -----------------------------------------------------------------

        [Test]
        public void Cooldown_New_IsReady()
        {
            Assert.IsTrue(new WeaponCooldown().IsReady);
        }

        [Test]
        public void Cooldown_AfterStart_WaitsTheFullTime()
        {
            var cooldown = new WeaponCooldown();
            cooldown.Start(1.154f); // rifle: 52 rpm

            cooldown.Tick(1.1f);
            Assert.IsFalse(cooldown.IsReady);

            cooldown.Tick(0.06f);
            Assert.IsTrue(cooldown.IsReady);
            Assert.AreEqual(0f, cooldown.Remaining, Tolerance);
        }

        [Test]
        public void Cooldown_Smg_BlocksForAbout5FramesAt60Fps()
        {
            var cooldown = new WeaponCooldown();
            cooldown.Start(60f / 720f); // 0.083 s

            for (int i = 0; i < 4; i++)
            {
                cooldown.Tick(Frame);
            }

            Assert.IsFalse(cooldown.IsReady, "0.067 s after the shot");

            cooldown.Tick(Frame);
            cooldown.Tick(Frame);
            Assert.IsTrue(cooldown.IsReady, "0.1 s after the shot");
        }

        // ---- switching ----------------------------------------------------------------

        [Test]
        public void Switch_RequestActiveWeapon_IsIgnored()
        {
            var switcher = new WeaponSwitcher(3, 0);
            Assert.IsFalse(switcher.Request(0, 0.4f));
            Assert.IsFalse(switcher.Request(5, 0.4f));
            Assert.IsTrue(switcher.IsReady);
        }

        [Test]
        public void Switch_SwapsAfterLowering_AndIsReadyAfterTheFullTime()
        {
            var switcher = new WeaponSwitcher(3, 0);
            Assert.IsTrue(switcher.Request(1, 0.4f));
            Assert.AreEqual(SwitchState.Lowering, switcher.State);

            Assert.IsFalse(switcher.Tick(0.17f)); // lowering takes 0.18 s
            Assert.AreEqual(0, switcher.ActiveIndex);

            Assert.IsTrue(switcher.Tick(0.02f)); // swapped
            Assert.AreEqual(1, switcher.ActiveIndex);
            Assert.AreEqual(SwitchState.Raising, switcher.State);
            Assert.AreEqual(1f, switcher.Lowered, Tolerance);

            switcher.Tick(0.23f); // raising takes 0.22 s
            Assert.IsTrue(switcher.IsReady);
            Assert.AreEqual(0f, switcher.Lowered, Tolerance);
        }

        [Test]
        public void Switch_RequestWhileLowering_ReplacesThePendingWeapon()
        {
            var switcher = new WeaponSwitcher(3, 0);
            switcher.Request(1, 0.4f);
            switcher.Tick(0.1f);
            switcher.Request(2, 0.4f);

            switcher.Tick(0.1f);
            Assert.AreEqual(2, switcher.ActiveIndex);
        }

        [Test]
        public void Switch_Neighbour_WrapsAround()
        {
            var switcher = new WeaponSwitcher(3, 0);
            Assert.AreEqual(1, switcher.Neighbour(1));
            Assert.AreEqual(2, switcher.Neighbour(-1));
        }
    }
}
